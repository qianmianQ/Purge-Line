// ============================================================================
// PurgeLine.Resource — ResourceEvents.cs
// 资源系统事件定义，通过 EventManager.Global 或 R3 Observable 派发
// ============================================================================

namespace PurgeLine.Resource
{
    /// <summary>
    /// 资源加载成功事件
    /// </summary>
    public readonly struct ResourceLoadedEvent
    {
        public readonly string Address;
        public readonly ulong TraceId;
        public readonly float ElapsedMs;

        public ResourceLoadedEvent(string address, ulong traceId, float elapsedMs)
        {
            Address = address;
            TraceId = traceId;
            ElapsedMs = elapsedMs;
        }
    }

    /// <summary>
    /// 资源加载失败事件
    /// </summary>
    public readonly struct ResourceLoadFailedEvent
    {
        public readonly string Address;
        public readonly ulong TraceId;
        public readonly string Error;

        public ResourceLoadFailedEvent(string address, ulong traceId, string error)
        {
            Address = address;
            TraceId = traceId;
            Error = error;
        }
    }

    /// <summary>
    /// 内存预警事件
    /// </summary>
    public readonly struct MemoryWarningEvent
    {
        public readonly long CurrentMemoryMB;
        public readonly long ThresholdMB;
        public readonly int EvictedCount;

        public MemoryWarningEvent(long currentMemoryMB, long thresholdMB, int evictedCount)
        {
            CurrentMemoryMB = currentMemoryMB;
            ThresholdMB = thresholdMB;
            EvictedCount = evictedCount;
        }
    }

    /// <summary>
    /// 资源泄漏检测事件（Editor/Debug 专用）
    /// </summary>
    public readonly struct ResourceLeakDetectedEvent
    {
        public readonly string Address;
        public readonly int RefCount;

        public ResourceLeakDetectedEvent(string address, int refCount)
        {
            Address = address;
            RefCount = refCount;
        }
    }

    /// <summary>
    /// 预加载进度事件
    /// </summary>
    public readonly struct PreloadProgressEvent
    {
        public readonly int Loaded;
        public readonly int Total;
        public readonly float Progress;

        public PreloadProgressEvent(int loaded, int total)
        {
            Loaded = loaded;
            Total = total;
            Progress = total > 0 ? (float)loaded / total : 1f;
        }
    }
}

