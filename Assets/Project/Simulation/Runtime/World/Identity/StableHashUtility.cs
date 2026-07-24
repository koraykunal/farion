using System;

namespace Farion.Simulation.World.Identity
{
    public static class StableHashUtility
    {
        public const ulong OffsetBasis = 14695981039346656037UL;
        public const ulong Prime = 1099511628211UL;

        public static ulong Begin()
        {
            return OffsetBasis;
        }

        public static ulong Mix(ulong hash, byte value)
        {
            unchecked
            {
                hash ^= value;
                return hash * Prime;
            }
        }

        public static ulong Mix(ulong hash, int value)
        {
            return Mix(hash, unchecked((ulong)value));
        }

        public static ulong Mix(ulong hash, long value)
        {
            return Mix(hash, unchecked((ulong)value));
        }

        public static ulong Mix(ulong hash, ulong value)
        {
            unchecked
            {
                for (int i = 0; i < 8; i++)
                {
                    hash = Mix(hash, (byte)(value >> (i * 8)));
                }

                return hash;
            }
        }

        public static ulong Mix(ulong hash, string value)
        {
            unchecked
            {
                if (value == null)
                {
                    return Mix(hash, 0UL);
                }

                hash = Mix(hash, value.Length);
                for (int i = 0; i < value.Length; i++)
                {
                    hash = Mix(hash, (ulong)value[i]);
                }

                return hash;
            }
        }

        public static ulong Combine(string stream)
        {
            return Mix(Begin(), stream);
        }

        public static ulong Combine(ulong seed, string stream)
        {
            return Mix(Mix(Begin(), seed), stream);
        }

        public static ulong Combine(ulong seed, string stream, long slot)
        {
            return Mix(Combine(seed, stream), slot);
        }

        public static ulong NormalizeNonZero(ulong hash)
        {
            return hash != 0UL ? hash : 1UL;
        }
    }
}
