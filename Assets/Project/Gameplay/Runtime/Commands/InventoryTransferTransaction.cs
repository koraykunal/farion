using Farion.Gameplay.Inventory;

namespace Farion.Gameplay.Commands
{
    public static class InventoryTransferTransaction
    {
        public static InventoryTransferResult CanExecute(
            IInventoryContainer source,
            IInventoryContainer destination,
            InventoryItemDefinition item,
            int amount)
        {
            if (source == null)
            {
                return InventoryTransferResult.MissingSource;
            }

            if (destination == null)
            {
                return InventoryTransferResult.MissingDestination;
            }

            if (source.ContainerId == destination.ContainerId)
            {
                return InventoryTransferResult.SameContainer;
            }

            if (item == null)
            {
                return InventoryTransferResult.MissingItem;
            }

            if (amount <= 0)
            {
                return InventoryTransferResult.InvalidQuantity;
            }

            if (!source.CanRemove(item, amount))
            {
                return InventoryTransferResult.InsufficientQuantity;
            }

            return destination.CanAdd(item, amount)
                ? InventoryTransferResult.Succeeded
                : InventoryTransferResult.InsufficientCapacity;
        }

        public static InventoryTransferResult TryExecute(
            IInventoryContainer source,
            IInventoryContainer destination,
            InventoryItemDefinition item,
            int amount)
        {
            InventoryTransferResult validation =
                CanExecute(source, destination, item, amount);
            if (validation != InventoryTransferResult.Succeeded)
            {
                return validation;
            }

            int removed = source.TryRemove(item, amount);
            if (removed != amount)
            {
                return removed <= 0 || source.TryAdd(item, removed) == removed
                    ? InventoryTransferResult.SourceRejected
                    : InventoryTransferResult.RollbackFailed;
            }

            int added = destination.TryAdd(item, amount);
            if (added == amount)
            {
                return InventoryTransferResult.Succeeded;
            }

            bool destinationRollbackSucceeded =
                added <= 0 || destination.TryRemove(item, added) == added;
            bool sourceRollbackSucceeded = source.TryAdd(item, removed) == removed;
            return destinationRollbackSucceeded && sourceRollbackSucceeded
                ? InventoryTransferResult.DestinationRejected
                : InventoryTransferResult.RollbackFailed;
        }
    }
}
