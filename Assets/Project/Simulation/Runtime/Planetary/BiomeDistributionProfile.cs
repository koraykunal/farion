using System.Collections.Generic;
using UnityEngine;

namespace Farion.Simulation.Planetary
{
    [CreateAssetMenu(menuName = "Farion/Simulation/Planetary/Biome Distribution Profile", fileName = "SO_BiomeDistribution")]
    public sealed class BiomeDistributionProfile : ScriptableObject
    {
        [SerializeField] BiomeDefinition fallbackBiome;
        [Header("Blending")]
        [SerializeField, Min(0f)] float altitudeBlend = 4f;
        [SerializeField, Min(0f)] float slopeBlendDegrees = 10f;
        [SerializeField, Min(0f)] float temperatureBlendCelsius = 8f;
        [SerializeField, Range(0f, 0.5f)] float aridityBlend = 0.16f;
        [SerializeField, Range(0f, 0.5f)] float radiationBlend = 0.08f;
        [SerializeField] List<BiomeDistributionRule> rules = new();

        public BiomeDefinition FallbackBiome => fallbackBiome;
        public float AltitudeBlend => Mathf.Max(0f, altitudeBlend);
        public float SlopeBlendDegrees => Mathf.Max(0f, slopeBlendDegrees);
        public float TemperatureBlendCelsius => Mathf.Max(0f, temperatureBlendCelsius);
        public float AridityBlend => Mathf.Clamp(aridityBlend, 0f, 0.5f);
        public float RadiationBlend => Mathf.Clamp(radiationBlend, 0f, 0.5f);
        public IReadOnlyList<BiomeDistributionRule> Rules => rules;

        void OnValidate()
        {
            altitudeBlend = Mathf.Max(0f, altitudeBlend);
            slopeBlendDegrees = Mathf.Max(0f, slopeBlendDegrees);
            temperatureBlendCelsius = Mathf.Max(0f, temperatureBlendCelsius);
            aridityBlend = Mathf.Clamp(aridityBlend, 0f, 0.5f);
            radiationBlend = Mathf.Clamp(radiationBlend, 0f, 0.5f);
            rules ??= new List<BiomeDistributionRule>();
        }

        public BiomeSample SampleBiome(
            PlanetGenerationContext context,
            PlanetClimateSample climate,
            float altitude,
            float slopeDegrees)
        {
            return BiomeSampler.Sample(this, context, climate, altitude, slopeDegrees);
        }

        public int SampleBiomeWeights(
            PlanetGenerationContext context,
            PlanetClimateSample climate,
            float altitude,
            float slopeDegrees,
            List<BiomeWeight> results)
        {
            return BiomeSampler.SampleWeights(this, context, climate, altitude, slopeDegrees, results);
        }
    }
}
