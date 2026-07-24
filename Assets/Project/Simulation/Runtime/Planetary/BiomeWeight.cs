namespace Farion.Simulation.Planetary
{
    public readonly struct BiomeWeight
    {
        public BiomeWeight(BiomeDefinition biome, float weight)
        {
            Biome = biome;
            Weight = weight;
        }

        public BiomeDefinition Biome { get; }
        public float Weight { get; }
    }
}
