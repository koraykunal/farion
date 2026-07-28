using System.Collections.Generic;
using System.Text;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    static class CelestialSurfaceCoverageReportBuilder
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

            if (surfaceProfile is not TerrestrialSurfaceProfile terrestrialSurface
                || terrestrialSurface.SurfaceVisualProfile == null)
            {
                skipReason = "no terrestrial surface visual profile is assigned";
                return false;
            }

            PlanetaryGenerationProfile generation = surfaceModel.GenerationProfile;
            if (generation == null
                || generation.BiomeDistribution == null
                || generation.SurfaceMaterialDistribution == null)
            {
                skipReason = "biome or surface-material distribution is missing";
                return false;
            }

            if (surfaceModel.Body == null)
            {
                skipReason = "the surface model has no celestial body";
                return false;
            }

            BuildReport(
                reportName,
                surfaceModel,
                terrestrialSurface.SurfaceVisualProfile,
                generation.BiomeDistribution,
                generation.SurfaceMaterialDistribution,
                out report);
            return true;
        }

        static void BuildReport(
            string reportName,
            PlanetSurfaceModel surfaceModel,
            SurfaceVisualProfile visualProfile,
            BiomeDistributionProfile biomeDistribution,
            SurfaceMaterialDistributionProfile materialDistribution,
            out string report)
        {
            int sampled = 0;
            int missingSurface = 0;
            int missingBiome = 0;
            int missingBiomeWeights = 0;
            int missingMaterialWeights = 0;
            int missingVisualMaterialWeights = 0;
            int fallbackBiome = 0;
            float minTemperature = float.PositiveInfinity;
            float maxTemperature = float.NegativeInfinity;
            float minPrecipitation = float.PositiveInfinity;
            float maxPrecipitation = float.NegativeInfinity;
            float minAridity = float.PositiveInfinity;
            float maxAridity = float.NegativeInfinity;
            float minRadiation = float.PositiveInfinity;
            float maxRadiation = float.NegativeInfinity;
            List<BiomeWeight> biomeWeights = new();
            List<SurfaceMaterialWeight> materialWeights = new(SurfaceVisualProfile.MaxSurfaceSlots);
            Dictionary<string, int> biomeCounts = new();
            Dictionary<string, int> materialCounts = new();
            Dictionary<string, int> featureCounts = new();

            for (int i = 0; i < SampleCount; i++)
            {
                Vector3 direction = EvaluateReportDirection(i, SampleCount);
                if (!surfaceModel.TrySamplePlanetSurface(direction, out PlanetSurfaceSample sample))
                {
                    missingSurface++;
                    continue;
                }

                sampled++;
                minTemperature = Mathf.Min(minTemperature, sample.Climate.TemperatureCelsius);
                maxTemperature = Mathf.Max(maxTemperature, sample.Climate.TemperatureCelsius);
                minPrecipitation = Mathf.Min(minPrecipitation, sample.Climate.Precipitation);
                maxPrecipitation = Mathf.Max(maxPrecipitation, sample.Climate.Precipitation);
                minAridity = Mathf.Min(minAridity, sample.Climate.Aridity);
                maxAridity = Mathf.Max(maxAridity, sample.Climate.Aridity);
                minRadiation = Mathf.Min(minRadiation, sample.Climate.Radiation);
                maxRadiation = Mathf.Max(maxRadiation, sample.Climate.Radiation);

                BiomeDefinition biome = sample.Biome.Biome;
                if (biome == null)
                {
                    missingBiome++;
                }
                else
                {
                    Increment(biomeCounts, biome.DisplayName);
                    if (biome == biomeDistribution.FallbackBiome)
                    {
                        fallbackBiome++;
                    }
                }

                SurfaceMaterialDefinition material = sample.SurfaceMaterial.Material;
                if (material != null)
                {
                    Increment(materialCounts, material.DisplayName);
                }

                if (sample.TerrainFeature.HasFeature)
                {
                    Increment(featureCounts, sample.TerrainFeature.Feature.DisplayName);
                }

                if (biomeDistribution.SampleBiomeWeights(
                        sample.Context,
                        sample.Climate,
                        sample.TerrainAltitude,
                        sample.Surface.SlopeAngleDegrees,
                        biomeWeights) <= 0)
                {
                    missingBiomeWeights++;
                }

                int materialWeightCount = materialDistribution.SampleWeights(
                    sample.Context,
                    sample.Climate,
                    biome,
                    sample.LocalDirection,
                    sample.TerrainAltitude,
                    sample.Surface.SlopeAngleDegrees,
                    materialWeights);
                if (materialWeightCount <= 0)
                {
                    missingMaterialWeights++;
                    missingVisualMaterialWeights++;
                    continue;
                }

                float visualWeight = 0f;
                for (int weightIndex = 0; weightIndex < materialWeightCount; weightIndex++)
                {
                    SurfaceMaterialWeight weight = materialWeights[weightIndex];
                    if (visualProfile.ResolveMaterialIndex(weight.Material) >= 0)
                    {
                        visualWeight += weight.Weight;
                    }
                }

                if (visualWeight <= 0.001f)
                {
                    missingVisualMaterialWeights++;
                }
            }

            StringBuilder builder = new(768);
            builder.Append(reportName);
            builder.Append(": planetary surface coverage. samples=");
            builder.Append(SampleCount);
            builder.Append(", sampled=");
            builder.Append(sampled);
            AppendMetric(builder, "missingSurface", missingSurface);
            AppendMetric(builder, "missingBiome", missingBiome);
            AppendMetric(builder, "missingBiomeWeights", missingBiomeWeights);
            AppendMetric(builder, "missingMaterialWeights", missingMaterialWeights);
            AppendMetric(builder, "missingVisualMaterialWeights", missingVisualMaterialWeights);
            AppendMetric(builder, "fallbackBiome", fallbackBiome);
            AppendRange(builder, "temperatureC", minTemperature, maxTemperature);
            AppendRange(builder, "precipitation", minPrecipitation, maxPrecipitation);
            AppendRange(builder, "aridity", minAridity, maxAridity);
            AppendRange(builder, "radiation", minRadiation, maxRadiation);
            AppendCounts(builder, "dominantBiomes", biomeCounts);
            AppendCounts(builder, "dominantMaterials", materialCounts);
            AppendCounts(builder, "terrainFeatures", featureCounts);
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

        static void Increment(Dictionary<string, int> counts, string key)
        {
            key = string.IsNullOrWhiteSpace(key) ? "<unnamed>" : key;
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }

        static void AppendMetric(StringBuilder builder, string label, int value)
        {
            builder.Append(", ");
            builder.Append(label);
            builder.Append('=');
            builder.Append(value);
        }

        static void AppendRange(StringBuilder builder, string label, float min, float max)
        {
            builder.Append(", ");
            builder.Append(label);
            builder.Append('=');
            if (float.IsInfinity(min) || float.IsInfinity(max))
            {
                builder.Append("<none>");
                return;
            }

            builder.Append(min.ToString("0.###"));
            builder.Append("..");
            builder.Append(max.ToString("0.###"));
        }

        static void AppendCounts(StringBuilder builder, string label, Dictionary<string, int> counts)
        {
            builder.Append(", ");
            builder.Append(label);
            builder.Append("=[");
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

            builder.Append(']');
        }
    }
}
