// ============================================================================
// PurgeLine.Resource.ObjectPool — ObjectPoolManager.cs
// 对象池管理器：管理所有 Prefab 的 GameObjectPool
// ============================================================================

using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace PurgeLine.Resource.ObjectPool
{
    /// <summary>
    /// 对象池管理器。按 Prefab address 维护独立的 GameObjectPool。
    /// </summary>
    internal sealed class ObjectPoolManager
    {
        private readonly Dictionary<string, GameObjectPool> _pools;
        private readonly ResourceManagerConfig _config;
        private readonly ILogger _logger;

        // 反向映射：InstanceId → address，用于归还时查找所属池
        private readonly Dictionary<int, string> _instanceToAddress;

        public ObjectPoolManager(ResourceManagerConfig config, ILogger logger)
        {
            _config = config;
            _logger = logger;
            _pools = new Dictionary<string, GameObjectPool>(32);
            _instanceToAddress = new Dictionary<int, string>(128);
        }

        /// <summary>
        /// 尝试从池中租借 GameObject
        /// </summary>
        public bool TryRent(string address, out GameObject go)
        {
            if (_pools.TryGetValue(address, out var pool) && pool.TryRent(out go))
            {
                _instanceToAddress[go.GetInstanceID()] = address;
                return true;
            }

            go = null;
            return false;
        }

        /// <summary>
        /// 归还 GameObject 到对应池。超容量或无池时返回 false。
        /// </summary>
        public bool Return(GameObject go)
        {
            if (go == null) return false;

            int instanceId = go.GetInstanceID();
            if (!_instanceToAddress.TryGetValue(instanceId, out var address))
            {
                _logger.LogWarning("[ObjectPoolManager] Cannot return unknown instance {InstanceId}", instanceId);
                return false;
            }

            if (!_pools.TryGetValue(address, out var pool))
            {
                pool = new GameObjectPool(address, _config.DefaultPoolCapacity, _config.PoolTimeoutSeconds);
                _pools[address] = pool;
            }

            if (pool.Return(go))
            {
                return true;
            }

            // 超容量，移除映射
            _instanceToAddress.Remove(instanceId);
            return false;
        }

        /// <summary>
        /// 注册实例到地址映射（实例化时调用）
        /// </summary>
        public void RegisterInstance(GameObject go, string address)
        {
            if (go != null)
            {
                _instanceToAddress[go.GetInstanceID()] = address;
                // 确保池存在
                if (!_pools.ContainsKey(address))
                {
                    _pools[address] = new GameObjectPool(address, _config.DefaultPoolCapacity, _config.PoolTimeoutSeconds);
                }
            }
        }

        /// <summary>
        /// 获取实例对应的地址
        /// </summary>
        public bool TryGetAddress(GameObject go, out string address)
        {
            if (go != null)
                return _instanceToAddress.TryGetValue(go.GetInstanceID(), out address);
            address = null;
            return false;
        }

        /// <summary>
        /// 淘汰所有池中超时的对象。返回总淘汰数。
        /// </summary>
        public int EvictExpired(float currentTime)
        {
            int total = 0;
            foreach (var kv in _pools)
            {
                total += kv.Value.EvictExpired(currentTime);
            }
            return total;
        }

        /// <summary>
        /// 销毁所有池
        /// </summary>
        public void DestroyAll()
        {
            foreach (var kv in _pools)
            {
                kv.Value.DestroyAll();
            }
            _pools.Clear();
            _instanceToAddress.Clear();
        }
    }
}


