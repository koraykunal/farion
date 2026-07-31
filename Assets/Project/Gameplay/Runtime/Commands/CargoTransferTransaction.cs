using System.Collections.Generic;
using Farion.Gameplay.Domain.Economy;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Inventory;

namespace Farion.Gameplay.Commands
{
    public static class CargoTransferTransaction
    {
        public static CargoTransferResult CanExecute(
            InventoryContainerComponent source,
            InventoryContainerComponent destination)
        {
            return Evaluate(source, destination, apply: false);
        }

        public static CargoTransferResult TryExecute(
            InventoryContainerComponent source,
            InventoryContainerComponent destination)
        {
            return Evaluate(source, destination, apply: true);
        }

        static CargoTransferResult Evaluate(
            InventoryContainerComponent source,
            InventoryContainerComponent destination,
            bool apply)
        {
            if (source == null)
            {
                return CargoTransferResult.MissingSource;
            }

            if (destination == null)
            {
                return CargoTransferResult.MissingDestination;
            }

            List<InventoryItemDefinition> definitions =
                CollectDefinitions(source.Stacks);
            if (definitions.Count == 0)
            {
                return CargoTransferResult.EmptySource;
            }

            if (!destination.CanReferenceDefinitions(definitions))
            {
                return CargoTransferResult.DefinitionMismatch;
            }

            long sourceRevision = source.Revision;
            long destinationRevision = destination.Revision;
            InventoryOperationResult result = apply
                ? InventoryTransferService.TryTransferAllStacks(
                    source.DomainState,
                    destination.DomainState,
                    sourceRevision,
                    destinationRevision)
                : InventoryTransferService.CanTransferAllStacks(
                    source.DomainState,
                    destination.DomainState,
                    sourceRevision,
                    destinationRevision);
            if (result != InventoryOperationResult.Succeeded)
            {
                return MapResult(result);
            }

            if (apply)
            {
                destination.CompleteDomainTransfer(definitions);
                source.CompleteDomainTransfer(null);
            }

            return CargoTransferResult.Succeeded;
        }

        static List<InventoryItemDefinition> CollectDefinitions(
            IReadOnlyList<InventoryStack> stacks)
        {
            List<InventoryItemDefinition> definitions = new();
            HashSet<DefinitionId> ids = new();
            if (stacks == null)
            {
                return definitions;
            }

            for (int i = 0; i < stacks.Count; i++)
            {
                InventoryItemDefinition item = stacks[i]?.Item;
                if (item != null &&
                    DefinitionId.TryCreate(
                        item.ItemId,
                        out DefinitionId id) &&
                    ids.Add(id))
                {
                    definitions.Add(item);
                }
            }

            return definitions;
        }

        static CargoTransferResult MapResult(
            InventoryOperationResult result)
        {
            return result switch
            {
                InventoryOperationResult.EmptyContainer =>
                    CargoTransferResult.EmptySource,
                InventoryOperationResult.InsufficientCapacity =>
                    CargoTransferResult.InsufficientCapacity,
                InventoryOperationResult.StaleRevision =>
                    CargoTransferResult.StaleState,
                InventoryOperationResult.DefinitionPolicyMismatch or
                InventoryOperationResult.InvalidDefinition or
                InventoryOperationResult.InvalidStackLimit =>
                    CargoTransferResult.DefinitionMismatch,
                _ => CargoTransferResult.Rejected
            };
        }
    }
}
