using System;
using System.Collections.Generic;
using UnityEngine;

namespace Farion.Simulation.Planetary
{
    [Serializable]
    public sealed class TerrainFeatureDistributionRule
    {
        [SerializeField] TerrainFeatureDefinition feature;
        [Min(0f)]
        [SerializeField] float selectionPriority = 1f;
        [SerializeField] List<BiomeDefinition> allowedBiomes = new();
        [SerializeField] Vector2 altitudeRange = new(-1000000f, 1000000f);
        [SerializeField] Vector2 slopeRange = new(0f, 90f);
        [SerializeField] Vector2 localTemperatureRange = new(-1000000f, 1000000f);
        [SerializeField] Vector2 localMoistureRange = new(0f, 1f);
        [SerializeField] Vector2 localRadiationRange = new(0f, 1f);
        [Range(0f, 1f)]
        [SerializeField] float minFeatureNoise;
        [Range(0f, 1f)]
        [SerializeField] float maxFeatureNoise = 1f;

        public TerrainFeatureDefinition Feature => feature;
        public float SelectionPriority => Mathf.Max(0f, selectionPriority);

        public float EvaluateSuitability(
            BiomeDefinition biome,
            float altitude,
            float slopeDegrees,
            PlanetClimateSample climate,
            float featureNoise,
            TerrainFeatureDistributionProfile profile)
        {
            if (feature == null || SelectionPriority <= 0f || !AllowsBiome(biome))
            {
                return 0f;
            }

            float score = SelectionPriority;
            score *= EvaluateRange(altitudeRange, altitude, profile != null ? profile.AltitudeBlend : 0f, -1000000f, 1000000f);
            score *= EvaluateRange(slopeRange, slopeDegrees, profile != null ? profile.SlopeBlendDegrees : 0f, 0f, 90f);
            score *= EvaluateRange(localTemperatureRange, climate.TemperatureCelsius, profile != null ? profile.LocalTemperatureBlendCelsius : 0f, -1000000f, 1000000f);
            score *= EvaluateRange(localMoistureRange, climate.Moisture, profile != null ? profile.LocalMoistureBlend : 0f, 0f, 1f);
            score *= EvaluateRange(localRadiationRange, climate.Radiation, profile != null ? profile.LocalRadiationBlend : 0f, 0f, 1f);
            score *= EvaluateRange(new Vector2(minFeatureNoise, maxFeatureNoise), featureNoise, profile != null ? profile.NoiseBlend : 0f, 0f, 1f);
            return Mathf.Max(0f, score);
        }

        public bool HasMissingFeature()
        {
            return feature == null && SelectionPriority > 0f;
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

        static float EvaluateRange(Vector2 range, float value, float blend, float defaultMin, float defaultMax)
        {
            bool unconfiguredSerializedRange = Mathf.Approximately(range.x, 0f) && Mathf.Approximately(range.y, 0f);
            float min = unconfiguredSerializedRange ? defaultMin : range.x;
            float max = unconfiguredSerializedRange ? defaultMax : range.y;
            if (max < min)
            {
                max = min;
            }

            if (value < min - blend || value > max + blend)
            {
                return 0f;
            }

            if (blend <= 0f)
            {
                return value >= min && value <= max ? 1f : 0f;
            }

            float lower = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(min - blend, min + blend, value));
            float upper = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(max - blend, max + blend, value));
            return Mathf.Clamp01(Mathf.Min(lower, upper));
        }
    }
}
