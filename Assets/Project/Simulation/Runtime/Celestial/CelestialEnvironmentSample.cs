using Farion.Simulation.Physics;
using UnityEngine;

namespace Farion.Simulation.Celestial
{
    public readonly struct CelestialEnvironmentSample
    {
        public CelestialEnvironmentSample(
            CelestialBody body,
            bool hasOcean,
            float oceanRadius,
            bool hasAtmosphere,
            float atmosphereRadius,
            Vector2 terrainRadiusMinMax,
            float atmosphereBaseRadius)
        {
            Body = body;
            HasOcean = hasOcean;
            OceanRadius = Mathf.Max(0f, oceanRadius);
            HasAtmosphere = hasAtmosphere;
            AtmosphereRadius = Mathf.Max(0f, atmosphereRadius);
            TerrainRadiusMinMax = terrainRadiusMinMax;
            AtmosphereBaseRadius = Mathf.Max(0f, atmosphereBaseRadius);
        }

        public CelestialBody Body { get; }
        public bool HasBody => Body != null;
        public bool HasOcean { get; }
        public float OceanRadius { get; }
        public bool HasAtmosphere { get; }
        public float AtmosphereRadius { get; }
        public Vector2 TerrainRadiusMinMax { get; }
        public float AtmosphereBaseRadius { get; }

        public static CelestialEnvironmentSample Empty(CelestialBody body)
        {
            float radius = body != null ? Mathf.Max(0.01f, body.Radius) : 0f;
            return new CelestialEnvironmentSample(
                body,
                false,
                0f,
                false,
                0f,
                new Vector2(radius, radius),
                radius);
        }
    }
}
