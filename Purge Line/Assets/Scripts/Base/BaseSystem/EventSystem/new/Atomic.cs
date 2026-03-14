#nullable disable
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Base.BaseSystem.EventSystem.New
{
    internal static class Atomic
    {
        public static int Increment(ref int location)
        {
            return Interlocked.Increment(ref location);
        }

        public static uint Increment(ref uint location)
        {
            int value = (int)location;
            int result = Interlocked.Increment(ref value);
            location = (uint)result;
            return location;
        }

        public static long Increment(ref long location)
        {
            return Interlocked.Increment(ref location);
        }

        public static bool TryIncrement(ref int location, int max)
        {
            int old, newValue;
            do
            {
                old = location;
                if (old >= max) return false;
                newValue = old + 1;
            }
            while (Interlocked.CompareExchange(ref location, newValue, old) != old);
            return true;
        }
    }
}
