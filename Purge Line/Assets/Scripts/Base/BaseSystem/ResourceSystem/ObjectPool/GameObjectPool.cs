// ============================================================================
// PurgeLine.Resource.ObjectPool — GameObjectPool.cs
// 单 Prefab 对象池：Stack 实现，支持容量限制和超时回收
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace PurgeLine.Resource.ObjectPool
{
    /// <summary>
    /// 池化 GameObject 条目
    /// </summary>
    internal struct PoolEntry
    {
        public GameObject Instance;
        public float ReturnTime;
    }

    /// <summary>
    /// 单个 Prefab 的 GameObject 对象池。
    /// 使用 Stack 实现 LIFO 复用策略（热缓存友好）。
    /// </summary>
    internal sealed class GameObjectPool
    {
        private readonly Stack<PoolEntry> _pool;
        private readonly int _capacity;
        private readonly float _timeout;
        private readonly string _address;

        /// <summary>池内当前可用对象数</summary>
        public int AvailableCount => _pool.Count;

        /// <summary>地址标识</summary>
        public string Address => _address;

        public GameObjectPool(string address, int capacity, float timeout)
        {
            _address = address;
            _capacity = capacity;
            _timeout = timeout;
            _pool = new Stack<PoolEntry>(capacity);
        }

        /// <summary>
        /// 尝试从池中租借一个 GameObject。成功返回 true。
        /// </summary>
        public bool TryRent(out GameObject go)
        {
            while (_pool.Count > 0)
            {
                var entry = _pool.Pop();
                // 对象可能已被外部销毁
                if (entry.Instance != null)
                {
                    go = entry.Instance;
                    go.SetActive(true);
                    return true;
                }
            }

            go = null;
            return false;
        }

        /// <summary>
        /// 归还 GameObject 到池中。超容量时返回 false（调用方销毁）。
        /// </summary>
        public bool Return(GameObject go)
        {
            if (go == null) return false;

            if (_pool.Count >= _capacity)
                return false;

            go.SetActive(false);
            _pool.Push(new PoolEntry
            {
                Instance = go,
                ReturnTime = Time.realtimeSinceStartup,
            });
            return true;
        }

        /// <summary>
        /// 淘汰超时的池对象。返回被销毁的数量。
        /// </summary>
        public int EvictExpired(float currentTime)
        {
            if (_timeout <= 0f || _pool.Count == 0)
                return 0;

            // Stack 不支持随机访问，需要临时缓冲
            int evicted = 0;
            var keepList = new List<PoolEntry>(_pool.Count);

            while (_pool.Count > 0)
            {
                var entry = _pool.Pop();
                if (entry.Instance == null)
                {
                    evicted++;
                    continue;
                }

                float age = currentTime - entry.ReturnTime;
                if (age >= _timeout)
                {
                    Object.Destroy(entry.Instance);
                    evicted++;
                }
                else
                {
                    keepList.Add(entry);
                }
            }

            // 反转放回保持 LIFO 顺序
            for (int i = keepList.Count - 1; i >= 0; i--)
                _pool.Push(keepList[i]);

            return evicted;
        }

        /// <summary>
        /// 销毁池中所有对象
        /// </summary>
        public void DestroyAll()
        {
            while (_pool.Count > 0)
            {
                var entry = _pool.Pop();
                if (entry.Instance != null)
                    Object.Destroy(entry.Instance);
            }
        }
    }
}

