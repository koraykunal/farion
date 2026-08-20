using System.Collections.Generic;
using Farion.Core.Identity;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Processing;
using Farion.Gameplay.ResourceNodes;
using Farion.Gameplay.Session;
using Farion.Gameplay.Ships;

namespace Farion.App.Commands
{
    internal sealed class SessionCommandScope
    {
        readonly GameplaySessionRuntime session;

        public SessionCommandScope(GameplaySessionRuntime session)
        {
            this.session = session;
        }

        public InventoryContainerComponent LocalInventory => session?.LocalInventory;

        public FleetStorageInventory FleetStorage => session?.Fleet?.Storage;

        public ShuttleCargoInventory AssignedShuttleCargo => session?.ShuttleBinding?.Cargo;

        public bool TryResolveOwnedContainer(
            PersistentEntityId containerId,
            out IInventoryContainer container)
        {
            container = null;
            if (!containerId.IsValid)
            {
                return false;
            }

            InventoryContainerComponent local = LocalInventory;
            if (local != null &&
                local.ContainerId == containerId &&
                OwnsLocalInventory(local))
            {
                container = local;
                return true;
            }

            ShuttleCargoInventory cargo = AssignedShuttleCargo;
            if (cargo != null &&
                cargo.ContainerId == containerId &&
                OwnsAssignedShuttleCargo(cargo))
            {
                container = cargo;
                return true;
            }

            return false;
        }

        public bool TryResolveAssignedShuttleCargo(
            PersistentEntityId containerId,
            out ShuttleCargoInventory cargo)
        {
            cargo = AssignedShuttleCargo;
            if (cargo == null ||
                !containerId.IsValid ||
                cargo.ContainerId != containerId ||
                !OwnsAssignedShuttleCargo(cargo))
            {
                cargo = null;
                return false;
            }

            return true;
        }

        public bool TryResolveOwnedFleetStorage(out FleetStorageInventory storage)
        {
            storage = FleetStorage;
            if (storage == null || !OwnsFleetStorage(storage))
            {
                storage = null;
                return false;
            }

            return true;
        }

        public bool TryResolveOwnedLocalInventory(out InventoryContainerComponent inventory)
        {
            inventory = LocalInventory;
            if (inventory == null || !OwnsLocalInventory(inventory))
            {
                inventory = null;
                return false;
            }

            return true;
        }

        public bool TryResolveDeposit(
            GeneratedEntityId depositId,
            out ResourceNodeInteractable node)
        {
            node = null;
            IReadOnlyList<ResourceDepositRuntimeSpawner> streamers =
                session?.Bindings?.ResourceStreamers;
            if (streamers == null || !depositId.IsValid)
            {
                return false;
            }

            for (int i = 0; i < streamers.Count; i++)
            {
                if (streamers[i] != null &&
                    streamers[i].TryGetSpawnedNode(depositId, out node))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryResolveProcessingRecipe(
            DefinitionId recipeId,
            out ProcessingRecipeDefinition recipe)
        {
            recipe = null;
            GameplayDefinitionRegistry definitions = session?.Bindings?.Definitions;
            return definitions != null &&
                   definitions.TryGetProcessingRecipe(recipeId, out recipe);
        }

        public static bool RevisionMatches(IInventoryContainer container, long expectedRevision)
        {
            return container != null && container.Revision == expectedRevision;
        }

        bool OwnsLocalInventory(IInventoryContainer inventory)
        {
            return session != null &&
                   inventory != null &&
                   ReferenceEquals(session.LocalInventory, inventory) &&
                   inventory.ContainerId == session.Identity.CarriedInventoryId;
        }

        bool OwnsAssignedShuttleCargo(ShuttleCargoInventory inventory)
        {
            ShuttleRuntimeBinding shuttle = session?.ShuttleBinding;
            return shuttle != null &&
                   inventory != null &&
                   ReferenceEquals(shuttle.Cargo, inventory) &&
                   shuttle.ShipId == session.Identity.AssignedShuttleId;
        }

        bool OwnsFleetStorage(FleetStorageInventory inventory)
        {
            FleetRuntime fleet = session?.Fleet;
            return fleet != null &&
                   inventory != null &&
                   ReferenceEquals(fleet.Storage, inventory) &&
                   fleet.FleetId == session.Identity.FleetId;
        }
    }
}
