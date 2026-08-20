using System;
using Farion.Core.Identity;
using System.Collections.Generic;
using Farion.Core.Persistence;
using Farion.Gameplay.Domain.Systems;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.ResourceNodes;
using Farion.Simulation.Physics;
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
        [SerializeField] double celestialSimulationTime;
        [SerializeField] List<CelestialBodySnapshot> celestialBodies = new();
        [SerializeField] WorldOriginSnapshot worldOrigin;
        [SerializeField] PlayerInventorySnapshot playerInventory;
        [SerializeField] InventoryContainerSnapshot personalShipCargo;
        [SerializeField] InventoryContainerSnapshot fleetStorage;
        [SerializeField] PlayerPossessionSnapshot playerPossession;
        [SerializeField] FleetKnowledgeSnapshot fleetKnowledge;
        [SerializeField] ResourcePool shuttleFuel;
        [SerializeField] ResourcePool shuttleHull;
        [SerializeField] List<ResourceDepositDeltaSnapshot> resourceDepositDeltas = new();
        [SerializeField] List<MultiplayerPlayerSaveEntry> multiplayerPlayers = new();
        [SerializeField] List<MultiplayerShipSaveEntry> multiplayerShipCargo = new();
        [NonSerialized] int sourceSchemaVersion;

        public GameplaySaveData(
            string savedAtUtc,
            double celestialSimulationTime,
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
                celestialSimulationTime,
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
            double celestialSimulationTime,
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
            this.savedAtUtc = IdentifierText.Normalize(savedAtUtc);
            this.celestialSimulationTime = celestialSimulationTime >= 0d ? celestialSimulationTime : 0d;
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
        public string SavedAtUtc => IdentifierText.Normalize(savedAtUtc);
        public double CelestialSimulationTime => celestialSimulationTime >= 0d ? celestialSimulationTime : 0d;
        public IReadOnlyList<CelestialBodySnapshot> CelestialBodies => celestialBodies;
        public WorldOriginSnapshot WorldOrigin => worldOrigin;
        public PlayerInventorySnapshot PlayerInventory => playerInventory;
        public InventoryContainerSnapshot ShuttleCargo => personalShipCargo;
        public InventoryContainerSnapshot FleetStorage => fleetStorage;
        public PlayerPossessionSnapshot PlayerPossession => playerPossession;
        public FleetKnowledgeSnapshot FleetKnowledge => fleetKnowledge;
        public ResourcePool ShuttleFuel => shuttleFuel;
        public ResourcePool ShuttleHull => shuttleHull;
        public IReadOnlyList<ResourceDepositDeltaSnapshot> ResourceDepositDeltas => resourceDepositDeltas;
        public IReadOnlyList<MultiplayerPlayerSaveEntry> MultiplayerPlayers =>
            multiplayerPlayers;
        public IReadOnlyList<MultiplayerShipSaveEntry> MultiplayerShipCargo =>
            multiplayerShipCargo;

        public void SetShuttleFuel(ResourcePool fuel)
        {
            shuttleFuel = fuel;
        }

        public void SetShuttleHull(ResourcePool hull)
        {
            shuttleHull = hull;
        }

        public void SetMultiplayerState(
            IReadOnlyList<MultiplayerPlayerSaveEntry> players,
            IReadOnlyList<MultiplayerShipSaveEntry> shipCargo)
        {
            multiplayerPlayers.Clear();
            multiplayerShipCargo.Clear();
            if (players != null)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i] != null && players[i].IsValid)
                    {
                        multiplayerPlayers.Add(players[i]);
                    }
                }
            }

            if (shipCargo == null)
            {
                return;
            }

            for (int i = 0; i < shipCargo.Count; i++)
            {
                if (shipCargo[i] != null && shipCargo[i].IsValid)
                {
                    multiplayerShipCargo.Add(shipCargo[i]);
                }
            }
        }
        public bool IsSupported => SaveGameSchema.IsSupportedVersion(schemaVersion);

        internal static GameplaySaveData CreateMigrated(
            GameplaySaveData source,
            InventoryContainerSnapshot shuttleCargo,
            InventoryContainerSnapshot fleetStorage,
            FleetKnowledgeSnapshot fleetKnowledge)
        {
            GameplaySaveData migrated = new(
                source.SourceSchemaVersion,
                source.SavedAtUtc,
                source.CelestialSimulationTime,
                source.CelestialBodies,
                source.WorldOrigin,
                source.PlayerInventory,
                shuttleCargo,
                fleetStorage,
                source.PlayerPossession,
                fleetKnowledge,
                source.ResourceDepositDeltas);
            migrated.SetShuttleFuel(source.ShuttleFuel);
            migrated.SetShuttleHull(source.ShuttleHull);
            migrated.SetMultiplayerState(
                source.MultiplayerPlayers,
                source.MultiplayerShipCargo);
            return migrated;
        }
    }
}
