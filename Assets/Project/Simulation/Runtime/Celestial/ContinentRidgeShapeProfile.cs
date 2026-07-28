using Farion.Simulation.Planetary;
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
                baseRadius * EvaluateHeightRatio(unitDirection, ResolveRidgeFilterRadius(angularSampleFootprint)));
        }

        public override CelestialShapeSample EvaluateSample(
            float baseRadius,
            Vector3 unitDirection,
            float angularSampleFootprint)
        {
            baseRadius = Mathf.Max(0.01f, baseRadius);
            unitDirection = unitDirection.sqrMagnitude > 0f ? unitDirection.normalized : Vector3.up;

            float heightRatio = EvaluateHeightRatio(
                unitDirection,
                ResolveRidgeFilterRadius(angularSampleFootprint));
            float radius = Mathf.Max(0.01f, baseRadius * heightRatio);
            return new CelestialShapeSample(radius, EvaluateShadingData(unitDirection));
        }

        float EvaluateHeightRatio(Vector3 unitDirection, float ridgeFilterRadius)
        {
            float continentShape = continentNoise.Sample(unitDirection, seed + 100);
            continentShape = SmoothMax(continentShape, -oceanFloorDepth, oceanFloorSmoothing);
            float coastProximity = CalculateCoastProximity(continentShape);

            if (continentShape < 0f)
            {
                float deepWaterFactor = CalculateDeepWaterFactor(continentShape);
                continentShape *= 1f + oceanDepthMultiplier * Mathf.Lerp(1f, deepWaterFactor, coastalShelfStrength);
            }

            float mountainShape = ridgeNoise.Sample(unitDirection, seed + 200, ridgeFilterRadius);
            float mountainMask = Blend(0f, mountainBlend, mountainMaskNoise.Sample(unitDirection, seed + 300));
            mountainMask *= 1f - coastProximity * coastalMountainFade;
            return 1f + continentShape * 0.01f + mountainShape * 0.01f * mountainMask;
        }

        float ResolveRidgeFilterRadius(float angularSampleFootprint)
        {
            return Mathf.Max(0f, angularSampleFootprint) * 1.5f;
        }

        Vector4 EvaluateShadingData(Vector3 unitDirection)
        {
            float large = largeNoise.Sample(unitDirection, seed + 400);
            float detailWarp = detailWarpNoise.Sample(unitDirection, seed + 500);
            float detail = detailNoise.Sample(unitDirection + Vector3.one * detailWarp * 0.1f, seed + 600);
            float small = smallNoise.Sample(unitDirection, seed + 700);

            Vector3 warpOffset = new(
                smallNoise.Sample(unitDirection + Vector3.right * 11.37f, seed + 800),
                smallNoise.Sample(unitDirection + Vector3.up * 29.71f, seed + 900),
                smallNoise.Sample(unitDirection + Vector3.forward * 47.13f, seed + 1000));

            float warped = detailNoise.Sample(unitDirection + warpOffset * 0.1f, seed + 1100);
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
            continentNoise.Clamp();
            mountainMaskNoise.Clamp();
            ridgeNoise.Clamp();
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

        static float SmoothMax(float a, float b, float k)
        {
            if (k <= 0f)
            {
                return Mathf.Max(a, b);
            }

            return -SmoothMin(-a, -b, k);
        }

        static float SmoothMin(float a, float b, float k)
        {
            if (k <= 0f)
            {
                return Mathf.Min(a, b);
            }

            float h = Mathf.Clamp01((b - a + k) / (2f * k));
            return a * h + b * (1f - h) - k * h * (1f - h);
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
            public Vector3 Offset;

            public float Sample(Vector3 direction, int noiseSeed)
            {
                float noise = PlanetarySampling.SampleFractalSigned(
                    direction + Offset,
                    Scale,
                    Octaves,
                    Lacunarity,
                    Persistence,
                    noiseSeed);
                return noise * Elevation + VerticalShift;
            }

            public void Clamp()
            {
                Octaves = Mathf.Clamp(Octaves, 1, 8);
                Lacunarity = Mathf.Max(1f, Lacunarity);
                Persistence = Mathf.Clamp01(Persistence);
                Scale = Mathf.Max(0.001f, Scale);
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
            public Vector3 Offset;

            public float Sample(Vector3 direction, int noiseSeed, float minimumSpatialSmoothing)
            {
                direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
                float center = SampleRaw(direction, noiseSeed);
                float filterRadius = Mathf.Max(SpatialSmoothing, minimumSpatialSmoothing);
                if (filterRadius <= 0f)
                {
                    return center;
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
                return smoothed;
            }

            float SampleOffset(Vector3 direction, Vector3 tangent, float distance, int noiseSeed)
            {
                return SampleRaw((direction + tangent * distance).normalized, noiseSeed);
            }

            float SampleRaw(Vector3 direction, int noiseSeed)
            {
                float ridge = PlanetarySampling.SampleRidged01(
                    direction + Offset,
                    Scale,
                    Octaves,
                    Lacunarity,
                    Persistence,
                    Power,
                    Gain,
                    noiseSeed);
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
            }
        }
    }
}
