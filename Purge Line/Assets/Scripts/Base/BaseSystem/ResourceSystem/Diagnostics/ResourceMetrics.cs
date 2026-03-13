// ============================================================================
// PurgeLine.Resource.Diagnostics — ResourceMetrics.cs
// 资源系统运行时指标收集，支持 Editor 监控窗口和 Profiler 面板
// ============================================================================

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PurgeLine.Resource.Diagnostics
{
    /// <summary>
    /// 单个资源的运行时指标快照
    /// </summary>
    public struct ResourceMetricEntry
    {
        public string Address;
        public int RefCount;
        public float LoadTimeMs;
        public long EstimatedMemoryBytes;
        public float LastAccessTime;
        public bool InLru;
    }

    /// <summary>
    /// 资源系统全局指标收集器（单例模式，主线程调用）。
    /// Editor 监控窗口通过此类获取实时数据。
    /// </summary>
    public sealed class ResourceMetrics
    {
        // ── 计数器 ───────────────────────────────────────────────
        private int _totalLoadRequests;
        private int _totalLoadSuccesses;
        private int _totalLoadFailures;
        private int _totalCacheHits;
        private int _totalCacheMisses;
        private int _totalRetries;
        private int _totalEvictions;
        private int _totalPoolHits;
        private int _totalPoolMisses;

        // ── 公开只读属性 ─────────────────────────────────────────
        public int TotalLoadRequests => _totalLoadRequests;
        public int TotalLoadSuccesses => _totalLoadSuccesses;
        public int TotalLoadFailures => _totalLoadFailures;
        public int TotalCacheHits => _totalCacheHits;
        public int TotalCacheMisses => _totalCacheMisses;
        public int TotalRetries => _totalRetries;
        public int TotalEvictions => _totalEvictions;
        public int TotalPoolHits => _totalPoolHits;
        public int TotalPoolMisses => _totalPoolMisses;

        // ── 增量方法（主线程调用，无需锁）─────────────────────────

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordLoadRequest() => _totalLoadRequests++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordLoadSuccess() => _totalLoadSuccesses++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordLoadFailure() => _totalLoadFailures++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordCacheHit() => _totalCacheHits++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordCacheMiss() => _totalCacheMisses++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordRetry() => _totalRetries++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordEviction() => _totalEvictions++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordPoolHit() => _totalPoolHits++;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordPoolMiss() => _totalPoolMisses++;

        /// <summary>
        /// 重置所有计数器（仅测试/Editor 使用）
        /// </summary>
        public void Reset()
        {
            _totalLoadRequests = 0;
            _totalLoadSuccesses = 0;
            _totalLoadFailures = 0;
            _totalCacheHits = 0;
            _totalCacheMisses = 0;
            _totalRetries = 0;
            _totalEvictions = 0;
            _totalPoolHits = 0;
            _totalPoolMisses = 0;
        }

        /// <summary>
        /// 收集所有缓存中的指标快照（用于 Editor 监控窗口）。
        /// 调用方传入预分配 list 以避免 GC。
        /// </summary>
        internal void CollectSnapshots(
            Internal.ReferenceTracker refTracker,
            Internal.ResourceCache cache,
            List<ResourceMetricEntry> output)
        {
            output.Clear();
            cache.CollectMetrics(refTracker, output);
        }
    }
}

