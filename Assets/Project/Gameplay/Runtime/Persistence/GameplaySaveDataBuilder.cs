using System.Collections.Generic;
using Farion.Core.Physics;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Resources;
using Farion.Simulation.World;

namespace Farion.Gameplay.Persistence
{
    public sealed class GameplaySaveDataBuilder
    {
        readonly List<CelestialBodySnapshot> celestialBodies = new();
        readonly List<ResourceDepositDeltaSnapshot> resourceDepositDeltas = new();
        PlayerInventorySnapshot playerInventory;
        PlayerPossessionSnapshot playerPossession;
        WorldOriginSnapshot worldOrigin;

        public GameplaySaveDataBuilder(string savedAtUtc)
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

        public void SetPlayerPossession(PlayerPossessionSnapshot snapshot)
        {
            playerPossession = snapshot;
        }

        public void SetWorldOrigin(WorldOriginSnapshot snapshot)
        {
            worldOrigin = snapshot;
        }

        public GameplaySaveData Build()
        {
            return new GameplaySaveData(
                SavedAtUtc,
                celestialBodies,
                worldOrigin,
                playerInventory,
                playerPossession,
                resourceDepositDeltas);
        }
    }
}
