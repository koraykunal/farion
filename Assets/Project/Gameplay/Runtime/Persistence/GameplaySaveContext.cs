using System.Collections.Generic;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.ResourceNodes;
using Farion.Gameplay.Session;
using Farion.Simulation.Physics;
using Farion.Simulation.World;

namespace Farion.Gameplay.Persistence
{
    public readonly struct GameplaySaveContext
    {
        public GameplaySaveContext(GameplayRuntimeBindings bindings)
        {
            Bindings = bindings;
        }

        public GameplayRuntimeBindings Bindings { get; }
        public GameplayDefinitionRegistry Definitions => Bindings?.Definitions;
        public GravitySimulation GravitySimulation => Bindings?.GravitySimulation;
        public WorldOriginRebaser OriginRebaser => Bindings?.OriginRebaser;
        public FleetRuntime Fleet => Bindings?.Fleet;
        public FleetStorageInventory FleetStorage => Bindings?.FleetStorage;
        public IReadOnlyList<ResourceDepositRuntimeSpawner> ResourceStreamers =>
            Bindings?.ResourceStreamers;
    }
}
