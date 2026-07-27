using System.Collections.Generic;
using System.Text;
using Farion.Core.Physics;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    static class CelestialBiomeVisualCoverageReportBuilder
    {
        const int SampleCount = 180;

        public static bool TryBuild(
            string reportName,
            PlanetSurfaceModel surfaceModel,
            CelestialSurfaceProfileBase surfaceProfile,
            out string report,
            out string skipReason)
        {
            report = string.Empty;
            skipReason = string.Empty;

            if (surfaceModel == null)
            {
                skipReason = "no planet surface model is assigned";
                return false;
            }

            if (surfaceProfile is not TerrestrialSurfaceProfile terrestrialSurface ||
                terrestrialSurface.BiomeVisualProfile == null)
            {
                skipReason = "no terrestrial biome visual profile is assigned";
                return false;
            }

            BiomeDistributionProfile distribution = surfaceModel.GenerationProfile != null
                ? surfaceModel.GenerationProfile.BiomeDistribution
                : null;
            if (distribution == null)
            {
                skipReason = "no biome distribution profile is assigned";
                return false;
            }

            CelestialBody sourceBody = surfaceModel.Body;
            if (sourceBody == null)
            {
                skipReason = "the surface model has no celestial body";
                return false;
            }

            BuildReport(reportName, surfaceModel, terrestrialSurface.BiomeVisualProfile, distribution, sourceBody, out report);
            return true;
        }

        static void BuildReport(
            string reportName,
            PlanetSurfaceModel surfaceModel,
            BiomeVisualProfile biomeVisualProfile,
            BiomeDistributionProfile distribution,
            CelestialBody sourceBody,
            out string report)
        {
            int sampled = 0;
            int missingSurface = 0;
            int missingDominantBiome = 0;
            int missingDistributionWeights = 0;
            int missingVisualWeights = 0;
            int fallbackDominant = 0;
            int fallbackWeighted = 0;
            float minTemperature = float.PositiveInfinity;
            float maxTemperature = float.NegativeInfinity;
            float minMoisture = float.PositiveInfinity;
            float maxMoisture = float.NegativeInfinity;
            float minRadiation = float.PositiveInfinity;
            float maxRadiation = float.NegativeInfinity;
            List<BiomeWeight> weights = new(BiomeVisualProfile.MaxBiomeSlots);
            Dictionary<string, int> dominantBiomeCounts = new();
            Dictionary<string, int> terrainFeatureCounts = new();

            for (int i = 0; i < SampleCount; i++)
            {
                Vector3 localDirection = EvaluateReportDirection(i, SampleCount);
                Vector3 worldDirection = sourceBody.transform.TransformDirection(localDirection);
                float probeRadius = surfaceModel.ShapeProfile != null
                    ? surfaceModel.ShapeProfile.EvaluateSample(sourceBody.Radius, localDirection).Radius
                    : sourceBody.Radius;
                Vector3 probePosition = sourceBody.Position + worldDirection * probeRadius;
                if (!surfaceModel.TrySamplePlanetSurface(sourceBody, probePosition, out PlanetSurfaceSample sample))
                {
                    missingSurface++;
                    continue;
                }

                sampled++;
                minTemperature = Mathf.Min(minTemperature, sample.Climate.TemperatureCelsius);
                maxTemperature = Mathf.Max(maxTemperature, sample.Climate.TemperatureCelsius);
                minMoisture = Mathf.Min(minMoisture, sample.Climate.Moisture);
                maxMoisture = Mathf.Max(maxMoisture, sample.Climate.Moisture);
                minRadiation = Mathf.Min(minRadiation, sample.Climate.Radiation);
                maxRadiation = Mathf.Max(maxRadiation, sample.Climate.Radiation);

                BiomeDefinition dominantBiome = sample.Biome.Biome;
                if (dominantBiome == null)
                {
                    missingDominantBiome++;
                }
                else
                {
                    IncrementReportCount(dominantBiomeCounts, dominantBiome.DisplayName);
                    if (dominantBiome == distribution.FallbackBiome)
                    {
                        fallbackDominant++;
                    }
                }

                if (sample.TerrainFeature.HasFeature)
                {
                    IncrementReportCount(terrainFeatureCounts, sample.TerrainFeature.Feature.DisplayName);
                }

                int weightCount = distribution.SampleBiomeWeights(
                    sample.Context,
                    sample.Climate,
                    sample.LocalDirection,
                    sample.TerrainAltitude,
                    sample.Surface.SlopeAngleDegrees,
                    weights);
                if (weightCount <= 0)
                {
                    missingDistributionWeights++;
                    missingVisualWeights++;
                    continue;
                }

                bool hasFallbackWeight = false;
                float visualWeightTotal = 0f;
                for (int weightIndex = 0; weightIndex < weightCount; weightIndex++)
                {
                    BiomeWeight weight = weights[weightIndex];
                    if (weight.Biome == distribution.FallbackBiome && weight.Weight > 0.001f)
                    {
                        hasFallbackWeight = true;
                    }

                    if (biomeVisualProfile.ResolveBiomeIndex(weight.Biome) >= 0)
                    {
                        visualWeightTotal += Mathf.Max(0f, weight.Weight);
                    }
                }

                if (hasFallbackWeight)
                {
                    fallbackWeighted++;
                }

                if (visualWeightTotal <= 0.001f)
                {
                    missingVisualWeights++;
                }
            }

            StringBuilder builder = new(512);
            builder.Append(reportName);
            builder.Append(": biome visual coverage report. samples=");
            builder.Append(SampleCount);
            builder.Append(", sampled=");
            builder.Append(sampled);
            builder.Append(", missingSurface=");
            builder.Append(missingSurface);
            builder.Append(", missingDominantBiome=");
            builder.Append(missingDominantBiome);
            builder.Append(", missingDistributionWeights=");
            builder.Append(missingDistributionWeights);
            builder.Append(", missingVisualWeights=");
            builder.Append(missingVisualWeights);
            builder.Append(", fallbackDominant=");
            builder.Append(fallbackDominant);
            builder.Append(", fallbackWeighted=");
            builder.Append(fallbackWeighted);
            builder.Append(", temperatureC=");
            AppendReportRange(builder, minTemperature, maxTemperature);
            builder.Append(", moisture=");
            AppendReportRange(builder, minMoisture, maxMoisture);
            builder.Append(", radiation=");
            AppendReportRange(builder, minRadiation, maxRadiation);
            builder.Append(", dominantBiomes=[");
            AppendReportCounts(builder, dominantBiomeCounts);
            builder.Append("], terrainFeatures=[");
            AppendReportCounts(builder, terrainFeatureCounts);
            builder.Append(']');
            report = builder.ToString();
        }

        static Vector3 EvaluateReportDirection(int index, int count)
        {
            float t = (index + 0.5f) / Mathf.Max(1, count);
            float y = 1f - 2f * t;
            float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float theta = index * 2.3999632f;
            return new Vector3(Mathf.Cos(theta) * radius, y, Mathf.Sin(theta) * radius);
        }

        static void IncrementReportCount(Dictionary<string, int> counts, string key)
        {
            key = string.IsNullOrWhiteSpace(key) ? "<unnamed>" : key;
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }

        static void AppendReportRange(StringBuilder builder, float min, float max)
        {
            if (float.IsInfinity(min) || float.IsInfinity(max))
            {
                builder.Append("<none>");
                return;
            }

            builder.Append(min.ToString("0.###"));
            builder.Append("..");
            builder.Append(max.ToString("0.###"));
        }

        static void AppendReportCounts(StringBuilder builder, Dictionary<string, int> counts)
        {
            bool first = true;
            foreach (KeyValuePair<string, int> pair in counts)
            {
                if (!first)
                {
                    builder.Append(", ");
                }

                builder.Append(pair.Key);
                builder.Append(": ");
                builder.Append(pair.Value);
                first = false;
            }

            if (first)
            {
                builder.Append("<none>");
            }
        }
    }
}
