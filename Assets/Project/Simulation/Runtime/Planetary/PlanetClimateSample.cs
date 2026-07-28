using UnityEngine;

namespace Farion.Simulation.Planetary
{
    public readonly struct PlanetClimateSample
    {
        public PlanetClimateSample(
            PlanetGenerationContext context,
            Vector3 localDirection,
            float latitudeDegrees,
            float altitude,
            float slopeDegrees,
            float temperatureCelsius,
            float precipitation,
            float effectiveMoisture,
            float aridity,
            float radiation)
        {
            Context = context;
            LocalDirection = localDirection.sqrMagnitude > 0.0001f ? localDirection.normalized : Vector3.up;
            LatitudeDegrees = latitudeDegrees;
            Altitude = altitude;
            SlopeDegrees = slopeDegrees;
            TemperatureCelsius = temperatureCelsius;
            Precipitation = Mathf.Clamp01(precipitation);
            EffectiveMoisture = Mathf.Clamp01(effectiveMoisture);
            Aridity = Mathf.Clamp01(aridity);
            Radiation = Mathf.Clamp01(radiation);
        }

        public PlanetGenerationContext Context { get; }
        public Vector3 LocalDirection { get; }
        public float LatitudeDegrees { get; }
        public float Altitude { get; }
        public float SlopeDegrees { get; }
        public float TemperatureCelsius { get; }
        public float Precipitation { get; }
        public float EffectiveMoisture { get; }
        public float Aridity { get; }
        public float Radiation { get; }
    }
}
