using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Farion.Simulation.Planetary
{
    [CreateAssetMenu(menuName = "Farion/Simulation/Planetary/Biome Distribution Profile", fileName = "SO_BiomeDistribution")]
    public sealed class BiomeDistributionProfile : ScriptableObject
    {
        [FormerlySerializedAs("baseSeed")]
        [SerializeField] int seedSalt = 2001;
        [SerializeField] BiomeDefinition fallbackBiome;
        [Min(0.01f)]
        [SerializeField] float temperatureNoiseScale = 1.75f;
        [Min(0.01f)]
        [SerializeField] float moistureNoiseScale = 2.25f;
        [Header("Blending")]
        [SerializeField, Min(0f)] float altitudeBlend = 4f;
        [SerializeField, Min(0f)] float slopeBlendDegrees = 10f;
        [SerializeField, Range(0f, 0.5f)] float noiseBlend = 0.14f;
        [SerializeField, Min(0f)] float localTemperatureBlendCelsius = 8f;
        [SerializeField, Range(0f, 0.5f)] float localMoistureBlend = 0.16f;
        [SerializeField, Range(0f, 0.5f)] float localRadiationBlend = 0.08f;
        [SerializeField] List<BiomeDistributionRule> rules = new();

        public int SeedSalt => seedSalt;
        public BiomeDefinition FallbackBiome => fallbackBiome;
        public float TemperatureNoiseScale => temperatureNoiseScale;
        public float MoistureNoiseScale => moistureNoiseScale;
        public float AltitudeBlend => Mathf.Max(0f, altitudeBlend);
        public float SlopeBlendDegrees => Mathf.Max(0f, slopeBlendDegrees);
        public float NoiseBlend => Mathf.Clamp(noiseBlend, 0f, 0.5f);
        public float LocalTemperatureBlendCelsius => Mathf.Max(0f, localTemperatureBlendCelsius);
        public float LocalMoistureBlend => Mathf.Clamp(localMoistureBlend, 0f, 0.5f);
        public float LocalRadiationBlend => Mathf.Clamp(localRadiationBlend, 0f, 0.5f);
        public IReadOnlyList<BiomeDistributionRule> Rules => rules;

        void OnValidate()
        {
            temperatureNoiseScale = Mathf.Max(0.01f, temperatureNoiseScale);
            moistureNoiseScale = Mathf.Max(0.01f, moistureNoiseScale);
            altitudeBlend = Mathf.Max(0f, altitudeBlend);
            slopeBlendDegrees = Mathf.Max(0f, slopeBlendDegrees);
            noiseBlend = Mathf.Clamp(noiseBlend, 0f, 0.5f);
            localTemperatureBlendCelsius = Mathf.Max(0f, localTemperatureBlendCelsius);
            localMoistureBlend = Mathf.Clamp(localMoistureBlend, 0f, 0.5f);
            localRadiationBlend = Mathf.Clamp(localRadiationBlend, 0f, 0.5f);
            rules ??= new List<BiomeDistributionRule>();
        }

        public BiomeDefinition EvaluateBiome(Vector3 localDirection, float altitude, float slopeDegrees)
        {
            return SampleBiome(
                    PlanetGenerationContext.CreateDefault(seedSalt),
                    localDirection,
                    altitude,
                    slopeDegrees)
                .Biome;
        }

        public BiomeSample SampleBiome(
            PlanetGenerationContext context,
            Vector3 localDirection,
            float altitude,
            float slopeDegrees)
        {
            return BiomeSampler.Sample(this, context, localDirection, altitude, slopeDegrees);
        }

        public BiomeSample SampleBiome(
            PlanetGenerationContext context,
            PlanetClimateSample climate,
            Vector3 localDirection,
            float altitude,
            float slopeDegrees)
        {
            return BiomeSampler.Sample(this, context, climate, localDirection, altitude, slopeDegrees);
        }

        public int SampleBiomeWeights(
            PlanetGenerationContext context,
            PlanetClimateSample climate,
            Vector3 localDirection,
            float altitude,
            float slopeDegrees,
            List<BiomeWeight> results)
        {
            return BiomeSampler.SampleWeights(this, context, climate, localDirection, altitude, slopeDegrees, results);
        }
    }
}
