// ============================================================================
// PurgeLine.Resource.Diagnostics — TraceContext.cs
// 加载请求追踪上下文，每次加载生成唯一 TraceId 用于日志关联
// ============================================================================

using System.Runtime.CompilerServices;
using System.Threading;

namespace PurgeLine.Resource.Diagnostics
{
    /// <summary>
    /// 追踪上下文：为每次资源加载请求分配单调递增的 TraceId。
    /// 线程安全，使用 Interlocked 原子操作，零 GC。
    /// </summary>
    internal static class TraceContext
    {
        private static long _counter;

        /// <summary>
        /// 生成下一个唯一 TraceId
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong NextTraceId()
        {
            return (ulong)Interlocked.Increment(ref _counter);
        }

        /// <summary>
        /// 重置计数器（仅测试/Editor 使用）
        /// </summary>
        internal static void Reset()
        {
            Interlocked.Exchange(ref _counter, 0);
        }
    }
}

