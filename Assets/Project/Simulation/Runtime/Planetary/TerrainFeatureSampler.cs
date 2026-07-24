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
            float featureNoise = SampleDirectionalNoise(direction, profile.FeatureNoiseScale, featureSeed);
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

        static float SampleDirectionalNoise(Vector3 direction, float scale, int seed)
        {
            float u = Mathf.Atan2(direction.z, direction.x) / (Mathf.PI * 2f) + 0.5f;
            float v = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) / Mathf.PI + 0.5f;
            float seedOffset = (seed & 2047) * 0.00731f;
            return Mathf.PerlinNoise(
                u * Mathf.Max(0.01f, scale) + seedOffset,
                v * Mathf.Max(0.01f, scale) + seedOffset * 1.731f);
        }
    }
}
