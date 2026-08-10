using System.Collections.Generic;
using UnityEngine;

namespace Farion.Simulation.Planetary
{
    public static class BiomeSampler
    {
        public static BiomeSample Sample(
            BiomeDistributionProfile profile,
            PlanetGenerationContext context,
            PlanetClimateSample climate,
            Vector3 localDirection,
            float altitude,
            float slopeDegrees)
        {
            if (profile == null)
            {
                return new BiomeSample(null, 0f);
            }

            Vector2 climateWarp = profile.EvaluateClimateWarp(localDirection);
            BiomeDefinition selected = profile.FallbackBiome != null
                && profile.FallbackBiome.IsCompatibleWith(context)
                ? profile.FallbackBiome
                : null;
            float bestSuitability = selected != null ? 0f : float.NegativeInfinity;
            for (int i = 0; i < profile.Rules.Count; i++)
            {
                BiomeDistributionRule rule = profile.Rules[i];
                if (rule == null ||
                    rule.Biome == null ||
                    !rule.Biome.IsCompatibleWith(context))
                {
                    continue;
                }

                float suitability = rule.EvaluateSuitability(
                    altitude,
                    slopeDegrees,
                    climate,
                    profile,
                    climateWarp);
                if (suitability > bestSuitability)
                {
                    bestSuitability = suitability;
                    selected = rule.Biome;
                }
            }

            return new BiomeSample(selected, Mathf.Max(0f, bestSuitability));
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

            Vector2 climateWarp = profile.EvaluateClimateWarp(localDirection);
            float total = 0f;
            for (int i = 0; i < profile.Rules.Count; i++)
            {
                BiomeDistributionRule rule = profile.Rules[i];
                if (rule == null ||
                    rule.Biome == null ||
                    !rule.Biome.IsCompatibleWith(context))
                {
                    continue;
                }

                float suitability = rule.EvaluateSuitability(
                    altitude,
                    slopeDegrees,
                    climate,
                    profile,
                    climateWarp);
                if (suitability <= 0f)
                {
                    continue;
                }

                results.Add(new BiomeWeight(rule.Biome, suitability));
                total += suitability;
            }

            if (total <= 0f)
            {
                if (profile.FallbackBiome != null && profile.FallbackBiome.IsCompatibleWith(context))
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
    }
}
