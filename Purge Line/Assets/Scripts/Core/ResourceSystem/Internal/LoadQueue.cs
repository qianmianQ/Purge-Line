// ============================================================================
// PurgeLine.Resource.Internal — LoadQueue.cs
// 优先级加载队列：基于最小堆的优先级调度，支持并发控制
// ============================================================================

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace PurgeLine.Resource.Internal
{
    /// <summary>
    /// 加载请求
    /// </summary>
    internal struct LoadRequest
    {
        public string Address;
        public Type AssetType;
        public LoadPriority Priority;
        public ulong TraceId;
        public CancellationToken Ct;

        /// <summary>插入时间戳，同优先级按 FIFO 排序</summary>
        public long InsertOrder;

        // 完成回调（通过 UniTaskCompletionSource 传递结果）
        // 使用 object 以支持泛型擦除
        public object CompletionSource;
    }

    /// <summary>
    /// 优先级加载队列。基于最小堆实现。
    /// 每帧从队列中出队一定数量的请求交给加载器执行。
    /// </summary>
    internal sealed class LoadQueue
    {
        private readonly List<LoadRequest> _heap;
        private long _insertCounter;

        public int Count => _heap.Count;

        public LoadQueue(int initialCapacity = 64)
        {
            _heap = new List<LoadRequest>(initialCapacity);
            _insertCounter = 0;
        }

        /// <summary>
        /// 入队加载请求
        /// </summary>
        public void Enqueue(LoadRequest request)
        {
            request.InsertOrder = _insertCounter++;
            _heap.Add(request);
            SiftUp(_heap.Count - 1);
        }

        /// <summary>
        /// 出队最高优先级请求。队列为空时返回 false。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out LoadRequest request)
        {
            if (_heap.Count == 0)
            {
                request = default;
                return false;
            }

            request = _heap[0];
            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);

            if (_heap.Count > 0)
                SiftDown(0);

            return true;
        }

        /// <summary>
        /// 查看队首但不出队
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeek(out LoadRequest request)
        {
            if (_heap.Count == 0)
            {
                request = default;
                return false;
            }

            request = _heap[0];
            return true;
        }

        /// <summary>
        /// 清空队列（取消所有待处理请求）
        /// </summary>
        public void Clear()
        {
            _heap.Clear();
        }

        // ── 最小堆操作 ───────────────────────────────────────────

        private void SiftUp(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (Compare(_heap[index], _heap[parent]) < 0)
                {
                    Swap(index, parent);
                    index = parent;
                }
                else
                {
                    break;
                }
            }
        }

        private void SiftDown(int index)
        {
            int count = _heap.Count;
            while (true)
            {
                int smallest = index;
                int left = 2 * index + 1;
                int right = 2 * index + 2;

                if (left < count && Compare(_heap[left], _heap[smallest]) < 0)
                    smallest = left;
                if (right < count && Compare(_heap[right], _heap[smallest]) < 0)
                    smallest = right;

                if (smallest == index)
                    break;

                Swap(index, smallest);
                index = smallest;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Compare(LoadRequest a, LoadRequest b)
        {
            int cmp = ((byte)a.Priority).CompareTo((byte)b.Priority);
            if (cmp != 0) return cmp;
            return a.InsertOrder.CompareTo(b.InsertOrder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Swap(int i, int j)
        {
            var tmp = _heap[i];
            _heap[i] = _heap[j];
            _heap[j] = tmp;
        }
    }
}

