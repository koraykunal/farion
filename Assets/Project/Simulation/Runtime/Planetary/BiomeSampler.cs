using System.Collections.Generic;
using UnityEngine;

namespace Farion.Simulation.Planetary
{
    public static class BiomeSampler
    {
        public static BiomeSample Sample(
            BiomeDistributionProfile profile,
            PlanetGenerationContext context,
            Vector3 localDirection,
            float altitude,
            float slopeDegrees)
        {
            return Sample(profile, context, default, localDirection, altitude, slopeDegrees, useClimate: false);
        }

        public static BiomeSample Sample(
            BiomeDistributionProfile profile,
            PlanetGenerationContext context,
            PlanetClimateSample climate,
            Vector3 localDirection,
            float altitude,
            float slopeDegrees)
        {
            return Sample(profile, context, climate, localDirection, altitude, slopeDegrees, useClimate: true);
        }

        static BiomeSample Sample(
            BiomeDistributionProfile profile,
            PlanetGenerationContext context,
            PlanetClimateSample climate,
            Vector3 localDirection,
            float altitude,
            float slopeDegrees,
            bool useClimate)
        {
            if (profile == null)
            {
                return new BiomeSample(null, 0f, 0f, altitude, slopeDegrees);
            }

            Vector3 direction = localDirection.sqrMagnitude > 0.0001f ? localDirection.normalized : Vector3.up;
            int temperatureSeed = SeedUtility.Derive(context.PlanetSeed, profile.SeedSalt, "biome.temperature");
            int moistureSeed = SeedUtility.Derive(context.PlanetSeed, profile.SeedSalt, "biome.moisture");
            float temperatureNoise = SampleDirectionalNoise(direction, profile.TemperatureNoiseScale, temperatureSeed);
            float moistureNoise = SampleDirectionalNoise(direction, profile.MoistureNoiseScale, moistureSeed);

            float localTemperature = useClimate ? climate.TemperatureCelsius : temperatureNoise;
            float localMoisture = useClimate ? climate.Moisture : moistureNoise;
            float localRadiation = useClimate ? climate.Radiation : context.RadiationLevel;

            BiomeDefinition selected = profile.FallbackBiome != null &&
                (useClimate
                    ? profile.FallbackBiome.IsCompatibleWith(context, climate)
                    : profile.FallbackBiome.IsCompatibleWith(context))
                ? profile.FallbackBiome
                : null;
            float bestPriority = float.NegativeInfinity;
            foreach (BiomeDistributionRule rule in profile.Rules)
            {
                if (rule == null ||
                    rule.Biome == null ||
                    !(useClimate
                        ? rule.Biome.IsCompatibleWith(context, climate)
                        : rule.Biome.IsCompatibleWith(context)))
                {
                    continue;
                }

                float suitability = rule.EvaluateSuitability(
                    altitude,
                    slopeDegrees,
                    temperatureNoise,
                    moistureNoise,
                    localTemperature,
                    localMoisture,
                    localRadiation,
                    profile);
                if (suitability <= 0f)
                {
                    continue;
                }

                if (suitability > bestPriority)
                {
                    bestPriority = suitability;
                    selected = rule.Biome;
                }
            }

            return new BiomeSample(selected, temperatureNoise, moistureNoise, altitude, slopeDegrees);
        }

        public static int SampleWeights(
            BiomeDistributionProfile profile,
            PlanetGenerationContext context,
            PlanetClimateSample climate,
            Vector3 localDirection,
            float altitude,
            float slopeDegrees,
            List<BiomeWeight> results)
        {
            if (results == null)
            {
                return 0;
            }

            results.Clear();
            if (profile == null)
            {
                return 0;
            }

            Vector3 direction = localDirection.sqrMagnitude > 0.0001f ? localDirection.normalized : Vector3.up;
            int temperatureSeed = SeedUtility.Derive(context.PlanetSeed, profile.SeedSalt, "biome.temperature");
            int moistureSeed = SeedUtility.Derive(context.PlanetSeed, profile.SeedSalt, "biome.moisture");
            float temperatureNoise = SampleDirectionalNoise(direction, profile.TemperatureNoiseScale, temperatureSeed);
            float moistureNoise = SampleDirectionalNoise(direction, profile.MoistureNoiseScale, moistureSeed);

            float total = 0f;
            foreach (BiomeDistributionRule rule in profile.Rules)
            {
                if (rule == null ||
                    rule.Biome == null ||
                    !rule.Biome.IsCompatibleWith(context, climate))
                {
                    continue;
                }

                float suitability = rule.EvaluateSuitability(
                    altitude,
                    slopeDegrees,
                    temperatureNoise,
                    moistureNoise,
                    climate.TemperatureCelsius,
                    climate.Moisture,
                    climate.Radiation,
                    profile);
                if (suitability <= 0f)
                {
                    continue;
                }

                results.Add(new BiomeWeight(rule.Biome, suitability));
                total += suitability;
            }

            if (total <= 0f)
            {
                if (profile.FallbackBiome != null && profile.FallbackBiome.IsCompatibleWith(context, climate))
                {
                    results.Add(new BiomeWeight(profile.FallbackBiome, 1f));
                    return 1;
                }

                return 0;
            }

            for (int i = 0; i < results.Count; i++)
            {
                BiomeWeight weight = results[i];
                results[i] = new BiomeWeight(weight.Biome, weight.Weight / total);
            }

            return results.Count;
        }

        static float SampleDirectionalNoise(Vector3 direction, float scale, int seed)
        {
            float u = Mathf.Atan2(direction.z, direction.x) / (Mathf.PI * 2f) + 0.5f;
            float v = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) / Mathf.PI + 0.5f;
            float seedOffset = (seed & 2047) * 0.00731f;
            return Mathf.PerlinNoise(u * scale + seedOffset, v * scale + seedOffset * 1.731f);
        }
    }
}
