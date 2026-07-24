using System;
using UnityEngine;

namespace Farion.Simulation.Celestial
{
    [CreateAssetMenu(menuName = "Farion/Simulation/Celestial/Moon Crater Shape Profile", fileName = "SO_MoonCraterShapeProfile")]
    public sealed class MoonCraterShapeProfile : CelestialShapeProfile
    {
        const float BiomeWarpStrength = 0.1f;
        const float BiomeWarpScale = 1.88f;
        const int BiomeWarpOctaves = 4;
        const float DetailWarpStrength = 0.1f;
        const float DetailWarpScale = 2.5f;
        const int DetailWarpOctaves = 4;
        const float DetailNoisePersistence = 0.5f;
        const float DetailNoiseLacunarity = 2.34f;

        [Header("Seed")]
        [SerializeField] int seed = 1;
        [SerializeField] int craterSeed = 17;

        [Header("Craters")]
        [SerializeField] bool cratersEnabled = true;
        [Range(1, 2500)]
        [SerializeField] int craterCount = 400;
        [SerializeField] Vector2 craterRadiusMinMax = new(0.01f, 0.1f);
        [Range(0f, 1f)]
        [SerializeField] float sizeDistribution = 0.6f;
        [SerializeField] Vector2 floorHeightMinMax = new(-1.2f, -0.2f);
        [SerializeField] Vector2 smoothnessMinMax = new(0.4f, 1.5f);
        [Min(0f)]
        [SerializeField] float rimSteepness = 0.13f;
        [Min(0f)]
        [SerializeField] float rimWidth = 1.6f;
        [Min(0f)]
        [SerializeField] float craterDepthScale = 1f;

        [Header("Base Deformation")]
        [Min(0f)]
        [SerializeField] float lowFrequencyAmplitude = 0.01f;
        [Min(0.001f)]
        [SerializeField] float lowFrequencyScale = 2f;
        [Range(1, 8)]
        [SerializeField] int lowFrequencyOctaves = 4;

        [Header("Ridges")]
        [Min(0f)]
        [SerializeField] float ridgeAmplitude = 0.006f;
        [Min(0.001f)]
        [SerializeField] float ridgeScale = 2.5f;
        [Range(1, 8)]
        [SerializeField] int ridgeOctaves = 4;
        [Min(0.1f)]
        [SerializeField] float ridgePower = 2.5f;
        [Range(0f, 1f)]
        [SerializeField] float ridgePersistence = 0.42f;
        [Min(1f)]
        [SerializeField] float ridgeLacunarity = 2f;

        [Header("Shading Data")]
        [Range(1, 128)]
        [SerializeField] int biomePointCount = 24;
        [SerializeField] Vector2 biomeRadiusMinMax = new(0.02f, 0.1f);
        [Min(0.001f)]
        [SerializeField] float detailNoiseScale = 1.35f;
        [Range(1, 8)]
        [SerializeField] int detailNoiseOctaves = 4;

        [Header("Ejecta Rays")]
        [SerializeField] bool ejectaEnabled = true;
        [Range(0.01f, 1f)]
        [SerializeField] float ejectaCandidatePoolSize = 0.2f;
        [Range(0, 12)]
        [SerializeField] int desiredEjectaCraterCount = 5;
        [SerializeField] int ejectaRaySeed = 101;
        [Min(0.1f)]
        [SerializeField] float ejectaRayScale = 10f;

        Crater[] cachedCraters;
        Crater[] cachedEjectaCraters;
        Vector4[] cachedBiomePoints;
        int cachedHash;

        public override float EvaluateDisplacement(float baseRadius, Vector3 unitDirection)
        {
            return EvaluateUnitDisplacement(unitDirection) * Mathf.Max(0.01f, baseRadius);
        }

        public override CelestialShapeSample EvaluateSample(float baseRadius, Vector3 unitDirection)
        {
            baseRadius = Mathf.Max(0.01f, baseRadius);
            unitDirection = unitDirection.sqrMagnitude > 0f ? unitDirection.normalized : Vector3.up;

            float displacement = EvaluateUnitDisplacement(unitDirection) * baseRadius;
            float radius = Mathf.Max(0.01f, baseRadius + displacement);
            return new CelestialShapeSample(radius, EvaluateShadingData(unitDirection));
        }

        float EvaluateUnitDisplacement(Vector3 unitDirection)
        {
            EnsureCachedData();

            float unitDisplacement = 0f;
            if (cratersEnabled)
            {
                for (int i = 0; i < cachedCraters.Length; i++)
                {
                    unitDisplacement += CalculateCraterHeight(unitDirection, cachedCraters[i]);
                }
            }

            unitDisplacement *= craterDepthScale;
            unitDisplacement += FractalNoise(unitDirection, lowFrequencyScale, lowFrequencyOctaves, 0.5f, 2f, seed + 1000) * lowFrequencyAmplitude;
            unitDisplacement += RidgedNoise(unitDirection, ridgeScale, ridgeOctaves, ridgePersistence, ridgeLacunarity, ridgePower, seed + 2000) * ridgeAmplitude;

            return unitDisplacement;
        }

        Vector4 EvaluateShadingData(Vector3 unitDirection)
        {
            EnsureCachedData();

            Vector2 ejectaUv = CalculateEjectaUv(unitDirection);
            Vector3 biomeDirection = WarpDirection(
                unitDirection,
                BiomeWarpStrength,
                BiomeWarpScale,
                BiomeWarpOctaves,
                seed + 3000);

            Vector3 detailDirection = WarpDirection(
                unitDirection,
                DetailWarpStrength,
                DetailWarpScale,
                DetailWarpOctaves,
                seed + 4000);

            float detailNoise = FractalNoise(
                detailDirection,
                detailNoiseScale,
                detailNoiseOctaves,
                DetailNoisePersistence,
                DetailNoiseLacunarity,
                seed + 5000);

            float biomeNoise = CalculateBiomeNoise(biomeDirection);

            return new Vector4(ejectaUv.x, ejectaUv.y, detailNoise, biomeNoise);
        }

        void EnsureCachedData()
        {
            int hash = CalculateSettingsHash();
            if (cachedCraters != null && cachedHash == hash)
            {
                return;
            }

            cachedHash = hash;
            GenerateCraters();
            GenerateBiomePoints();
            SelectEjectaCraters();
        }

        void GenerateCraters()
        {
            int count = cratersEnabled ? Mathf.Max(1, craterCount) : 0;
            cachedCraters = new Crater[count];
            DeterministicRandom random = new(seed + craterSeed);

            for (int i = 0; i < count; i++)
            {
                float sizeT = random.ValueBiasLower(sizeDistribution);
                float radius = Mathf.Lerp(craterRadiusMinMax.x, craterRadiusMinMax.y, sizeT);
                float floorHeight = Mathf.Lerp(
                    floorHeightMinMax.x,
                    floorHeightMinMax.y,
                    Mathf.Clamp01(sizeT + random.ValueBiasLower(0.3f)));

                float smoothness = Mathf.Lerp(smoothnessMinMax.x, smoothnessMinMax.y, 1f - sizeT);

                cachedCraters[i] = new Crater(
                    random.OnUnitSphere(),
                    Mathf.Max(0.0001f, radius),
                    floorHeight,
                    Mathf.Max(0f, smoothness));
            }
        }

        void GenerateBiomePoints()
        {
            cachedBiomePoints = new Vector4[Mathf.Max(1, biomePointCount)];
            DeterministicRandom random = new(seed + 7000);

            for (int i = 0; i < cachedBiomePoints.Length; i++)
            {
                Vector3 point = random.OnUnitSphere();
                float radius = Mathf.Lerp(biomeRadiusMinMax.x, biomeRadiusMinMax.y, random.Value());
                cachedBiomePoints[i] = new Vector4(point.x, point.y, point.z, Mathf.Max(0.0001f, radius));
            }
        }

        void SelectEjectaCraters()
        {
            if (!ejectaEnabled || desiredEjectaCraterCount <= 0 || cachedCraters.Length == 0)
            {
                cachedEjectaCraters = Array.Empty<Crater>();
                return;
            }

            Crater[] candidates = new Crater[cachedCraters.Length];
            Array.Copy(cachedCraters, candidates, cachedCraters.Length);
            Array.Sort(candidates, (a, b) => b.Radius.CompareTo(a.Radius));

            int poolSize = Mathf.Clamp(
                Mathf.CeilToInt(candidates.Length * ejectaCandidatePoolSize),
                1,
                candidates.Length);

            DeterministicRandom random = new(seed + ejectaRaySeed);
            for (int i = 0; i < poolSize - 1; i++)
            {
                int swapIndex = random.Range(i, poolSize);
                (candidates[i], candidates[swapIndex]) = (candidates[swapIndex], candidates[i]);
            }

            Crater[] selected = new Crater[Mathf.Min(desiredEjectaCraterCount, poolSize)];
            int selectedCount = 0;

            for (int i = 0; i < poolSize && selectedCount < selected.Length; i++)
            {
                Crater candidate = candidates[i];
                bool overlaps = false;

                for (int selectedIndex = 0; selectedIndex < selectedCount; selectedIndex++)
                {
                    float distance = Vector3.Distance(candidate.Centre, selected[selectedIndex].Centre);
                    float ejectaRadiusSum = (candidate.Radius + selected[selectedIndex].Radius) * ejectaRayScale * 0.5f;
                    if (distance < ejectaRadiusSum)
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (overlaps)
                {
                    continue;
                }

                selected[selectedCount] = candidate;
                selectedCount++;
            }

            cachedEjectaCraters = new Crater[selectedCount];
            Array.Copy(selected, cachedEjectaCraters, selectedCount);
        }

        float CalculateCraterHeight(Vector3 unitDirection, Crater crater)
        {
            float x = Vector3.Distance(unitDirection, crater.Centre) / crater.Radius;
            float cavity = x * x - 1f;
            float rimX = Mathf.Min(x - 1f - rimWidth, 0f);
            float rim = rimSteepness * rimX * rimX;

            float craterShape = SmoothMax(cavity, crater.FloorHeight, crater.Smoothness);
            craterShape = SmoothMin(craterShape, rim, crater.Smoothness);
            return craterShape * crater.Radius;
        }

        Vector2 CalculateEjectaUv(Vector3 unitDirection)
        {
            if (cachedEjectaCraters == null || cachedEjectaCraters.Length == 0)
            {
                return Vector2.one;
            }

            float minScaledDistance = float.PositiveInfinity;
            float angle = 0f;

            for (int i = 0; i < cachedEjectaCraters.Length; i++)
            {
                Crater crater = cachedEjectaCraters[i];
                float scaledDistance = Vector3.Distance(crater.Centre, unitDirection) / Mathf.Max(0.0001f, crater.Radius * ejectaRayScale);
                if (scaledDistance >= minScaledDistance)
                {
                    continue;
                }

                minScaledDistance = scaledDistance;
                Vector3 craterUp = crater.Centre.normalized;
                Vector3 craterRight = Vector3.Cross(Vector3.up, craterUp);
                if (craterRight.sqrMagnitude < 0.0001f)
                {
                    craterRight = Vector3.Cross(Vector3.right, craterUp);
                }

                craterRight.Normalize();
                Vector3 craterForward = Vector3.Cross(craterUp, craterRight).normalized;
                Vector3 sampleTangent = unitDirection - craterUp * Vector3.Dot(unitDirection, craterUp);
                if (sampleTangent.sqrMagnitude < 0.0001f)
                {
                    sampleTangent = craterForward;
                }

                sampleTangent.Normalize();
                angle = Mathf.Atan2(
                    Vector3.Dot(sampleTangent, craterRight),
                    Vector3.Dot(sampleTangent, craterForward));
            }

            return new Vector2(angle, minScaledDistance);
        }

        float CalculateBiomeNoise(Vector3 unitDirection)
        {
            if (cachedBiomePoints == null || cachedBiomePoints.Length == 0)
            {
                return 1f;
            }

            float minDistance = float.PositiveInfinity;
            for (int i = 0; i < cachedBiomePoints.Length; i++)
            {
                Vector3 point = new(cachedBiomePoints[i].x, cachedBiomePoints[i].y, cachedBiomePoints[i].z);
                float radius = Mathf.Max(0.0001f, cachedBiomePoints[i].w);
                float distance = Vector3.Distance(unitDirection, point) / radius;
                minDistance = Mathf.Min(minDistance, distance);
            }

            return minDistance;
        }

        int CalculateSettingsHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + seed;
                hash = hash * 31 + craterSeed;
                hash = hash * 31 + cratersEnabled.GetHashCode();
                hash = hash * 31 + craterCount;
                hash = hash * 31 + craterRadiusMinMax.GetHashCode();
                hash = hash * 31 + sizeDistribution.GetHashCode();
                hash = hash * 31 + floorHeightMinMax.GetHashCode();
                hash = hash * 31 + smoothnessMinMax.GetHashCode();
                hash = hash * 31 + rimSteepness.GetHashCode();
                hash = hash * 31 + rimWidth.GetHashCode();
                hash = hash * 31 + craterDepthScale.GetHashCode();
                hash = hash * 31 + biomePointCount;
                hash = hash * 31 + biomeRadiusMinMax.GetHashCode();
                hash = hash * 31 + ejectaEnabled.GetHashCode();
                hash = hash * 31 + ejectaCandidatePoolSize.GetHashCode();
                hash = hash * 31 + desiredEjectaCraterCount;
                hash = hash * 31 + ejectaRaySeed;
                hash = hash * 31 + ejectaRayScale.GetHashCode();
                return hash;
            }
        }

        void OnValidate()
        {
            craterCount = Mathf.Max(1, craterCount);
            craterRadiusMinMax.x = Mathf.Max(0.0001f, craterRadiusMinMax.x);
            craterRadiusMinMax.y = Mathf.Max(craterRadiusMinMax.x, craterRadiusMinMax.y);
            smoothnessMinMax.x = Mathf.Max(0f, smoothnessMinMax.x);
            smoothnessMinMax.y = Mathf.Max(smoothnessMinMax.x, smoothnessMinMax.y);
            craterDepthScale = Mathf.Max(0f, craterDepthScale);
            lowFrequencyAmplitude = Mathf.Max(0f, lowFrequencyAmplitude);
            lowFrequencyScale = Mathf.Max(0.001f, lowFrequencyScale);
            ridgeAmplitude = Mathf.Max(0f, ridgeAmplitude);
            ridgeScale = Mathf.Max(0.001f, ridgeScale);
            ridgePower = Mathf.Max(0.1f, ridgePower);
            ridgeLacunarity = Mathf.Max(1f, ridgeLacunarity);
            biomePointCount = Mathf.Max(1, biomePointCount);
            biomeRadiusMinMax.x = Mathf.Max(0.0001f, biomeRadiusMinMax.x);
            biomeRadiusMinMax.y = Mathf.Max(biomeRadiusMinMax.x, biomeRadiusMinMax.y);
            detailNoiseScale = Mathf.Max(0.001f, detailNoiseScale);
            ejectaRayScale = Mathf.Max(0.1f, ejectaRayScale);
            cachedCraters = null;
            cachedEjectaCraters = null;
            cachedBiomePoints = null;
            NotifyChanged();
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

        static float SmoothMax(float a, float b, float k)
        {
            if (k <= 0f)
            {
                return Mathf.Max(a, b);
            }

            return -SmoothMin(-a, -b, k);
        }

        static float FractalNoise(
            Vector3 direction,
            float scale,
            int octaves,
            float persistence,
            float lacunarity,
            int noiseSeed)
        {
            float amplitude = 1f;
            float frequency = scale;
            float total = 0f;
            float amplitudeTotal = 0f;

            for (int octave = 0; octave < octaves; octave++)
            {
                total += SampleSignedNoise(direction, frequency, noiseSeed + octave * 101) * amplitude;
                amplitudeTotal += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return amplitudeTotal > 0f ? total / amplitudeTotal : 0f;
        }

        static float RidgedNoise(
            Vector3 direction,
            float scale,
            int octaves,
            float persistence,
            float lacunarity,
            float power,
            int noiseSeed)
        {
            float amplitude = 1f;
            float frequency = scale;
            float total = 0f;
            float amplitudeTotal = 0f;

            for (int octave = 0; octave < octaves; octave++)
            {
                float value = 1f - Mathf.Abs(SampleSignedNoise(direction, frequency, noiseSeed + octave * 137));
                value = Mathf.Pow(Mathf.Abs(value), power);
                total += value * amplitude;
                amplitudeTotal += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            float normalized = amplitudeTotal > 0f ? total / amplitudeTotal : 0f;
            return normalized * 2f - 1f;
        }

        static Vector3 WarpDirection(
            Vector3 direction,
            float strength,
            float scale,
            int octaves,
            int noiseSeed)
        {
            if (strength <= 0f)
            {
                return direction;
            }

            Vector3 warp = new(
                FractalNoise(direction + new Vector3(11.3f, -7.1f, 3.8f), scale, octaves, 0.5f, 2f, noiseSeed),
                FractalNoise(direction + new Vector3(-5.9f, 13.7f, 2.4f), scale, octaves, 0.5f, 2f, noiseSeed + 211),
                FractalNoise(direction + new Vector3(4.1f, -2.6f, 17.9f), scale, octaves, 0.5f, 2f, noiseSeed + 421));

            Vector3 warpedDirection = direction + warp * strength;
            return warpedDirection.sqrMagnitude > 0f ? warpedDirection.normalized : direction;
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

        readonly struct Crater
        {
            public Crater(Vector3 centre, float radius, float floorHeight, float smoothness)
            {
                Centre = centre;
                Radius = radius;
                FloorHeight = floorHeight;
                Smoothness = smoothness;
            }

            public Vector3 Centre { get; }
            public float Radius { get; }
            public float FloorHeight { get; }
            public float Smoothness { get; }
        }

        sealed class DeterministicRandom
        {
            readonly System.Random random;

            public DeterministicRandom(int seed)
            {
                random = new System.Random(seed);
            }

            public float Value()
            {
                const double maxExclusive = 1.0000000004656613;
                return Mathf.Clamp01((float)(random.NextDouble() * maxExclusive));
            }

            public float ValueBiasLower(float biasStrength)
            {
                float t = Value();
                if (biasStrength >= 1f)
                {
                    return 0f;
                }

                float k = Mathf.Clamp01(1f - biasStrength);
                k = k * k * k - 1f;
                return Mathf.Clamp01((t + t * k) / (t * k + 1f));
            }

            public int Range(int minInclusive, int maxExclusive)
            {
                return random.Next(minInclusive, maxExclusive);
            }

            public Vector3 OnUnitSphere()
            {
                float z = Value() * 2f - 1f;
                float angle = Value() * Mathf.PI * 2f;
                float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                return new Vector3(
                    radius * Mathf.Cos(angle),
                    z,
                    radius * Mathf.Sin(angle));
            }
        }
    }
}
