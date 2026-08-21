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
        [SerializeField] SurfaceScatterSuitability suitability = new();
        [SerializeField] SurfaceScatterDistribution distribution = new();

        [Header("Association")]
        [SerializeField, Range(-1f, 4f)] float formationAffinity;

        [Header("Transform")]
        [SerializeField] Vector2 uniformScaleRange = new(0.85f, 1.15f);
        [SerializeField, Range(0f, 1f), Tooltip("0 follows local gravity; 1 follows the terrain normal.")]
        float normalAlignment = 1f;
        [SerializeField, Min(0f)] float surfaceOffset = 0.03f;

        [Header("Surface Blending")]
        [SerializeField, Range(0f, 1f)] float surfaceTintStrength;
        [SerializeField, Range(0f, 2f)] float snowResponse = 1f;
        [SerializeField, Range(0f, 2f)] float mossResponse = 1f;

        [Header("Rendering")]
        [SerializeField, Min(0f)] float shadowDistance = 60f;
        [SerializeField, Min(0f)] float farLodStartDistance = 80f;

        public string StableId => stableId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Surface Decoration" : displayName;
        public IReadOnlyList<SurfaceDecorationVariant> Variants => variants;
        public SurfaceScatterSuitability Suitability => suitability;
        public SurfaceScatterDistribution Distribution => distribution;
        public Vector2 UniformScaleRange => uniformScaleRange;
        public float NormalAlignment => Mathf.Clamp01(normalAlignment);
        public float SurfaceOffset => Mathf.Max(0f, surfaceOffset);
        public float ShadowDistance =>
            Mathf.Clamp(shadowDistance, 0f, distribution.FarVisibilityDistance);
        public float FarLodStartDistance =>
            Mathf.Clamp(farLodStartDistance, 0f, distribution.FarVisibilityDistance);
        public float FormationAffinity => Mathf.Clamp(formationAffinity, -1f, 4f);
        public float SurfaceTintStrength => Mathf.Clamp01(surfaceTintStrength);
        public float SnowResponse => Mathf.Clamp(snowResponse, 0f, 2f);
        public float MossResponse => Mathf.Clamp(mossResponse, 0f, 2f);

        public float ApplyFormationAffinity(float suitabilityWeight, float formationInfluence)
        {
            if (Mathf.Approximately(FormationAffinity, 0f) || formationInfluence <= 0f)
            {
                return suitabilityWeight;
            }

            return Mathf.Clamp01(
                suitabilityWeight * Mathf.Max(0f, 1f + FormationAffinity * Mathf.Clamp01(formationInfluence)));
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
            foreach (SurfaceDecorationVariant variant in variants)
            {
                variant?.Validate();
            }

            suitability ??= new SurfaceScatterSuitability();
            suitability.Validate();
            distribution ??= new SurfaceScatterDistribution();
            distribution.Validate();
            formationAffinity = Mathf.Clamp(formationAffinity, -1f, 4f);
            surfaceTintStrength = Mathf.Clamp01(surfaceTintStrength);
            snowResponse = Mathf.Clamp(snowResponse, 0f, 2f);
            mossResponse = Mathf.Clamp(mossResponse, 0f, 2f);
            normalAlignment = Mathf.Clamp01(normalAlignment);
            surfaceOffset = Mathf.Max(0f, surfaceOffset);
            shadowDistance = Mathf.Clamp(shadowDistance, 0f, distribution.FarVisibilityDistance);
            farLodStartDistance =
                Mathf.Clamp(farLodStartDistance, 0f, distribution.FarVisibilityDistance);
        }
    }

    [CreateAssetMenu(
        menuName = "Farion/Rendering/Celestial/Surface Decoration Profile",
        fileName = "SO_SurfaceDecoration")]
    public sealed class SurfaceDecorationProfile : ScriptableObject
    {
        [SerializeField, Range(1, 512)] int maximumCandidateEvaluationsPerFrame = 96;
        [SerializeField, Range(0.1f, 8f)] float candidateEvaluationBudgetMilliseconds = 1.5f;
        [SerializeField, Min(0f)] float placementPauseSpeed = 18f;
        [SerializeField, Min(0f)] float placementPauseAltitude = 140f;
        [SerializeField, Range(0f, 8f)] float prefetchSeconds = 1.5f;
        [SerializeField] SurfaceScatterShaderBinding shaderBinding = new();
        [SerializeField] List<SurfaceDecorationRule> rules = new();

        public int MaximumCandidateEvaluationsPerFrame =>
            Mathf.Clamp(maximumCandidateEvaluationsPerFrame, 1, 512);
        public float CandidateEvaluationBudgetMilliseconds =>
            Mathf.Clamp(candidateEvaluationBudgetMilliseconds, 0.1f, 8f);
        public float PlacementPauseSpeed => Mathf.Max(0f, placementPauseSpeed);
        public float PlacementPauseAltitude => Mathf.Max(0f, placementPauseAltitude);
        public float PrefetchSeconds => Mathf.Clamp(prefetchSeconds, 0f, 8f);
        public SurfaceScatterShaderBinding ShaderBinding => shaderBinding;
        public IReadOnlyList<SurfaceDecorationRule> Rules => rules;

        void OnValidate()
        {
            maximumCandidateEvaluationsPerFrame =
                Mathf.Clamp(maximumCandidateEvaluationsPerFrame, 1, 512);
            candidateEvaluationBudgetMilliseconds =
                Mathf.Clamp(candidateEvaluationBudgetMilliseconds, 0.1f, 8f);
            placementPauseSpeed = Mathf.Max(0f, placementPauseSpeed);
            placementPauseAltitude = Mathf.Max(0f, placementPauseAltitude);
            prefetchSeconds = Mathf.Clamp(prefetchSeconds, 0f, 8f);
            shaderBinding ??= new SurfaceScatterShaderBinding();
            shaderBinding.Validate();
            rules ??= new List<SurfaceDecorationRule>();
            for (int i = 0; i < rules.Count; i++)
            {
                rules[i]?.Validate(i);
            }
        }
    }
}
