using System.Collections.Generic;
using UnityEngine;

namespace Farion.Simulation.Planetary
{
    [CreateAssetMenu(menuName = "Farion/Simulation/Planetary/Terrain Feature Distribution Profile", fileName = "SO_TerrainFeatureDistribution")]
    public sealed class TerrainFeatureDistributionProfile : ScriptableObject
    {
        [SerializeField] int seedSalt = 3001;
        [Min(0.01f)]
        [SerializeField] float featureNoiseScale = 2.5f;
        [Header("Blending")]
        [SerializeField, Min(0f)] float altitudeBlend = 4f;
        [SerializeField, Min(0f)] float slopeBlendDegrees = 8f;
        [SerializeField, Range(0f, 0.5f)] float noiseBlend = 0.08f;
        [SerializeField, Min(0f)] float localTemperatureBlendCelsius = 8f;
        [SerializeField, Range(0f, 0.5f)] float localMoistureBlend = 0.12f;
        [SerializeField, Range(0f, 0.5f)] float localRadiationBlend = 0.08f;
        [SerializeField] List<TerrainFeatureDistributionRule> rules = new();

        public int SeedSalt => seedSalt;
        public float FeatureNoiseScale => Mathf.Max(0.01f, featureNoiseScale);
        public float AltitudeBlend => Mathf.Max(0f, altitudeBlend);
        public float SlopeBlendDegrees => Mathf.Max(0f, slopeBlendDegrees);
        public float NoiseBlend => Mathf.Clamp(noiseBlend, 0f, 0.5f);
        public float LocalTemperatureBlendCelsius => Mathf.Max(0f, localTemperatureBlendCelsius);
        public float LocalMoistureBlend => Mathf.Clamp(localMoistureBlend, 0f, 0.5f);
        public float LocalRadiationBlend => Mathf.Clamp(localRadiationBlend, 0f, 0.5f);
        public IReadOnlyList<TerrainFeatureDistributionRule> Rules => rules;

        void OnValidate()
        {
            featureNoiseScale = Mathf.Max(0.01f, featureNoiseScale);
            altitudeBlend = Mathf.Max(0f, altitudeBlend);
            slopeBlendDegrees = Mathf.Max(0f, slopeBlendDegrees);
            noiseBlend = Mathf.Clamp(noiseBlend, 0f, 0.5f);
            localTemperatureBlendCelsius = Mathf.Max(0f, localTemperatureBlendCelsius);
            localMoistureBlend = Mathf.Clamp(localMoistureBlend, 0f, 0.5f);
            localRadiationBlend = Mathf.Clamp(localRadiationBlend, 0f, 0.5f);
            rules ??= new List<TerrainFeatureDistributionRule>();
        }

        public TerrainFeatureSample SampleFeature(
            PlanetGenerationContext context,
            PlanetClimateSample climate,
            BiomeDefinition biome,
            Vector3 localDirection,
            float altitude,
            float slopeDegrees)
        {
            return TerrainFeatureSampler.Sample(this, context, climate, biome, localDirection, altitude, slopeDegrees);
        }
    }
}
