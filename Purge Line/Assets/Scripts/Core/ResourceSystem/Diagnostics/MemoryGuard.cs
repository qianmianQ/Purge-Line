// ============================================================================
// PurgeLine.Resource.Diagnostics — MemoryGuard.cs
// 内存水位守卫：周期性检查内存占用，超阈值自动触发 LRU 淘汰
// ============================================================================

using Microsoft.Extensions.Logging;
using UnityEngine.Profiling;
using PurgeLine.Resource.Internal;

namespace PurgeLine.Resource.Diagnostics
{
    /// <summary>
    /// 内存水位守卫。由 ResourceManager.OnTick 每帧驱动。
    /// 按配置间隔检查当前进程内存，超阈值触发 ResourceCache LRU 淘汰。
    /// </summary>
    internal sealed class MemoryGuard
    {
        private readonly ResourceManagerConfig _config;
        private readonly ResourceCache _cache;
        private readonly ResourceMetrics _metrics;
        private readonly ILogger _logger;

        private float _timeSinceLastCheck;

        public MemoryGuard(
            ResourceManagerConfig config,
            ResourceCache cache,
            ResourceMetrics metrics,
            ILogger logger)
        {
            _config = config;
            _cache = cache;
            _metrics = metrics;
            _logger = logger;
            _timeSinceLastCheck = 0f;
        }

        /// <summary>
        /// 每帧调用。按配置间隔检查内存。
        /// </summary>
        /// <returns>本次淘汰的条目数</returns>
        public int Tick(float deltaTime)
        {
            _timeSinceLastCheck += deltaTime;
            if (_timeSinceLastCheck < _config.MemoryCheckIntervalSeconds)
                return 0;

            _timeSinceLastCheck = 0f;
            return CheckAndEvict();
        }

        private int CheckAndEvict()
        {
            long totalBytes = Profiler.GetTotalAllocatedMemoryLong();
            long totalMB = totalBytes / (1024L * 1024L);
            long threshold = _config.MemoryWarningThresholdMB;

            if (totalMB <= threshold)
                return 0;

            _logger.LogWarning(
                "[MemoryGuard] Memory {CurrentMB}MB exceeds threshold {ThresholdMB}MB, evicting LRU entries",
                totalMB, threshold);

            int evicted = _cache.EvictLru(_config.MemoryEvictBatchSize);

            for (int i = 0; i < evicted; i++)
                _metrics.RecordEviction();

            return evicted;
        }
    }
}

