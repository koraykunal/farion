using UnityEngine;

namespace Farion.Simulation.Planetary
{
    public static class TerrainFeatureSampler
    {
        public static TerrainFeatureSample Sample(
            TerrainFeatureDistributionProfile profile,
            PlanetGenerationContext context,
            PlanetClimateSample climate,
            BiomeDefinition biome,
            Vector3 localDirection,
            float altitude,
            float slopeDegrees)
        {
            if (profile == null)
            {
                return new TerrainFeatureSample(null, 0f, altitude, slopeDegrees);
            }

            Vector3 direction = localDirection.sqrMagnitude > 0.0001f ? localDirection.normalized : Vector3.up;
            int featureSeed = SeedUtility.Derive(context.PlanetSeed, profile.SeedSalt, "terrain.feature");
            float featureNoise = PlanetarySampling.SampleFractal01(
                direction,
                profile.FeatureNoiseScale,
                4,
                2f,
                0.5f,
                featureSeed);
            TerrainFeatureDefinition selected = null;
            float bestSuitability = float.NegativeInfinity;

            foreach (TerrainFeatureDistributionRule rule in profile.Rules)
            {
                if (rule == null)
                {
                    continue;
                }

                float suitability = rule.EvaluateSuitability(
                    biome,
                    altitude,
                    slopeDegrees,
                    climate,
                    featureNoise,
                    profile);
                if (suitability <= 0f)
                {
                    continue;
                }

                if (suitability > bestSuitability)
                {
                    bestSuitability = suitability;
                    selected = rule.Feature;
                }
            }

            return new TerrainFeatureSample(selected, featureNoise, altitude, slopeDegrees);
        }
    }
}
