using UnityEngine;

namespace Farion.Simulation.Planetary
{
    public static class SeedUtility
    {
        public static int Derive(int seed, string stream)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = Mix(hash, seed);

                if (!string.IsNullOrEmpty(stream))
                {
                    for (int i = 0; i < stream.Length; i++)
                    {
                        hash ^= stream[i];
                        hash *= 16777619u;
                    }
                }

                return (int)(hash & 0x7fffffff);
            }
        }

        public static int Derive(int seed, int salt, string stream)
        {
            return Derive(Derive(seed, stream), salt.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        public static float Unit01(int seed, string stream)
        {
            return Derive(seed, stream) / 2147483647f;
        }

        public static float Range(int seed, string stream, Vector2 range)
        {
            return Mathf.Lerp(range.x, range.y, Unit01(seed, stream));
        }

        public static int RangeInt(int seed, string stream, Vector2Int range)
        {
            int min = Mathf.Min(range.x, range.y);
            int max = Mathf.Max(range.x, range.y);
            return Mathf.Clamp(min + Mathf.FloorToInt(Unit01(seed, stream) * (max - min + 1)), min, max);
        }

        static uint Mix(uint hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                return hash * 16777619u;
            }
        }
    }
}
