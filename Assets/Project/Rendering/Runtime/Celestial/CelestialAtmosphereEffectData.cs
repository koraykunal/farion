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
            CelestialAtmosphereProfile profile)
        {
            Center = center;
            BodyRadius = bodyRadius;
            SurfaceRadius = surfaceRadius;
            AtmosphereRadius = atmosphereRadius;
            Profile = profile;
        }

        public Vector3 Center { get; }
        public float BodyRadius { get; }
        public float SurfaceRadius { get; }
        public float AtmosphereRadius { get; }
        public CelestialAtmosphereProfile Profile { get; }

        public float GetCameraSortDistance(Vector3 cameraPosition)
        {
            return Vector3.Distance(cameraPosition, Center) - AtmosphereRadius;
        }
    }
}
