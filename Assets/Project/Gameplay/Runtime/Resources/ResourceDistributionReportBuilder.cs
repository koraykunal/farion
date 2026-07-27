using System.Collections.Generic;
using System.Text;
using Farion.Core.Physics;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Gameplay.Resources
{
    static class ResourceDistributionReportBuilder
    {
        public static bool TryBuild(
            string reportName,
            CelestialBody body,
            PlanetSurfaceModel surfaceModel,
            ResourceDistributionProfile resourceDistribution,
            out string report)
        {
            report = string.Empty;
            PlanetaryGenerationProfile generationProfile = surfaceModel != null
                ? surfaceModel.GenerationProfile
                : null;
            if (body == null || surfaceModel == null || generationProfile == null || resourceDistribution == null)
            {
                return false;
            }

            int sampleCount = resourceDistribution.CalculateSurfaceSampleCount(body.Radius);
            PlanetGenerationContext context = generationProfile.CreateContext(body.Radius, body.SurfaceGravity);
            List<ResourceSpawnRule> allowedRules = new();
            List<ResourceDepositData> reportDeposits = new();
            Dictionary<string, int> biomeCounts = new();
            Dictionary<string, int> featureCounts = new();
            Dictionary<string, int> allowedRuleCounts = new();
            Dictionary<string, int> depositCounts = new();

            int sampledSurfaceCount = 0;
            int missingSurfaceCount = 0;
            int missingBiomeCount = 0;
            int missingFeatureCount = 0;
            int samplesWithAllowedRules = 0;
            float minAltitude = float.PositiveInfinity;
            float maxAltitude = float.NegativeInfinity;
            float minSlope = float.PositiveInfinity;
            float maxSlope = float.NegativeInfinity;
            float minTemperature = float.PositiveInfinity;
            float maxTemperature = float.NegativeInfinity;
            float minMoisture = float.PositiveInfinity;
            float maxMoisture = float.NegativeInfinity;
            float minRadiation = float.PositiveInfinity;
            float maxRadiation = float.NegativeInfinity;

            int resourceSeed = SeedUtility.Derive(context.PlanetSeed, resourceDistribution.BaseSeed, "resources");
            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                Vector3 localDirection = FibonacciSphereDirection(sampleIndex, sampleCount);
                Vector3 worldDirection = body.transform.TransformDirection(localDirection.normalized);
                Vector3 probePosition = body.Position + worldDirection * Mathf.Max(0.01f, body.Radius);
                if (!surfaceModel.TrySamplePlanetSurface(body, probePosition, out PlanetSurfaceSample sample))
                {
                    missingSurfaceCount++;
                    continue;
                }

                sampledSurfaceCount++;
                minAltitude = Mathf.Min(minAltitude, sample.TerrainAltitude);
                maxAltitude = Mathf.Max(maxAltitude, sample.TerrainAltitude);
                minSlope = Mathf.Min(minSlope, sample.Surface.SlopeAngleDegrees);
                maxSlope = Mathf.Max(maxSlope, sample.Surface.SlopeAngleDegrees);
                minTemperature = Mathf.Min(minTemperature, sample.Climate.TemperatureCelsius);
                maxTemperature = Mathf.Max(maxTemperature, sample.Climate.TemperatureCelsius);
                minMoisture = Mathf.Min(minMoisture, sample.Climate.Moisture);
                maxMoisture = Mathf.Max(maxMoisture, sample.Climate.Moisture);
                minRadiation = Mathf.Min(minRadiation, sample.Climate.Radiation);
                maxRadiation = Mathf.Max(maxRadiation, sample.Climate.Radiation);

                BiomeDefinition biome = sample.Biome.Biome;
                if (biome == null)
                {
                    missingBiomeCount++;
                }
                else
                {
                    IncrementCount(biomeCounts, biome.DisplayName);
                }

                TerrainFeatureDefinition terrainFeature = sample.TerrainFeature.Feature;
                if (terrainFeature == null)
                {
                    missingFeatureCount++;
                }
                else
                {
                    IncrementCount(featureCounts, terrainFeature.DisplayName);
                }

                float resourceNoise = SampleDirectionalNoise(
                    localDirection,
                    resourceDistribution.ResourceNoiseScale,
                    SeedUtility.Derive(resourceSeed, sampleIndex, "resource.noise"));
                resourceDistribution.CollectAllowedRules(
                    biome,
                    terrainFeature,
                    sample.TerrainAltitude,
                    sample.Surface.SlopeAngleDegrees,
                    resourceNoise,
                    allowedRules);
                if (allowedRules.Count <= 0)
                {
                    continue;
                }

                samplesWithAllowedRules++;
                for (int i = 0; i < allowedRules.Count; i++)
                {
                    ResourceNodeDefinition resource = allowedRules[i].Resource;
                    IncrementCount(allowedRuleCounts, resource != null ? resource.DisplayName : "Missing Resource");
                }
            }

            ResourceDepositGenerator.Generate(resourceDistribution, generationProfile, body, surfaceModel, reportDeposits);
            for (int i = 0; i < reportDeposits.Count; i++)
            {
                ResourceDepositData deposit = reportDeposits[i];
                IncrementCount(depositCounts, deposit.Resource != null ? deposit.Resource.DisplayName : "Missing Resource");
            }

            StringBuilder builder = new(768);
            builder.Append(reportName);
            builder.Append(": resource distribution report. samples=");
            builder.Append(sampleCount);
            builder.Append(", sampledSurface=");
            builder.Append(sampledSurfaceCount);
            builder.Append(", missingSurface=");
            builder.Append(missingSurfaceCount);
            builder.Append(", missingBiome=");
            builder.Append(missingBiomeCount);
            builder.Append(", missingFeature=");
            builder.Append(missingFeatureCount);
            builder.Append(", samplesWithAllowedRules=");
            builder.Append(samplesWithAllowedRules);
            builder.Append(", generatedDeposits=");
            builder.Append(reportDeposits.Count);
            builder.Append(", altitude=");
            AppendRange(builder, minAltitude, maxAltitude);
            builder.Append(", slope=");
            AppendRange(builder, minSlope, maxSlope);
            builder.Append(", temperatureC=");
            AppendRange(builder, minTemperature, maxTemperature);
            builder.Append(", moisture=");
            AppendRange(builder, minMoisture, maxMoisture);
            builder.Append(", radiation=");
            AppendRange(builder, minRadiation, maxRadiation);
            builder.Append(", biomes=[");
            AppendCounts(builder, biomeCounts);
            builder.Append("], terrainFeatures=[");
            AppendCounts(builder, featureCounts);
            builder.Append("], allowedRules=[");
            AppendCounts(builder, allowedRuleCounts);
            builder.Append("], deposits=[");
            AppendCounts(builder, depositCounts);
            builder.Append(']');
            report = builder.ToString();
            return true;
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

        static float SampleDirectionalNoise(Vector3 direction, float scale, int seed)
        {
            float u = Mathf.Atan2(direction.z, direction.x) / (Mathf.PI * 2f) + 0.5f;
            float v = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) / Mathf.PI + 0.5f;
            float seedOffset = (seed & 2047) * 0.00731f;
            return Mathf.PerlinNoise(u * Mathf.Max(0.001f, scale) + seedOffset, v * Mathf.Max(0.001f, scale) + seedOffset * 1.731f);
        }

        static void IncrementCount(Dictionary<string, int> counts, string key)
        {
            if (counts == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            key = key.Trim();
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }

        static void AppendCounts(StringBuilder builder, Dictionary<string, int> counts)
        {
            if (counts == null || counts.Count == 0)
            {
                builder.Append("none");
                return;
            }

            bool first = true;
            foreach (KeyValuePair<string, int> pair in counts)
            {
                if (!first)
                {
                    builder.Append(", ");
                }

                builder.Append(pair.Key);
                builder.Append('=');
                builder.Append(pair.Value);
                first = false;
            }
        }

        static void AppendRange(StringBuilder builder, float min, float max)
        {
            if (float.IsInfinity(min) || float.IsInfinity(max))
            {
                builder.Append("n/a");
                return;
            }

            builder.Append(min.ToString("0.###"));
            builder.Append("..");
            builder.Append(max.ToString("0.###"));
        }
    }
}
