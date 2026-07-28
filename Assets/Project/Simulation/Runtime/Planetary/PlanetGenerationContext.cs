namespace Farion.Simulation.Planetary
{
    public readonly struct PlanetGenerationContext
    {
        public PlanetGenerationContext(
            int planetSeed,
            float radius,
            float surfaceGravity,
            PlanetType planetType,
            float atmosphereDensity,
            float backgroundRadiation)
        {
            PlanetSeed = planetSeed;
            Radius = radius;
            SurfaceGravity = surfaceGravity;
            PlanetType = planetType;
            AtmosphereDensity = atmosphereDensity > 0f ? atmosphereDensity : 0f;
            BackgroundRadiation = UnityEngine.Mathf.Clamp01(backgroundRadiation);
        }

        public int PlanetSeed { get; }
        public float Radius { get; }
        public float SurfaceGravity { get; }
        public PlanetType PlanetType { get; }
        public float AtmosphereDensity { get; }
        public bool HasAtmosphere => AtmosphereDensity > 0f;
        public float BackgroundRadiation { get; }

        public int DeriveSeed(string stream)
        {
            return SeedUtility.Derive(PlanetSeed, stream);
        }

        public static PlanetGenerationContext CreateDefault(int seed)
        {
            return new PlanetGenerationContext(seed, 50f, 9.81f, PlanetType.Rocky, 1f, 0.05f);
        }
    }
}
