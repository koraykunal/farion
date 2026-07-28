namespace Farion.Simulation.Planetary
{
    public readonly struct BiomeSample
    {
        public BiomeSample(
            BiomeDefinition biome,
            float suitability)
        {
            Biome = biome;
            Suitability = suitability;
        }

        public BiomeDefinition Biome { get; }
        public float Suitability { get; }
    }
}
