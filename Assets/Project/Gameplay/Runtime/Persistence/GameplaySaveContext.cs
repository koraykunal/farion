using System.Collections.Generic;
using Farion.Core.Physics;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Resources;
using Farion.Gameplay.Session;
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
        public IReadOnlyList<ResourceDepositRuntimeSpawner> ResourceStreamers =>
            Bindings?.ResourceStreamers;
    }
}
