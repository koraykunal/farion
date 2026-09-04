using System.Collections.Generic;
using Farion.Core.Identity;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.ResourceNodes;
using Farion.Simulation.Physics;
using Farion.Simulation.World;

namespace Farion.Gameplay.Persistence
{
    public sealed class GameplaySaveCapture
    {
        readonly List<CelestialBodySnapshot> celestialBodies = new();
        readonly List<ResourceDepositDeltaSnapshot> resourceDepositDeltas = new();
        InventoryContainerSnapshot fleetStorage;
        WorldOriginSnapshot worldOrigin;

        public GameplaySaveCapture(string savedAtUtc)
        {
            SavedAtUtc = IdentifierText.Normalize(savedAtUtc);
        }

        public string SavedAtUtc { get; }
        public double CelestialSimulationTime { get; private set; }
        public int CelestialLayoutHash { get; private set; }
        public List<CelestialBodySnapshot> CelestialBodies => celestialBodies;
        public List<ResourceDepositDeltaSnapshot> ResourceDepositDeltas => resourceDepositDeltas;

        public void SetCelestialSimulationTime(double seconds)
        {
            CelestialSimulationTime = seconds >= 0d ? seconds : 0d;
        }

        public void SetCelestialLayoutHash(int layoutHash)
        {
            CelestialLayoutHash = layoutHash;
        }

        public void SetFleetStorage(InventoryContainerSnapshot snapshot)
        {
            fleetStorage = snapshot;
        }

        public void SetWorldOrigin(WorldOriginSnapshot snapshot)
        {
            worldOrigin = snapshot;
        }

        public GameplaySaveData CreateSnapshot()
        {
            GameplaySaveData snapshot = new(
                SavedAtUtc,
                CelestialSimulationTime,
                celestialBodies,
                worldOrigin,
                fleetStorage,
                resourceDepositDeltas);
            snapshot.SetCelestialLayoutHash(CelestialLayoutHash);
            return snapshot;
        }
    }
}
