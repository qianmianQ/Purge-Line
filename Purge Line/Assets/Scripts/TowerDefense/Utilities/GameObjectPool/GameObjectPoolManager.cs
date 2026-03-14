using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine;
using VContainer;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace TowerDefense.TowerDefense.Utilities.GameObjectPool
{
    /// <summary>
    /// 对象池管理器接口
    /// </summary>
    public interface IGameObjectPoolManager : IDisposable
    {
        UniTask CreatePoolAsync(string address, int initialSize = 5, bool trackInstanceAddress = true);
        void CreatePool(string address, int initialSize = 5, bool trackInstanceAddress = true);
        UniTask PreloadAsync(string address, int targetAvailableCount, int maxCreatePerFrame = 8);
        void Preload(string address, int targetAvailableCount);
        void SetRecyclePositionForAllPools(Vector3 position, bool worldSpace = false, bool applyToExistingReturned = true);
        GameObject Get(string address);
        UniTask<GameObject> GetAsync(string address);
        void Return(GameObject instance, string address = null);
        void CleanOrphanedInstances();
        void Clear();
    }

    /// <summary>
    /// 对象池管理器
    /// </summary>
    public class GameObjectPoolManager : IGameObjectPoolManager
    {
        private readonly Dictionary<string, GameObjectPool> _pools = new Dictionary<string, GameObjectPool>();
        private readonly Dictionary<int, string> _instanceToAddressMap = new Dictionary<int, string>();
        private readonly Dictionary<string, bool> _poolTrackAddressMap = new Dictionary<string, bool>();
        
        private static readonly ILogger Logger = GameLogger.Create<GameObjectPoolManager>();

        private readonly Transform _globalPoolRoot;
        private readonly object _lockObj = new object();
        
        private readonly IObjectResolver _container;

        private Vector3 _recyclePosition = Vector3.zero;
        private bool _recycleWorldSpace;

        public GameObjectPoolManager(IObjectResolver container)
        {
            _container = container;
            
            var go = new GameObject("[PoolManager_Root]");
            _globalPoolRoot = go.transform;
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        /// <summary>
        /// 预热池 (异步)
        /// </summary>
        public async UniTask CreatePoolAsync(string address, int initialSize = 5, bool trackInstanceAddress = true)
        {
            GameObjectPool pool;
            lock (_lockObj)
            {
                if (!_pools.TryGetValue(address, out pool))
                {
                    pool = new GameObjectPool(address, initialSize, _globalPoolRoot);
                    _container.Inject(pool); // 注入依赖
                    pool.SetRecyclePosition(_recyclePosition, _recycleWorldSpace, applyToExistingReturned: false);
                    _pools[address] = pool;
                    _poolTrackAddressMap[address] = trackInstanceAddress;
                }
                else if (_poolTrackAddressMap.TryGetValue(address, out var oldTrackFlag) && oldTrackFlag != trackInstanceAddress)
                {
                    Logger.LogWarning("[PoolManager] Pool {Address} already exists with trackInstanceAddress={OldFlag}; ignore new value {NewFlag}.",
                        address, oldTrackFlag, trackInstanceAddress);
                }
            }

            await pool.InitializeAsync();
            await pool.WarmupAsync(initialSize);
        }

        /// <summary>
        /// 预热池 (同步)
        /// </summary>
        public void CreatePool(string address, int initialSize = 5, bool trackInstanceAddress = true)
        {
            GameObjectPool pool;
            lock (_lockObj)
            {
                if (!_pools.TryGetValue(address, out pool))
                {
                    pool = new GameObjectPool(address, initialSize, _globalPoolRoot);
                    _container.Inject(pool); // 注入依赖
                    pool.SetRecyclePosition(_recyclePosition, _recycleWorldSpace, applyToExistingReturned: false);
                    _pools[address] = pool;
                    _poolTrackAddressMap[address] = trackInstanceAddress;
                }
                else if (_poolTrackAddressMap.TryGetValue(address, out var oldTrackFlag) && oldTrackFlag != trackInstanceAddress)
                {
                    Logger.LogWarning("[PoolManager] Pool {Address} already exists with trackInstanceAddress={OldFlag}; ignore new value {NewFlag}.",
                        address, oldTrackFlag, trackInstanceAddress);
                }
            }

            pool.InitializeSync();
            pool.Warmup(initialSize);
        }

        public async UniTask PreloadAsync(string address, int targetAvailableCount, int maxCreatePerFrame = 8)
        {
            var pool = GetOrCreatePool(address);
            await pool.WarmupAsync(targetAvailableCount, maxCreatePerFrame);
        }

        public void Preload(string address, int targetAvailableCount)
        {
            var pool = GetOrCreatePool(address);
            pool.Warmup(targetAvailableCount);
        }

        public void SetRecyclePositionForAllPools(Vector3 position, bool worldSpace = false, bool applyToExistingReturned = true)
        {
            List<GameObjectPool> poolsSnapshot;
            lock (_lockObj)
            {
                _recyclePosition = position;
                _recycleWorldSpace = worldSpace;
                poolsSnapshot = new List<GameObjectPool>(_pools.Values);
            }

            foreach (var pool in poolsSnapshot)
            {
                pool.SetRecyclePosition(position, worldSpace, applyToExistingReturned);
            }
        }

        /// <summary>
        /// 同步获取对象
        /// </summary>
        public GameObject Get(string address)
        {
            GameObjectPool pool = GetOrCreatePool(address);
            if (pool == null) return null;

            GameObject instance = pool.Get();

            lock (_lockObj)
            {
                if (instance != null && ShouldTrackAddress(address)) _instanceToAddressMap[instance.GetInstanceID()] = address;
            }

            return instance;
        }

        /// <summary>
        /// 异步获取对象
        /// </summary>
        public async UniTask<GameObject> GetAsync(string address)
        {
            GameObjectPool pool = GetOrCreatePool(address);
            if (pool == null) return null;

            GameObject instance = await pool.GetAsync();

            lock (_lockObj)
            {
                if (instance != null && ShouldTrackAddress(address)) _instanceToAddressMap[instance.GetInstanceID()] = address;
            }

            return instance;
        }

        private GameObjectPool GetOrCreatePool(string address)
        {
            lock (_lockObj)
            {
                if (!_pools.TryGetValue(address, out var pool))
                {
                    // 懒加载创建池，使用默认大小
                    pool = new GameObjectPool(address, 1, _globalPoolRoot);
                    pool.SetRecyclePosition(_recyclePosition, _recycleWorldSpace, applyToExistingReturned: false);
                    _pools[address] = pool;
                    _poolTrackAddressMap[address] = true;
                }

                return pool;
            }
        }

        public void Return(GameObject instance, string address = null)
        {
            if (instance == null) return;

            int instanceID = instance.GetInstanceID();
            GameObjectPool pool = null;

            lock (_lockObj)
            {
                if (!string.IsNullOrEmpty(address))
                {
                    // 外部显式传入 address 时，不依赖内部映射，可用于关闭映射追踪的高性能模式。
                    _instanceToAddressMap.Remove(instanceID);
                }
                else
                {
                    _instanceToAddressMap.TryGetValue(instanceID, out address);
                    if (string.IsNullOrEmpty(address))
                    {
                        Logger.LogError($"[PoolManager] Attempted to return instance with ID {instanceID} that is not tracked in the pool manager.");
                        return;
                    }
                }
                _instanceToAddressMap.Remove(instanceID);
                _pools.TryGetValue(address, out pool);
            }

            // 避免在管理器锁内进入子池锁，消除反向锁顺序导致的死锁风险。
            pool?.Return(instance);
        }

        public void CleanOrphanedInstances()
        {
            List<GameObjectPool> poolsSnapshot;
            lock (_lockObj)
            {
                poolsSnapshot = new List<GameObjectPool>(_pools.Values);
            }

            foreach (var pool in poolsSnapshot)
            {
                pool.CleanOrphanedInstances();
            }
        }

        public void Clear()
        {
            List<GameObjectPool> poolsToDispose;
            lock (_lockObj)
            {
                poolsToDispose = new List<GameObjectPool>(_pools.Values);

                _pools.Clear();
                _instanceToAddressMap.Clear();
                _poolTrackAddressMap.Clear();
            }

            foreach (var pool in poolsToDispose)
            {
                pool.Dispose();
            }
        }

        public void Dispose()
        {
            Clear();
            if (_globalPoolRoot != null && _globalPoolRoot.gameObject != null)
            {
                UnityEngine.Object.Destroy(_globalPoolRoot.gameObject);
            }
        }

        private bool ShouldTrackAddress(string address)
        {
            return _poolTrackAddressMap.TryGetValue(address, out var trackAddress) && trackAddress;
        }
    }
}