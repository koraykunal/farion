using System.Collections.Generic;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Resources;
using Farion.Gameplay.Session;
using Farion.Gameplay.Ships;
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
        public PlayerInventory PlayerInventory => Bindings?.LocalPlayerInventory;
        public PlayerPossessionController PossessionController => Bindings?.Possession;
        public FleetRuntime Fleet => Bindings?.Fleet;
        public FleetStorageInventory FleetStorage => Bindings?.FleetStorage;
        public FleetKnowledgeRuntime FleetKnowledge => Bindings?.FleetKnowledge;
        public ShuttleRuntimeBinding Shuttle => Bindings?.AssignedShuttle;
        public ShuttleCargoInventory ShuttleCargo =>
            Bindings?.AssignedShuttle != null
                ? Bindings.AssignedShuttle.Cargo
                : null;
        public IReadOnlyList<ResourceDepositRuntimeSpawner> ResourceStreamers =>
            Bindings?.ResourceStreamers;
    }
}
