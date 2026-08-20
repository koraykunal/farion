using Farion.Core.Identity;
using Farion.Simulation.Planetary;
using Farion.Simulation.World;
using UnityEngine;

namespace Farion.Gameplay.ResourceNodes
{
    public readonly struct ResourceDepositData
    {
        public ResourceDepositData(
            GeneratedEntityId depositId,
            GeneratedEntityId legacyDepositId,
            ResourceNodeDefinition resource,
            BiomeDefinition biome,
            TerrainFeatureDefinition terrainFeature,
            Vector3 localDirection,
            float altitude,
            float slopeDegrees,
            int initialReserve,
            int generationSeed)
        {
            DepositId = depositId;
            LegacyDepositId = legacyDepositId;
            Resource = resource;
            Biome = biome;
            TerrainFeature = terrainFeature;
            LocalDirection = localDirection.sqrMagnitude > 0.0001f ? localDirection.normalized : Vector3.up;
            Altitude = altitude;
            SlopeDegrees = Mathf.Max(0f, slopeDegrees);
            InitialReserve = Mathf.Max(0, initialReserve);
            GenerationSeed = generationSeed;
        }

        public GeneratedEntityId DepositId { get; }
        public GeneratedEntityId LegacyDepositId { get; }
        public ResourceNodeDefinition Resource { get; }
        public BiomeDefinition Biome { get; }
        public TerrainFeatureDefinition TerrainFeature { get; }
        public Vector3 LocalDirection { get; }
        public float Altitude { get; }
        public float SlopeDegrees { get; }
        public int InitialReserve { get; }
        public int GenerationSeed { get; }
        public bool IsValid => DepositId.IsValid && Resource != null && InitialReserve > 0;
    }
}
