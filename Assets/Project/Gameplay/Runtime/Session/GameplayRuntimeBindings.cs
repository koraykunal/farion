using System.Collections.Generic;
using System.Collections.ObjectModel;
using Farion.Core.Physics;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Resources;
using Farion.Gameplay.Ships;
using Farion.Simulation.World;

namespace Farion.Gameplay.Session
{
    public sealed class GameplayRuntimeBindings
    {
        readonly ReadOnlyCollection<ResourceDepositRuntimeSpawner> resourceStreamers;

        public GameplayRuntimeBindings(
            GameplayDefinitionRegistry definitions,
            GravitySimulation gravitySimulation,
            WorldOriginRebaser originRebaser,
            PlayerInventory localPlayerInventory,
            PlayerPossessionController possession,
            FleetKnowledgeRuntime fleetKnowledge,
            ShuttleRuntimeBinding assignedShuttle,
            IReadOnlyList<ResourceDepositRuntimeSpawner> resourceStreamers)
        {
            Definitions = definitions;
            GravitySimulation = gravitySimulation;
            OriginRebaser = originRebaser;
            LocalPlayerInventory = localPlayerInventory;
            Possession = possession;
            FleetKnowledge = fleetKnowledge;
            AssignedShuttle = assignedShuttle;
            this.resourceStreamers = new List<ResourceDepositRuntimeSpawner>(
                resourceStreamers ?? System.Array.Empty<ResourceDepositRuntimeSpawner>())
                .AsReadOnly();
        }

        public GameplayDefinitionRegistry Definitions { get; }
        public GravitySimulation GravitySimulation { get; }
        public WorldOriginRebaser OriginRebaser { get; }
        public PlayerInventory LocalPlayerInventory { get; }
        public PlayerPossessionController Possession { get; }
        public FleetKnowledgeRuntime FleetKnowledge { get; }
        public ShuttleRuntimeBinding AssignedShuttle { get; }
        public IReadOnlyList<ResourceDepositRuntimeSpawner> ResourceStreamers =>
            resourceStreamers;
        public bool IsValid =>
            Definitions != null &&
            GravitySimulation != null &&
            OriginRebaser != null &&
            LocalPlayerInventory != null &&
            Possession != null &&
            FleetKnowledge != null &&
            AssignedShuttle != null &&
            AssignedShuttle.HasValidAuthoring &&
            HasNoMissingResourceStreamers();

        public bool Matches(GameplayRuntimeBindings other)
        {
            if (other == null ||
                !ReferenceEquals(Definitions, other.Definitions) ||
                !ReferenceEquals(GravitySimulation, other.GravitySimulation) ||
                !ReferenceEquals(OriginRebaser, other.OriginRebaser) ||
                !ReferenceEquals(LocalPlayerInventory, other.LocalPlayerInventory) ||
                !ReferenceEquals(Possession, other.Possession) ||
                !ReferenceEquals(FleetKnowledge, other.FleetKnowledge) ||
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
