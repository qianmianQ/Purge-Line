using System;
using System.Collections.Generic;
using System.Threading;

namespace TowerDefense.Data
{
    /// <summary>
    /// 共享数据仓库 — 用于 Managed ↔ ECS 数据传递
    ///
    /// ECS 的 IComponentData 不支持托管类型（string, byte[] 等），
    /// 因此通过此静态仓库存放关卡数据，ECS System 通过 ID 索引获取。
    ///
    /// 线程安全：使用 Interlocked 原子操作生成唯一 ID，
    /// 字典访问仅在主线程进行（Unity 约束）。
    /// </summary>
    public static class SharedLevelDataStore
    {
        private static int _nextId;
        private static readonly Dictionary<int, LevelConfig> _store = new Dictionary<int, LevelConfig>();

        /// <summary>
        /// 存储关卡配置，返回唯一 ID
        /// </summary>
        /// <param name="config">关卡配置</param>
        /// <returns>数据 ID，用于 GridSpawnRequest.CellDataId</returns>
        public static int Store(LevelConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            int id = Interlocked.Increment(ref _nextId);
            _store[id] = config;
            return id;
        }

        /// <summary>
        /// 获取并移除关卡配置（消费语义）
        /// </summary>
        /// <param name="id">数据 ID</param>
        /// <returns>关卡配置，不存在返回 null</returns>
        public static LevelConfig Consume(int id)
        {
            if (_store.TryGetValue(id, out var config))
            {
                _store.Remove(id);
                return config;
            }
            return null;
        }

        /// <summary>
        /// 获取关卡配置（不移除）
        /// </summary>
        public static LevelConfig Peek(int id)
        {
            _store.TryGetValue(id, out var config);
            return config;
        }

        /// <summary>
        /// 清空所有数据（用于场景卸载时清理）
        /// </summary>
        public static void Clear()
        {
            _store.Clear();
        }

        /// <summary>当前存储数量</summary>
        public static int Count => _store.Count;
    }
}

