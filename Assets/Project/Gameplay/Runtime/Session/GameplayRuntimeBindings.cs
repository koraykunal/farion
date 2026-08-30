using System.Collections.Generic;
using System.Collections.ObjectModel;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.ResourceNodes;
using Farion.Gameplay.Ships;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.World;

namespace Farion.Gameplay.Session
{
    public sealed class GameplayRuntimeBindings
    {
        readonly ReadOnlyCollection<ResourceDepositRuntimeSpawner> resourceStreamers;

        public GameplayRuntimeBindings(
            GameplayDefinitionRegistry definitions,
            GravitySimulation gravitySimulation,
            CelestialFrameProvider celestialFrameProvider,
            WorldOriginRebaser originRebaser,
            PlayerInventory localPlayerInventory,
            PlayerPossessionController possession,
            FleetRuntime fleet,
            ShuttleRuntimeBinding assignedShuttle,
            IReadOnlyList<ResourceDepositRuntimeSpawner> resourceStreamers)
        {
            Definitions = definitions;
            GravitySimulation = gravitySimulation;
            CelestialFrameProvider = celestialFrameProvider;
            OriginRebaser = originRebaser;
            LocalPlayerInventory = localPlayerInventory;
            Possession = possession;
            Fleet = fleet;
            AssignedShuttle = assignedShuttle;
            this.resourceStreamers = new List<ResourceDepositRuntimeSpawner>(
                resourceStreamers ?? System.Array.Empty<ResourceDepositRuntimeSpawner>())
                .AsReadOnly();
        }

        public GameplayDefinitionRegistry Definitions { get; }
        public GravitySimulation GravitySimulation { get; }
        public CelestialFrameProvider CelestialFrameProvider { get; }
        public WorldOriginRebaser OriginRebaser { get; }
        public PlayerInventory LocalPlayerInventory { get; }
        public PlayerPossessionController Possession { get; }
        public FleetRuntime Fleet { get; }
        public FleetStorageInventory FleetStorage => Fleet?.Storage;
        public ShuttleRuntimeBinding AssignedShuttle { get; }
        public IReadOnlyList<ResourceDepositRuntimeSpawner> ResourceStreamers =>
            resourceStreamers;
        public bool IsValid =>
            Definitions != null &&
            GravitySimulation != null &&
            CelestialFrameProvider != null &&
            CelestialFrameProvider.UsesSimulation(GravitySimulation) &&
            OriginRebaser != null &&
            LocalPlayerInventory != null &&
            Possession != null &&
            Fleet != null &&
            Fleet.HasValidAuthoring &&
            AssignedShuttle != null &&
            AssignedShuttle.HasValidAuthoring &&
            HasNoMissingResourceStreamers();

        public bool Matches(GameplayRuntimeBindings other)
        {
            if (other == null ||
                !ReferenceEquals(Definitions, other.Definitions) ||
                !ReferenceEquals(GravitySimulation, other.GravitySimulation) ||
                !ReferenceEquals(CelestialFrameProvider, other.CelestialFrameProvider) ||
                !ReferenceEquals(OriginRebaser, other.OriginRebaser) ||
                !ReferenceEquals(LocalPlayerInventory, other.LocalPlayerInventory) ||
                !ReferenceEquals(Possession, other.Possession) ||
                !ReferenceEquals(Fleet, other.Fleet) ||
                !ReferenceEquals(AssignedShuttle, other.AssignedShuttle) ||
                resourceStreamers.Count != other.resourceStreamers.Count)
            {
                return false;
            }

            for (int i = 0; i < resourceStreamers.Count; i++)
            {
                if (!ReferenceEquals(
                        resourceStreamers[i],
                        other.resourceStreamers[i]))
                {
                    return false;
                }
            }

            return true;
        }

        bool HasNoMissingResourceStreamers()
        {
            for (int i = 0; i < resourceStreamers.Count; i++)
            {
                if (resourceStreamers[i] == null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
