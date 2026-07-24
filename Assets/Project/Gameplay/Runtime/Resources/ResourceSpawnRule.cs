using System;
using System.Collections.Generic;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Gameplay.Resources
{
    [Serializable]
    public sealed class ResourceSpawnRule
    {
        [SerializeField] ResourceNodeDefinition resource;
        [Min(0f)]
        [SerializeField] float weight = 1f;
        [SerializeField] Vector2 altitudeRange = new(-1000000f, 1000000f);
        [SerializeField] Vector2 slopeRange = new(0f, 90f);
        [SerializeField] List<BiomeDefinition> allowedBiomes = new();
        [SerializeField] List<TerrainFeatureDefinition> allowedTerrainFeatures = new();
        [Range(0f, 1f)]
        [SerializeField] float noiseThreshold = 0.65f;
        [Min(1)]
        [SerializeField] int minClusterSize = 1;
        [Min(1)]
        [SerializeField] int maxClusterSize = 1;

        public ResourceNodeDefinition Resource => resource;
        public float Weight => Mathf.Max(0f, weight);
        public Vector2 AltitudeRange => altitudeRange;
        public Vector2 SlopeRange => slopeRange;
        public IReadOnlyList<BiomeDefinition> AllowedBiomes => allowedBiomes;
        public IReadOnlyList<TerrainFeatureDefinition> AllowedTerrainFeatures => allowedTerrainFeatures;
        public float NoiseThreshold => Mathf.Clamp01(noiseThreshold);
        public int MinClusterSize => Mathf.Max(1, minClusterSize);
        public int MaxClusterSize => Mathf.Max(MinClusterSize, maxClusterSize);

        public bool Allows(float altitude, float slopeDegrees, float noise)
        {
            return Allows(null, altitude, slopeDegrees, noise);
        }

        public bool Allows(BiomeDefinition biome, float altitude, float slopeDegrees, float noise)
        {
            return Allows(biome, null, altitude, slopeDegrees, noise);
        }

        public bool Allows(
            BiomeDefinition biome,
            TerrainFeatureDefinition terrainFeature,
            float altitude,
            float slopeDegrees,
            float noise)
        {
            if (resource == null || Weight <= 0f)
            {
                return false;
            }

            if (!AllowsBiome(biome))
            {
                return false;
            }

            if (!AllowsTerrainFeature(terrainFeature))
            {
                return false;
            }

            return altitude >= altitudeRange.x &&
                altitude <= altitudeRange.y &&
                slopeDegrees >= slopeRange.x &&
                slopeDegrees <= slopeRange.y &&
                noise >= NoiseThreshold;
        }

        bool AllowsBiome(BiomeDefinition biome)
        {
            if (allowedBiomes == null || allowedBiomes.Count == 0)
            {
                return true;
            }

            if (biome == null)
            {
                return false;
            }

            for (int i = 0; i < allowedBiomes.Count; i++)
            {
                if (allowedBiomes[i] == biome)
                {
                    return true;
                }
            }

            return false;
        }

        bool AllowsTerrainFeature(TerrainFeatureDefinition terrainFeature)
        {
            if (allowedTerrainFeatures == null || allowedTerrainFeatures.Count == 0)
            {
                return true;
            }

            if (terrainFeature == null)
            {
                return false;
            }

            for (int i = 0; i < allowedTerrainFeatures.Count; i++)
            {
                if (allowedTerrainFeatures[i] == terrainFeature)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
