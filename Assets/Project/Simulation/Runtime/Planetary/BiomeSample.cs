namespace Farion.Simulation.Planetary
{
    public readonly struct BiomeSample
    {
        public BiomeSample(
            BiomeDefinition biome,
            float temperatureNoise,
            float moistureNoise,
            float altitude,
            float slopeDegrees)
        {
            Biome = biome;
            TemperatureNoise = temperatureNoise;
            MoistureNoise = moistureNoise;
            Altitude = altitude;
            SlopeDegrees = slopeDegrees;
        }

        public BiomeDefinition Biome { get; }
        public float TemperatureNoise { get; }
        public float MoistureNoise { get; }
        public float Altitude { get; }
        public float SlopeDegrees { get; }
    }
}
