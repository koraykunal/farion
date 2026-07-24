using UnityEngine;

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
            PeakSmoothing = 1f,
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
            return (EvaluateHeightRatio(unitDirection) - 1f) * Mathf.Max(0.01f, baseRadius);
        }

        public override CelestialShapeSample EvaluateSample(float baseRadius, Vector3 unitDirection)
        {
            baseRadius = Mathf.Max(0.01f, baseRadius);
            unitDirection = unitDirection.sqrMagnitude > 0f ? unitDirection.normalized : Vector3.up;

            float heightRatio = EvaluateHeightRatio(unitDirection);
            float radius = Mathf.Max(0.01f, baseRadius * heightRatio);
            return new CelestialShapeSample(radius, EvaluateShadingData(unitDirection));
        }

        float EvaluateHeightRatio(Vector3 unitDirection)
        {
            float continentShape = continentNoise.Sample(unitDirection, seed + 100);
            continentShape = SmoothMax(continentShape, -oceanFloorDepth, oceanFloorSmoothing);
            float coastProximity = CalculateCoastProximity(continentShape);

            if (continentShape < 0f)
            {
                float deepWaterFactor = CalculateDeepWaterFactor(continentShape);
                continentShape *= 1f + oceanDepthMultiplier * Mathf.Lerp(1f, deepWaterFactor, coastalShelfStrength);
            }

            float mountainShape = ridgeNoise.Sample(unitDirection, seed + 200);
            float mountainMask = Blend(0f, mountainBlend, mountainMaskNoise.Sample(unitDirection, seed + 300));
            mountainMask *= 1f - coastProximity * coastalMountainFade;
            return 1f + continentShape * 0.01f + mountainShape * 0.01f * mountainMask;
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
                float amplitude = 1f;
                float frequency = Scale;
                float total = 0f;
                float amplitudeTotal = 0f;
                Vector3 seededOffset = Offset + SeedOffset(noiseSeed);

                for (int octave = 0; octave < Mathf.Max(1, Octaves); octave++)
                {
                    total += SampleSignedNoise(direction + seededOffset, frequency, noiseSeed + octave * 101) * amplitude;
                    amplitudeTotal += amplitude;
                    amplitude *= Persistence;
                    frequency *= Lacunarity;
                }

                float normalized = amplitudeTotal > 0f ? total / amplitudeTotal : 0f;
                return normalized * Elevation + VerticalShift;
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
            [Min(0f)] public float PeakSmoothing;
            public Vector3 Offset;

            public float Sample(Vector3 direction, int noiseSeed)
            {
                float amplitude = 1f;
                float frequency = Scale;
                float total = 0f;
                float amplitudeTotal = 0f;
                Vector3 seededOffset = Offset + SeedOffset(noiseSeed);

                for (int octave = 0; octave < Mathf.Max(1, Octaves); octave++)
                {
                    float value = 1f - Mathf.Abs(SampleSignedNoise(direction + seededOffset, frequency, noiseSeed + octave * 137));
                    value = Mathf.Pow(Mathf.Max(0f, value), Power);
                    if (PeakSmoothing > 0f)
                    {
                        value = Mathf.SmoothStep(0f, 1f, value / Mathf.Max(0.0001f, PeakSmoothing));
                    }

                    total += value * amplitude;
                    amplitudeTotal += amplitude;
                    amplitude *= Mathf.Lerp(Persistence, Persistence * Gain, 0.5f);
                    frequency *= Lacunarity;
                }

                float normalized = amplitudeTotal > 0f ? total / amplitudeTotal : 0f;
                return (normalized * 2f - 1f) * Elevation + VerticalShift;
            }

            public void Clamp()
            {
                Octaves = Mathf.Clamp(Octaves, 1, 8);
                Lacunarity = Mathf.Max(1f, Lacunarity);
                Persistence = Mathf.Clamp01(Persistence);
                Scale = Mathf.Max(0.001f, Scale);
                Power = Mathf.Max(0.1f, Power);
                Gain = Mathf.Max(0f, Gain);
                PeakSmoothing = Mathf.Max(0f, PeakSmoothing);
            }
        }

        static Vector3 SeedOffset(int noiseSeed)
        {
            float x = Mathf.Sin(noiseSeed * 12.9898f) * 43758.5453f;
            float y = Mathf.Sin(noiseSeed * 78.233f) * 24634.6345f;
            float z = Mathf.Sin(noiseSeed * 37.719f) * 12515.8731f;
            return new Vector3(x - Mathf.Floor(x), y - Mathf.Floor(y), z - Mathf.Floor(z)) * 1000f;
        }

        static float SampleSignedNoise(Vector3 direction, float frequency, int sampleSeed)
        {
            float seedOffset = sampleSeed * 0.137f;

            float xy = Mathf.PerlinNoise(
                (direction.x + seedOffset) * frequency,
                (direction.y - seedOffset) * frequency);

            float yz = Mathf.PerlinNoise(
                (direction.y + seedOffset) * frequency,
                (direction.z - seedOffset) * frequency);

            float zx = Mathf.PerlinNoise(
                (direction.z + seedOffset) * frequency,
                (direction.x - seedOffset) * frequency);

            return ((xy + yz + zx) / 3f) * 2f - 1f;
        }
    }
}
