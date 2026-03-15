using System;
using System.Collections.Generic;

namespace TowerDefense.Data.EntityData
{
    /// <summary>
    /// 简单 LRU (Least Recently Used) 缓存实现。
    /// 当缓存达到容量上限时，移除最久未访问的项。
    /// </summary>
    public class LRUCache<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;
        private readonly LinkedList<CacheItem> _lruList;

        public LRUCache(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));

            _capacity = capacity;
            _cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(capacity);
            _lruList = new LinkedList<CacheItem>();
        }

        /// <summary>
        /// 当前缓存项数量
        /// </summary>
        public int Count => _cache.Count;

        /// <summary>
        /// 缓存容量上限
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// 尝试获取缓存值
        /// </summary>
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                // 移动到链表头部（表示最近使用）
                _lruList.Remove(node);
                _lruList.AddFirst(node);

                value = node.Value.Value;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// 添加或更新缓存项
        /// </summary>
        public void Set(TKey key, TValue value)
        {
            // 如果已存在，更新值并移动到头部
            if (_cache.TryGetValue(key, out var existingNode))
            {
                existingNode.Value.Value = value;
                _lruList.Remove(existingNode);
                _lruList.AddFirst(existingNode);
                return;
            }

            // 如果达到容量上限，移除最久未使用的项（链表尾部）
            if (_cache.Count >= _capacity)
            {
                var leastRecentlyUsed = _lruList.Last;
                if (leastRecentlyUsed != null)
                {
                    _cache.Remove(leastRecentlyUsed.Value.Key);
                    _lruList.RemoveLast();
                }
            }

            // 添加新项到链表头部
            var newItem = new CacheItem(key, value);
            var newNode = _lruList.AddFirst(newItem);
            _cache[key] = newNode;
        }

        /// <summary>
        /// 移除指定缓存项
        /// </summary>
        public bool Remove(TKey key)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                _cache.Remove(key);
                _lruList.Remove(node);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            _lruList.Clear();
        }

        /// <summary>
        /// 获取所有缓存的键
        /// </summary>
        public IEnumerable<TKey> Keys => _cache.Keys;

        private class CacheItem
        {
            public TKey Key { get; }
            public TValue Value { get; set; }

            public CacheItem(TKey key, TValue value)
            {
                Key = key;
                Value = value;
            }
        }
    }
}
