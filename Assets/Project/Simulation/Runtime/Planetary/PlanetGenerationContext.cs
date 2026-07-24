namespace Farion.Simulation.Planetary
{
    public readonly struct PlanetGenerationContext
    {
        public PlanetGenerationContext(
            int planetSeed,
            float radius,
            float surfaceGravity,
            PlanetType planetType,
            ClimateType climate,
            bool hasAtmosphere,
            float atmosphereDensity,
            bool hasStableLiquidSurface,
            float liquidCoverage,
            float meanTemperatureCelsius,
            float radiationLevel)
        {
            PlanetSeed = planetSeed;
            Radius = radius;
            SurfaceGravity = surfaceGravity;
            PlanetType = planetType;
            Climate = climate;
            HasAtmosphere = hasAtmosphere;
            AtmosphereDensity = atmosphereDensity;
            HasStableLiquidSurface = hasStableLiquidSurface;
            LiquidCoverage = liquidCoverage;
            MeanTemperatureCelsius = meanTemperatureCelsius;
            RadiationLevel = radiationLevel;
        }

        public int PlanetSeed { get; }
        public float Radius { get; }
        public float SurfaceGravity { get; }
        public PlanetType PlanetType { get; }
        public ClimateType Climate { get; }
        public bool HasAtmosphere { get; }
        public float AtmosphereDensity { get; }
        public bool HasStableLiquidSurface { get; }
        public float LiquidCoverage { get; }
        public float MeanTemperatureCelsius { get; }
        public float RadiationLevel { get; }

        public int DeriveSeed(string stream)
        {
            return SeedUtility.Derive(PlanetSeed, stream);
        }

        public static PlanetGenerationContext CreateDefault(int seed)
        {
            return new PlanetGenerationContext(seed, 50f, 9.81f, PlanetType.Rocky, ClimateType.Temperate, true, 1f, false, 0f, 15f, 0.05f);
        }
    }
}
