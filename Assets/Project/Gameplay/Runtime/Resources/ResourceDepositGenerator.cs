using System.Collections.Generic;
using Farion.Simulation.Physics;
using Farion.Simulation.Planetary;
using Farion.Simulation.World.Generation;
using Farion.Simulation.World.Identity;
using UnityEngine;

namespace Farion.Gameplay.Resources
{
    public static class ResourceDepositGenerator
    {
        public static void Generate(
            ResourceDistributionProfile profile,
            PlanetaryGenerationProfile generationProfile,
            CelestialBody body,
            PlanetSurfaceModel surfaceModel,
            List<ResourceDepositData> results)
        {
            if (generationProfile == null)
            {
                results?.Clear();
                return;
            }

            Generate(
                profile,
                generationProfile.CreateContext(body != null ? body.Radius : 0f, body != null ? body.SurfaceGravity : 0f),
                generationProfile.BiomeDistribution,
                body,
                surfaceModel,
                results);
        }

        public static void Generate(
            ResourceDistributionProfile profile,
            PlanetGenerationContext context,
            BiomeDistributionProfile biomeDistribution,
            CelestialBody body,
            PlanetSurfaceModel surfaceModel,
            List<ResourceDepositData> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            if (profile == null || biomeDistribution == null || body == null || surfaceModel == null || profile.Rules.Count == 0)
            {
                return;
            }

            if (string.IsNullOrEmpty(body.PersistentId))
            {
                Debug.LogError(
                    $"{nameof(ResourceDepositGenerator)} requires a persistent id on celestial body {body.name}.",
                    body);
                return;
            }

            int sampleCount = profile.CalculateSurfaceSampleCount(body.Radius);
            int resourceSeed = SeedUtility.Derive(context.PlanetSeed, profile.BaseSeed, "resources");
            ulong bodyIdentitySeed = StableHashUtility.Combine((ulong)(uint)context.PlanetSeed, body.PersistentId);
            GeneratedEntityId bodyId = SeedDerivationUtility.DeriveId(
                bodyIdentitySeed,
                GenerationVersion.Current,
                UniverseEntityKind.CelestialBody,
                body.PersistentId);
            List<ResourceSpawnRule> allowedRules = new();
            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                Vector3 localDirection = FibonacciSphereDirection(sampleIndex, sampleCount);
                if (!TrySamplePlanetSurface(body, surfaceModel, localDirection, out PlanetSurfaceSample planetSample))
                {
                    continue;
                }

                float altitude = planetSample.TerrainAltitude;
                float slopeDegrees = planetSample.Surface.SlopeAngleDegrees;
                BiomeDefinition biome = planetSample.Biome.Biome;
                TerrainFeatureDefinition terrainFeature = planetSample.TerrainFeature.Feature;
                float resourceNoise = SampleDirectionalNoise(
                    localDirection,
                    profile.ResourceNoiseScale,
                    SeedUtility.Derive(resourceSeed, sampleIndex, "resource.noise"));
                profile.CollectAllowedRules(biome, terrainFeature, altitude, slopeDegrees, resourceNoise, allowedRules);
                if (allowedRules.Count == 0)
                {
                    continue;
                }

                ResourceSpawnRule selectedRule = SelectWeightedRule(
                    allowedRules,
                    SeedUtility.Derive(resourceSeed, sampleIndex, "resource.rule"));
                if (selectedRule == null || selectedRule.Resource == null)
                {
                    continue;
                }

                int clusterSize = EvaluateClusterSize(
                    selectedRule,
                    SeedUtility.Derive(resourceSeed, sampleIndex, "resource.cluster"));
                for (int clusterIndex = 0; clusterIndex < clusterSize; clusterIndex++)
                {
                    Vector3 depositDirection = clusterIndex == 0
                        ? localDirection
                        : OffsetDirection(localDirection, profile.ClusterAngularJitterDegrees, resourceSeed, sampleIndex, clusterIndex);

                    if (!TrySamplePlanetSurface(body, surfaceModel, depositDirection, out PlanetSurfaceSample depositPlanetSample))
                    {
                        continue;
                    }

                    float depositAltitude = depositPlanetSample.TerrainAltitude;
                    float depositSlope = depositPlanetSample.Surface.SlopeAngleDegrees;
                    BiomeDefinition depositBiome = depositPlanetSample.Biome.Biome;
                    TerrainFeatureDefinition depositTerrainFeature = depositPlanetSample.TerrainFeature.Feature;
                    float depositNoise = SampleDirectionalNoise(
                        depositDirection,
                        profile.ResourceNoiseScale,
                        SeedUtility.Derive(resourceSeed, sampleIndex * 397 + clusterIndex, "resource.noise"));
                    if (!selectedRule.Allows(depositBiome, depositTerrainFeature, depositAltitude, depositSlope, depositNoise))
                    {
                        continue;
                    }

                    int depositSeed = SeedUtility.Derive(
                        resourceSeed,
                        sampleIndex * 397 + clusterIndex,
                        selectedRule.Resource.NodeId);
                    int initialReserve = selectedRule.Resource.EvaluateInitialReserve(depositSeed);
                    GeneratedEntityId legacyDepositId = SeedDerivationUtility.DeriveId(
                        (ulong)(uint)resourceSeed,
                        GenerationVersion.Current,
                        UniverseEntityKind.ResourceDeposit,
                        selectedRule.Resource.NodeId,
                        sampleIndex * 397L + clusterIndex);
                    GeneratedEntityId depositId = SeedDerivationUtility.DeriveChildId(
                        bodyId,
                        UniverseEntityKind.ResourceDeposit,
                        selectedRule.Resource.NodeId,
                        sampleIndex * 397L + clusterIndex);
                    results.Add(new ResourceDepositData(
                        depositId,
                        legacyDepositId,
                        selectedRule.Resource,
                        depositBiome,
                        depositTerrainFeature,
                        depositDirection,
                        depositAltitude,
                        depositSlope,
                        initialReserve,
                        depositSeed));
                }
            }
        }

        static bool TrySamplePlanetSurface(
            CelestialBody body,
            PlanetSurfaceModel surfaceModel,
            Vector3 localDirection,
            out PlanetSurfaceSample sample)
        {
            sample = default;
            if (body == null || surfaceModel == null)
            {
                return false;
            }

            Vector3 worldDirection = body.transform.TransformDirection(localDirection.normalized);
            Vector3 probePosition = body.Position + worldDirection * Mathf.Max(0.01f, body.Radius);
            return surfaceModel.TrySamplePlanetSurface(body, probePosition, out sample);
        }

        static Vector3 FibonacciSphereDirection(int index, int count)
        {
            if (count <= 1)
            {
                return Vector3.up;
            }

            float t = index / (float)(count - 1);
            float y = 1f - 2f * t;
            float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float theta = index * Mathf.PI * (3f - Mathf.Sqrt(5f));
            return new Vector3(Mathf.Cos(theta) * radius, y, Mathf.Sin(theta) * radius).normalized;
        }

        static ResourceSpawnRule SelectWeightedRule(IReadOnlyList<ResourceSpawnRule> rules, int seed)
        {
            float totalWeight = 0f;
            for (int i = 0; i < rules.Count; i++)
            {
                totalWeight += rules[i] != null ? rules[i].Weight : 0f;
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float target = Unit01(seed) * totalWeight;
            float cursor = 0f;
            for (int i = 0; i < rules.Count; i++)
            {
                ResourceSpawnRule rule = rules[i];
                if (rule == null || rule.Weight <= 0f)
                {
                    continue;
                }

                cursor += rule.Weight;
                if (target <= cursor)
                {
                    return rule;
                }
            }

            return rules[rules.Count - 1];
        }

        static int EvaluateClusterSize(ResourceSpawnRule rule, int seed)
        {
            int min = rule.MinClusterSize;
            int max = rule.MaxClusterSize;
            if (min >= max)
            {
                return min;
            }

            return min + Mathf.FloorToInt(Unit01(seed) * (max - min + 1));
        }

        static Vector3 OffsetDirection(
            Vector3 localDirection,
            float maxAngleDegrees,
            int resourceSeed,
            int sampleIndex,
            int clusterIndex)
        {
            Vector3 direction = localDirection.sqrMagnitude > 0.0001f ? localDirection.normalized : Vector3.up;
            Vector3 tangentA = Vector3.ProjectOnPlane(Vector3.forward, direction);
            if (tangentA.sqrMagnitude <= 0.0001f)
            {
                tangentA = Vector3.ProjectOnPlane(Vector3.right, direction);
            }

            tangentA.Normalize();
            Vector3 tangentB = Vector3.Cross(direction, tangentA).normalized;
            int jitterSeed = SeedUtility.Derive(resourceSeed, sampleIndex * 997 + clusterIndex, "resource.cluster.jitter");
            float angle = Unit01(jitterSeed) * Mathf.Max(0f, maxAngleDegrees) * Mathf.Deg2Rad;
            float rotation = Unit01(SeedUtility.Derive(jitterSeed, "resource.cluster.rotation")) * Mathf.PI * 2f;
            Vector3 offset = tangentA * Mathf.Cos(rotation) + tangentB * Mathf.Sin(rotation);
            return (direction * Mathf.Cos(angle) + offset * Mathf.Sin(angle)).normalized;
        }

        static float SampleDirectionalNoise(Vector3 direction, float scale, int seed)
        {
            float u = Mathf.Atan2(direction.z, direction.x) / (Mathf.PI * 2f) + 0.5f;
            float v = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) / Mathf.PI + 0.5f;
            float seedOffset = (seed & 2047) * 0.00731f;
            return Mathf.PerlinNoise(u * Mathf.Max(0.001f, scale) + seedOffset, v * Mathf.Max(0.001f, scale) + seedOffset * 1.731f);
        }

        static float Unit01(int seed)
        {
            return (seed & 0x00ffffff) / 16777216f;
        }
    }
}
