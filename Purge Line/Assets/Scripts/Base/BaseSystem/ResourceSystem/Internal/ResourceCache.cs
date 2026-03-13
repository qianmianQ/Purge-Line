// ============================================================================
// PurgeLine.Resource.Internal — ResourceCache.cs
// 多级缓存：热缓存（正在使用）+ LRU 缓存（引用归零后保留）
// ============================================================================

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using PurgeLine.Resource.Diagnostics;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace PurgeLine.Resource.Internal
{
    /// <summary>
    /// 缓存条目
    /// </summary>
    internal struct CacheEntry
    {
        public string Address;
        public object Asset;
        public AsyncOperationHandle OperationHandle;
        public float LoadTimeMs;
        public bool IsReleased;
    }

    /// <summary>
    /// LRU 缓存条目
    /// </summary>
    internal struct LruEntry
    {
        public string Address;
        public CacheEntry Cache;
        public float EnqueueTime;
    }

    /// <summary>
    /// 资源多级缓存。
    /// - 热缓存：引用计数 > 0 的资源
    /// - LRU 缓存：引用计数 = 0 但保留在内存中的资源，按访问时间排序
    /// 所有操作限主线程。
    /// </summary>
    internal sealed class ResourceCache
    {
        // ── 热缓存 ───────────────────────────────────────────────
        private readonly Dictionary<string, CacheEntry> _hotCache;

        // ── LRU 缓存 ─────────────────────────────────────────────
        private readonly LinkedList<LruEntry> _lruList;
        private readonly Dictionary<string, LinkedListNode<LruEntry>> _lruIndex;
        private readonly int _lruCapacity;
        private readonly float _lruTimeout;
        private readonly ILogger _logger;

        public int HotCount => _hotCache.Count;
        public int LruCount => _lruList.Count;

        public ResourceCache(ResourceManagerConfig config, ILogger logger)
        {
            _hotCache = new Dictionary<string, CacheEntry>(256);
            _lruList = new LinkedList<LruEntry>();
            _lruIndex = new Dictionary<string, LinkedListNode<LruEntry>>(config.LruCapacity);
            _lruCapacity = config.LruCapacity;
            _lruTimeout = config.LruTimeoutSeconds;
            _logger = logger;
        }

        // ── 热缓存操作 ───────────────────────────────────────────

        /// <summary>尝试从热缓存获取资源</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetHot(string address, out CacheEntry entry)
        {
            return _hotCache.TryGetValue(address, out entry) && !entry.IsReleased;
        }

        /// <summary>添加到热缓存</summary>
        public void AddHot(string address, CacheEntry entry)
        {
            _hotCache[address] = entry;
        }

        /// <summary>尝试将 LRU 中的资源提升到热缓存</summary>
        public bool TryPromoteLru(string address, out CacheEntry entry)
        {
            if (_lruIndex.TryGetValue(address, out var node))
            {
                entry = node.Value.Cache;
                _lruList.Remove(node);
                _lruIndex.Remove(address);
                _hotCache[address] = entry;
                return true;
            }

            entry = default;
            return false;
        }

        /// <summary>将资源从热缓存移入 LRU（引用归零时调用）</summary>
        public void MoveToLru(string address, float currentTime)
        {
            if (!_hotCache.TryGetValue(address, out var entry))
                return;

            _hotCache.Remove(address);

            // LRU 已满，淘汰最旧条目
            while (_lruList.Count >= _lruCapacity && _lruList.Count > 0)
            {
                EvictOldest();
            }

            var lruEntry = new LruEntry
            {
                Address = address,
                Cache = entry,
                EnqueueTime = currentTime,
            };
            var newNode = _lruList.AddFirst(lruEntry);
            _lruIndex[address] = newNode;
        }

        /// <summary>
        /// 从热缓存中移除并释放 Addressables 资源
        /// </summary>
        public void RemoveAndRelease(string address)
        {
            if (_hotCache.TryGetValue(address, out var entry))
            {
                _hotCache.Remove(address);
                ReleaseEntry(ref entry);
                return;
            }

            if (_lruIndex.TryGetValue(address, out var node))
            {
                var e = node.Value.Cache;
                _lruList.Remove(node);
                _lruIndex.Remove(address);
                ReleaseEntry(ref e);
            }
        }

        // ── LRU 淘汰 ─────────────────────────────────────────────

        /// <summary>淘汰指定数量的 LRU 条目。返回实际淘汰数。</summary>
        public int EvictLru(int count)
        {
            int evicted = 0;
            while (evicted < count && _lruList.Count > 0)
            {
                EvictOldest();
                evicted++;
            }
            return evicted;
        }

        /// <summary>淘汰超时的 LRU 条目</summary>
        public int EvictExpired(float currentTime)
        {
            if (_lruTimeout <= 0f)
                return 0;

            int evicted = 0;
            var node = _lruList.Last;
            while (node != null)
            {
                var prev = node.Previous;
                float age = currentTime - node.Value.EnqueueTime;
                if (age >= _lruTimeout)
                {
                    var entry = node.Value.Cache;
                    _lruIndex.Remove(node.Value.Address);
                    _lruList.Remove(node);
                    ReleaseEntry(ref entry);
                    evicted++;
                }
                node = prev;
            }
            return evicted;
        }

        private void EvictOldest()
        {
            var last = _lruList.Last;
            if (last == null) return;

            var entry = last.Value.Cache;
            var address = last.Value.Address;
            _lruIndex.Remove(address);
            _lruList.RemoveLast();
            ReleaseEntry(ref entry);

            _logger.LogDebug("[ResourceCache] Evicted LRU entry: {Address}", address);
        }

        private void ReleaseEntry(ref CacheEntry entry)
        {
            if (entry.IsReleased) return;
            entry.IsReleased = true;

            if (entry.OperationHandle.IsValid())
            {
                Addressables.Release(entry.OperationHandle);
            }
        }

        // ── 全量清理 ─────────────────────────────────────────────

        /// <summary>释放所有缓存（Dispose 时调用）</summary>
        public void ReleaseAll()
        {
            foreach (var kv in _hotCache)
            {
                var entry = kv.Value;
                ReleaseEntry(ref entry);
            }
            _hotCache.Clear();

            foreach (var node in _lruList)
            {
                var entry = node.Cache;
                ReleaseEntry(ref entry);
            }
            _lruList.Clear();
            _lruIndex.Clear();
        }

        /// <summary>
        /// 查询缓存中是否包含指定地址（热或 LRU）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(string address)
        {
            return _hotCache.ContainsKey(address) || _lruIndex.ContainsKey(address);
        }

        // ── 诊断 ─────────────────────────────────────────────────

        /// <summary>收集所有缓存条目的指标快照</summary>
        public void CollectMetrics(ReferenceTracker refTracker, List<ResourceMetricEntry> output)
        {
            foreach (var kv in _hotCache)
            {
                output.Add(new ResourceMetricEntry
                {
                    Address = kv.Key,
                    RefCount = refTracker.GetCount(kv.Key),
                    LoadTimeMs = kv.Value.LoadTimeMs,
                    LastAccessTime = 0f,
                    InLru = false,
                });
            }

            foreach (var node in _lruList)
            {
                output.Add(new ResourceMetricEntry
                {
                    Address = node.Address,
                    RefCount = 0,
                    LoadTimeMs = node.Cache.LoadTimeMs,
                    LastAccessTime = node.EnqueueTime,
                    InLru = true,
                });
            }
        }
    }
}

