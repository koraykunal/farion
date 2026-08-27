using Farion.Simulation.Planetary;
using Farion.Core.Numerics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Farion.Simulation.Celestial
{
    [CreateAssetMenu(menuName = "Farion/Simulation/Celestial/Continent Ridge Shape Profile", fileName = "SO_ContinentRidgeShapeProfile")]
    public sealed class ContinentRidgeShapeProfile : CelestialShapeProfile
    {
        [Header("Seed")]
        [SerializeField] int seed = 5748;

        [Header("Relief")]
        [Tooltip("Metres of surface displacement per unit of accumulated noise. Absolute so terrain relief stays physically sized when the body radius changes; the noise layers below sum to roughly +13 / -9 units.")]
        [Min(0f)]
        [SerializeField] float elevationScaleMeters = 24f;
        [Tooltip("Body radius the noise scales below were authored against. Every scale is rescaled by the actual radius over this one, so a continent or a dune keeps the same size in metres on a body of any radius.")]
        [Min(1f)]
        [SerializeField] float featureReferenceRadiusMeters = 1600f;

        [Header("Continents")]
        [Min(0f)]
        [SerializeField] float oceanDepthMultiplier = 5f;
        [SerializeField] float oceanFloorDepth = 1.36f;
        [Min(0f)]
        [SerializeField] float oceanFloorSmoothing = 0.5f;
        [Min(0f)]
        [SerializeField] float coastalShelfWidth;
        [Range(0f, 1f)]
        [SerializeField] float coastalShelfStrength;
        [Range(0f, 1f)]
        [SerializeField] float coastalMountainFade;
        [Min(0f)]
        [SerializeField] float mountainBlend = 1.16f;

        [Header("Noise")]
        [SerializeField] SimpleNoise continentNoise = new()
        {
            Octaves = 6,
            Lacunarity = 2f,
            Persistence = 0.5f,
            Scale = 1f,
            Elevation = 2.64f,
            VerticalShift = -0.63f
        };

        [SerializeField] SimpleNoise mountainMaskNoise = new()
        {
            Octaves = 3,
            Lacunarity = 1.66f,
            Persistence = 0.55f,
            Scale = 1.09f,
            Elevation = 1f,
            VerticalShift = 0.02f
        };

        [SerializeField] RidgeNoise ridgeNoise = new()
        {
            Octaves = 5,
            Lacunarity = 4f,
            Persistence = 0.5f,
            Scale = 1.5f,
            Power = 2.18f,
            Elevation = 8.7f,
            Gain = 0.8f,
            VerticalShift = 0.09f,
            SpatialSmoothing = 0.015f,
            Offset = Vector3.zero
        };


        [SerializeField] RidgeNoise escarpmentNoise = new()
        {
            Octaves = 4,
            Lacunarity = 2f,
            Persistence = 0.5f,
            Scale = 11f,
            Power = 2.2f,
            Elevation = 4f,
            Gain = 0.85f,
            VerticalShift = 0.05f,
            SpatialSmoothing = 0.004f
        };

        [SerializeField] SimpleNoise escarpmentMaskNoise = new()
        {
            Octaves = 3,
            Lacunarity = 2f,
            Persistence = 0.5f,
            Scale = 3.4f,
            Elevation = 1f,
            VerticalShift = -0.15f
        };

        [Range(0f, 3f)]
        [SerializeField] float escarpmentBlend = 1.1f;
        [Range(0f, 1f)]
        [SerializeField] float escarpmentStrength;

        [Header("Surface Detail")]
        [SerializeField] RidgeNoise detailRidgeNoise = new()
        {
            Octaves = 6,
            Lacunarity = 2.15f,
            Persistence = 0.62f,
            Scale = 46f,
            Power = 1.9f,
            Elevation = 0.4f,
            Gain = 0.75f,
            VerticalShift = 0f,
            SpatialSmoothing = 0f,
            FadeFootprint = 0.006f,
            Offset = new Vector3(37f, -19f, 8f)
        };

        [SerializeField] SimpleNoise detailFieldNoise = new()
        {
            Octaves = 4,
            Lacunarity = 2.3f,
            Persistence = 0.6f,
            Scale = 78f,
            Elevation = 0.14f,
            VerticalShift = 0f,
            FadeFootprint = 0.006f,
            Offset = new Vector3(-11f, 26f, 41f)
        };

        [Range(0f, 2f)]
        [SerializeField] float detailStrength = 1f;

        [Header("Geology")]
        [Min(1f)]
        [SerializeField] float geologySpanMeters = 12f;
        [Min(1f)]
        [SerializeField] float featureSpanMeters = 280f;
        [Range(0.01f, 2f)]
        [SerializeField] float featureSlopeReference = 0.12f;

        [Header("Shading Data")]
        [SerializeField] SimpleNoise detailWarpNoise = new()
        {
            Octaves = 4,
            Lacunarity = 2f,
            Persistence = 0.5f,
            Scale = 2.96f,
            Elevation = 9.41f
        };

        [SerializeField] SimpleNoise detailNoise = new()
        {
            Octaves = 4,
            Lacunarity = 2f,
            Persistence = 0.5f,
            Scale = 1.5f,
            Elevation = 1f
        };

        [SerializeField] SimpleNoise largeNoise = new()
        {
            Octaves = 4,
            Lacunarity = 2f,
            Persistence = 0.5f,
            Scale = 1.77f,
            Elevation = 1f,
            Offset = new Vector3(0.25f, -0.09f, 0f)
        };

        [SerializeField] SimpleNoise smallNoise = new()
        {
            Octaves = 5,
            Lacunarity = 4.13f,
            Persistence = 0.65f,
            Scale = 4.44f,
            Elevation = 0.52f
        };

        public override float EvaluateDisplacement(float baseRadius, Vector3 unitDirection)
        {
            return EvaluateElevationMeters(
                unitDirection,
                0f,
                ResolveFeatureScaleFactor(baseRadius));
        }

        public override CelestialShapeSample EvaluateSample(float baseRadius, Vector3 unitDirection)
        {
            return EvaluateSample(baseRadius, unitDirection, 0f);
        }

        public override float EvaluateRadius(
            float baseRadius,
            Vector3 unitDirection,
            float angularSampleFootprint)
        {
            baseRadius = Mathf.Max(0.01f, baseRadius);
            unitDirection = unitDirection.sqrMagnitude > 0f ? unitDirection.normalized : Vector3.up;
            return Mathf.Max(
                0.01f,
                baseRadius + EvaluateElevationMeters(
                    unitDirection,
                    angularSampleFootprint,
                    ResolveFeatureScaleFactor(baseRadius)));
        }

        public override CelestialShapeSample EvaluateSample(
            float baseRadius,
            Vector3 unitDirection,
            float angularSampleFootprint)
        {
            baseRadius = Mathf.Max(0.01f, baseRadius);
            unitDirection = unitDirection.sqrMagnitude > 0f ? unitDirection.normalized : Vector3.up;

            float featureScale = ResolveFeatureScaleFactor(baseRadius);
            float radius = Mathf.Max(
                0.01f,
                baseRadius + EvaluateElevationMeters(
                    unitDirection,
                    angularSampleFootprint,
                    featureScale));
            return new CelestialShapeSample(
                radius,
                EvaluateShadingData(unitDirection, angularSampleFootprint, featureScale));
        }

        float ResolveFeatureScaleFactor(float baseRadius)
        {
            return Mathf.Max(0.01f, baseRadius) / Mathf.Max(1f, featureReferenceRadiusMeters);
        }

        float EvaluateElevationMeters(
            Vector3 unitDirection,
            float sampleFootprint,
            float featureScale)
        {
            float ridgeFilterRadius = ResolveRidgeFilterRadius(sampleFootprint);
            float continentShape = continentNoise.Sample(
                unitDirection,
                seed + 100,
                sampleFootprint,
                featureScale);
            continentShape = FarionMath.SmoothMax(continentShape, -oceanFloorDepth, oceanFloorSmoothing);
            float coastProximity = CalculateCoastProximity(continentShape);

            if (continentShape < 0f)
            {
                float deepWaterFactor = CalculateDeepWaterFactor(continentShape);
                continentShape *= 1f + oceanDepthMultiplier * Mathf.Lerp(1f, deepWaterFactor, coastalShelfStrength);
            }

            float mountainShape = ridgeNoise.Sample(
                unitDirection,
                seed + 200,
                ridgeFilterRadius,
                sampleFootprint,
                featureScale);
            float mountainMask = Blend(
                0f,
                mountainBlend,
                mountainMaskNoise.Sample(
                    unitDirection,
                    seed + 300,
                    sampleFootprint,
                    featureScale));
            mountainMask *= 1f - coastProximity * coastalMountainFade;

            float escarpmentShape = escarpmentNoise.Sample(
                unitDirection,
                seed + 1100,
                ridgeFilterRadius,
                sampleFootprint,
                featureScale);
            float escarpmentMask = Blend(
                0f,
                escarpmentBlend,
                escarpmentMaskNoise.Sample(
                    unitDirection,
                    seed + 1200,
                    sampleFootprint,
                    featureScale));
            escarpmentMask *= 1f - coastProximity * coastalMountainFade;

            float detail = detailStrength <= 0f
                ? 0f
                : detailRidgeNoise.Sample(
                        unitDirection,
                        seed + 2100,
                        0f,
                        sampleFootprint,
                        featureScale) +
                    detailFieldNoise.Sample(
                        unitDirection,
                        seed + 2200,
                        sampleFootprint,
                        featureScale);

            return elevationScaleMeters * (
                continentShape +
                mountainShape * mountainMask +
                escarpmentShape * escarpmentMask * escarpmentStrength +
                detail * detailStrength);
        }

        public override float EstimatePeakElevationMeters()
        {
            float continentPeak = continentNoise.Elevation + continentNoise.VerticalShift;
            float mountainPeak =
                (ridgeNoise.Elevation + ridgeNoise.VerticalShift) * Mathf.Max(0f, mountainBlend);
            float escarpmentPeak =
                (escarpmentNoise.Elevation + escarpmentNoise.VerticalShift) *
                Mathf.Max(0f, escarpmentBlend) *
                Mathf.Max(0f, escarpmentStrength);
            float detailPeak =
                (detailRidgeNoise.Elevation + detailFieldNoise.Elevation) *
                Mathf.Max(0f, detailStrength);
            return elevationScaleMeters * Mathf.Max(
                0f,
                continentPeak + mountainPeak + escarpmentPeak + detailPeak);
        }

        public override float EstimateTroughElevationMeters()
        {
            float oceanFloor = -Mathf.Abs(oceanFloorDepth) *
                (1f + Mathf.Max(0f, oceanDepthMultiplier));
            float detailTrough =
                -(detailRidgeNoise.Elevation + detailFieldNoise.Elevation) *
                Mathf.Max(0f, detailStrength);
            return elevationScaleMeters * Mathf.Min(0f, oceanFloor + detailTrough);
        }

        public override bool TrySampleGeology(
            float baseRadius,
            Vector3 unitDirection,
            out CelestialGeologySample sample)
        {
            baseRadius = Mathf.Max(0.01f, baseRadius);
            unitDirection = unitDirection.sqrMagnitude > 0f ? unitDirection.normalized : Vector3.up;
            if (escarpmentStrength <= 0f)
            {
                sample = CelestialGeologySample.None;
                return false;
            }

            float featureScale = ResolveFeatureScaleFactor(baseRadius);
            float mask = EvaluateEscarpmentMask(unitDirection, featureScale);
            if (mask <= 0.0001f)
            {
                sample = CelestialGeologySample.None;
                return false;
            }

            float spanMeters = Mathf.Max(1f, geologySpanMeters);
            float step = spanMeters / baseRadius;
            BuildTangentBasis(unitDirection, out Vector3 tangent, out Vector3 bitangent);
            float displacementScale = elevationScaleMeters * mask * escarpmentStrength;
            float here = SampleEscarpmentShape(unitDirection, featureScale) * displacementScale;
            float tangentPlus = SampleEscarpmentShape(
                (unitDirection + tangent * step).normalized,
                featureScale) * displacementScale;
            float tangentMinus = SampleEscarpmentShape(
                (unitDirection - tangent * step).normalized,
                featureScale) * displacementScale;
            float bitangentPlus = SampleEscarpmentShape(
                (unitDirection + bitangent * step).normalized,
                featureScale) * displacementScale;
            float bitangentMinus = SampleEscarpmentShape(
                (unitDirection - bitangent * step).normalized,
                featureScale) * displacementScale;

            Vector3 gradient =
                tangent * (tangentPlus - tangentMinus) +
                bitangent * (bitangentPlus - bitangentMinus);
            float rise = gradient.magnitude;
            float featureSpan = Mathf.Max(1f, featureSpanMeters);
            float slopeRatio = rise / (spanMeters * 2f);
            float curvature =
                (tangentPlus + tangentMinus + bitangentPlus + bitangentMinus) * 0.25f - here;
            float curvatureScale = featureSpan / spanMeters;

            sample = new CelestialGeologySample(
                mask * Mathf.Clamp01(slopeRatio / featureSlopeReference),
                slopeRatio * featureSpan,
                rise > 0.0001f ? gradient / rise : Vector3.zero,
                rise > 0.0001f ? -curvature * curvatureScale / rise : 0f);
            return sample.HasFeature;
        }

        float SampleEscarpmentShape(Vector3 unitDirection, float featureScale)
        {
            return escarpmentNoise.Sample(unitDirection, seed + 1100, 0f, 0f, featureScale);
        }

        float EvaluateEscarpmentMask(Vector3 unitDirection, float featureScale)
        {
            float mask = Blend(
                0f,
                escarpmentBlend,
                escarpmentMaskNoise.Sample(unitDirection, seed + 1200, 0f, featureScale));
            float continentShape =
                continentNoise.Sample(unitDirection, seed + 100, 0f, featureScale);
            continentShape = FarionMath.SmoothMax(continentShape, -oceanFloorDepth, oceanFloorSmoothing);
            float coastProximity = CalculateCoastProximity(continentShape);
            return Mathf.Max(0f, mask * (1f - coastProximity * coastalMountainFade));
        }

        static void BuildTangentBasis(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
        {
            Vector3 reference = Mathf.Abs(normal.y) < 0.95f ? Vector3.up : Vector3.right;
            tangent = Vector3.Cross(reference, normal).normalized;
            bitangent = Vector3.Cross(normal, tangent).normalized;
        }

        float ResolveRidgeFilterRadius(float angularSampleFootprint)
        {
            return Mathf.Max(0f, angularSampleFootprint) * 1.5f;
        }

        Vector4 EvaluateShadingData(
            Vector3 unitDirection,
            float sampleFootprint,
            float featureScale)
        {
            float large = largeNoise.Sample01(
                unitDirection,
                seed + 400,
                sampleFootprint,
                featureScale);
            float detailWarp = detailWarpNoise.Sample(
                unitDirection,
                seed + 500,
                sampleFootprint,
                featureScale);
            float detail = detailNoise.Sample01(
                unitDirection + Vector3.one * detailWarp * 0.1f,
                seed + 600,
                sampleFootprint,
                featureScale);
            float small = smallNoise.Sample01(
                unitDirection,
                seed + 700,
                sampleFootprint,
                featureScale);

            Vector3 warpOffset = new(
                smallNoise.Sample(unitDirection, seed + 800, sampleFootprint, featureScale),
                smallNoise.Sample(unitDirection, seed + 900, sampleFootprint, featureScale),
                smallNoise.Sample(unitDirection, seed + 1000, sampleFootprint, featureScale));

            float warped = detailNoise.Sample01(
                unitDirection + warpOffset * 0.1f,
                seed + 1100,
                sampleFootprint,
                featureScale);
            return new Vector4(large, detail, small, warped);
        }

        void OnValidate()
        {
            elevationScaleMeters = Mathf.Max(0f, elevationScaleMeters);
            featureReferenceRadiusMeters = Mathf.Max(1f, featureReferenceRadiusMeters);
            oceanDepthMultiplier = Mathf.Max(0f, oceanDepthMultiplier);
            oceanFloorSmoothing = Mathf.Max(0f, oceanFloorSmoothing);
            coastalShelfWidth = Mathf.Max(0f, coastalShelfWidth);
            coastalShelfStrength = Mathf.Clamp01(coastalShelfStrength);
            coastalMountainFade = Mathf.Clamp01(coastalMountainFade);
            mountainBlend = Mathf.Max(0f, mountainBlend);
            geologySpanMeters = Mathf.Max(1f, geologySpanMeters);
            featureSpanMeters = Mathf.Max(1f, featureSpanMeters);
            featureSlopeReference = Mathf.Clamp(featureSlopeReference, 0.01f, 2f);
            detailStrength = Mathf.Clamp(detailStrength, 0f, 2f);
            continentNoise.Clamp();
            mountainMaskNoise.Clamp();
            ridgeNoise.Clamp();
            escarpmentNoise.Clamp();
            escarpmentMaskNoise.Clamp();
            detailRidgeNoise.Clamp();
            detailFieldNoise.Clamp();
            detailWarpNoise.Clamp();
            detailNoise.Clamp();
            largeNoise.Clamp();
            smallNoise.Clamp();
            NotifyChanged();
        }

        float CalculateCoastProximity(float continentShape)
        {
            if (coastalShelfWidth <= 0f)
            {
                return 0f;
            }

            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Abs(continentShape) / coastalShelfWidth));
        }

        float CalculateDeepWaterFactor(float continentShape)
        {
            if (coastalShelfWidth <= 0f)
            {
                return 1f;
            }

            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-continentShape / coastalShelfWidth));
        }

        static float Blend(float startHeight, float blendDistance, float height)
        {
            if (blendDistance <= 0f)
            {
                return height >= startHeight ? 1f : 0f;
            }

            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
                startHeight - blendDistance * 0.5f,
                startHeight + blendDistance * 0.5f,
                height));
        }

        static float ResolveFadeWeight(float fadeFootprint, float sampleFootprint)
        {
            return PlanetarySampling.ResolveFootprintFade(fadeFootprint, sampleFootprint);
        }

        [System.Serializable]
        struct SimpleNoise
        {
            [Range(1, 8)] public int Octaves;
            [Min(1f)] public float Lacunarity;
            [Range(0f, 1f)] public float Persistence;
            [Min(0.001f)] public float Scale;
            public float Elevation;
            public float VerticalShift;
            [Min(0f)] public float FadeFootprint;
            public Vector3 Offset;

            public float Sample(Vector3 direction, int noiseSeed)
            {
                return PlanetarySampling.SampleFractalSigned(
                    direction,
                    Scale,
                    Octaves,
                    Lacunarity,
                    Persistence,
                    noiseSeed,
                    Offset) * Elevation + VerticalShift;
            }

            public float Sample(Vector3 direction, int noiseSeed, float sampleFootprint)
            {
                return Sample(direction, noiseSeed, sampleFootprint, 1f);
            }

            public float Sample(
                Vector3 direction,
                int noiseSeed,
                float sampleFootprint,
                float featureScale)
            {
                featureScale = Mathf.Max(0.0001f, featureScale);
                float scale = Scale * featureScale;
                float fade = ResolveFadeWeight(FadeFootprint / featureScale, sampleFootprint);
                if (fade <= 0f)
                {
                    return 0f;
                }

                float noise = PlanetarySampling.SampleBandLimitedFractalSigned(
                    direction,
                    scale,
                    Octaves,
                    Lacunarity,
                    Persistence,
                    noiseSeed,
                    Offset,
                    sampleFootprint);

                return (noise * Elevation + VerticalShift) * fade;
            }

            public float Sample01(
                Vector3 direction,
                int noiseSeed,
                float sampleFootprint,
                float featureScale = 1f)
            {
                return Mathf.Clamp01(
                    Sample(direction, noiseSeed, sampleFootprint, featureScale) * 0.5f + 0.5f);
            }

            public void Clamp()
            {
                Octaves = Mathf.Clamp(Octaves, 1, 8);
                Lacunarity = Mathf.Max(1f, Lacunarity);
                Persistence = Mathf.Clamp01(Persistence);
                Scale = Mathf.Max(0.001f, Scale);
                FadeFootprint = Mathf.Max(0f, FadeFootprint);
            }
        }

        [System.Serializable]
        struct RidgeNoise
        {
            [Range(1, 8)] public int Octaves;
            [Min(1f)] public float Lacunarity;
            [Range(0f, 1f)] public float Persistence;
            [Min(0.001f)] public float Scale;
            [Min(0.1f)] public float Power;
            public float Elevation;
            [Min(0f)] public float Gain;
            public float VerticalShift;
            [FormerlySerializedAs("PeakSmoothing")]
            [Range(0f, 0.1f)]
            public float SpatialSmoothing;
            [Min(0f)] public float FadeFootprint;
            public Vector3 Offset;

            public float Sample(
                Vector3 direction,
                int noiseSeed,
                float minimumSpatialSmoothing,
                float sampleFootprint)
            {
                return Sample(direction, noiseSeed, minimumSpatialSmoothing, sampleFootprint, 1f);
            }

            public float Sample(
                Vector3 direction,
                int noiseSeed,
                float minimumSpatialSmoothing,
                float sampleFootprint,
                float featureScale)
            {
                featureScale = Mathf.Max(0.0001f, featureScale);
                float scale = Scale * featureScale;
                float fade = ResolveFadeWeight(FadeFootprint / featureScale, sampleFootprint);
                if (fade <= 0f)
                {
                    return 0f;
                }

                direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
                float center = SampleRaw(direction, noiseSeed, scale);
                float filterRadius = Mathf.Max(
                    SpatialSmoothing / featureScale,
                    minimumSpatialSmoothing);
                if (filterRadius <= 0f)
                {
                    return center * fade;
                }

                Vector3 tangentA = Vector3.Cross(
                    Mathf.Abs(direction.y) < 0.95f ? Vector3.up : Vector3.right,
                    direction).normalized;
                Vector3 tangentB = Vector3.Cross(direction, tangentA).normalized;
                Vector3 diagonalA = (tangentA + tangentB).normalized;
                Vector3 diagonalB = (tangentA - tangentB).normalized;
                float smoothed = center * 0.24f;
                smoothed += SampleOffset(direction, tangentA, filterRadius, noiseSeed, scale) * 0.12f;
                smoothed += SampleOffset(direction, -tangentA, filterRadius, noiseSeed, scale) * 0.12f;
                smoothed += SampleOffset(direction, tangentB, filterRadius, noiseSeed, scale) * 0.12f;
                smoothed += SampleOffset(direction, -tangentB, filterRadius, noiseSeed, scale) * 0.12f;
                smoothed += SampleOffset(direction, diagonalA, filterRadius, noiseSeed, scale) * 0.07f;
                smoothed += SampleOffset(direction, -diagonalA, filterRadius, noiseSeed, scale) * 0.07f;
                smoothed += SampleOffset(direction, diagonalB, filterRadius, noiseSeed, scale) * 0.07f;
                smoothed += SampleOffset(direction, -diagonalB, filterRadius, noiseSeed, scale) * 0.07f;
                return smoothed * fade;
            }

            float SampleOffset(
                Vector3 direction,
                Vector3 tangent,
                float distance,
                int noiseSeed,
                float scale)
            {
                return SampleRaw((direction + tangent * distance).normalized, noiseSeed, scale);
            }

            float SampleRaw(Vector3 direction, int noiseSeed, float scale)
            {
                float ridge = PlanetarySampling.SampleRidged01(
                    direction,
                    scale,
                    Octaves,
                    Lacunarity,
                    Persistence,
                    Power,
                    Gain,
                    noiseSeed,
                    Offset);
                return (ridge * 2f - 1f) * Elevation + VerticalShift;
            }

            public void Clamp()
            {
                Octaves = Mathf.Clamp(Octaves, 1, 8);
                Lacunarity = Mathf.Max(1f, Lacunarity);
                Persistence = Mathf.Clamp01(Persistence);
                Scale = Mathf.Max(0.001f, Scale);
                Power = Mathf.Max(0.1f, Power);
                Gain = Mathf.Max(0f, Gain);
                SpatialSmoothing = Mathf.Clamp(SpatialSmoothing, 0f, 0.1f);
                FadeFootprint = Mathf.Max(0f, FadeFootprint);
            }
        }
    }
}
