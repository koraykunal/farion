using System;
using System.Collections.Generic;
using Farion.Core.Physics;
using Farion.Core.Persistence;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Resources;
using Farion.Simulation.World;
using UnityEngine;

namespace Farion.Gameplay.Persistence
{
    [Serializable]
    public sealed class GameplaySaveData
    {
        public const int CurrentSchemaVersion = SaveGameSchema.CurrentVersion;

        [SerializeField] int schemaVersion = CurrentSchemaVersion;
        [SerializeField] string savedAtUtc;
        [SerializeField] List<CelestialBodySnapshot> celestialBodies = new();
        [SerializeField] WorldOriginSnapshot worldOrigin;
        [SerializeField] PlayerInventorySnapshot playerInventory;
        [SerializeField] PlayerPossessionSnapshot playerPossession;
        [SerializeField] List<ResourceDepositDeltaSnapshot> resourceDepositDeltas = new();

        public GameplaySaveData(
            string savedAtUtc,
            IReadOnlyList<CelestialBodySnapshot> celestialBodies,
            WorldOriginSnapshot worldOrigin,
            PlayerInventorySnapshot playerInventory,
            PlayerPossessionSnapshot playerPossession,
            IReadOnlyList<ResourceDepositDeltaSnapshot> resourceDepositDeltas)
        {
            schemaVersion = CurrentSchemaVersion;
            this.savedAtUtc = string.IsNullOrWhiteSpace(savedAtUtc) ? string.Empty : savedAtUtc.Trim();
            this.celestialBodies = celestialBodies != null
                ? new List<CelestialBodySnapshot>(celestialBodies)
                : new List<CelestialBodySnapshot>();
            this.worldOrigin = worldOrigin;
            this.playerInventory = playerInventory;
            this.playerPossession = playerPossession;
            this.resourceDepositDeltas = resourceDepositDeltas != null
                ? new List<ResourceDepositDeltaSnapshot>(resourceDepositDeltas)
                : new List<ResourceDepositDeltaSnapshot>();
        }

        public int SchemaVersion => schemaVersion;
        public string SavedAtUtc => string.IsNullOrWhiteSpace(savedAtUtc) ? string.Empty : savedAtUtc.Trim();
        public IReadOnlyList<CelestialBodySnapshot> CelestialBodies => celestialBodies;
        public WorldOriginSnapshot WorldOrigin => worldOrigin;
        public PlayerInventorySnapshot PlayerInventory => playerInventory;
        public PlayerPossessionSnapshot PlayerPossession => playerPossession;
        public IReadOnlyList<ResourceDepositDeltaSnapshot> ResourceDepositDeltas => resourceDepositDeltas;
        public bool IsSupported => SaveGameSchema.IsSupportedVersion(schemaVersion);
    }
}
