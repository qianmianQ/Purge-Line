// ============================================================================
// PurgeLine.Resource.Internal — ResourceManager.cs
// 资源管理器门面实现。注册到 DependencyManager，驱动所有子系统。
// ============================================================================

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using PurgeLine.Resource.Diagnostics;
using PurgeLine.Resource.Extensions;
using PurgeLine.Resource.ObjectPool;
using VContainer.Unity;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace PurgeLine.Resource.Internal
{
    /// <summary>
    /// 资源管理器门面实现。
    /// - 实现 IResourceManager 公开接口
    /// - 实现 IInitializable + ITickable 注册到 DependencyManager
    /// - 纯 C# 类，不依赖 MonoBehaviour
    /// </summary>
    public sealed class ResourceManager : IResourceManager, IInitializable, IStartable, ITickable , IDisposable
    {
        // ── 配置 ─────────────────────────────────────────────────
        private readonly ResourceManagerConfig _config;

        // ── 子系统 ───────────────────────────────────────────────
        private AddressablesProvider _provider;
        private ReferenceTracker _refTracker;
        private ResourceCache _cache;
        private LoadQueue _loadQueue;
        private ObjectPoolManager _poolManager;
        private MemoryGuard _memoryGuard;
        private ResourceMetrics _metrics;

        // ── P3 扩展 ──────────────────────────────────────────────
        private readonly IVariantSelector _variantSelector;
        private readonly IResourceDecryptor _decryptor;
        private readonly IHotUpdateCallback _hotUpdateCallback;
        private readonly IEcsResourceBridge _ecsBridge;

        // ── 句柄 ID 生成 ─────────────────────────────────────────
        private uint _nextHandleId;

        // ── 并发控制 ─────────────────────────────────────────────
        private int _activeLoadCount;

        // ── 场景分组 ─────────────────────────────────────────────
        private readonly Dictionary<string, HashSet<string>> _sceneGroups;

        // ── Label 追踪 ───────────────────────────────────────────
        private readonly Dictionary<string, HashSet<string>> _labelAddresses;

        // ── 日志 ─────────────────────────────────────────────────
        private ILogger _logger;
        private bool _disposed;

        // ── LRU 超时检查间隔控制 ──────────────────────────────────
        private float _lruEvictTimer;
        private const float LruEvictInterval = 10f;

        // ── 对象池超时检查间隔 ────────────────────────────────────
        private float _poolEvictTimer;
        private const float PoolEvictInterval = 30f;

        // ── 公开诊断属性 ─────────────────────────────────────────
        public int CachedCount => _cache?.HotCount ?? 0;
        public int LruCount => _cache?.LruCount ?? 0;
        public int PendingLoadCount => _loadQueue?.Count ?? 0;

        /// <summary>诊断指标（Editor 监控窗口可访问）</summary>
        public ResourceMetrics Metrics => _metrics;

        // =====================================================================
        // 构造
        // =====================================================================

        public ResourceManager(
            ResourceManagerConfig config = null,
            IVariantSelector variantSelector = null,
            IResourceDecryptor decryptor = null,
            IHotUpdateCallback hotUpdateCallback = null,
            IEcsResourceBridge ecsBridge = null)
        {
            _config = config ?? ResourceManagerConfig.Default;
            _variantSelector = variantSelector;
            _decryptor = decryptor;
            _hotUpdateCallback = hotUpdateCallback;
            _ecsBridge = ecsBridge;
            _sceneGroups = new Dictionary<string, HashSet<string>>(16);
            _labelAddresses = new Dictionary<string, HashSet<string>>(16);
            _nextHandleId = 1;
        }

        // =====================================================================
        // IInitializable
        // =====================================================================

        public void Initialize()
        {
            _logger = GameLogger.Create("ResourceManager");
            _metrics = new ResourceMetrics();
            _refTracker = new ReferenceTracker(256);
            _provider = new AddressablesProvider(_logger);
            _cache = new ResourceCache(_config, _logger);
            _loadQueue = new LoadQueue(64);
            _poolManager = new ObjectPoolManager(_config, _logger);
            _memoryGuard = new MemoryGuard(_config, _cache, _metrics, _logger);

            _logger.LogInformation("[ResourceManager] Initialized with config: " +
                "MaxConcurrent={MaxConcurrent}, LruCapacity={LruCapacity}, MemoryThreshold={MemoryThreshold}MB",
                _config.MaxConcurrentLoads, _config.LruCapacity, _config.MemoryWarningThresholdMB);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _logger.LogInformation("[ResourceManager] Disposing...");

            // 销毁对象池
            _poolManager?.DestroyAll();

            // 释放所有缓存资源
            _cache?.ReleaseAll();

            // 清理引用计数
            _refTracker?.Clear();

            // 清空加载队列
            _loadQueue?.Clear();

            _sceneGroups.Clear();
            _labelAddresses.Clear();

            _logger.LogInformation("[ResourceManager] Disposed. Metrics: " +
                "Loads={Loads}, Hits={Hits}, Misses={Misses}, Failures={Failures}",
                _metrics.TotalLoadRequests, _metrics.TotalCacheHits,
                _metrics.TotalCacheMisses, _metrics.TotalLoadFailures);
        }

        // =====================================================================
        // IStartable
        // =====================================================================

        public void Start()
        {
            // 延迟初始化逻辑（若有依赖其他系统的情况）
        }

        // =====================================================================
        // ITickable — 每帧驱动
        // =====================================================================

        public void Tick()
        {
            if (_disposed) return;

            // 1. 处理加载队列
            ProcessLoadQueue();

            // 2. 内存水位检查
            _memoryGuard.Tick(Time.deltaTime);

            // 3. LRU 超时淘汰
            _lruEvictTimer += Time.deltaTime;
            if (_lruEvictTimer >= LruEvictInterval)
            {
                _lruEvictTimer = 0f;
                _cache.EvictExpired(Time.realtimeSinceStartup);
            }

            // 4. 对象池超时回收
            _poolEvictTimer += Time.deltaTime;
            if (_poolEvictTimer >= PoolEvictInterval)
            {
                _poolEvictTimer = 0f;
                _poolManager.EvictExpired(Time.realtimeSinceStartup);
            }
        }

        // =====================================================================
        // IResourceManager — 异步加载
        // =====================================================================

        public async UniTask<ResourceHandle<T>> LoadAsync<T>(
            string address,
            LoadPriority priority = LoadPriority.Normal,
            CancellationToken ct = default) where T : class
        {
            ThrowIfDisposed();

            // P3 变体选择
            address = ResolveAddress(address);

            var traceId = TraceContext.NextTraceId();
            _metrics.RecordLoadRequest();

            _logger.LogDebug("[ResourceManager] LoadAsync<{Type}> address={Address} priority={Priority} traceId={TraceId}",
                typeof(T).Name, address, priority, traceId);

            // 1. 热缓存命中
            if (_cache.TryGetHot(address, out var hotEntry))
            {
                _metrics.RecordCacheHit();
                _refTracker.Retain(address, Time.realtimeSinceStartup);
                return CreateHandle<T>(address, hotEntry.Asset);
            }

            // 2. LRU 命中 → 提升到热缓存
            if (_cache.TryPromoteLru(address, out var lruEntry))
            {
                _metrics.RecordCacheHit();
                _refTracker.Retain(address, Time.realtimeSinceStartup);
                return CreateHandle<T>(address, lruEntry.Asset);
            }

            // 3. 缓存未命中 → 入队
            _metrics.RecordCacheMiss();
            return await EnqueueAndWait<T>(address, priority, traceId, ct);
        }

        public async UniTask<ResourceHandle<T>> LoadAsync<T>(
            AssetReference assetReference,
            LoadPriority priority = LoadPriority.Normal,
            CancellationToken ct = default) where T : class
        {
            ThrowIfDisposed();

            if (assetReference == null)
                throw new ArgumentNullException(nameof(assetReference));

            string address = assetReference.RuntimeKey.ToString();
            address = ResolveAddress(address);

            var traceId = TraceContext.NextTraceId();
            _metrics.RecordLoadRequest();

            // 缓存检查
            if (_cache.TryGetHot(address, out var hotEntry))
            {
                _metrics.RecordCacheHit();
                _refTracker.Retain(address, Time.realtimeSinceStartup);
                return CreateHandle<T>(address, hotEntry.Asset);
            }

            if (_cache.TryPromoteLru(address, out var lruEntry))
            {
                _metrics.RecordCacheHit();
                _refTracker.Retain(address, Time.realtimeSinceStartup);
                return CreateHandle<T>(address, lruEntry.Asset);
            }

            _metrics.RecordCacheMiss();

            // AssetReference 直接加载（不走队列，因为句柄管理不同）
            float startTime = Time.realtimeSinceStartup;
            try
            {
                var (asset, handle) = await ExecuteLoadWithRetry(
                    async () => await _provider.LoadAssetAsync<T>(assetReference, ct),
                    address, traceId);

                float elapsed = (Time.realtimeSinceStartup - startTime) * 1000f;
                _refTracker.Retain(address, Time.realtimeSinceStartup);

                _cache.AddHot(address, new CacheEntry
                {
                    Address = address,
                    Asset = asset,
                    OperationHandle = handle,
                    LoadTimeMs = elapsed,
                    IsReleased = false,
                });

                _metrics.RecordLoadSuccess();
                _logger.LogDebug("[ResourceManager] Loaded {Address} in {Elapsed:F1}ms traceId={TraceId}",
                    address, elapsed, traceId);

                return CreateHandle<T>(address, asset);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("[ResourceManager] Load cancelled: {Address} traceId={TraceId}", address, traceId);
                throw;
            }
            catch (Exception ex)
            {
                _metrics.RecordLoadFailure();
                _logger.LogError(ex, "[ResourceManager] Load failed: {Address} traceId={TraceId}", address, traceId);
                throw;
            }
        }

        public async UniTask<IReadOnlyList<ResourceHandle<T>>> LoadByLabelAsync<T>(
            string label,
            LoadPriority priority = LoadPriority.Normal,
            CancellationToken ct = default) where T : class
        {
            ThrowIfDisposed();

            var traceId = TraceContext.NextTraceId();
            _metrics.RecordLoadRequest();

            _logger.LogDebug("[ResourceManager] LoadByLabel<{Type}> label={Label} traceId={TraceId}",
                typeof(T).Name, label, traceId);

            float startTime = Time.realtimeSinceStartup;
            try
            {
                var (assets, handle) = await _provider.LoadAssetsByLabelAsync<T>(label, ct);
                float elapsed = (Time.realtimeSinceStartup - startTime) * 1000f;

                var handles = new List<ResourceHandle<T>>(assets.Count);
                var labelSet = GetOrCreateLabelSet(label);

                for (int i = 0; i < assets.Count; i++)
                {
                    // Label 加载的 address 使用 label + index 作为 key
                    string virtualAddress = $"{label}#{i}";
                    labelSet.Add(virtualAddress);

                    _refTracker.Retain(virtualAddress, Time.realtimeSinceStartup);

                    // 首个资源持有原始句柄，其余共享
                    if (i == 0)
                    {
                        _cache.AddHot(virtualAddress, new CacheEntry
                        {
                            Address = virtualAddress,
                            Asset = assets[i],
                            OperationHandle = handle,
                            LoadTimeMs = elapsed,
                            IsReleased = false,
                        });
                    }
                    else
                    {
                        _cache.AddHot(virtualAddress, new CacheEntry
                        {
                            Address = virtualAddress,
                            Asset = assets[i],
                            OperationHandle = default, // 共享句柄，不重复释放
                            LoadTimeMs = elapsed,
                            IsReleased = false,
                        });
                    }

                    handles.Add(CreateHandle<T>(virtualAddress, assets[i]));
                }

                _metrics.RecordLoadSuccess();
                _logger.LogDebug("[ResourceManager] Label '{Label}' loaded {Count} assets in {Elapsed:F1}ms traceId={TraceId}",
                    label, assets.Count, elapsed, traceId);

                return handles;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _metrics.RecordLoadFailure();
                _logger.LogError(ex, "[ResourceManager] Label load failed: {Label} traceId={TraceId}", label, traceId);
                throw;
            }
        }

        // =====================================================================
        // IResourceManager — 同步缓存读取
        // =====================================================================

        public ResourceHandle<T> TryGetCached<T>(string address) where T : class
        {
            if (_disposed) return ResourceHandle<T>.Invalid;

            address = ResolveAddress(address);

            if (_cache.TryGetHot(address, out var entry) && entry.Asset is T asset)
            {
                _refTracker.Retain(address, Time.realtimeSinceStartup);
                _metrics.RecordCacheHit();
                return CreateHandle<T>(address, asset);
            }

            if (_cache.TryPromoteLru(address, out var lruEntry) && lruEntry.Asset is T lruAsset)
            {
                _refTracker.Retain(address, Time.realtimeSinceStartup);
                _metrics.RecordCacheHit();
                return CreateHandle<T>(address, lruAsset);
            }

            return ResourceHandle<T>.Invalid;
        }

        // =====================================================================
        // IResourceManager — 释放
        // =====================================================================

        public void Release<T>(ResourceHandle<T> handle) where T : class
        {
            if (_disposed || !handle.IsValid) return;

            string address = handle.Address;
            int remaining = _refTracker.Release(address);

            _logger.LogDebug("[ResourceManager] Release {Address}, remaining refs={Remaining}", address, remaining);

            if (remaining <= 0)
            {
                // 引用归零 → 移入 LRU
                _cache.MoveToLru(address, Time.realtimeSinceStartup);
                _refTracker.Remove(address);
            }
        }

        public void ReleaseByLabel(string label)
        {
            if (_disposed) return;

            if (!_labelAddresses.TryGetValue(label, out var addresses))
            {
                _logger.LogWarning("[ResourceManager] No addresses tracked for label '{Label}'", label);
                return;
            }

            foreach (var addr in addresses)
            {
                int remaining = _refTracker.Release(addr);
                if (remaining <= 0)
                {
                    _cache.MoveToLru(addr, Time.realtimeSinceStartup);
                    _refTracker.Remove(addr);
                }
            }

            _labelAddresses.Remove(label);
            _logger.LogDebug("[ResourceManager] Released label group '{Label}'", label);
        }

        public void ReleaseByScene(string sceneName)
        {
            if (_disposed) return;

            if (!_sceneGroups.TryGetValue(sceneName, out var addresses))
            {
                _logger.LogWarning("[ResourceManager] No addresses tracked for scene '{Scene}'", sceneName);
                return;
            }

            foreach (var addr in addresses)
            {
                int remaining = _refTracker.Release(addr);
                if (remaining <= 0)
                {
                    _cache.MoveToLru(addr, Time.realtimeSinceStartup);
                    _refTracker.Remove(addr);
                }
            }

            _sceneGroups.Remove(sceneName);
            _logger.LogDebug("[ResourceManager] Released scene group '{Scene}'", sceneName);
        }

        public void ReleaseAll()
        {
            if (_disposed) return;

            _logger.LogWarning("[ResourceManager] ReleaseAll called — all resources will be freed");

            _poolManager.DestroyAll();
            _cache.ReleaseAll();
            _refTracker.Clear();
            _sceneGroups.Clear();
            _labelAddresses.Clear();
        }

        // =====================================================================
        // IResourceManager — 实例化与对象池
        // =====================================================================

        /// <summary>
        /// 从对象池异步实例化 GameObject（优先从对象池取）
        /// </summary>
        public async UniTask<GameObject> InstantiateFromPoolAsync(
            string address,
            Transform parent = null,
            Vector3 position = default,
            Quaternion rotation = default,
            LoadPriority priority = LoadPriority.Normal,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();
            address = ResolveAddress(address);

            // 1. 尝试从池中获取
            if (_poolManager.TryRent(address, out var pooled))
            {
                _metrics.RecordPoolHit();
                if (parent != null) pooled.transform.SetParent(parent, false);
                pooled.transform.SetPositionAndRotation(position, rotation);
                _logger.LogDebug("[ResourceManager] Pool hit for {Address}", address);
                return pooled;
            }

            _metrics.RecordPoolMiss();

            // 2. 异步实例化
            var traceId = TraceContext.NextTraceId();
            try
            {
                var go = await _provider.InstantiateAsync(address, parent, position, rotation, ct);
                _poolManager.RegisterInstance(go, address);
                _refTracker.Retain(address, Time.realtimeSinceStartup);
                return go;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[ResourceManager] InstantiateFromPoolAsync failed: {Address} traceId={TraceId}",
                    address, traceId);
                throw;
            }
        }

        /// <summary>
        /// 异步实例化 GameObject（不走对象池，直接实例化）
        /// </summary>
        public async UniTask<GameObject> InstantiateAsync(
            string address,
            Transform parent = null,
            Vector3 position = default,
            Quaternion rotation = default,
            LoadPriority priority = LoadPriority.Normal,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();
            address = ResolveAddress(address);

            var traceId = TraceContext.NextTraceId();
            try
            {
                var go = await _provider.InstantiateAsync(address, parent, position, rotation, ct);
                _refTracker.Retain(address, Time.realtimeSinceStartup);

                _logger.LogDebug("[ResourceManager] Instantiated {Address} traceId={TraceId}", address, traceId);
                return go;
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("[ResourceManager] InstantiateAsync cancelled: {Address} traceId={TraceId}", address, traceId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ResourceManager] InstantiateAsync failed: {Address} traceId={TraceId}", address, traceId);
                throw;
            }
        }

        /// <summary>
        /// 同步实例化 GameObject（全程同步等待，会阻塞主线程）
        /// 注意：此方法会阻塞主线程，仅在必要情况下使用（如加载画面）
        /// </summary>
        public GameObject InstantiateSync(
            string address,
            Transform parent = null,
            Vector3 position = default,
            Quaternion rotation = default)
        {
            ThrowIfDisposed();
            address = ResolveAddress(address);

            var traceId = TraceContext.NextTraceId();
            float startTime = Time.realtimeSinceStartup;

            try
            {
                // 使用 Task.Run + Wait 实现同步等待
                // 注意：这会阻塞主线程
                var handle = Addressables.InstantiateAsync(address, position, rotation, parent);
                GameObject go = handle.WaitForCompletion();

                _refTracker.Retain(address, Time.realtimeSinceStartup);

                float elapsed = (Time.realtimeSinceStartup - startTime) * 1000f;
                _logger.LogDebug("[ResourceManager] Sync instantiated {Address} in {Elapsed:F1}ms traceId={TraceId}",
                    address, elapsed, traceId);

                return go;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ResourceManager] InstantiateSync failed: {Address} traceId={TraceId}",
                    address, traceId);
                throw;
            }
        }

        public void ReturnToPool(GameObject instance)
        {
            if (_disposed || instance == null) return;

            if (!_poolManager.TryGetAddress(instance, out var address))
            {
                _logger.LogWarning("[ResourceManager] ReturnToPool: unknown instance '{Name}'", instance.name);
                return;
            }

            if (_poolManager.Return(instance))
            {
                _logger.LogDebug("[ResourceManager] Returned to pool: {Address}", address);
            }
            else
            {
                // 池满，直接销毁
                _provider.ReleaseInstance(instance);
                _logger.LogDebug("[ResourceManager] Pool full, destroyed: {Address}", address);
            }
        }

        // =====================================================================
        // IResourceManager — 预加载
        // =====================================================================

        public async UniTask PreloadAsync(
            IReadOnlyList<string> addresses,
            LoadPriority priority = LoadPriority.Low,
            IProgress<PreloadProgressEvent> progress = null,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (addresses == null || addresses.Count == 0) return;

            int total = addresses.Count;
            int loaded = 0;

            _logger.LogInformation("[ResourceManager] Preloading {Total} assets at priority {Priority}", total, priority);

            // 使用 UniTask.WhenAll 并发加载，每个加载完成后报告进度
            var tasks = new UniTask[total];
            for (int i = 0; i < total; i++)
            {
                string addr = addresses[i];
                tasks[i] = PreloadSingle(addr, priority, ct).ContinueWith(() =>
                {
                    int current = Interlocked.Increment(ref loaded);
                    progress?.Report(new PreloadProgressEvent(current, total));
                });
            }

            await UniTask.WhenAll(tasks);

            _logger.LogInformation("[ResourceManager] Preload completed: {Loaded}/{Total}", loaded, total);
        }

        private async UniTask PreloadSingle(string address, LoadPriority priority, CancellationToken ct)
        {
            try
            {
                await LoadAsync<UnityEngine.Object>(address, priority, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("[ResourceManager] Preload failed for {Address}: {Error}", address, ex.Message);
                // 预加载失败不中断整体流程
            }
        }

        // =====================================================================
        // IResourceManager — 场景标记
        // =====================================================================

        public void TagScene(string address, string sceneName)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(sceneName)) return;

            if (!_sceneGroups.TryGetValue(sceneName, out var set))
            {
                set = new HashSet<string>();
                _sceneGroups[sceneName] = set;
            }

            set.Add(address);
        }

        // =====================================================================
        // IResourceManager — 诊断
        // =====================================================================

        public int GetRefCount(string address)
        {
            return _refTracker?.GetCount(address) ?? 0;
        }

        // =====================================================================
        // 内部：加载队列处理
        // =====================================================================

        private void ProcessLoadQueue()
        {
            int budget = _config.MaxConcurrentLoads - _activeLoadCount;
            while (budget > 0 && _loadQueue.TryDequeue(out var request))
            {
                budget--;
                ProcessLoadRequest(request).Forget();
            }
        }

        private async UniTaskVoid ProcessLoadRequest(LoadRequest request)
        {
            Interlocked.Increment(ref _activeLoadCount);
            try
            {
                if (request.Ct.IsCancellationRequested)
                {
                    TrySetCanceled(request);
                    return;
                }

                float startTime = Time.realtimeSinceStartup;

                // 再次检查缓存（可能在排队期间已被其他请求加载）
                if (_cache.TryGetHot(request.Address, out var hot))
                {
                    _metrics.RecordCacheHit();
                    TrySetResult(request, hot.Asset);
                    return;
                }

                if (_cache.TryPromoteLru(request.Address, out var lru))
                {
                    _metrics.RecordCacheHit();
                    TrySetResult(request, lru.Asset);
                    return;
                }

                // 执行加载（含重试）
                var (asset, handle) = await ExecuteLoadWithRetry(
                    async () => await _provider.LoadAssetAsync<object>(request.Address, request.Ct),
                    request.Address, request.TraceId);

                float elapsed = (Time.realtimeSinceStartup - startTime) * 1000f;

                // 加入热缓存
                _cache.AddHot(request.Address, new CacheEntry
                {
                    Address = request.Address,
                    Asset = asset,
                    OperationHandle = handle,
                    LoadTimeMs = elapsed,
                    IsReleased = false,
                });

                _metrics.RecordLoadSuccess();
                _logger.LogDebug("[ResourceManager] Loaded {Address} in {Elapsed:F1}ms traceId={TraceId}",
                    request.Address, elapsed, request.TraceId);

                TrySetResult(request, asset);
            }
            catch (OperationCanceledException)
            {
                TrySetCanceled(request);
            }
            catch (Exception ex)
            {
                _metrics.RecordLoadFailure();
                _logger.LogError(ex, "[ResourceManager] Load failed: {Address} traceId={TraceId}",
                    request.Address, request.TraceId);
                TrySetException(request, ex);
            }
            finally
            {
                Interlocked.Decrement(ref _activeLoadCount);
            }
        }

        // =====================================================================
        // 内部：入队并等待
        // =====================================================================

        private async UniTask<ResourceHandle<T>> EnqueueAndWait<T>(
            string address, LoadPriority priority, ulong traceId, CancellationToken ct) where T : class
        {
            var tcs = new UniTaskCompletionSource<object>();

            _loadQueue.Enqueue(new LoadRequest
            {
                Address = address,
                AssetType = typeof(T),
                Priority = priority,
                TraceId = traceId,
                Ct = ct,
                CompletionSource = tcs,
            });

            object result = await tcs.Task;

            // 结果已由 ProcessLoadRequest 写入缓存
            _refTracker.Retain(address, Time.realtimeSinceStartup);
            return CreateHandle<T>(address, result);
        }

        // =====================================================================
        // 内部：重试策略
        // =====================================================================

        private async UniTask<(T asset, AsyncOperationHandle handle)>
            ExecuteLoadWithRetry<T>(
                Func<UniTask<(T, AsyncOperationHandle)>> loadFunc,
                string address,
                ulong traceId) where T : class
        {
            int attempt = 0;
            float delay = _config.RetryBaseDelaySeconds;

            while (true)
            {
                try
                {
                    return await loadFunc();
                }
                catch (OperationCanceledException)
                {
                    throw; // 取消不重试
                }
                catch (Exception ex)
                {
                    attempt++;
                    if (attempt > _config.MaxRetryCount)
                    {
                        // 尝试降级
                        if (!string.IsNullOrEmpty(_config.FallbackAddress) && address != _config.FallbackAddress)
                        {
                            _logger.LogWarning(
                                "[ResourceManager] Falling back to '{Fallback}' for '{Address}' traceId={TraceId}",
                                _config.FallbackAddress, address, traceId);

                            try
                            {
                                var (asset, handle) = await _provider.LoadAssetAsync<T>(_config.FallbackAddress);
                                return (asset, handle);
                            }
                            catch (Exception fallbackEx)
                            {
                                _logger.LogError(fallbackEx,
                                    "[ResourceManager] Fallback also failed for '{Address}' traceId={TraceId}",
                                    address, traceId);
                            }
                        }

                        throw new InvalidOperationException(
                            $"[ResourceManager] Failed to load '{address}' after {_config.MaxRetryCount} retries. traceId={traceId}",
                            ex);
                    }

                    _metrics.RecordRetry();
                    _logger.LogWarning(
                        "[ResourceManager] Retry {Attempt}/{Max} for '{Address}' in {Delay:F1}s traceId={TraceId}: {Error}",
                        attempt, _config.MaxRetryCount, address, delay, traceId, ex.Message);

                    await UniTask.Delay(TimeSpan.FromSeconds(delay));
                    delay *= _config.RetryBackoffMultiplier;
                }
            }
        }

        // =====================================================================
        // 内部：工具方法
        // =====================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ResourceHandle<T> CreateHandle<T>(string address, object asset) where T : class
        {
            uint id = _nextHandleId++;
            return new ResourceHandle<T>(id, address, asset as T);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string ResolveAddress(string address)
        {
            if (_variantSelector != null)
                return _variantSelector.SelectVariant(address);
            return address;
        }

        private HashSet<string> GetOrCreateLabelSet(string label)
        {
            if (!_labelAddresses.TryGetValue(label, out var set))
            {
                set = new HashSet<string>();
                _labelAddresses[label] = set;
            }
            return set;
        }

        private static void TrySetResult(LoadRequest request, object result)
        {
            if (request.CompletionSource is UniTaskCompletionSource<object> tcs)
                tcs.TrySetResult(result);
        }

        private static void TrySetCanceled(LoadRequest request)
        {
            if (request.CompletionSource is UniTaskCompletionSource<object> tcs)
                tcs.TrySetCanceled();
        }

        private static void TrySetException(LoadRequest request, Exception ex)
        {
            if (request.CompletionSource is UniTaskCompletionSource<object> tcs)
                tcs.TrySetException(ex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ResourceManager));
        }

        /// <summary>
        /// 收集资源指标快照（Editor 监控窗口调用）。
        /// 调用方提供预分配 list 以避免 GC。
        /// </summary>
        public void CollectMetricSnapshots(List<ResourceMetricEntry> output)
        {
            if (_disposed || output == null) return;
            _metrics.CollectSnapshots(_refTracker, _cache, output);
        }
    }
}




