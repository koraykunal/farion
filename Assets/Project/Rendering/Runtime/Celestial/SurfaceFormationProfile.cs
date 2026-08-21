using System;
using System.Collections.Generic;
using Farion.Simulation.Celestial;
using Farion.Simulation.Planetary;
using UnityEngine;
using UnityEngine.Serialization;

namespace Farion.Rendering.Celestial
{
    public enum SurfaceFormationRole
    {
        Outcrop = 0,
        Buttress = 1,
        Talus = 2,
        Debris = 3
    }

    [Serializable]
    public sealed class SurfaceFormationPiece
    {
        [SerializeField] GameObject prefab;
        [SerializeField, Min(0f)] float weight = 1f;
        [SerializeField, Min(0.0001f)] float baseScale = 1f;
        [SerializeField, Min(0.01f)] float footprintRadius = 4f;
        [SerializeField, Range(0f, 0.9f)] float embedFraction = 0.15f;
        [SerializeField, Min(0.01f)] float pieceHeight = 4f;

        public GameObject Prefab => prefab;
        public float Weight => Mathf.Max(0f, weight);
        public float BaseScale => Mathf.Max(0.0001f, baseScale);
        public float FootprintRadius => Mathf.Max(0.01f, footprintRadius);
        public float EmbedFraction => Mathf.Clamp(embedFraction, 0f, 0.9f);
        public float PieceHeight => Mathf.Max(0.01f, pieceHeight);

        internal void Validate()
        {
            weight = Mathf.Max(0f, weight);
            baseScale = Mathf.Max(0.0001f, baseScale);
            footprintRadius = Mathf.Max(0.01f, footprintRadius);
            embedFraction = Mathf.Clamp(embedFraction, 0f, 0.9f);
            pieceHeight = Mathf.Max(0.01f, pieceHeight);
        }
    }

    [Serializable]
    public sealed class SurfaceFormationKit
    {
        [FormerlySerializedAs("primary")]
        [SerializeField] List<SurfaceFormationPiece> outcrop = new();
        [FormerlySerializedAs("secondary")]
        [SerializeField] List<SurfaceFormationPiece> buttress = new();
        [FormerlySerializedAs("transition")]
        [SerializeField] List<SurfaceFormationPiece> talus = new();
        [FormerlySerializedAs("accent")]
        [SerializeField] List<SurfaceFormationPiece> debris = new();

        public IReadOnlyList<SurfaceFormationPiece> Outcrop => outcrop;
        public IReadOnlyList<SurfaceFormationPiece> Buttress => buttress;
        public IReadOnlyList<SurfaceFormationPiece> Talus => talus;
        public IReadOnlyList<SurfaceFormationPiece> Debris => debris;
        public bool HasOutcrop => CountUsable(outcrop) > 0;

        public IReadOnlyList<SurfaceFormationPiece> Resolve(SurfaceFormationRole role)
        {
            return role switch
            {
                SurfaceFormationRole.Outcrop => outcrop,
                SurfaceFormationRole.Buttress => buttress,
                SurfaceFormationRole.Talus => talus,
                _ => debris
            };
        }

        public int ChoosePiece(SurfaceFormationRole role, float random01)
        {
            IReadOnlyList<SurfaceFormationPiece> pieces = Resolve(role);
            float total = 0f;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] != null && pieces[i].Prefab != null)
                {
                    total += pieces[i].Weight;
                }
            }

            if (total <= 0f)
            {
                return -1;
            }

            float target = Mathf.Clamp01(random01) * total;
            for (int i = 0; i < pieces.Count; i++)
            {
                SurfaceFormationPiece piece = pieces[i];
                if (piece == null || piece.Prefab == null)
                {
                    continue;
                }

                target -= piece.Weight;
                if (target <= 0f)
                {
                    return i;
                }
            }

            return pieces.Count - 1;
        }

        public float AveragePieceHeight(SurfaceFormationRole role)
        {
            IReadOnlyList<SurfaceFormationPiece> pieces = Resolve(role);
            float total = 0f;
            int count = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] != null && pieces[i].Prefab != null)
                {
                    total += pieces[i].PieceHeight * pieces[i].BaseScale;
                    count++;
                }
            }

            return count > 0 ? total / count : 1f;
        }

        public float AverageFootprint(SurfaceFormationRole role)
        {
            IReadOnlyList<SurfaceFormationPiece> pieces = Resolve(role);
            float total = 0f;
            int count = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] != null && pieces[i].Prefab != null)
                {
                    total += pieces[i].FootprintRadius * pieces[i].BaseScale;
                    count++;
                }
            }

            return count > 0 ? total / count : 2f;
        }

        internal void Validate()
        {
            outcrop ??= new List<SurfaceFormationPiece>();
            buttress ??= new List<SurfaceFormationPiece>();
            talus ??= new List<SurfaceFormationPiece>();
            debris ??= new List<SurfaceFormationPiece>();
            ValidateList(outcrop);
            ValidateList(buttress);
            ValidateList(talus);
            ValidateList(debris);
        }

        static void ValidateList(List<SurfaceFormationPiece> pieces)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                pieces[i]?.Validate();
            }
        }

        static int CountUsable(List<SurfaceFormationPiece> pieces)
        {
            int count = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] != null && pieces[i].Prefab != null)
                {
                    count++;
                }
            }

            return count;
        }
    }

    [Serializable]
    public sealed class SurfaceFormationRule
    {
        [SerializeField, HideInInspector] string stableId;
        [SerializeField] string displayName = "Surface Formation";
        [SerializeField] SurfaceFormationKit kit = new();
        [SerializeField] SurfaceScatterSuitability suitability = new();
        [SerializeField] SurfaceScatterDistribution distribution = new();

        [Header("Geology Response")]
        [SerializeField, Range(0f, 1f)] float slopeBreakBias = 0.6f;
        [SerializeField, Min(0.5f)] float slopeBreakSampleMeters = 12f;
        [SerializeField, Range(0f, 1f)] float geologyRequirement = 0.6f;
        [SerializeField, Range(0f, 1f)] float convexityBias = 0.5f;
        [SerializeField, Range(-1f, 1f)] float preferredConvexity = 0.35f;
        [SerializeField, Min(0.05f)] float featureHeightRatio = 1f;

        [Header("Composition")]
        [FormerlySerializedAs("primaryCount")]
        [SerializeField] Vector2Int outcropCount = new(3, 6);
        [FormerlySerializedAs("primarySpacingRatio")]
        [SerializeField, Min(0.25f)] float outcropSpacingRatio = 1f;
        [FormerlySerializedAs("secondaryCount")]
        [SerializeField] Vector2Int buttressCount = new(2, 4);
        [FormerlySerializedAs("transitionCount")]
        [SerializeField] Vector2Int talusCount = new(2, 4);
        [FormerlySerializedAs("accentCount")]
        [SerializeField] Vector2Int debrisCount = new(6, 14);
        [SerializeField, Min(1f)] float formationRadius = 40f;
        [SerializeField] Vector2 uniformScaleRange = new(0.85f, 1.25f);
        [SerializeField, Range(0f, 0.6f)] float scaleVariation = 0.22f;
        [SerializeField, Range(0f, 0.6f)] float shapeVariation = 0.25f;
        [SerializeField, Range(0.2f, 6f)] float talusReach = 2.2f;
        [SerializeField, Range(0.1f, 1f)] float talusScaleFalloff = 0.55f;
        [SerializeField, Range(0f, 90f)] float yawJitterDegrees = 18f;
        [SerializeField, Range(0f, 45f)] float tiltJitterDegrees = 4f;
        [SerializeField, Range(0f, 1f)] float surfaceNormalAlignment = 0.35f;

        [Header("Surface Blending")]
        [SerializeField, Range(0f, 1f)] float surfaceTintStrength = 0.45f;
        [SerializeField, Range(0f, 2f)] float snowResponse = 1f;
        [SerializeField, Range(0f, 2f)] float mossResponse = 1f;

        [Header("Collision")]
        [SerializeField, Min(0f)] float collisionDistance = 200f;

        [Header("Association")]
        [SerializeField, Min(0f)] float influenceRadius = 26f;

        public string StableId => stableId;
        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName) ? "Surface Formation" : displayName;
        public SurfaceFormationKit Kit => kit;
        public SurfaceScatterSuitability Suitability => suitability;
        public SurfaceScatterDistribution Distribution => distribution;
        public float SlopeBreakBias => Mathf.Clamp01(slopeBreakBias);
        public float SlopeBreakSampleMeters => Mathf.Max(0.5f, slopeBreakSampleMeters);
        public float GeologyRequirement => Mathf.Clamp01(geologyRequirement);
        public float ConvexityBias => Mathf.Clamp01(convexityBias);
        public float PreferredConvexity => Mathf.Clamp(preferredConvexity, -1f, 1f);
        public float FeatureHeightRatio => Mathf.Max(0.05f, featureHeightRatio);
        public Vector2Int OutcropCount => outcropCount;
        public float OutcropSpacingRatio => Mathf.Max(0.25f, outcropSpacingRatio);
        public Vector2Int ButtressCount => buttressCount;
        public Vector2Int TalusCount => talusCount;
        public Vector2Int DebrisCount => debrisCount;
        public float FormationRadius => Mathf.Max(1f, formationRadius);
        public Vector2 UniformScaleRange => uniformScaleRange;
        public float ScaleVariation => Mathf.Clamp(scaleVariation, 0f, 0.6f);
        public float ShapeVariation => Mathf.Clamp(shapeVariation, 0f, 0.6f);
        public float TalusReach => Mathf.Clamp(talusReach, 0.2f, 6f);
        public float TalusScaleFalloff => Mathf.Clamp(talusScaleFalloff, 0.1f, 1f);
        public float YawJitterDegrees => Mathf.Clamp(yawJitterDegrees, 0f, 90f);
        public float TiltJitterDegrees => Mathf.Clamp(tiltJitterDegrees, 0f, 45f);
        public float SurfaceNormalAlignment => Mathf.Clamp01(surfaceNormalAlignment);
        public float InfluenceRadius => Mathf.Max(0f, influenceRadius);
        public float CollisionDistance => Mathf.Max(0f, collisionDistance);
        public float SurfaceTintStrength => Mathf.Clamp01(surfaceTintStrength);
        public float SnowResponse => Mathf.Clamp(snowResponse, 0f, 2f);
        public float MossResponse => Mathf.Clamp(mossResponse, 0f, 2f);

        public Vector2Int ResolveCount(SurfaceFormationRole role)
        {
            return role switch
            {
                SurfaceFormationRole.Outcrop => outcropCount,
                SurfaceFormationRole.Buttress => buttressCount,
                SurfaceFormationRole.Talus => talusCount,
                _ => debrisCount
            };
        }

        public float EvaluateGeologyWeight(in CelestialGeologySample geology)
        {
            if (!geology.HasFeature)
            {
                return 1f - GeologyRequirement;
            }

            float strength = Mathf.Lerp(1f, geology.FeatureStrength, GeologyRequirement);
            if (ConvexityBias <= 0f)
            {
                return strength;
            }

            float alignment = 1f - Mathf.Abs(geology.Convexity - PreferredConvexity) * 0.5f;
            return strength * Mathf.Lerp(1f, Mathf.Clamp01(alignment), ConvexityBias);
        }

        internal void Validate(int index)
        {
            stableId = string.IsNullOrWhiteSpace(stableId)
                ? Guid.NewGuid().ToString("N")
                : stableId.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName)
                ? $"Surface Formation {index + 1}"
                : displayName.Trim();
            kit ??= new SurfaceFormationKit();
            kit.Validate();
            suitability ??= new SurfaceScatterSuitability();
            suitability.Validate();
            distribution ??= new SurfaceScatterDistribution();
            distribution.Validate();
            outcropCount = ClampCount(outcropCount);
            outcropSpacingRatio = Mathf.Max(0.25f, outcropSpacingRatio);
            buttressCount = ClampCount(buttressCount);
            talusCount = ClampCount(talusCount);
            debrisCount = ClampCount(debrisCount);
            formationRadius = Mathf.Max(1f, formationRadius);
            scaleVariation = Mathf.Clamp(scaleVariation, 0f, 0.6f);
            shapeVariation = Mathf.Clamp(shapeVariation, 0f, 0.6f);
            talusReach = Mathf.Clamp(talusReach, 0.2f, 6f);
            talusScaleFalloff = Mathf.Clamp(talusScaleFalloff, 0.1f, 1f);
            featureHeightRatio = Mathf.Max(0.05f, featureHeightRatio);
            geologyRequirement = Mathf.Clamp01(geologyRequirement);
            convexityBias = Mathf.Clamp01(convexityBias);
            preferredConvexity = Mathf.Clamp(preferredConvexity, -1f, 1f);
            yawJitterDegrees = Mathf.Clamp(yawJitterDegrees, 0f, 90f);
            tiltJitterDegrees = Mathf.Clamp(tiltJitterDegrees, 0f, 45f);
            surfaceNormalAlignment = Mathf.Clamp01(surfaceNormalAlignment);
            influenceRadius = Mathf.Max(0f, influenceRadius);
            collisionDistance = Mathf.Max(0f, collisionDistance);
            surfaceTintStrength = Mathf.Clamp01(surfaceTintStrength);
            snowResponse = Mathf.Clamp(snowResponse, 0f, 2f);
            mossResponse = Mathf.Clamp(mossResponse, 0f, 2f);
            slopeBreakSampleMeters = Mathf.Max(0.5f, slopeBreakSampleMeters);
        }

        static Vector2Int ClampCount(Vector2Int value)
        {
            int min = Mathf.Max(0, value.x);
            return new Vector2Int(min, Mathf.Max(min, value.y));
        }
    }

    [CreateAssetMenu(
        menuName = "Farion/Rendering/Celestial/Surface Formation Profile",
        fileName = "SO_SurfaceFormation")]
    public sealed class SurfaceFormationProfile : ScriptableObject
    {
        [SerializeField, Range(1, 32)] int maximumFormationBuildsPerFrame = 2;
        [SerializeField, Min(0f)] float placementPauseSpeed = 24f;
        [SerializeField, Min(0f)] float placementPauseAltitude = 220f;
        [SerializeField, Range(0f, 8f)] float prefetchSeconds = 2.5f;
        [SerializeField] SurfaceScatterShaderBinding shaderBinding = new();
        [SerializeField] List<SurfaceFormationRule> rules = new();

        public int MaximumFormationBuildsPerFrame =>
            Mathf.Clamp(maximumFormationBuildsPerFrame, 1, 32);
        public float PlacementPauseSpeed => Mathf.Max(0f, placementPauseSpeed);
        public float PlacementPauseAltitude => Mathf.Max(0f, placementPauseAltitude);
        public float PrefetchSeconds => Mathf.Clamp(prefetchSeconds, 0f, 8f);
        public SurfaceScatterShaderBinding ShaderBinding => shaderBinding;
        public IReadOnlyList<SurfaceFormationRule> Rules => rules;

        void OnValidate()
        {
            maximumFormationBuildsPerFrame =
                Mathf.Clamp(maximumFormationBuildsPerFrame, 1, 32);
            placementPauseSpeed = Mathf.Max(0f, placementPauseSpeed);
            placementPauseAltitude = Mathf.Max(0f, placementPauseAltitude);
            prefetchSeconds = Mathf.Clamp(prefetchSeconds, 0f, 8f);
            shaderBinding ??= new SurfaceScatterShaderBinding();
            shaderBinding.Validate();
            rules ??= new List<SurfaceFormationRule>();
            for (int i = 0; i < rules.Count; i++)
            {
                rules[i]?.Validate(i);
            }
        }
    }
}
