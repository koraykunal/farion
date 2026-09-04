using System.Collections.Generic;
using System.Collections.ObjectModel;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.ResourceNodes;
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
            FleetRuntime fleet,
            IReadOnlyList<ResourceDepositRuntimeSpawner> resourceStreamers)
        {
            Definitions = definitions;
            GravitySimulation = gravitySimulation;
            CelestialFrameProvider = celestialFrameProvider;
            OriginRebaser = originRebaser;
            Fleet = fleet;
            this.resourceStreamers = new List<ResourceDepositRuntimeSpawner>(
                resourceStreamers ?? System.Array.Empty<ResourceDepositRuntimeSpawner>())
                .AsReadOnly();
        }

        public GameplayDefinitionRegistry Definitions { get; }
        public GravitySimulation GravitySimulation { get; }
        public CelestialFrameProvider CelestialFrameProvider { get; }
        public WorldOriginRebaser OriginRebaser { get; }
        public FleetRuntime Fleet { get; }
        public FleetStorageInventory FleetStorage => Fleet?.Storage;
        public IReadOnlyList<ResourceDepositRuntimeSpawner> ResourceStreamers =>
            resourceStreamers;
        public bool IsValid =>
            Definitions != null &&
            GravitySimulation != null &&
            CelestialFrameProvider != null &&
            CelestialFrameProvider.UsesSimulation(GravitySimulation) &&
            OriginRebaser != null &&
            Fleet != null &&
            Fleet.HasValidAuthoring &&
            HasNoMissingResourceStreamers();

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
