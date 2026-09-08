using System;
using System.Collections.Generic;
using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Simulation.Planetary
{
    [CreateAssetMenu(menuName = "Farion/Simulation/Planetary/Planet Archetype", fileName = "SO_PlanetArchetype")]
    public sealed class PlanetArchetype : ScriptableObject
    {
        [SerializeField] PlanetType planetType = PlanetType.Rocky;

        [Header("Templates")]
        [Tooltip("Every template is cloned per planet and never mutated; the seed perturbs the clone.")]
        [SerializeField] CelestialShapeProfile shapeProfile;
        [SerializeField] PlanetClimateProfile climateProfile;
        [SerializeField] PlanetHydrosphereProfile hydrosphereProfile;
        [SerializeField] PlanetSurfaceStateProfile surfaceStateProfile;
        [SerializeField] TerrainFeatureDistributionProfile terrainFeatureDistribution;

        [Header("Libraries")]
        [Tooltip("Every biome this archetype may host. The seed picks a subset and shifts its climate bands.")]
        [SerializeField] BiomeDistributionProfile biomeLibrary;
        [Tooltip("Every surface material this archetype may show. Rules gated to biomes the seed did not pick are dropped.")]
        [SerializeField] SurfaceMaterialDistributionProfile surfaceMaterialLibrary;

        [Header("Environment")]
        [SerializeField] Vector2 atmosphereDensityRange = new(1f, 1f);
        [SerializeField] Vector2 backgroundRadiationRange = new(0.05f, 0.1f);
        [Tooltip("Atmosphere shell thickness as a fraction of the atmosphere base radius.")]
        [Min(0f)]
        [SerializeField] float atmosphereScale = 0.322f;
        [Min(0f)]
        [SerializeField] float atmosphereRadiusOffset;

        [Header("Seed Variation")]
        [Tooltip("Celsius added to the thermal design bias of the climate template.")]
        [SerializeField] Vector2 temperatureBiasRange;
        [Tooltip("Added to the baseline precipitation of the climate template.")]
        [SerializeField] Vector2 precipitationBiasRange;
        [SerializeField] Vector2 oceanLevelRange = new(0.5f, 0.5f);
        [SerializeField] Vector2 waterAvailabilityRange = new(0.35f, 0.35f);
        [SerializeField] Vector2 volcanicActivityRange = new(0.06f, 0.06f);
        [Tooltip("Multiplier on the shape template relief.")]
        [SerializeField] Vector2 elevationScaleRange = new(1f, 1f);
        [Tooltip("How many biomes from the library a single planet hosts, in addition to the fallback biome.")]
        [SerializeField] Vector2Int biomeCountRange = new(3, 4);
        [Tooltip("Maximum Celsius a picked biome band is shifted up or down.")]
        [Min(0f)]
        [SerializeField] float biomeTemperatureShiftCelsius = 6f;
        [Tooltip("Maximum aridity a picked biome band is shifted up or down.")]
        [Range(0f, 0.5f)]
        [SerializeField] float biomeAridityShift = 0.1f;

        public event Action Changed;

        public PlanetType PlanetType => planetType;
        public CelestialShapeProfile ShapeTemplate => shapeProfile;
        public BiomeDistributionProfile BiomeLibrary => biomeLibrary;
        public SurfaceMaterialDistributionProfile SurfaceMaterialLibrary => surfaceMaterialLibrary;
        public Vector2Int BiomeCountRange => biomeCountRange;

        void OnValidate()
        {
            atmosphereScale = Mathf.Max(0f, atmosphereScale);
            atmosphereRadiusOffset = Mathf.Max(0f, atmosphereRadiusOffset);
            biomeCountRange = new Vector2Int(
                Mathf.Max(0, biomeCountRange.x),
                Mathf.Max(biomeCountRange.x, biomeCountRange.y));
            Changed?.Invoke();
        }

        public PlanetaryGenerationProfile Derive(int seed, float radius, float surfaceGravity)
        {
            float atmosphereDensity = Mathf.Max(0f, SeedUtility.Range(seed, "archetype.atmosphere", atmosphereDensityRange));
            float radiation = SeedUtility.Range(seed, "archetype.radiation", backgroundRadiationRange);
            PlanetGenerationContext context = new(seed, radius, surfaceGravity, planetType, atmosphereDensity, radiation);
            List<ScriptableObject> owned = new();

            CelestialShapeProfile shape = null;
            if (shapeProfile != null)
            {
                shape = shapeProfile.CreateVariant(
                    SeedUtility.Derive(seed, "archetype.shape"),
                    SeedUtility.Range(seed, "archetype.elevation", elevationScaleRange));
                if (shape != shapeProfile)
                {
                    owned.Add(shape);
                }
            }

            PlanetClimateProfile climate = null;
            if (climateProfile != null)
            {
                PlanetThermalProfile thermal = climateProfile.ThermalProfile != null
                    ? Own(climateProfile.ThermalProfile.CreateVariant(
                        SeedUtility.Range(seed, "archetype.temperature", temperatureBiasRange)), owned)
                    : null;
                climate = Own(climateProfile.CreateVariant(
                    thermal,
                    SeedUtility.Range(seed, "archetype.precipitation", precipitationBiasRange)), owned);
            }

            PlanetHydrosphereProfile hydrosphere = hydrosphereProfile != null
                ? Own(hydrosphereProfile.CreateVariant(
                    SeedUtility.Range(seed, "archetype.ocean_level", oceanLevelRange),
                    SeedUtility.Range(seed, "archetype.water", waterAvailabilityRange)), owned)
                : null;
            PlanetSurfaceStateProfile surfaceState = surfaceStateProfile != null
                ? Own(surfaceStateProfile.CreateVariant(
                    SeedUtility.Range(seed, "archetype.volcanism", volcanicActivityRange)), owned)
                : null;

            List<BiomeDefinition> hostedBiomes = new();
            BiomeDistributionProfile biomes = biomeLibrary != null
                ? Own(DeriveBiomes(seed, context, hostedBiomes), owned)
                : null;
            SurfaceMaterialDistributionProfile materials = surfaceMaterialLibrary != null
                ? Own(DeriveMaterials(hostedBiomes), owned)
                : null;
            if (shape != null && shape != shapeProfile && terrainFeatureDistribution != null)
            {
                shape.ApplyTerrainSculpts(terrainFeatureDistribution.BuildSculpts(seed, hostedBiomes));
            }

            return new PlanetaryGenerationProfile(
                name,
                seed,
                planetType,
                atmosphereDensity,
                radiation,
                atmosphereScale,
                atmosphereRadiusOffset,
                shape,
                climate,
                hydrosphere,
                biomes,
                materials,
                surfaceState,
                terrainFeatureDistribution,
                owned);
        }

        BiomeDistributionProfile DeriveBiomes(
            int seed,
            PlanetGenerationContext context,
            List<BiomeDefinition> hostedBiomes)
        {
            if (biomeLibrary.FallbackBiome != null)
            {
                hostedBiomes.Add(biomeLibrary.FallbackBiome);
            }

            IReadOnlyList<BiomeDistributionRule> library = biomeLibrary.Rules;
            List<(float key, int index)> candidates = new(library.Count);
            for (int i = 0; i < library.Count; i++)
            {
                BiomeDistributionRule rule = library[i];
                if (rule?.Biome == null || !rule.Biome.IsCompatibleWith(context))
                {
                    continue;
                }

                candidates.Add((SeedUtility.Unit01(seed, "archetype.biome.pick." + i), i));
            }

            candidates.Sort((a, b) => a.key.CompareTo(b.key));
            int count = Mathf.Min(
                SeedUtility.RangeInt(seed, "archetype.biome.count", biomeCountRange),
                candidates.Count);
            List<int> picked = new(count);
            for (int i = 0; i < count; i++)
            {
                picked.Add(candidates[i].index);
            }

            picked.Sort();
            List<BiomeDistributionRule> rules = new(count);
            for (int i = 0; i < picked.Count; i++)
            {
                int index = picked[i];
                BiomeDistributionRule rule = library[index];
                float temperatureShift = (SeedUtility.Unit01(seed, "archetype.biome.temperature." + index) * 2f - 1f)
                    * biomeTemperatureShiftCelsius;
                float aridityShift = (SeedUtility.Unit01(seed, "archetype.biome.aridity." + index) * 2f - 1f)
                    * biomeAridityShift;
                rules.Add(rule.CreateShifted(temperatureShift, aridityShift));
                if (!hostedBiomes.Contains(rule.Biome))
                {
                    hostedBiomes.Add(rule.Biome);
                }
            }

            return biomeLibrary.CreateVariant(rules, SeedUtility.Derive(seed, "archetype.biome.warp"));
        }

        SurfaceMaterialDistributionProfile DeriveMaterials(IReadOnlyList<BiomeDefinition> hostedBiomes)
        {
            IReadOnlyList<SurfaceMaterialDistributionRule> library = surfaceMaterialLibrary.Rules;
            List<SurfaceMaterialDistributionRule> rules = new(library.Count);
            for (int i = 0; i < library.Count; i++)
            {
                SurfaceMaterialDistributionRule rule = library[i];
                if (rule != null && rule.AllowsAnyOf(hostedBiomes))
                {
                    rules.Add(rule);
                }
            }

            return surfaceMaterialLibrary.CreateVariant(rules);
        }

        static T Own<T>(T variant, List<ScriptableObject> owned) where T : ScriptableObject
        {
            owned.Add(variant);
            return variant;
        }
    }
}
