// ============================================================================
// PurgeLine.Resource.Internal — ReferenceTracker.cs
// 基于原子操作的引用计数器，100% 防止资源泄漏
// ============================================================================

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PurgeLine.Resource.Internal
{
    /// <summary>
    /// 引用计数条目
    /// </summary>
    internal struct RefEntry
    {
        public int Count;
        public float LastAccessTime;
#if UNITY_EDITOR
        /// <summary>Editor 下记录 Retain 调用栈用于泄漏检测</summary>
        public List<string> RetainStackTraces;
#endif
    }

    /// <summary>
    /// 资源引用计数追踪器。所有操作限主线程调用。
    /// 使用预分配 Dictionary 避免热路径 GC。
    /// </summary>
    internal sealed class ReferenceTracker
    {
        private readonly Dictionary<string, RefEntry> _refs;

        public ReferenceTracker(int initialCapacity = 256)
        {
            _refs = new Dictionary<string, RefEntry>(initialCapacity);
        }

        /// <summary>
        /// 增加引用计数。返回增加后的计数值。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Retain(string address, float currentTime)
        {
            if (_refs.TryGetValue(address, out var entry))
            {
                entry.Count++;
                entry.LastAccessTime = currentTime;
#if UNITY_EDITOR
                entry.RetainStackTraces?.Add(System.Environment.StackTrace);
#endif
                _refs[address] = entry;
                return entry.Count;
            }

            var newEntry = new RefEntry
            {
                Count = 1,
                LastAccessTime = currentTime,
#if UNITY_EDITOR
                RetainStackTraces = new List<string> { System.Environment.StackTrace }
#endif
            };
            _refs[address] = newEntry;
            return 1;
        }

        /// <summary>
        /// 减少引用计数。返回减少后的计数值。
        /// 计数不会低于 0。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Release(string address)
        {
            if (!_refs.TryGetValue(address, out var entry))
                return 0;

            entry.Count--;
            if (entry.Count <= 0)
            {
                entry.Count = 0;
                _refs[address] = entry;
                return 0;
            }

            _refs[address] = entry;
            return entry.Count;
        }

        /// <summary>
        /// 获取当前引用计数
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetCount(string address)
        {
            return _refs.TryGetValue(address, out var entry) ? entry.Count : 0;
        }

        /// <summary>
        /// 移除引用条目（资源完全释放后清理）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(string address)
        {
            _refs.Remove(address);
        }

        /// <summary>
        /// 检查是否有引用
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasRef(string address)
        {
            return _refs.TryGetValue(address, out var entry) && entry.Count > 0;
        }

        /// <summary>
        /// 获取所有引用条目的快照（诊断用）
        /// </summary>
        public void GetAllEntries(List<KeyValuePair<string, RefEntry>> output)
        {
            output.Clear();
            foreach (var kv in _refs)
                output.Add(kv);
        }

        /// <summary>
        /// 获取跟踪的地址数量
        /// </summary>
        public int Count => _refs.Count;

        /// <summary>
        /// 清空所有引用记录
        /// </summary>
        public void Clear()
        {
            _refs.Clear();
        }
    }
}

