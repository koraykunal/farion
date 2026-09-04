using Farion.Simulation.Planetary;

namespace Farion.Rendering.Celestial
{
    public sealed class DerivedPlanetVisual
    {
        public DerivedPlanetVisual(
            SurfaceVisualProfile surfaceVisualProfile,
            TerrestrialSurfaceProfile surfaceProfile,
            CelestialOceanProfile oceanProfile,
            CelestialAtmosphereProfile atmosphereProfile,
            CelestialCloudProfile cloudProfile)
        {
            SurfaceVisualProfile = surfaceVisualProfile;
            SurfaceProfile = surfaceProfile;
            OceanProfile = oceanProfile;
            AtmosphereProfile = atmosphereProfile;
            CloudProfile = cloudProfile;
        }

        public SurfaceVisualProfile SurfaceVisualProfile { get; }
        public TerrestrialSurfaceProfile SurfaceProfile { get; }
        public CelestialOceanProfile OceanProfile { get; }
        public CelestialAtmosphereProfile AtmosphereProfile { get; }
        public CelestialCloudProfile CloudProfile { get; }

        public void Release()
        {
            ProfileVariants.Destroy(SurfaceProfile);
            ProfileVariants.Destroy(SurfaceVisualProfile);
            ProfileVariants.Destroy(OceanProfile);
            ProfileVariants.Destroy(AtmosphereProfile);
            ProfileVariants.Destroy(CloudProfile);
        }
    }
}
