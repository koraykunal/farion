using UnityEngine;

namespace Farion.Simulation.Planetary
{
    public static class PlanetEnvironmentGeometry
    {
        public static float GetOceanRadius(
            float bodyRadius,
            Vector2 terrainRadiusMinMax,
            float oceanLevel,
            float oceanRadiusOffset)
        {
            bodyRadius = Mathf.Max(0.01f, bodyRadius);
            float minRadius = terrainRadiusMinMax.x > 0f ? terrainRadiusMinMax.x : bodyRadius;
            float seaRadius = Mathf.Lerp(minRadius, bodyRadius, Mathf.Clamp01(oceanLevel));
            return Mathf.Max(0.01f, seaRadius + Mathf.Max(0f, oceanRadiusOffset));
        }

        public static float GetAtmosphereRadius(
            float atmosphereBaseRadius,
            float atmosphereScale,
            float atmosphereRadiusOffset)
        {
            atmosphereBaseRadius = Mathf.Max(0.01f, atmosphereBaseRadius);
            return Mathf.Max(
                atmosphereBaseRadius + 0.001f,
                atmosphereBaseRadius * (1f + Mathf.Max(0f, atmosphereScale)) +
                Mathf.Max(0f, atmosphereRadiusOffset));
        }
    }
}
