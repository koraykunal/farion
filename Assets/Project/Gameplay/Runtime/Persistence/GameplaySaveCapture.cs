using System.Collections.Generic;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Resources;
using Farion.Simulation.Physics;
using Farion.Simulation.World;

namespace Farion.Gameplay.Persistence
{
    public sealed class GameplaySaveCapture
    {
        readonly List<CelestialBodySnapshot> celestialBodies = new();
        readonly List<ResourceDepositDeltaSnapshot> resourceDepositDeltas = new();
        PlayerInventorySnapshot playerInventory;
        InventoryContainerSnapshot shuttleCargo;
        InventoryContainerSnapshot fleetStorage;
        PlayerPossessionSnapshot playerPossession;
        FleetKnowledgeSnapshot fleetKnowledge;
        WorldOriginSnapshot worldOrigin;

        public GameplaySaveCapture(string savedAtUtc)
        {
            SavedAtUtc = string.IsNullOrWhiteSpace(savedAtUtc) ? string.Empty : savedAtUtc.Trim();
        }

        public string SavedAtUtc { get; }
        public double CelestialSimulationTime { get; private set; }
        public List<CelestialBodySnapshot> CelestialBodies => celestialBodies;
        public List<ResourceDepositDeltaSnapshot> ResourceDepositDeltas => resourceDepositDeltas;

        public void SetCelestialSimulationTime(double seconds)
        {
            CelestialSimulationTime = seconds >= 0d ? seconds : 0d;
        }

        public void SetPlayerInventory(PlayerInventorySnapshot snapshot)
        {
            playerInventory = snapshot;
        }

        public void SetShuttleCargo(InventoryContainerSnapshot snapshot)
        {
            shuttleCargo = snapshot;
        }

        public void SetFleetStorage(InventoryContainerSnapshot snapshot)
        {
            fleetStorage = snapshot;
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
                CelestialSimulationTime,
                celestialBodies,
                worldOrigin,
                playerInventory,
                shuttleCargo,
                fleetStorage,
                playerPossession,
                fleetKnowledge,
                resourceDepositDeltas);
        }
    }
}
