using System.Collections.Generic;
using Farion.Core.Physics;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Resources;
using Farion.Simulation.World;

namespace Farion.Gameplay.Persistence
{
    public readonly struct GameplaySaveContext
    {
        public GameplaySaveContext(
            GameplayDefinitionRegistry definitions,
            GravitySimulation gravitySimulation,
            WorldOriginRebaser originRebaser,
            PlayerInventory playerInventory,
            PlayerPossessionController possessionController,
            IReadOnlyList<ResourceDepositRuntimeSpawner> resourceStreamers)
        {
            Definitions = definitions;
            GravitySimulation = gravitySimulation;
            OriginRebaser = originRebaser;
            PlayerInventory = playerInventory;
            PossessionController = possessionController;
            ResourceStreamers = resourceStreamers;
        }

        public GameplayDefinitionRegistry Definitions { get; }
        public GravitySimulation GravitySimulation { get; }
        public WorldOriginRebaser OriginRebaser { get; }
        public PlayerInventory PlayerInventory { get; }
        public PlayerPossessionController PossessionController { get; }
        public IReadOnlyList<ResourceDepositRuntimeSpawner> ResourceStreamers { get; }
    }
}
