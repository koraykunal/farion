using UnityEngine;

namespace Farion.Rendering.Celestial
{
    public readonly struct CelestialCloudEffectData
    {
        public CelestialCloudEffectData(
            Vector3 center,
            float surfaceRadius,
            float innerRadius,
            float outerRadius,
            float atmosphereRadius,
            Matrix4x4 worldToLocalRotation,
            int seed,
            CelestialCloudProfile profile,
            CelestialAtmosphereProfile atmosphereProfile)
        {
            Center = center;
            SurfaceRadius = surfaceRadius;
            InnerRadius = innerRadius;
            OuterRadius = outerRadius;
            AtmosphereRadius = atmosphereRadius;
            WorldToLocalRotation = worldToLocalRotation;
            Seed = seed;
            Profile = profile;
            AtmosphereProfile = atmosphereProfile;
        }

        public Vector3 Center { get; }
        public float SurfaceRadius { get; }
        public float InnerRadius { get; }
        public float OuterRadius { get; }
        public float AtmosphereRadius { get; }
        public Matrix4x4 WorldToLocalRotation { get; }
        public int Seed { get; }
        public CelestialCloudProfile Profile { get; }
        public CelestialAtmosphereProfile AtmosphereProfile { get; }

        public float GetCameraSortDistance(Vector3 cameraPosition) =>
            Vector3.Distance(cameraPosition, Center) - OuterRadius;
    }
}
