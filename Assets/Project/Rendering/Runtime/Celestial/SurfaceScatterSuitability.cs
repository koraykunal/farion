using System;
using System.Collections.Generic;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [Serializable]
    public sealed class SurfaceScatterSuitability
    {
        [Header("Surface Compatibility")]
        [SerializeField] List<BiomeDefinition> allowedBiomes = new();
        [SerializeField] List<SurfaceMaterialDefinition> allowedSurfaceMaterials = new();
        [SerializeField] List<TerrainFeatureDefinition> allowedTerrainFeatures = new();
        [SerializeField] bool requireDryLand = true;
        [SerializeField] bool limitHeightAboveOcean;
        [SerializeField] Vector2 heightAboveOceanRange = new(0.25f, 1000000f);
        [SerializeField, Min(0f)] float heightAboveOceanFeather = 1f;

        [Header("Environmental Suitability")]
        [SerializeField] Vector2 altitudeRange = new(-1000000f, 1000000f);
        [SerializeField, Min(0f)] float altitudeFeather = 4f;
        [SerializeField] Vector2 slopeRange = new(0f, 90f);
        [SerializeField, Min(0f)] float slopeFeather = 4f;
        [SerializeField] Vector2 temperatureRange = new(-273.15f, 1000f);
        [SerializeField, Min(0f)] float temperatureFeather = 4f;
        [SerializeField] Vector2 moistureRange = new(0f, 1f);
        [SerializeField, Range(0f, 0.5f)] float moistureFeather = 0.08f;
        [SerializeField] Vector2 radiationRange = new(0f, 1f);
        [SerializeField, Range(0f, 0.5f)] float radiationFeather = 0.05f;
        [SerializeField] Vector2 snowRange = new(0f, 1f);
        [SerializeField, Range(0f, 0.5f)] float snowFeather = 0.05f;
        [SerializeField] Vector2 wetnessRange = new(0f, 1f);
        [SerializeField, Range(0f, 0.5f)] float wetnessFeather = 0.08f;
        [SerializeField] Vector2 volcanismRange = new(0f, 1f);
        [SerializeField, Range(0f, 0.5f)] float volcanismFeather = 0.05f;

        public bool RequireDryLand => requireDryLand;
        public Vector2 SlopeRange => slopeRange;

        public bool AllowsBiome(BiomeDefinition biome)
        {
            return allowedBiomes.Count == 0 || allowedBiomes.Contains(biome);
        }

        public float EvaluateCoarse(in PlanetSurfaceSample sample, bool hasOcean, float oceanRadius)
        {
            float heightAboveOcean = hasOcean
                ? sample.SurfaceRadius - oceanRadius
                : float.PositiveInfinity;
            if (requireDryLand && hasOcean && heightAboveOcean < 0.25f)
            {
                return 0f;
            }

            float ecologicalAltitude = hasOcean ? heightAboveOcean : sample.TerrainAltitude;
            float suitability = PlanetarySampling.EvaluateRange(
                altitudeRange,
                ecologicalAltitude,
                altitudeFeather);
            if (suitability <= 0f)
            {
                return 0f;
            }

            suitability *= PlanetarySampling.EvaluateRange(
                slopeRange,
                sample.Surface.SlopeAngleDegrees,
                slopeFeather);
            if (suitability <= 0f || !limitHeightAboveOcean)
            {
                return suitability;
            }

            if (!hasOcean)
            {
                return 0f;
            }

            return suitability * PlanetarySampling.EvaluateRange(
                heightAboveOceanRange,
                heightAboveOcean,
                heightAboveOceanFeather);
        }

        public float Evaluate(
            in PlanetSurfaceSample sample,
            float allowedBiomeWeight,
            bool hasOcean,
            float oceanRadius)
        {
            if (allowedBiomes.Count > 0 && allowedBiomeWeight <= 0f)
            {
                return 0f;
            }

            if (allowedSurfaceMaterials.Count > 0 &&
                !allowedSurfaceMaterials.Contains(sample.SurfaceMaterial.Material))
            {
                return 0f;
            }

            if (allowedTerrainFeatures.Count > 0 &&
                !allowedTerrainFeatures.Contains(sample.TerrainFeature.Feature))
            {
                return 0f;
            }

            float suitability = EvaluateCoarse(sample, hasOcean, oceanRadius);
            if (suitability <= 0f)
            {
                return 0f;
            }

            if (allowedBiomes.Count > 0)
            {
                suitability *= Mathf.Clamp01(allowedBiomeWeight);
            }

            suitability *= PlanetarySampling.EvaluateRange(
                temperatureRange,
                sample.Climate.TemperatureCelsius,
                temperatureFeather);
            suitability *= PlanetarySampling.EvaluateRange(
                moistureRange,
                sample.Climate.EffectiveMoisture,
                moistureFeather);
            suitability *= PlanetarySampling.EvaluateRange(
                radiationRange,
                sample.Climate.Radiation,
                radiationFeather);
            suitability *= PlanetarySampling.EvaluateRange(
                snowRange,
                sample.SurfaceState.SnowCover,
                snowFeather);
            suitability *= PlanetarySampling.EvaluateRange(
                wetnessRange,
                sample.SurfaceState.Wetness,
                wetnessFeather);
            suitability *= PlanetarySampling.EvaluateRange(
                volcanismRange,
                sample.SurfaceState.VolcanicActivity,
                volcanismFeather);

            if (allowedSurfaceMaterials.Count > 0)
            {
                suitability *= Mathf.Clamp01(sample.SurfaceMaterial.Suitability);
            }

            if (allowedTerrainFeatures.Count > 0)
            {
                suitability *= Mathf.Clamp01(sample.TerrainFeature.FeatureNoise);
            }

            return Mathf.Clamp01(suitability);
        }

        public float ResolveAllowedBiomeWeight(
            PlanetSurfaceModel surfaceModel,
            in PlanetSurfaceSample sample,
            List<BiomeWeight> buffer)
        {
            if (surfaceModel.GenerationProfile == null ||
                surfaceModel.GenerationProfile.BiomeDistribution == null)
            {
                return AllowsBiome(sample.Biome.Biome) ? sample.Biome.Suitability : 0f;
            }

            buffer.Clear();
            surfaceModel.GenerationProfile.BiomeDistribution.SampleBiomeWeights(
                sample.Context,
                sample.Climate,
                sample.LocalDirection,
                sample.TerrainAltitude,
                sample.Surface.SlopeAngleDegrees,
                buffer);
            float total = 0f;
            for (int i = 0; i < buffer.Count; i++)
            {
                BiomeWeight weight = buffer[i];
                if (AllowsBiome(weight.Biome))
                {
                    total += weight.Weight;
                }
            }

            return Mathf.Clamp01(total);
        }

        internal void Validate()
        {
            allowedBiomes ??= new List<BiomeDefinition>();
            allowedSurfaceMaterials ??= new List<SurfaceMaterialDefinition>();
            allowedTerrainFeatures ??= new List<TerrainFeatureDefinition>();
            altitudeFeather = Mathf.Max(0f, altitudeFeather);
            slopeFeather = Mathf.Max(0f, slopeFeather);
            temperatureFeather = Mathf.Max(0f, temperatureFeather);
            moistureFeather = Mathf.Clamp(moistureFeather, 0f, 0.5f);
            radiationFeather = Mathf.Clamp(radiationFeather, 0f, 0.5f);
            snowFeather = Mathf.Clamp(snowFeather, 0f, 0.5f);
            wetnessFeather = Mathf.Clamp(wetnessFeather, 0f, 0.5f);
            volcanismFeather = Mathf.Clamp(volcanismFeather, 0f, 0.5f);
            heightAboveOceanFeather = Mathf.Max(0f, heightAboveOceanFeather);
        }
    }
}
