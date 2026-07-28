using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("localTemperatureBlendCelsius")]
        [SerializeField, Min(0f)] float temperatureBlendCelsius = 8f;
        [FormerlySerializedAs("localMoistureBlend")]
        [SerializeField, Range(0f, 0.5f)] float effectiveMoistureBlend = 0.12f;
        [FormerlySerializedAs("localRadiationBlend")]
        [SerializeField, Range(0f, 0.5f)] float radiationBlend = 0.08f;
        [SerializeField] List<TerrainFeatureDistributionRule> rules = new();

        public int SeedSalt => seedSalt;
        public float FeatureNoiseScale => Mathf.Max(0.01f, featureNoiseScale);
        public float AltitudeBlend => Mathf.Max(0f, altitudeBlend);
        public float SlopeBlendDegrees => Mathf.Max(0f, slopeBlendDegrees);
        public float NoiseBlend => Mathf.Clamp(noiseBlend, 0f, 0.5f);
        public float TemperatureBlendCelsius => Mathf.Max(0f, temperatureBlendCelsius);
        public float EffectiveMoistureBlend => Mathf.Clamp(effectiveMoistureBlend, 0f, 0.5f);
        public float RadiationBlend => Mathf.Clamp(radiationBlend, 0f, 0.5f);
        public IReadOnlyList<TerrainFeatureDistributionRule> Rules => rules;

        void OnValidate()
        {
            featureNoiseScale = Mathf.Max(0.01f, featureNoiseScale);
            altitudeBlend = Mathf.Max(0f, altitudeBlend);
            slopeBlendDegrees = Mathf.Max(0f, slopeBlendDegrees);
            noiseBlend = Mathf.Clamp(noiseBlend, 0f, 0.5f);
            temperatureBlendCelsius = Mathf.Max(0f, temperatureBlendCelsius);
            effectiveMoistureBlend = Mathf.Clamp(effectiveMoistureBlend, 0f, 0.5f);
            radiationBlend = Mathf.Clamp(radiationBlend, 0f, 0.5f);
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
