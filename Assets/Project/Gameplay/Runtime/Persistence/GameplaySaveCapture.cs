using System.Collections.Generic;
using Farion.Core.Physics;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Research;
using Farion.Gameplay.Resources;
using Farion.Simulation.World;

namespace Farion.Gameplay.Persistence
{
    public sealed class GameplaySaveCapture
    {
        readonly List<CelestialBodySnapshot> celestialBodies = new();
        readonly List<ResourceDepositDeltaSnapshot> resourceDepositDeltas = new();
        PlayerInventorySnapshot playerInventory;
        InventoryContainerSnapshot personalShipCargo;
        PlayerPossessionSnapshot playerPossession;
        FleetKnowledgeSnapshot fleetKnowledge;
        WorldOriginSnapshot worldOrigin;

        public GameplaySaveCapture(string savedAtUtc)
        {
            SavedAtUtc = string.IsNullOrWhiteSpace(savedAtUtc) ? string.Empty : savedAtUtc.Trim();
        }

        public string SavedAtUtc { get; }
        public List<CelestialBodySnapshot> CelestialBodies => celestialBodies;
        public List<ResourceDepositDeltaSnapshot> ResourceDepositDeltas => resourceDepositDeltas;

        public void SetPlayerInventory(PlayerInventorySnapshot snapshot)
        {
            playerInventory = snapshot;
        }

        public void SetPersonalShipCargo(InventoryContainerSnapshot snapshot)
        {
            personalShipCargo = snapshot;
        }

        public void SetPlayerPossession(PlayerPossessionSnapshot snapshot)
        {
            playerPossession = snapshot;
        }

        public void SetFleetKnowledge(FleetKnowledgeSnapshot snapshot)
        {
            fleetKnowledge = snapshot;
        }

        public void SetWorldOrigin(WorldOriginSnapshot snapshot)
        {
            worldOrigin = snapshot;
        }

        public GameplaySaveData CreateSnapshot()
        {
            return new GameplaySaveData(
                SavedAtUtc,
                celestialBodies,
                worldOrigin,
                playerInventory,
                personalShipCargo,
                playerPossession,
                fleetKnowledge,
                resourceDepositDeltas);
        }
    }
}
