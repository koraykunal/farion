using System.Collections.Generic;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Gameplay.Resources
{
    [CreateAssetMenu(menuName = "Farion/Gameplay/Resources/Resource Distribution Profile", fileName = "SO_ResourceDistribution")]
    public sealed class ResourceDistributionProfile : ScriptableObject
    {
        [SerializeField] int baseSeed = 1001;
        [Min(0.01f)]
        [SerializeField] float surfaceSampleSpacing = 12f;
        [Min(1)]
        [SerializeField] int maxSurfaceSamples = 2048;
        [Min(0.001f)]
        [SerializeField] float resourceNoiseScale = 3.5f;
        [Min(0f)]
        [SerializeField] float clusterAngularJitterDegrees = 1.5f;
        [SerializeField] List<ResourceSpawnRule> rules = new();

        public int BaseSeed => baseSeed;
        public float SurfaceSampleSpacing => Mathf.Max(0.01f, surfaceSampleSpacing);
        public int MaxSurfaceSamples => Mathf.Max(1, maxSurfaceSamples);
        public float ResourceNoiseScale => Mathf.Max(0.001f, resourceNoiseScale);
        public float ClusterAngularJitterDegrees => Mathf.Max(0f, clusterAngularJitterDegrees);
        public IReadOnlyList<ResourceSpawnRule> Rules => rules;

        void OnValidate()
        {
            surfaceSampleSpacing = Mathf.Max(0.01f, surfaceSampleSpacing);
            maxSurfaceSamples = Mathf.Max(1, maxSurfaceSamples);
            resourceNoiseScale = Mathf.Max(0.001f, resourceNoiseScale);
            clusterAngularJitterDegrees = Mathf.Max(0f, clusterAngularJitterDegrees);
            rules ??= new List<ResourceSpawnRule>();
        }

        public int CalculateSurfaceSampleCount(float bodyRadius)
        {
            float radius = Mathf.Max(0.01f, bodyRadius);
            float area = 4f * Mathf.PI * radius * radius;
            float sampleArea = SurfaceSampleSpacing * SurfaceSampleSpacing;
            return Mathf.Clamp(Mathf.CeilToInt(area / sampleArea), 1, MaxSurfaceSamples);
        }

        public void CollectAllowedRules(
            BiomeDefinition biome,
            float altitude,
            float slopeDegrees,
            float resourceNoise,
            List<ResourceSpawnRule> results)
        {
            CollectAllowedRules(biome, null, altitude, slopeDegrees, resourceNoise, results);
        }

        public void CollectAllowedRules(
            BiomeDefinition biome,
            TerrainFeatureDefinition terrainFeature,
            float altitude,
            float slopeDegrees,
            float resourceNoise,
            List<ResourceSpawnRule> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            for (int i = 0; i < rules.Count; i++)
            {
                ResourceSpawnRule rule = rules[i];
                if (rule != null && rule.Allows(biome, terrainFeature, altitude, slopeDegrees, resourceNoise))
                {
                    results.Add(rule);
                }
            }
        }
    }
}
