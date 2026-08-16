using System;
using System.Collections.Generic;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [Serializable]
    public sealed class SurfaceDecorationVariant
    {
        [SerializeField] Mesh nearMesh;
        [SerializeField] Mesh farMesh;
        [SerializeField] Material[] materials = Array.Empty<Material>();
        [SerializeField, Min(0f)] float weight = 1f;
        [SerializeField] Vector3 rotationOffset;
        [SerializeField, Min(0.0001f)] float baseScale = 1f;

        public Mesh NearMesh => nearMesh;
        public Mesh FarMesh => farMesh != null ? farMesh : nearMesh;
        public IReadOnlyList<Material> Materials => materials;
        public float Weight => Mathf.Max(0f, weight);
        public Vector3 RotationOffset => rotationOffset;
        public float BaseScale => Mathf.Max(0.0001f, baseScale);

        internal void Validate()
        {
            materials ??= Array.Empty<Material>();
            weight = Mathf.Max(0f, weight);
            baseScale = Mathf.Max(0.0001f, baseScale);
        }
    }

    [Serializable]
    public sealed class SurfaceDecorationRule
    {
        [SerializeField, HideInInspector] string stableId;
        [SerializeField] string displayName = "Surface Decoration";
        [SerializeField] List<SurfaceDecorationVariant> variants = new();

        [Header("Surface Compatibility")]
        [SerializeField] List<BiomeDefinition> allowedBiomes = new();
        [SerializeField] List<SurfaceMaterialDefinition> allowedSurfaceMaterials = new();
        [SerializeField] List<TerrainFeatureDefinition> allowedTerrainFeatures = new();
        [SerializeField] bool requireDryLand = true;
        [SerializeField] bool limitHeightAboveOcean;
        [SerializeField] Vector2 heightAboveOceanRange = new(0.25f, 1000000f);
        [SerializeField, Min(0f)] float heightAboveOceanFeather = 1f;

        [Header("Environmental Suitability")]
        [SerializeField] Vector2 altitudeRange = new(-1000000f, 1000000f);
        [SerializeField, Min(0f)] float altitudeFeather = 4f;
        [SerializeField] Vector2 slopeRange = new(0f, 90f);
        [SerializeField, Min(0f)] float slopeFeather = 4f;
        [SerializeField] Vector2 temperatureRange = new(-273.15f, 1000f);
        [SerializeField, Min(0f)] float temperatureFeather = 4f;
        [SerializeField] Vector2 moistureRange = new(0f, 1f);
        [SerializeField, Range(0f, 0.5f)] float moistureFeather = 0.08f;
        [SerializeField] Vector2 radiationRange = new(0f, 1f);
        [SerializeField, Range(0f, 0.5f)] float radiationFeather = 0.05f;
        [SerializeField] Vector2 snowRange = new(0f, 1f);
        [SerializeField, Range(0f, 0.5f)] float snowFeather = 0.05f;
        [SerializeField] Vector2 wetnessRange = new(0f, 1f);
        [SerializeField, Range(0f, 0.5f)] float wetnessFeather = 0.08f;
        [SerializeField] Vector2 volcanismRange = new(0f, 1f);
        [SerializeField, Range(0f, 0.5f)] float volcanismFeather = 0.05f;

        [Header("Distribution")]
        [SerializeField, Min(0.25f)] float spacingMeters = 8f;
        [SerializeField, Range(0f, 1f)] float spawnChance = 0.1f;
        [SerializeField, Min(0.01f)] float clusterScaleMeters = 30f;
        [SerializeField, Range(0f, 1f)] float clusterCutoff = 0.42f;
        [SerializeField, Range(0.001f, 0.5f)] float clusterFeather = 0.12f;

        [Header("Transform")]
        [SerializeField] Vector2 uniformScaleRange = new(0.85f, 1.15f);
        [SerializeField, Range(0f, 1f), Tooltip("0 follows local gravity; 1 follows the terrain normal.")]
        float normalAlignment = 1f;
        [SerializeField, Min(0f)] float surfaceOffset = 0.03f;

        [Header("Streaming")]
        [SerializeField, Min(1f)] float drawDistance = 200f;
        [SerializeField, Min(0f)] float shadowDistance = 60f;
        [SerializeField, Min(0f)] float farLodStartDistance = 80f;
        [SerializeField, Min(1)] int maxInstances = 256;

        public string StableId => stableId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Surface Decoration" : displayName;
        public IReadOnlyList<SurfaceDecorationVariant> Variants => variants;
        public float SpacingMeters => Mathf.Max(0.25f, spacingMeters);
        public float SpawnChance => Mathf.Clamp01(spawnChance);
        public float ClusterScaleMeters => Mathf.Max(0.01f, clusterScaleMeters);
        public float ClusterCutoff => Mathf.Clamp01(clusterCutoff);
        public float ClusterFeather => Mathf.Clamp(clusterFeather, 0.001f, 0.5f);
        public Vector2 UniformScaleRange => uniformScaleRange;
        public float NormalAlignment => Mathf.Clamp01(normalAlignment);
        public float SurfaceOffset => Mathf.Max(0f, surfaceOffset);
        public float DrawDistance => Mathf.Max(1f, drawDistance);
        public float ShadowDistance => Mathf.Clamp(shadowDistance, 0f, DrawDistance);
        public float FarLodStartDistance => Mathf.Clamp(farLodStartDistance, 0f, DrawDistance);
        public int MaxInstances => Mathf.Max(1, maxInstances);
        public bool RequireDryLand => requireDryLand;

        public float EvaluateSuitability(
            PlanetSurfaceSample sample,
            float allowedBiomeWeight,
            bool hasOcean,
            float oceanRadius)
        {
            if (allowedBiomes.Count > 0 && allowedBiomeWeight <= 0f)
            {
                return 0f;
            }

            if (allowedSurfaceMaterials.Count > 0 &&
                !allowedSurfaceMaterials.Contains(sample.SurfaceMaterial.Material))
            {
                return 0f;
            }

            if (allowedTerrainFeatures.Count > 0 &&
                !allowedTerrainFeatures.Contains(sample.TerrainFeature.Feature))
            {
                return 0f;
            }

            float heightAboveOcean = hasOcean ? sample.SurfaceRadius - oceanRadius : float.PositiveInfinity;
            if (requireDryLand && hasOcean && heightAboveOcean < 0.25f)
            {
                return 0f;
            }

            float ecologicalAltitude = hasOcean ? heightAboveOcean : sample.TerrainAltitude;
            float suitability = allowedBiomes.Count > 0 ? Mathf.Clamp01(allowedBiomeWeight) : 1f;
            suitability *= PlanetarySampling.EvaluateRange(altitudeRange, ecologicalAltitude, altitudeFeather);
            suitability *= PlanetarySampling.EvaluateRange(slopeRange, sample.Surface.SlopeAngleDegrees, slopeFeather);
            suitability *= PlanetarySampling.EvaluateRange(
                temperatureRange,
                sample.Climate.TemperatureCelsius,
                temperatureFeather);
            suitability *= PlanetarySampling.EvaluateRange(
                moistureRange,
                sample.Climate.EffectiveMoisture,
                moistureFeather);
            suitability *= PlanetarySampling.EvaluateRange(
                radiationRange,
                sample.Climate.Radiation,
                radiationFeather);
            suitability *= PlanetarySampling.EvaluateRange(snowRange, sample.SurfaceState.SnowCover, snowFeather);
            suitability *= PlanetarySampling.EvaluateRange(wetnessRange, sample.SurfaceState.Wetness, wetnessFeather);
            suitability *= PlanetarySampling.EvaluateRange(
                volcanismRange,
                sample.SurfaceState.VolcanicActivity,
                volcanismFeather);

            if (limitHeightAboveOcean)
            {
                if (!hasOcean)
                {
                    return 0f;
                }

                suitability *= PlanetarySampling.EvaluateRange(
                    heightAboveOceanRange,
                    heightAboveOcean,
                    heightAboveOceanFeather);
            }

            if (allowedSurfaceMaterials.Count > 0)
            {
                suitability *= Mathf.Clamp01(sample.SurfaceMaterial.Suitability);
            }

            if (allowedTerrainFeatures.Count > 0)
            {
                suitability *= Mathf.Clamp01(sample.TerrainFeature.FeatureNoise);
            }

            return Mathf.Clamp01(suitability);
        }

        public bool AllowsBiome(BiomeDefinition biome)
        {
            return allowedBiomes.Count == 0 || allowedBiomes.Contains(biome);
        }

        internal int ChooseVariant(float random01)
        {
            float total = 0f;
            for (int i = 0; i < variants.Count; i++)
            {
                SurfaceDecorationVariant variant = variants[i];
                if (variant != null && variant.NearMesh != null)
                {
                    total += variant.Weight;
                }
            }

            if (total <= 0f)
            {
                return -1;
            }

            float target = Mathf.Clamp01(random01) * total;
            for (int i = 0; i < variants.Count; i++)
            {
                SurfaceDecorationVariant variant = variants[i];
                if (variant == null || variant.NearMesh == null)
                {
                    continue;
                }

                target -= variant.Weight;
                if (target <= 0f)
                {
                    return i;
                }
            }

            return variants.Count - 1;
        }

        internal void Validate(int index)
        {
            stableId = string.IsNullOrWhiteSpace(stableId)
                ? Guid.NewGuid().ToString("N")
                : stableId.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? $"Surface Decoration {index + 1}" : displayName.Trim();
            variants ??= new List<SurfaceDecorationVariant>();
            allowedBiomes ??= new List<BiomeDefinition>();
            allowedSurfaceMaterials ??= new List<SurfaceMaterialDefinition>();
            allowedTerrainFeatures ??= new List<TerrainFeatureDefinition>();
            foreach (SurfaceDecorationVariant variant in variants)
            {
                variant?.Validate();
            }

            spacingMeters = Mathf.Max(0.25f, spacingMeters);
            spawnChance = Mathf.Clamp01(spawnChance);
            clusterScaleMeters = Mathf.Max(0.01f, clusterScaleMeters);
            clusterCutoff = Mathf.Clamp01(clusterCutoff);
            clusterFeather = Mathf.Clamp(clusterFeather, 0.001f, 0.5f);
            normalAlignment = Mathf.Clamp01(normalAlignment);
            surfaceOffset = Mathf.Max(0f, surfaceOffset);
            drawDistance = Mathf.Max(1f, drawDistance);
            shadowDistance = Mathf.Clamp(shadowDistance, 0f, drawDistance);
            farLodStartDistance = Mathf.Clamp(farLodStartDistance, 0f, drawDistance);
            maxInstances = Mathf.Max(1, maxInstances);
        }
    }

    [CreateAssetMenu(
        menuName = "Farion/Rendering/Surface Decoration Profile",
        fileName = "SO_SurfaceDecoration")]
    public sealed class SurfaceDecorationProfile : ScriptableObject
    {
        [SerializeField, Range(1, 256)] int maximumCandidateEvaluationsPerFrame = 32;
        [SerializeField, Range(0.1f, 4f)] float candidateEvaluationBudgetMilliseconds = 0.75f;
        [SerializeField, Min(0f)] float placementPauseSpeed = 18f;
        [SerializeField, Min(0f)] float placementPauseAltitude = 140f;
        [SerializeField] List<SurfaceDecorationRule> rules = new();

        public int MaximumCandidateEvaluationsPerFrame =>
            Mathf.Clamp(maximumCandidateEvaluationsPerFrame, 1, 256);
        public float CandidateEvaluationBudgetMilliseconds =>
            Mathf.Clamp(candidateEvaluationBudgetMilliseconds, 0.1f, 4f);
        public float PlacementPauseSpeed => Mathf.Max(0f, placementPauseSpeed);
        public float PlacementPauseAltitude => Mathf.Max(0f, placementPauseAltitude);
        public IReadOnlyList<SurfaceDecorationRule> Rules => rules;

        void OnValidate()
        {
            maximumCandidateEvaluationsPerFrame =
                Mathf.Clamp(maximumCandidateEvaluationsPerFrame, 1, 256);
            candidateEvaluationBudgetMilliseconds =
                Mathf.Clamp(candidateEvaluationBudgetMilliseconds, 0.1f, 4f);
            placementPauseSpeed = Mathf.Max(0f, placementPauseSpeed);
            placementPauseAltitude = Mathf.Max(0f, placementPauseAltitude);
            rules ??= new List<SurfaceDecorationRule>();
            for (int i = 0; i < rules.Count; i++)
            {
                rules[i]?.Validate(i);
            }
        }
    }
}
