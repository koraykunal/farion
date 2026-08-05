using System;
using System.Collections.Generic;
using Farion.Simulation.Physics;
using Farion.Core.Persistence;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Resources;
using Farion.Simulation.World;
using UnityEngine;

namespace Farion.Gameplay.Persistence
{
    [Serializable]
    public sealed class GameplaySaveData
    {
        public const int CurrentSchemaVersion = SaveGameSchema.CurrentVersion;
        public const int CurrentPayloadRevision = 1;

        [SerializeField] int schemaVersion = CurrentSchemaVersion;
        [SerializeField] int payloadRevision;
        [SerializeField] string savedAtUtc;
        [SerializeField] List<CelestialBodySnapshot> celestialBodies = new();
        [SerializeField] WorldOriginSnapshot worldOrigin;
        [SerializeField] PlayerInventorySnapshot playerInventory;
        [SerializeField] InventoryContainerSnapshot personalShipCargo;
        [SerializeField] InventoryContainerSnapshot fleetStorage;
        [SerializeField] PlayerPossessionSnapshot playerPossession;
        [SerializeField] FleetKnowledgeSnapshot fleetKnowledge;
        [SerializeField] List<ResourceDepositDeltaSnapshot> resourceDepositDeltas = new();
        [NonSerialized] int sourceSchemaVersion;

        public GameplaySaveData(
            string savedAtUtc,
            IReadOnlyList<CelestialBodySnapshot> celestialBodies,
            WorldOriginSnapshot worldOrigin,
            PlayerInventorySnapshot playerInventory,
            InventoryContainerSnapshot shuttleCargo,
            InventoryContainerSnapshot fleetStorage,
            PlayerPossessionSnapshot playerPossession,
            FleetKnowledgeSnapshot fleetKnowledge,
            IReadOnlyList<ResourceDepositDeltaSnapshot> resourceDepositDeltas)
            : this(
                CurrentSchemaVersion,
                savedAtUtc,
                celestialBodies,
                worldOrigin,
                playerInventory,
                shuttleCargo,
                fleetStorage,
                playerPossession,
                fleetKnowledge,
                resourceDepositDeltas)
        {
        }

        GameplaySaveData(
            int sourceSchemaVersion,
            string savedAtUtc,
            IReadOnlyList<CelestialBodySnapshot> celestialBodies,
            WorldOriginSnapshot worldOrigin,
            PlayerInventorySnapshot playerInventory,
            InventoryContainerSnapshot shuttleCargo,
            InventoryContainerSnapshot fleetStorage,
            PlayerPossessionSnapshot playerPossession,
            FleetKnowledgeSnapshot fleetKnowledge,
            IReadOnlyList<ResourceDepositDeltaSnapshot> resourceDepositDeltas)
        {
            schemaVersion = CurrentSchemaVersion;
            payloadRevision = CurrentPayloadRevision;
            this.sourceSchemaVersion = sourceSchemaVersion;
            this.savedAtUtc = string.IsNullOrWhiteSpace(savedAtUtc) ? string.Empty : savedAtUtc.Trim();
            this.celestialBodies = celestialBodies != null
                ? new List<CelestialBodySnapshot>(celestialBodies)
                : new List<CelestialBodySnapshot>();
            this.worldOrigin = worldOrigin;
            this.playerInventory = playerInventory;
            personalShipCargo = shuttleCargo;
            this.fleetStorage = fleetStorage;
            this.playerPossession = playerPossession;
            this.fleetKnowledge = fleetKnowledge;
            this.resourceDepositDeltas = resourceDepositDeltas != null
                ? new List<ResourceDepositDeltaSnapshot>(resourceDepositDeltas)
                : new List<ResourceDepositDeltaSnapshot>();
        }

        public int SchemaVersion => schemaVersion;
        public int PayloadRevision => payloadRevision;
        public int SourceSchemaVersion =>
            sourceSchemaVersion > 0 ? sourceSchemaVersion : schemaVersion;
        public string SavedAtUtc => string.IsNullOrWhiteSpace(savedAtUtc) ? string.Empty : savedAtUtc.Trim();
        public IReadOnlyList<CelestialBodySnapshot> CelestialBodies => celestialBodies;
        public WorldOriginSnapshot WorldOrigin => worldOrigin;
        public PlayerInventorySnapshot PlayerInventory => playerInventory;
        public InventoryContainerSnapshot ShuttleCargo => personalShipCargo;
        public InventoryContainerSnapshot FleetStorage => fleetStorage;
        public PlayerPossessionSnapshot PlayerPossession => playerPossession;
        public FleetKnowledgeSnapshot FleetKnowledge => fleetKnowledge;
        public IReadOnlyList<ResourceDepositDeltaSnapshot> ResourceDepositDeltas => resourceDepositDeltas;
        public bool IsSupported => SaveGameSchema.IsSupportedVersion(schemaVersion);

        internal static GameplaySaveData CreateMigrated(
            GameplaySaveData source,
            InventoryContainerSnapshot shuttleCargo,
            InventoryContainerSnapshot fleetStorage,
            FleetKnowledgeSnapshot fleetKnowledge)
        {
            return new GameplaySaveData(
                source.SourceSchemaVersion,
                source.SavedAtUtc,
                source.CelestialBodies,
                source.WorldOrigin,
                source.PlayerInventory,
                shuttleCargo,
                fleetStorage,
                source.PlayerPossession,
                fleetKnowledge,
                source.ResourceDepositDeltas);
        }
    }
}
