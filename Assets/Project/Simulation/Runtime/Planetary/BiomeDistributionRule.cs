using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Farion.Simulation.Planetary
{
    [Serializable]
    public sealed class BiomeDistributionRule
    {
        [SerializeField] BiomeDefinition biome;
        [Min(0f)]
        [FormerlySerializedAs("weight")]
        [SerializeField] float selectionPriority = 1f;
        [SerializeField] Vector2 altitudeRange = new(-1000000f, 1000000f);
        [SerializeField] Vector2 slopeRange = new(0f, 90f);
        [SerializeField] Vector2 temperatureRange = new(-273.15f, 1000f);
        [SerializeField] Vector2 aridityRange = new(0f, 1f);
        [SerializeField] Vector2 radiationRange = new(0f, 1f);

        public BiomeDefinition Biome => biome;
        public float SelectionPriority => Mathf.Max(0f, selectionPriority);

        public float EvaluateSuitability(
            float altitude,
            float slopeDegrees,
            PlanetClimateSample climate,
            BiomeDistributionProfile profile)
        {
            if (biome == null || SelectionPriority <= 0f)
            {
                return 0f;
            }

            float score = SelectionPriority;
            score *= PlanetarySampling.EvaluateRange(altitudeRange, altitude, profile.AltitudeBlend);
            score *= PlanetarySampling.EvaluateRange(slopeRange, slopeDegrees, profile.SlopeBlendDegrees);
            score *= PlanetarySampling.EvaluateRange(
                temperatureRange,
                climate.TemperatureCelsius,
                profile.TemperatureBlendCelsius);
            score *= PlanetarySampling.EvaluateRange(aridityRange, climate.Aridity, profile.AridityBlend);
            score *= PlanetarySampling.EvaluateRange(radiationRange, climate.Radiation, profile.RadiationBlend);
            return Mathf.Max(0f, score);
        }
    }
}
