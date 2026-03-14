using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PurgeLine.Resource;
using UnityEngine;
using VContainer;
using IDisposable = System.IDisposable;
// ...existing code...
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace TowerDefense.TowerDefense.Utilities.GameObjectPool
{
    /// <summary>
    /// GameObject对象池
    /// </summary>
    public class GameObjectPool : IDisposable
    {
        private readonly Stack<GameObject> _availableInstances;
        private readonly HashSet<int> _activeInstanceIDs;

        private readonly string _address;
        private readonly Transform _poolRoot;
        private readonly object _lockObj = new object();

        private ResourceHandle<GameObject> _prefabHandle;
        private GameObject _loadedPrefab;
        private bool _isInitialized;

        private Vector3 _recyclePosition = Vector3.zero;
        private bool _useWorldRecyclePosition;

        private static readonly ILogger Logger = GameLogger.Create<GameObjectPool>();

        [Inject]
        public IResourceManager ResourceManager { get; set; }

        public GameObjectPool(string address, int initialSize, Transform poolRoot)
        {
            _address = address;
            _poolRoot = poolRoot;
            _availableInstances = new Stack<GameObject>(Mathf.Max(initialSize, 4));
            _activeInstanceIDs = new HashSet<int>();
        }

        /// <summary>
        /// 同步初始化 (会阻塞主线程直到资源加载完成)
        /// </summary>
        public void InitializeSync()
        {
            if (_isInitialized) return;

            try
            {
                if (ResourceManager != null)
                {
                    var handle = ResourceManager.LoadAsync<GameObject>(_address).GetAwaiter().GetResult();
                    if (handle.IsValid)
                    {
                        lock (_lockObj)
                        {
                            if (_isInitialized)
                            {
                                ResourceManager.Release(handle);
                                return;
                            }

                            _prefabHandle = handle;
                            _loadedPrefab = handle.Asset;
                            _isInitialized = true;
                        }
                    }
                    else
                    {
                        Logger.LogError("[GameObjectPool] Failed to load asset synchronously: {Address}, handle invalid", _address);
                        //throw new Exception($"[GameObjectPool] Failed to load asset synchronously: {_address}, handle invalid");
                    }
                }
                else
                {
                    Logger.LogError("[GameObjectPool] ResourceManager not available for address: {Address}", _address);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("加载资源失败：{ExMessage}", ex.Message);
                //throw;
            }
        }

        /// <summary>
        /// 异步初始化
        /// </summary>
        public async UniTask InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                if (ResourceManager != null)
                {
                    var handle = await ResourceManager.LoadAsync<GameObject>(_address);
                    if (handle.IsValid)
                    {
                        lock (_lockObj)
                        {
                            if (_isInitialized)
                            {
                                ResourceManager.Release(handle);
                                return;
                            }

                            _prefabHandle = handle;
                            _loadedPrefab = handle.Asset;
                            _isInitialized = true;
                        }
                    }
                    else
                    {
                        Logger.LogError("[GameObjectPool] Failed to load asset asynchronously: {Address}, handle invalid", _address);
                    }
                }
                else
                {
                    Logger.LogError("[GameObjectPool] ResourceManager not available for address: {Address}", _address);
                }
            }
            catch (Exception e)
            {
                Logger.LogError("[GameObjectPool] Exception during initialization: {Ex}", e);
            }
        }

        /// <summary>
        /// 同步获取对象
        /// </summary>
        public GameObject Get()
        {
            if (!_isInitialized)
            {
                InitializeSync();
            }

            GameObject instance = null;
            lock (_lockObj)
            {
                while (_availableInstances.Count > 0)
                {
                    instance = _availableInstances.Pop();
                    if (instance != null) break;
                }
            }

            if (instance == null)
            {
                instance = CreateNewInstanceSync();
            }

            if (instance == null) return null;

            lock (_lockObj)
            {
                _activeInstanceIDs.Add(instance.GetInstanceID());
            }

            return instance;
        }

        /// <summary>
        /// 异步获取对象 (确保资源已加载)
        /// </summary>
        public async UniTask<GameObject> GetAsync()
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }

            GameObject instance = null;
            lock (_lockObj)
            {
                while (_availableInstances.Count > 0)
                {
                    instance = _availableInstances.Pop();
                    if (instance != null) break;
                }
            }

            if (instance == null)
            {
                instance = await CreateNewInstanceAsync();
            }

            if (instance == null)
            {
                Logger.LogError("[GameObjectPool] Failed to create new instance asynchronously for address: {Address}", _address);
                return null;
            }

            lock (_lockObj)
            {
                _activeInstanceIDs.Add(instance.GetInstanceID());
            }

            return instance;
        }

        public bool Return(GameObject instance)
        {
            if (instance == null) return false;

            lock (_lockObj)
            {
                int instanceID = instance.GetInstanceID();

                // 状态校验：是否已归还？
                if (!_activeInstanceIDs.Remove(instanceID))
                {
                    return false;
                }

                PrepareForRecycle(instance);

                _availableInstances.Push(instance);
                return true;
            }
        }

        /// <summary>
        /// 同步预实例化：确保池中可回收对象数量达到 targetAvailableCount。
        /// </summary>
        public void Warmup(int targetAvailableCount)
        {
            if (targetAvailableCount <= 0) return;

            if (!_isInitialized)
            {
                InitializeSync();
            }

            int deficit;
            lock (_lockObj)
            {
                deficit = targetAvailableCount - _availableInstances.Count;
            }

            for (int i = 0; i < deficit; i++)
            {
                var instance = CreateNewInstanceSync();
                if (instance == null) return;

                lock (_lockObj)
                {
                    _availableInstances.Push(instance);
                }
            }
        }

        /// <summary>
        /// 异步预实例化：按每帧最大创建数分帧执行，避免单帧尖峰。
        /// </summary>
        public async UniTask WarmupAsync(int targetAvailableCount, int maxCreatePerFrame = 8)
        {
            if (targetAvailableCount <= 0) return;
            if (maxCreatePerFrame <= 0) maxCreatePerFrame = 1;

            if (!_isInitialized)
            {
                await InitializeAsync();
            }

            int deficit;
            lock (_lockObj)
            {
                deficit = targetAvailableCount - _availableInstances.Count;
            }

            int createdThisFrame = 0;
            for (int i = 0; i < deficit; i++)
            {
                var instance = await CreateNewInstanceAsync();
                if (instance == null) return;

                lock (_lockObj)
                {
                    _availableInstances.Push(instance);
                }

                createdThisFrame++;
                if (createdThisFrame >= maxCreatePerFrame)
                {
                    createdThisFrame = 0;
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }
        }

        /// <summary>
        /// 设置回收对象位置，并可选择立即应用到池中已回收对象。
        /// </summary>
        public void SetRecyclePosition(Vector3 position, bool worldSpace = false, bool applyToExistingReturned = true)
        {
            lock (_lockObj)
            {
                _recyclePosition = position;
                _useWorldRecyclePosition = worldSpace;

                if (!applyToExistingReturned) return;

                foreach (var go in _availableInstances)
                {
                    if (go == null) continue;
                    ApplyRecycleTransform(go);
                }
            }
        }

        private GameObject CreateNewInstanceSync()
        {
            GameObject go = null;

            if (ResourceManager != null)
            {
                go = ResourceManager.InstantiateSync(_address, _poolRoot);
            }
            else if (_loadedPrefab != null) // 增加降级前的空检查
            {
                go = UnityEngine.Object.Instantiate(_loadedPrefab, _poolRoot);
            }

            // 仅在实例化成功时设置状态，避免空引用异常
            if (go != null)
            {
                PrepareForRecycle(go);
            }
            else
            {
                Logger.LogError("[GameObjectPool] Failed to create instance synchronously: ResourceManager not available and loaded prefab is null for address: {Address}", _address);
            }

            return go; // 兜底返回 null
        }
        
        private async UniTask<GameObject> CreateNewInstanceAsync()
        {
            GameObject go = null;

            if (ResourceManager != null)
            {
                go = await ResourceManager.InstantiateAsync(_address, _poolRoot);
            }
            else if (_loadedPrefab != null)
            {
                go = UnityEngine.Object.Instantiate(_loadedPrefab, _poolRoot);
            }
            else
            {
                Logger.LogError("[GameObjectPool] Cannot create instance: ResourceManager not available and loaded prefab is null for address: {Address}", _address);
                return null;
            }

            PrepareForRecycle(go);
            return go;
        }

        private void PrepareForRecycle(GameObject instance)
        {
            // nstance.SetActive(false);
            if (instance.transform.parent != _poolRoot)
            {
                instance.transform.SetParent(_poolRoot, worldPositionStays: false);
            }

            ApplyRecycleTransform(instance);
        }

        private void ApplyRecycleTransform(GameObject instance)
        {
            if (_useWorldRecyclePosition)
            {
                instance.transform.position = _recyclePosition;
            }
            else
            {
                instance.transform.localPosition = _recyclePosition;
            }
        }

        /// <summary>
        /// 清理孤儿对象 (被动调用)
        /// </summary>
        public void CleanOrphanedInstances()
        {
            lock (_lockObj)
            {
                var tempStack = new Stack<GameObject>(_availableInstances.Count);
                while (_availableInstances.Count > 0)
                {
                    var go = _availableInstances.Pop();
                    if (go != null) tempStack.Push(go);
                }

                _availableInstances.Clear();
                while (tempStack.Count > 0) _availableInstances.Push(tempStack.Pop());
            }
        }

        public void Clear()
        {
            lock (_lockObj)
            {
                foreach (var go in _availableInstances)
                {
                    if (go != null) UnityEngine.Object.Destroy(go);
                }

                _availableInstances.Clear();
                _activeInstanceIDs.Clear();

                // 释放资源句柄
                if (ResourceManager != null && _prefabHandle.IsValid)
                {
                    ResourceManager.Release(_prefabHandle);
                    _prefabHandle = default;
                }

                _isInitialized = false;
                _loadedPrefab = null;
            }
        }

        public void Dispose()
        {
            Clear();
        }
    }
}