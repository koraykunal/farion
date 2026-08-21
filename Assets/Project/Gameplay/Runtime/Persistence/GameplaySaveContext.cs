using System.Collections.Generic;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.ResourceNodes;
using Farion.Gameplay.Session;
using Farion.Gameplay.Ships;
using Farion.Simulation.Physics;
using Farion.Simulation.World;

namespace Farion.Gameplay.Persistence
{
    public readonly struct GameplaySaveContext
    {
        public GameplaySaveContext(
            GameplayRuntimeBindings bindings,
            GameplaySessionMode mode = GameplaySessionMode.Offline)
        {
            Bindings = bindings;
            Mode = mode;
        }

        public GameplayRuntimeBindings Bindings { get; }
        public GameplaySessionMode Mode { get; }
        public bool IsMultiplayer => Mode == GameplaySessionMode.Multiplayer;
        public GameplayDefinitionRegistry Definitions => Bindings?.Definitions;
        public GravitySimulation GravitySimulation => Bindings?.GravitySimulation;
        public WorldOriginRebaser OriginRebaser => Bindings?.OriginRebaser;
        public PlayerInventory PlayerInventory => Bindings?.LocalPlayerInventory;
        public PlayerPossessionController PossessionController => Bindings?.Possession;
        public FleetRuntime Fleet => Bindings?.Fleet;
        public FleetStorageInventory FleetStorage => Bindings?.FleetStorage;
        public FleetKnowledgeRuntime FleetKnowledge => Bindings?.FleetKnowledge;
        public ShuttleRuntimeBinding Shuttle => Bindings?.AssignedShuttle;
        public SpacecraftMotor ShuttleMotor =>
            Bindings?.AssignedShuttle != null
                ? Bindings.AssignedShuttle.Motor
                : null;
        public SpacecraftHull ShuttleHull =>
            Bindings?.AssignedShuttle != null &&
            Bindings.AssignedShuttle.Motor != null
                ? Bindings.AssignedShuttle.Motor.GetComponent<SpacecraftHull>()
                : null;
        public ShuttleCargoInventory ShuttleCargo =>
            Bindings?.AssignedShuttle != null
                ? Bindings.AssignedShuttle.Cargo
                : null;
        public IReadOnlyList<ResourceDepositRuntimeSpawner> ResourceStreamers =>
            Bindings?.ResourceStreamers;
    }
}
