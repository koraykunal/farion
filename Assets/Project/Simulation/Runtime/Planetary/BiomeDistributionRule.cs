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
        public Vector2 TemperatureRange => temperatureRange;
        public Vector2 AridityRange => aridityRange;

        internal BiomeDistributionRule CreateShifted(float temperatureShiftCelsius, float aridityShift)
        {
            return new BiomeDistributionRule
            {
                biome = biome,
                selectionPriority = selectionPriority,
                altitudeRange = altitudeRange,
                slopeRange = slopeRange,
                temperatureRange = temperatureRange + Vector2.one * temperatureShiftCelsius,
                aridityRange = new Vector2(
                    Mathf.Clamp01(aridityRange.x + aridityShift),
                    Mathf.Clamp01(aridityRange.y + aridityShift)),
                radiationRange = radiationRange
            };
        }

        public float EvaluateSuitability(
            float altitude,
            float slopeDegrees,
            PlanetClimateSample climate,
            BiomeDistributionProfile profile,
            Vector2 climateOffset)
        {
            if (biome == null || SelectionPriority <= 0f)
            {
                return 0f;
            }

            const int FactorCount = 5;
            float product = PlanetarySampling.EvaluateRange(altitudeRange, altitude, profile.AltitudeBlend);
            if (product <= 0f)
            {
                return 0f;
            }

            product *= PlanetarySampling.EvaluateRange(slopeRange, slopeDegrees, profile.SlopeBlendDegrees);
            product *= PlanetarySampling.EvaluateRange(
                temperatureRange,
                climate.TemperatureCelsius + climateOffset.x,
                profile.TemperatureBlendCelsius);
            product *= PlanetarySampling.EvaluateRange(
                aridityRange,
                Mathf.Clamp01(climate.Aridity + climateOffset.y),
                profile.AridityBlend);
            product *= PlanetarySampling.EvaluateRange(radiationRange, climate.Radiation, profile.RadiationBlend);
            if (product <= 0f)
            {
                return 0f;
            }

            float blended = Mathf.Pow(product, profile.SuitabilitySharpness / FactorCount);
            return Mathf.Max(0f, SelectionPriority * blended);
        }
    }
}
