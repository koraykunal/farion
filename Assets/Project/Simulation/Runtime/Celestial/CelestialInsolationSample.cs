using Farion.Core.Physics;
using UnityEngine;

namespace Farion.Simulation.Celestial
{
    public readonly struct CelestialInsolationSample
    {
        public CelestialInsolationSample(
            CelestialBody source,
            Vector3 sourcePosition,
            Vector3 directionToSource,
            float distance,
            float normalizedIrradiance,
            float directExposure,
            float equilibriumTemperatureCelsius)
        {
            Source = source;
            SourcePosition = sourcePosition;
            DirectionToSource = directionToSource.sqrMagnitude > 0.0001f ? directionToSource.normalized : Vector3.up;
            Distance = Mathf.Max(0f, distance);
            NormalizedIrradiance = Mathf.Max(0f, normalizedIrradiance);
            DirectExposure = Mathf.Clamp01(directExposure);
            EquilibriumTemperatureCelsius = equilibriumTemperatureCelsius;
        }

        public CelestialBody Source { get; }
        public Vector3 SourcePosition { get; }
        public Vector3 DirectionToSource { get; }
        public float Distance { get; }
        public float NormalizedIrradiance { get; }
        public float DirectExposure { get; }
        public float EquilibriumTemperatureCelsius { get; }
        public bool HasSource => Source != null;

        public static CelestialInsolationSample None => default;
    }
}
