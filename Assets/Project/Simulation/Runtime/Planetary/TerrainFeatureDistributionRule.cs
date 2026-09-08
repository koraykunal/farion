using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("localTemperatureRange")]
        [SerializeField] Vector2 temperatureRange = new(-1000000f, 1000000f);
        [FormerlySerializedAs("localMoistureRange")]
        [SerializeField] Vector2 effectiveMoistureRange = new(0f, 1f);
        [FormerlySerializedAs("localRadiationRange")]
        [SerializeField] Vector2 radiationRange = new(0f, 1f);
        [Range(0f, 1f)]
        [SerializeField] float minFeatureNoise;
        [Range(0f, 1f)]
        [SerializeField] float maxFeatureNoise = 1f;

        public TerrainFeatureDefinition Feature => feature;
        public float SelectionPriority => Mathf.Max(0f, selectionPriority);
        public Vector2 FeatureNoiseRange => new(minFeatureNoise, maxFeatureNoise);

        public bool AllowsAnyOf(IReadOnlyList<BiomeDefinition> biomes)
        {
            if (allowedBiomes == null || allowedBiomes.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < biomes.Count; i++)
            {
                if (allowedBiomes.Contains(biomes[i]))
                {
                    return true;
                }
            }

            return false;
        }

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
            score *= PlanetarySampling.EvaluateRange(altitudeRange, altitude, profile.AltitudeBlend);
            score *= PlanetarySampling.EvaluateRange(slopeRange, slopeDegrees, profile.SlopeBlendDegrees);
            score *= PlanetarySampling.EvaluateRange(
                temperatureRange,
                climate.TemperatureCelsius,
                profile.TemperatureBlendCelsius);
            score *= PlanetarySampling.EvaluateRange(
                effectiveMoistureRange,
                climate.EffectiveMoisture,
                profile.EffectiveMoistureBlend);
            score *= PlanetarySampling.EvaluateRange(
                radiationRange,
                climate.Radiation,
                profile.RadiationBlend);
            score *= PlanetarySampling.EvaluateRange(
                new Vector2(minFeatureNoise, maxFeatureNoise),
                featureNoise,
                profile.NoiseBlend);
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
    }
}
