using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;

namespace Farion.Gameplay.Commands
{
    public static class ResourceHarvestTransaction
    {
        public static ResourceHarvestResult CanExecute(
            ResourceNodeInteractable source,
            IInventoryContainer destination)
        {
            if (source == null)
            {
                return ResourceHarvestResult.MissingSource;
            }

            if (destination == null)
            {
                return ResourceHarvestResult.MissingDestination;
            }

            if (!source.TryGetHarvestOffer(
                    out InventoryItemDefinition item,
                    out int amount))
            {
                return source.IsDepleted
                    ? ResourceHarvestResult.Depleted
                    : ResourceHarvestResult.InvalidYield;
            }

            return destination.CanAdd(item, amount)
                ? ResourceHarvestResult.Succeeded
                : ResourceHarvestResult.InsufficientCapacity;
        }

        public static ResourceHarvestResult TryExecute(
            ResourceNodeInteractable source,
            IInventoryContainer destination)
        {
            ResourceHarvestResult validation = CanExecute(source, destination);
            if (validation != ResourceHarvestResult.Succeeded)
            {
                return validation;
            }

            source.TryGetHarvestOffer(
                out InventoryItemDefinition item,
                out int amount);
            long expectedSourceRevision = source.Revision;
            int added = destination.TryAdd(item, amount);
            if (added != amount)
            {
                return added <= 0 || destination.TryRemove(item, added) == added
                    ? ResourceHarvestResult.InventoryRejected
                    : ResourceHarvestResult.RollbackFailed;
            }

            if (source.TryCommitHarvest(amount, expectedSourceRevision))
            {
                return ResourceHarvestResult.Succeeded;
            }

            return destination.TryRemove(item, added) == added
                ? ResourceHarvestResult.SourceChanged
                : ResourceHarvestResult.RollbackFailed;
        }
    }
}
