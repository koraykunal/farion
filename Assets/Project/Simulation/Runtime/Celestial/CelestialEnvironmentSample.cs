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
            float atmosphereRadius)
        {
            Body = body;
            HasOcean = hasOcean;
            OceanRadius = Mathf.Max(0f, oceanRadius);
            HasAtmosphere = hasAtmosphere;
            AtmosphereRadius = Mathf.Max(0f, atmosphereRadius);
        }

        public CelestialBody Body { get; }
        public bool HasBody => Body != null;
        public bool HasOcean { get; }
        public float OceanRadius { get; }
        public bool HasAtmosphere { get; }
        public float AtmosphereRadius { get; }

        public static CelestialEnvironmentSample Empty(CelestialBody body)
        {
            return new CelestialEnvironmentSample(body, false, 0f, false, 0f);
        }
    }
}
