#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Base.BaseSystem.EventSystem.New
{
    internal sealed class EventHandlerStorage
    {
        private readonly Dictionary<EventKey, object> _storage = new();
        private readonly SpinLock _lock = new(enableThreadOwnerTracking: false);

        private struct CacheEntry
        {
            public EventKey Key;
            public object Value;
            public long LastAccessTime;
        }

        private readonly CacheEntry[] _hotCache = new CacheEntry[16];

        public HandlerArray<T> GetOrCreateArray<T>(EventKey key)
        {
            var now = Stopwatch.GetTimestamp();

            for (int i = 0; i < _hotCache.Length; i++)
            {
                if (_hotCache[i].Key.Equals(key))
                {
                    _hotCache[i].LastAccessTime = now;
                    return (HandlerArray<T>)_hotCache[i].Value;
                }
            }

            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);

                if (_storage.TryGetValue(key, out var existing))
                {
                    UpdateHotCache(key, existing, now);
                    return (HandlerArray<T>)existing;
                }

                var array = new HandlerArray<T>();
                _storage[key] = array;
                UpdateHotCache(key, array, now);
                return array;
            }
            finally
            {
                if (lockTaken) _lock.Exit();
            }
        }

        public bool TryGetArray<T>(EventKey key, out HandlerArray<T> array)
        {
            var now = Stopwatch.GetTimestamp();

            for (int i = 0; i < _hotCache.Length; i++)
            {
                if (_hotCache[i].Key.Equals(key))
                {
                    _hotCache[i].LastAccessTime = now;
                    array = (HandlerArray<T>)_hotCache[i].Value;
                    return true;
                }
            }

            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);

                if (_storage.TryGetValue(key, out var existing))
                {
                    UpdateHotCache(key, existing, now);
                    array = (HandlerArray<T>)existing;
                    return true;
                }

                array = default;
                return false;
            }
            finally
            {
                if (lockTaken) _lock.Exit();
            }
        }

        private void UpdateHotCache(EventKey key, object value, long timestamp)
        {
            int oldestIndex = 0;
            long oldestTime = long.MaxValue;
            for (int i = 0; i < _hotCache.Length; i++)
            {
                if (_hotCache[i].LastAccessTime < oldestTime)
                {
                    oldestTime = _hotCache[i].LastAccessTime;
                    oldestIndex = i;
                }
            }
            _hotCache[oldestIndex] = new CacheEntry
            {
                Key = key,
                Value = value,
                LastAccessTime = timestamp
            };
        }
    }

    internal struct SpinLock
    {
        private int _lockState;

        public SpinLock(bool enableThreadOwnerTracking = false)
        {
            _lockState = 0;
        }

        public void Enter(ref bool lockTaken)
        {
            int iterations = 0;
            while (System.Threading.Interlocked.CompareExchange(ref _lockState, 1, 0) != 0)
            {
                if (iterations < 10)
                {
                    System.Threading.Thread.Yield();
                }
                else
                {
                    System.Threading.Thread.Sleep(1);
                }
                iterations++;
            }
            lockTaken = true;
        }

        public void Exit()
        {
            System.Threading.Interlocked.Exchange(ref _lockState, 0);
        }
    }
}
