using Farion.Gameplay.Commands;
using Farion.Gameplay.Crafting;
using Farion.Gameplay.Domain.Fleet;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Session;
using Farion.Gameplay.Ships;

namespace Farion.App.Commands
{
    public sealed class GameplayCommandService : IGameplayCommandGateway
    {
        readonly GameplaySessionRuntime session;

        public GameplayCommandService(GameplaySessionRuntime session)
        {
            this.session = session;
        }

        public GameplaySessionRuntime Session => session;

        public ResourceHarvestResult CanHarvest(
            ResourceNodeInteractable source,
            IInventoryContainer destination)
        {
            return OwnsInventory(destination)
                ? ResourceHarvestTransaction.CanExecute(source, destination)
                : ResourceHarvestResult.UnauthorizedDestination;
        }

        public ResourceHarvestResult TryHarvest(
            ResourceNodeInteractable source,
            IInventoryContainer destination)
        {
            return OwnsInventory(destination)
                ? ResourceHarvestTransaction.TryExecute(source, destination)
                : ResourceHarvestResult.UnauthorizedDestination;
        }

        public CraftingRecipeResult CanCraft(
            RecipeDefinition recipe,
            IInventoryContainer inventory,
            bool hasRequiredResearch)
        {
            return OwnsInventory(inventory)
                ? CraftingRecipeExecutor.CanCraft(
                    recipe,
                    inventory,
                    hasRequiredResearch)
                : CraftingRecipeResult.UnauthorizedInventory;
        }

        public CraftingRecipeResult TryCraft(
            RecipeDefinition recipe,
            IInventoryContainer inventory,
            bool hasRequiredResearch)
        {
            return OwnsInventory(inventory)
                ? CraftingRecipeExecutor.TryCraft(
                    recipe,
                    inventory,
                    hasRequiredResearch)
                : CraftingRecipeResult.UnauthorizedInventory;
        }

        public bool Matches(GameplaySessionRuntime candidate)
        {
            return ReferenceEquals(session, candidate);
        }

        public PersonalShipCommandResult RenamePersonalShip(
            PersistentEntityId shipId,
            string displayName,
            long expectedRevision = -1L)
        {
            if (!TryResolvePersonalShip(
                    shipId,
                    out PersonalShipRuntimeBinding binding,
                    out PersonalShipCommandResult failure))
            {
                return failure;
            }

            FleetOperationResult operation =
                binding.TryRename(displayName, expectedRevision);
            return BuildShipResult(binding, operation, 0d);
        }

        public PersonalShipCommandResult RefuelPersonalShip(
            PersistentEntityId shipId,
            double amount,
            long expectedRevision = -1L)
        {
            if (!TryResolvePersonalShip(
                    shipId,
                    out PersonalShipRuntimeBinding binding,
                    out PersonalShipCommandResult failure))
            {
                return failure;
            }

            FleetOperationResult operation =
                binding.TryRefuel(amount, out double accepted, expectedRevision);
            return BuildShipResult(binding, operation, accepted);
        }

        public PersonalShipCommandResult RepairPersonalShip(
            PersistentEntityId shipId,
            double amount,
            long expectedRevision = -1L)
        {
            if (!TryResolvePersonalShip(
                    shipId,
                    out PersonalShipRuntimeBinding binding,
                    out PersonalShipCommandResult failure))
            {
                return failure;
            }

            FleetOperationResult operation =
                binding.TryRepair(amount, out double accepted, expectedRevision);
            return BuildShipResult(binding, operation, accepted);
        }

        public PersonalShipCommandResult ApplyPersonalShipDamage(
            PersistentEntityId shipId,
            double amount,
            long expectedRevision = -1L)
        {
            if (!TryResolvePersonalShip(
                    shipId,
                    out PersonalShipRuntimeBinding binding,
                    out PersonalShipCommandResult failure))
            {
                return failure;
            }

            FleetOperationResult operation =
                binding.TryApplyDamage(
                    amount,
                    out double applied,
                    expectedRevision);
            return BuildShipResult(binding, operation, applied);
        }

        public PersonalShipCommandResult ConsumePersonalShipFuel(
            PersistentEntityId shipId,
            double amount,
            long expectedRevision = -1L)
        {
            if (!TryResolvePersonalShip(
                    shipId,
                    out PersonalShipRuntimeBinding binding,
                    out PersonalShipCommandResult failure))
            {
                return failure;
            }

            FleetOperationResult operation =
                binding.TryConsumeFuel(amount, expectedRevision);
            double consumed = operation == FleetOperationResult.Succeeded
                ? amount
                : 0d;
            return BuildShipResult(binding, operation, consumed);
        }

        bool OwnsInventory(IInventoryContainer inventory)
        {
            return session != null &&
                   inventory != null &&
                   ReferenceEquals(session.LocalInventory, inventory) &&
                   inventory.ContainerId == session.Identity.CarriedInventoryId;
        }

        bool TryResolvePersonalShip(
            PersistentEntityId shipId,
            out PersonalShipRuntimeBinding binding,
            out PersonalShipCommandResult failure)
        {
            binding = null;
            if (session == null)
            {
                failure = new PersonalShipCommandResult(
                    PersonalShipCommandStatus.MissingRuntime,
                    FleetOperationResult.NotFound,
                    0d,
                    0L);
                return false;
            }

            if (!shipId.IsValid)
            {
                failure = new PersonalShipCommandResult(
                    PersonalShipCommandStatus.InvalidShip,
                    FleetOperationResult.InvalidIdentifier,
                    0d,
                    0L);
                return false;
            }

            if (shipId != session.Identity.PersonalShipId)
            {
                failure = new PersonalShipCommandResult(
                    PersonalShipCommandStatus.UnauthorizedShip,
                    FleetOperationResult.Conflict,
                    0d,
                    session.PersonalShip?.Revision ?? 0L);
                return false;
            }

            binding = session.PersonalShipBinding;
            if (binding == null ||
                binding.State == null ||
                binding.State.OwnerPlayerId != session.Identity.LocalPlayerId ||
                binding.State.ShipId != shipId)
            {
                failure = new PersonalShipCommandResult(
                    PersonalShipCommandStatus.MissingRuntime,
                    FleetOperationResult.NotFound,
                    0d,
                    0L);
                binding = null;
                return false;
            }

            failure = default;
            return true;
        }

        static PersonalShipCommandResult BuildShipResult(
            PersonalShipRuntimeBinding binding,
            FleetOperationResult operation,
            double appliedAmount)
        {
            return new PersonalShipCommandResult(
                operation == FleetOperationResult.Succeeded
                    ? PersonalShipCommandStatus.Succeeded
                    : PersonalShipCommandStatus.Rejected,
                operation,
                appliedAmount,
                binding.State?.Revision ?? 0L);
        }
    }
}
