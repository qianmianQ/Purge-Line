#nullable disable
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Base.BaseSystem.EventSystem.New
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct PaddedLong
    {
        [FieldOffset(0)]
        public long Value;
    }

    internal sealed class RingBuffer<T>
    {
        private readonly T[] _buffer;
        private readonly int _mask;
        private PaddedLong _writeCursor;
        private PaddedLong _readCursor;

        public RingBuffer(int capacity = 1024)
        {
            int size = RoundUpToPowerOf2(capacity);
            _buffer = new T[size];
            _mask = size - 1;
            _writeCursor.Value = 0;
            _readCursor.Value = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int RoundUpToPowerOf2(int x)
        {
            if (x <= 0) return 1;
            x--;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x + 1;
        }

        public bool TryEnqueue(T item)
        {
            long current = _writeCursor.Value;
            long next = current + 1;

            if (next - _readCursor.Value > _buffer.Length)
                return false;

            _buffer[current & _mask] = item;
            _writeCursor.Value = next;
            return true;
        }

        public int DequeueBatch(Span<T> destination)
        {
            long readPos = _readCursor.Value;
            long writePos = _writeCursor.Value;

            int count = (int)(writePos - readPos);
            if (count == 0) return 0;

            count = Math.Min(count, destination.Length);

            int index = (int)(readPos & _mask);
            int firstCopy = Math.Min(count, _buffer.Length - index);
            new Span<T>(_buffer, index, firstCopy).CopyTo(destination);

            if (firstCopy < count)
            {
                new Span<T>(_buffer, 0, count - firstCopy)
                    .CopyTo(destination.Slice(firstCopy));
            }

            _readCursor.Value = readPos + count;
            return count;
        }
    }
}
