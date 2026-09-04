using System;
using System.Collections.Generic;
using UnityEngine;

namespace Farion.Simulation.Planetary
{
    [Serializable]
    public sealed class SurfaceMaterialDistributionRule
    {
        [SerializeField] SurfaceMaterialDefinition material;
        [SerializeField, Min(0f)] float selectionPriority = 1f;
        [SerializeField] List<BiomeDefinition> allowedBiomes = new();
        [SerializeField] Vector2 altitudeRange = new(-1000000f, 1000000f);
        [SerializeField] Vector2 slopeRange = new(0f, 90f);
        [SerializeField] Vector2 temperatureRange = new(-273.15f, 1000f);
        [SerializeField] Vector2 aridityRange = new(0f, 1f);
        [SerializeField] Vector2 radiationRange = new(0f, 1f);
        [SerializeField] Vector2 materialNoiseRange = new(0f, 1f);

        public SurfaceMaterialDefinition Material => material;

        public float EvaluateSuitability(
            BiomeDefinition biome,
            PlanetClimateSample climate,
            float altitude,
            float slopeDegrees,
            float materialNoise,
            SurfaceMaterialDistributionProfile profile)
        {
            if (material == null || selectionPriority <= 0f || !AllowsBiome(biome))
            {
                return 0f;
            }

            float score = selectionPriority;
            score *= PlanetarySampling.EvaluateRange(altitudeRange, altitude, profile.AltitudeBlend);
            score *= PlanetarySampling.EvaluateRange(slopeRange, slopeDegrees, profile.SlopeBlendDegrees);
            score *= PlanetarySampling.EvaluateRange(
                temperatureRange,
                climate.TemperatureCelsius,
                profile.TemperatureBlendCelsius);
            score *= PlanetarySampling.EvaluateRange(aridityRange, climate.Aridity, profile.AridityBlend);
            score *= PlanetarySampling.EvaluateRange(radiationRange, climate.Radiation, profile.RadiationBlend);
            score *= PlanetarySampling.EvaluateRange(materialNoiseRange, materialNoise, profile.NoiseBlend);
            return Mathf.Max(0f, score);
        }

        public bool HasMissingMaterial()
        {
            return material == null && selectionPriority > 0f;
        }

        internal bool AllowsAnyOf(IReadOnlyList<BiomeDefinition> biomes)
        {
            if (allowedBiomes == null || allowedBiomes.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < biomes.Count; i++)
            {
                if (allowedBiomes.Contains(biomes[i]))
                {
                    return true;
                }
            }

            return false;
        }

        bool AllowsBiome(BiomeDefinition biome)
        {
            return allowedBiomes == null
                || allowedBiomes.Count == 0
                || (biome != null && allowedBiomes.Contains(biome));
        }
    }

    [CreateAssetMenu(
        menuName = "Farion/Simulation/Planetary/Surface Material Distribution",
        fileName = "SO_SurfaceMaterialDistribution")]
    public sealed class SurfaceMaterialDistributionProfile : ScriptableObject
    {
        [SerializeField] int seedSalt = 4001;
        [SerializeField] SurfaceMaterialDefinition fallbackMaterial;
        [Min(0.01f)]
        [SerializeField] float materialNoiseScale = 3f;
        [Header("Blending")]
        [SerializeField, Min(0f)] float altitudeBlend = 3f;
        [SerializeField, Min(0f)] float slopeBlendDegrees = 8f;
        [SerializeField, Min(0f)] float temperatureBlendCelsius = 6f;
        [SerializeField, Range(0f, 0.5f)] float aridityBlend = 0.12f;
        [SerializeField, Range(0f, 0.5f)] float radiationBlend = 0.08f;
        [SerializeField, Range(0f, 0.5f)] float noiseBlend = 0.12f;
        [SerializeField] List<SurfaceMaterialDistributionRule> rules = new();

        public int SeedSalt => seedSalt;
        public SurfaceMaterialDefinition FallbackMaterial => fallbackMaterial;
        public float MaterialNoiseScale => Mathf.Max(0.01f, materialNoiseScale);
        public float AltitudeBlend => Mathf.Max(0f, altitudeBlend);
        public float SlopeBlendDegrees => Mathf.Max(0f, slopeBlendDegrees);
        public float TemperatureBlendCelsius => Mathf.Max(0f, temperatureBlendCelsius);
        public float AridityBlend => Mathf.Clamp(aridityBlend, 0f, 0.5f);
        public float RadiationBlend => Mathf.Clamp(radiationBlend, 0f, 0.5f);
        public float NoiseBlend => Mathf.Clamp(noiseBlend, 0f, 0.5f);
        public IReadOnlyList<SurfaceMaterialDistributionRule> Rules => rules;

        internal SurfaceMaterialDistributionProfile CreateVariant(List<SurfaceMaterialDistributionRule> selectedRules)
        {
            SurfaceMaterialDistributionProfile variant = ProfileVariants.Clone(this);
            variant.rules = selectedRules;
            return variant;
        }

        public void CollectMaterials(List<SurfaceMaterialDefinition> results)
        {
            results.Clear();
            if (fallbackMaterial != null)
            {
                results.Add(fallbackMaterial);
            }

            for (int i = 0; i < rules.Count; i++)
            {
                SurfaceMaterialDefinition material = rules[i]?.Material;
                if (material != null && !results.Contains(material))
                {
                    results.Add(material);
                }
            }
        }

        public SurfaceMaterialSample SampleDominant(
            PlanetGenerationContext context,
            PlanetClimateSample climate,
            BiomeDefinition biome,
            Vector3 localDirection,
            float altitude,
            float slopeDegrees)
        {
            Vector3 direction = localDirection.sqrMagnitude > 0.0001f
                ? localDirection.normalized
                : Vector3.up;
            float materialNoise = SampleMaterialNoise(context, direction);
            SurfaceMaterialDefinition dominantMaterial = null;
            float dominantSuitability = 0f;

            for (int i = 0; i < rules.Count; i++)
            {
                SurfaceMaterialDistributionRule rule = rules[i];
                if (rule == null)
                {
                    continue;
                }

                float suitability = rule.EvaluateSuitability(
                    biome,
                    climate,
                    altitude,
                    slopeDegrees,
                    materialNoise,
                    this);
                if (suitability > dominantSuitability)
                {
                    dominantMaterial = rule.Material;
                    dominantSuitability = suitability;
                }
            }

            if (dominantMaterial == null)
            {
                return new SurfaceMaterialSample(fallbackMaterial, fallbackMaterial != null ? 1f : 0f);
            }

            return new SurfaceMaterialSample(dominantMaterial, dominantSuitability);
        }

        public int SampleWeights(
            PlanetGenerationContext context,
            PlanetClimateSample climate,
            BiomeDefinition biome,
            Vector3 localDirection,
            float altitude,
            float slopeDegrees,
            List<SurfaceMaterialWeight> results)
        {
            if (results == null)
            {
                return 0;
            }

            results.Clear();
            Vector3 direction = localDirection.sqrMagnitude > 0.0001f
                ? localDirection.normalized
                : Vector3.up;
            float materialNoise = SampleMaterialNoise(context, direction);

            float total = 0f;
            for (int i = 0; i < rules.Count; i++)
            {
                SurfaceMaterialDistributionRule rule = rules[i];
                if (rule == null)
                {
                    continue;
                }

                float suitability = rule.EvaluateSuitability(
                    biome,
                    climate,
                    altitude,
                    slopeDegrees,
                    materialNoise,
                    this);
                if (suitability <= 0f)
                {
                    continue;
                }

                results.Add(new SurfaceMaterialWeight(rule.Material, suitability));
                total += suitability;
            }

            if (total <= 0f && fallbackMaterial != null)
            {
                results.Add(new SurfaceMaterialWeight(fallbackMaterial, 1f));
                return 1;
            }

            SurfaceMaterialWeightUtility.Normalize(results, total);
            return results.Count;
        }

        float SampleMaterialNoise(PlanetGenerationContext context, Vector3 direction)
        {
            return PlanetarySampling.SampleFractal01(
                direction,
                MaterialNoiseScale,
                4,
                2f,
                0.52f,
                SeedUtility.Derive(context.PlanetSeed, seedSalt, "surface.material"));
        }

        void OnValidate()
        {
            materialNoiseScale = Mathf.Max(0.01f, materialNoiseScale);
            altitudeBlend = Mathf.Max(0f, altitudeBlend);
            slopeBlendDegrees = Mathf.Max(0f, slopeBlendDegrees);
            temperatureBlendCelsius = Mathf.Max(0f, temperatureBlendCelsius);
            aridityBlend = Mathf.Clamp(aridityBlend, 0f, 0.5f);
            radiationBlend = Mathf.Clamp(radiationBlend, 0f, 0.5f);
            noiseBlend = Mathf.Clamp(noiseBlend, 0f, 0.5f);
            rules ??= new List<SurfaceMaterialDistributionRule>();
        }
    }
}
