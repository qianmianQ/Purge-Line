#nullable disable
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Base.BaseSystem.EventSystem.New
{
    public readonly struct EventKey : IEquatable<EventKey>
    {
        public readonly int TypeHash;
        public readonly uint Sequence;
        private readonly int _precomputedHash;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal EventKey(int typeHash, uint sequence)
        {
            TypeHash = typeHash;
            Sequence = sequence;
            _precomputedHash = ComputeHashCode(typeHash, sequence);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ComputeHashCode(int typeHash, uint sequence)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + typeHash;
                hash = hash * 23 + (int)sequence;
                return hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(EventKey other)
        {
            if (_precomputedHash != other._precomputedHash) return false;
            return TypeHash == other.TypeHash && Sequence == other.Sequence;
        }

        public override bool Equals(object? obj)
        {
            return obj is EventKey other && Equals(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return _precomputedHash;
        }

        public static bool operator ==(EventKey left, EventKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(EventKey left, EventKey right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"EventKey({TypeHash:x8}:{Sequence})";
        }
    }

    public readonly struct EventKey<T> : IEquatable<EventKey<T>>
    {
        internal readonly EventKey _core;

        internal EventKey(EventKey core)
        {
            _core = core;
        }

        internal EventKey(int typeHash, uint sequence)
        {
            _core = new EventKey(typeHash, sequence);
        }

        public static implicit operator EventKey(EventKey<T> key)
        {
            return key._core;
        }

        public bool Equals(EventKey<T> other)
        {
            return _core.Equals(other._core);
        }

        public override bool Equals(object? obj)
        {
            return obj is EventKey<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _core.GetHashCode();
        }

        public static bool operator ==(EventKey<T> left, EventKey<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(EventKey<T> left, EventKey<T> right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"EventKey<{typeof(T).Name}>({_core.TypeHash:x8}:{_core.Sequence})";
        }
    }

    public static class EventKeyFactory
    {
        private static uint _globalSequence;
        private static readonly ConcurrentDictionary<Type, object> _defaultKeys = new ConcurrentDictionary<Type, object>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EventKey<T> Create<T>(uint sequence = 0)
        {
            var typeHash = typeof(T).GetHashCode();
            var seq = sequence == 0 ? Atomic.Increment(ref _globalSequence) : sequence;
            return new EventKey<T>(new EventKey(typeHash, seq));
        }

        public static EventKey<T> Default<T>()
        {
            return (EventKey<T>)_defaultKeys.GetOrAdd(typeof(T), _ => Create<T>(sequence: 1));
        }
    }
}
