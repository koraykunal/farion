using UnityEngine;

namespace Farion.Rendering.Celestial
{
    public readonly struct CelestialAtmosphereEffectData
    {
        public CelestialAtmosphereEffectData(
            Vector3 center,
            float bodyRadius,
            float surfaceRadius,
            float atmosphereRadius,
            CelestialAtmosphereProfile profile,
            CelestialOceanEffectData oceanSurface = default)
        {
            Center = center;
            BodyRadius = bodyRadius;
            SurfaceRadius = surfaceRadius;
            AtmosphereRadius = atmosphereRadius;
            Profile = profile;
            OceanSurface = oceanSurface;
        }

        public Vector3 Center { get; }
        public float BodyRadius { get; }
        public float SurfaceRadius { get; }
        public float AtmosphereRadius { get; }
        public CelestialAtmosphereProfile Profile { get; }
        public CelestialOceanEffectData OceanSurface { get; }
        public bool HasOceanSurface => OceanSurface.OceanRadius > 0f;

        public float GetCameraSortDistance(Vector3 cameraPosition)
        {
            return Vector3.Distance(cameraPosition, Center) - AtmosphereRadius;
        }
    }
}
