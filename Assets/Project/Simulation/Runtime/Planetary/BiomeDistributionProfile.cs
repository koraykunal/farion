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
        [Tooltip("1 blends factors as a geometric mean so one weak factor cannot erase a biome. Higher values sharpen toward a strict product and narrow every transition.")]
        [SerializeField, Range(0.2f, 3f)] float suitabilitySharpness = 1f;
        [Tooltip("Low frequency offset applied to the climate a biome is judged against, so borders wander instead of tracing latitude bands and altitude contours.")]
        [SerializeField, Range(0f, 1f)] float boundaryWarpStrength = 0.45f;
        [SerializeField, Min(0.05f)] float boundaryWarpScale = 2.6f;
        [SerializeField, Min(0f)] float boundaryWarpTemperatureCelsius = 7f;
        [SerializeField, Range(0f, 0.5f)] float boundaryWarpAridity = 0.18f;
        [SerializeField] int boundaryWarpSeed = 7717;
        [SerializeField] List<BiomeDistributionRule> rules = new();

        public BiomeDefinition FallbackBiome => fallbackBiome;
        public float AltitudeBlend => Mathf.Max(0f, altitudeBlend);
        public float SlopeBlendDegrees => Mathf.Max(0f, slopeBlendDegrees);
        public float TemperatureBlendCelsius => Mathf.Max(0f, temperatureBlendCelsius);
        public float AridityBlend => Mathf.Clamp(aridityBlend, 0f, 0.5f);
        public float RadiationBlend => Mathf.Clamp(radiationBlend, 0f, 0.5f);
        public float SuitabilitySharpness => Mathf.Clamp(suitabilitySharpness, 0.2f, 3f);
        public float BoundaryWarpStrength => Mathf.Clamp01(boundaryWarpStrength);
        public float BoundaryWarpScale => Mathf.Max(0.05f, boundaryWarpScale);
        public float BoundaryWarpTemperatureCelsius => Mathf.Max(0f, boundaryWarpTemperatureCelsius);
        public float BoundaryWarpAridity => Mathf.Clamp(boundaryWarpAridity, 0f, 0.5f);
        public int BoundaryWarpSeed => boundaryWarpSeed;
        public IReadOnlyList<BiomeDistributionRule> Rules => rules;

        internal BiomeDistributionProfile CreateVariant(List<BiomeDistributionRule> selectedRules, int warpSeed)
        {
            BiomeDistributionProfile variant = ProfileVariants.Clone(this);
            variant.rules = selectedRules;
            variant.boundaryWarpSeed = warpSeed;
            return variant;
        }

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
            return BiomeSampler.SampleWeights(
                this,
                context,
                climate,
                localDirection,
                altitude,
                slopeDegrees,
                results);
        }

        public Vector2 EvaluateClimateWarp(Vector3 localDirection)
        {
            if (BoundaryWarpStrength <= 0f)
            {
                return Vector2.zero;
            }

            float temperature = PlanetarySampling.SampleFractalSigned(
                localDirection,
                BoundaryWarpScale,
                2,
                2.1f,
                0.5f,
                BoundaryWarpSeed);
            float aridity = PlanetarySampling.SampleFractalSigned(
                localDirection,
                BoundaryWarpScale * 1.37f,
                2,
                2.1f,
                0.5f,
                BoundaryWarpSeed + 811);
            return new Vector2(
                temperature * BoundaryWarpTemperatureCelsius,
                aridity * BoundaryWarpAridity) * BoundaryWarpStrength;
        }
    }
}
