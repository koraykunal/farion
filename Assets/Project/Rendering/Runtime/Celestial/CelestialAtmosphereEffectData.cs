using UnityEngine;

namespace Farion.Rendering.Celestial
{
    public readonly struct CelestialAtmosphereEffectData
    {
        public CelestialAtmosphereEffectData(
            Vector3 center,
            float bodyRadius,
            float atmosphereRadius,
            float oceanRadius,
            CelestialAtmosphereProfile profile)
        {
            Center = center;
            BodyRadius = bodyRadius;
            AtmosphereRadius = atmosphereRadius;
            OceanRadius = oceanRadius;
            Profile = profile;
        }

        public Vector3 Center { get; }
        public float BodyRadius { get; }
        public float AtmosphereRadius { get; }
        public float OceanRadius { get; }
        public CelestialAtmosphereProfile Profile { get; }

        public float GetCameraSortDistance(Vector3 cameraPosition)
        {
            return Vector3.Distance(cameraPosition, Center) - AtmosphereRadius;
        }
    }
}
