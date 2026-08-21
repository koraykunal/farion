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
            return (EvaluateHeightRatio(unitDirection, 0f) - 1f) * Mathf.Max(0.01f, baseRadius);
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
                baseRadius * EvaluateHeightRatio(unitDirection, angularSampleFootprint));
        }

        public override CelestialShapeSample EvaluateSample(
            float baseRadius,
            Vector3 unitDirection,
            float angularSampleFootprint)
        {
            baseRadius = Mathf.Max(0.01f, baseRadius);
            unitDirection = unitDirection.sqrMagnitude > 0f ? unitDirection.normalized : Vector3.up;

            float heightRatio = EvaluateHeightRatio(unitDirection, angularSampleFootprint);
            float radius = Mathf.Max(0.01f, baseRadius * heightRatio);
            return new CelestialShapeSample(
                radius,
                EvaluateShadingData(unitDirection, angularSampleFootprint));
        }

        float EvaluateHeightRatio(Vector3 unitDirection, float sampleFootprint)
        {
            float ridgeFilterRadius = ResolveRidgeFilterRadius(sampleFootprint);
            float continentShape = continentNoise.Sample(unitDirection, seed + 100, sampleFootprint);
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
                sampleFootprint);
            float mountainMask = Blend(
                0f,
                mountainBlend,
                mountainMaskNoise.Sample(unitDirection, seed + 300, sampleFootprint));
            mountainMask *= 1f - coastProximity * coastalMountainFade;

            float escarpmentShape = escarpmentNoise.Sample(
                unitDirection,
                seed + 1100,
                ridgeFilterRadius,
                sampleFootprint);
            float escarpmentMask = Blend(
                0f,
                escarpmentBlend,
                escarpmentMaskNoise.Sample(unitDirection, seed + 1200, sampleFootprint));
            escarpmentMask *= 1f - coastProximity * coastalMountainFade;

            float detail = detailStrength <= 0f
                ? 0f
                : detailRidgeNoise.Sample(unitDirection, seed + 2100, 0f, sampleFootprint) +
                    detailFieldNoise.Sample(unitDirection, seed + 2200, sampleFootprint);

            return 1f +
                continentShape * 0.01f +
                mountainShape * 0.01f * mountainMask +
                escarpmentShape * 0.01f * escarpmentMask * escarpmentStrength +
                detail * 0.01f * detailStrength;
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

            float mask = EvaluateEscarpmentMask(unitDirection);
            if (mask <= 0.0001f)
            {
                sample = CelestialGeologySample.None;
                return false;
            }

            float spanMeters = Mathf.Max(1f, geologySpanMeters);
            float step = spanMeters / baseRadius;
            BuildTangentBasis(unitDirection, out Vector3 tangent, out Vector3 bitangent);
            float displacementScale = 0.01f * mask * escarpmentStrength * baseRadius;
            float here = SampleEscarpmentShape(unitDirection) * displacementScale;
            float tangentPlus =
                SampleEscarpmentShape((unitDirection + tangent * step).normalized) * displacementScale;
            float tangentMinus =
                SampleEscarpmentShape((unitDirection - tangent * step).normalized) * displacementScale;
            float bitangentPlus =
                SampleEscarpmentShape((unitDirection + bitangent * step).normalized) * displacementScale;
            float bitangentMinus =
                SampleEscarpmentShape((unitDirection - bitangent * step).normalized) * displacementScale;

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

        float SampleEscarpmentShape(Vector3 unitDirection)
        {
            return escarpmentNoise.Sample(unitDirection, seed + 1100, 0f, 0f);
        }

        float EvaluateEscarpmentMask(Vector3 unitDirection)
        {
            float mask = Blend(
                0f,
                escarpmentBlend,
                escarpmentMaskNoise.Sample(unitDirection, seed + 1200));
            float continentShape = continentNoise.Sample(unitDirection, seed + 100);
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

        Vector4 EvaluateShadingData(Vector3 unitDirection, float sampleFootprint)
        {
            float large = largeNoise.Sample01(unitDirection, seed + 400, sampleFootprint);
            float detailWarp = detailWarpNoise.Sample(unitDirection, seed + 500, sampleFootprint);
            float detail = detailNoise.Sample01(unitDirection + Vector3.one * detailWarp * 0.1f, seed + 600, sampleFootprint);
            float small = smallNoise.Sample01(unitDirection, seed + 700, sampleFootprint);

            Vector3 warpOffset = new(
                smallNoise.Sample(unitDirection, seed + 800, sampleFootprint),
                smallNoise.Sample(unitDirection, seed + 900, sampleFootprint),
                smallNoise.Sample(unitDirection, seed + 1000, sampleFootprint));

            float warped = detailNoise.Sample01(unitDirection + warpOffset * 0.1f, seed + 1100, sampleFootprint);
            return new Vector4(large, detail, small, warped);
        }

        void OnValidate()
        {
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
            if (fadeFootprint <= 0f || sampleFootprint <= 0f)
            {
                return 1f;
            }

            return 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(fadeFootprint * 0.5f, fadeFootprint, sampleFootprint));
        }

        static float ResolveUsableOctaves(float scale, float lacunarity, int octaves, float sampleFootprint)
        {
            if (sampleFootprint <= 0f)
            {
                return octaves;
            }

            float nyquistFrequency = 1f / (2f * sampleFootprint);
            if (scale >= nyquistFrequency)
            {
                return 1f;
            }

            float safeLacunarity = Mathf.Max(1.0001f, lacunarity);
            float usable = 1f + Mathf.Log(nyquistFrequency / scale) / Mathf.Log(safeLacunarity);
            return Mathf.Clamp(usable, 1f, octaves);
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
                float fade = ResolveFadeWeight(FadeFootprint, sampleFootprint);
                if (fade <= 0f)
                {
                    return 0f;
                }

                float usableOctaves = ResolveUsableOctaves(Scale, Lacunarity, Octaves, sampleFootprint);
                int wholeOctaves = Mathf.Clamp(Mathf.FloorToInt(usableOctaves), 1, Octaves);
                float noise = PlanetarySampling.SampleFractalSigned(
                    direction,
                    Scale,
                    wholeOctaves,
                    Lacunarity,
                    Persistence,
                    noiseSeed,
                    Offset);

                float blend = Mathf.Clamp01(usableOctaves - wholeOctaves);
                if (blend > 0f && wholeOctaves < Octaves)
                {
                    float finer = PlanetarySampling.SampleFractalSigned(
                        direction,
                        Scale,
                        wholeOctaves + 1,
                        Lacunarity,
                        Persistence,
                        noiseSeed,
                        Offset);
                    noise = Mathf.Lerp(noise, finer, blend);
                }

                return (noise * Elevation + VerticalShift) * fade;
            }

            public float Sample01(Vector3 direction, int noiseSeed, float sampleFootprint)
            {
                return Mathf.Clamp01(Sample(direction, noiseSeed, sampleFootprint) * 0.5f + 0.5f);
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
                float fade = ResolveFadeWeight(FadeFootprint, sampleFootprint);
                if (fade <= 0f)
                {
                    return 0f;
                }

                direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
                float center = SampleRaw(direction, noiseSeed);
                float filterRadius = Mathf.Max(SpatialSmoothing, minimumSpatialSmoothing);
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
                smoothed += SampleOffset(direction, tangentA, filterRadius, noiseSeed) * 0.12f;
                smoothed += SampleOffset(direction, -tangentA, filterRadius, noiseSeed) * 0.12f;
                smoothed += SampleOffset(direction, tangentB, filterRadius, noiseSeed) * 0.12f;
                smoothed += SampleOffset(direction, -tangentB, filterRadius, noiseSeed) * 0.12f;
                smoothed += SampleOffset(direction, diagonalA, filterRadius, noiseSeed) * 0.07f;
                smoothed += SampleOffset(direction, -diagonalA, filterRadius, noiseSeed) * 0.07f;
                smoothed += SampleOffset(direction, diagonalB, filterRadius, noiseSeed) * 0.07f;
                smoothed += SampleOffset(direction, -diagonalB, filterRadius, noiseSeed) * 0.07f;
                return smoothed * fade;
            }

            float SampleOffset(Vector3 direction, Vector3 tangent, float distance, int noiseSeed)
            {
                return SampleRaw((direction + tangent * distance).normalized, noiseSeed);
            }

            float SampleRaw(Vector3 direction, int noiseSeed)
            {
                float ridge = PlanetarySampling.SampleRidged01(
                    direction,
                    Scale,
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
