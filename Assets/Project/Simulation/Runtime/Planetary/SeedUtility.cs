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
