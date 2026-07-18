using UnityEngine;

namespace Farion.Rendering.Celestial
{
    public readonly struct CelestialOceanEffectData
    {
        public CelestialOceanEffectData(
            Vector3 center,
            float bodyRadius,
            Vector2 terrainRadiusRange,
            float oceanRadius,
            CelestialOceanProfile profile)
        {
            Center = center;
            BodyRadius = bodyRadius;
            TerrainRadiusRange = terrainRadiusRange;
            OceanRadius = oceanRadius;
            Profile = profile;
        }

        public Vector3 Center { get; }
        public float BodyRadius { get; }
        public Vector2 TerrainRadiusRange { get; }
        public float OceanRadius { get; }
        public CelestialOceanProfile Profile { get; }

        public float GetCameraSortDistance(Vector3 cameraPosition)
        {
            return Vector3.Distance(cameraPosition, Center) - OceanRadius;
        }
    }
}
