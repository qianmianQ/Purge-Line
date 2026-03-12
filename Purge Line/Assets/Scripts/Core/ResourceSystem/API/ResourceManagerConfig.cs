// ============================================================================
// PurgeLine.Resource — ResourceManagerConfig.cs
// 资源管理器全局配置，支持 ScriptableObject 序列化或代码构造
// ============================================================================

using System;

namespace PurgeLine.Resource
{
    /// <summary>
    /// 资源管理器配置。所有阈值均可运行时调整。
    /// </summary>
    [Serializable]
    public sealed class ResourceManagerConfig
    {
        // ── 加载队列 ─────────────────────────────────────────────
        /// <summary>每帧最大并发加载数</summary>
        public int MaxConcurrentLoads = 3;

        // ── LRU 缓存 ─────────────────────────────────────────────
        /// <summary>LRU 缓存最大条目数（引用归零后保留上限）</summary>
        public int LruCapacity = 128;

        /// <summary>LRU 条目超时秒数，超过后自动淘汰（0 = 不按时间淘汰）</summary>
        public float LruTimeoutSeconds = 300f;

        // ── 内存治理 ─────────────────────────────────────────────
        /// <summary>内存预警阈值（MB），超过后触发 LRU 淘汰</summary>
        public long MemoryWarningThresholdMB = 512;

        /// <summary>内存检查间隔（秒）</summary>
        public float MemoryCheckIntervalSeconds = 5f;

        /// <summary>内存超阈值时单次最大淘汰数</summary>
        public int MemoryEvictBatchSize = 16;

        // ── 重试策略 ─────────────────────────────────────────────
        /// <summary>加载失败最大重试次数</summary>
        public int MaxRetryCount = 3;

        /// <summary>重试基础延迟（秒）</summary>
        public float RetryBaseDelaySeconds = 0.5f;

        /// <summary>退避系数（指数退避）</summary>
        public float RetryBackoffMultiplier = 2f;

        /// <summary>降级占位资源地址（为空则不降级）</summary>
        public string FallbackAddress = string.Empty;

        // ── 对象池 ───────────────────────────────────────────────
        /// <summary>单个 Prefab 池默认容量上限</summary>
        public int DefaultPoolCapacity = 32;

        /// <summary>池对象超时回收秒数（0 = 不按时间回收）</summary>
        public float PoolTimeoutSeconds = 120f;

        /// <summary>
        /// 创建默认配置实例
        /// </summary>
        public static ResourceManagerConfig Default => new ResourceManagerConfig();
    }
}

