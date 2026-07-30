using Farion.Gameplay.Commands;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;

namespace Farion.App.Commands.Handlers
{
    internal sealed class InventoryCommandHandler
    {
        readonly SessionInventoryAccess access;

        public InventoryCommandHandler(SessionInventoryAccess access)
        {
            this.access = access;
        }

        public ResourceHarvestResult CanHarvest(
            ResourceNodeInteractable source,
            IInventoryContainer destination)
        {
            return access.Owns(destination)
                ? ResourceHarvestTransaction.CanExecute(source, destination)
                : ResourceHarvestResult.UnauthorizedDestination;
        }

        public ResourceHarvestResult TryHarvest(
            ResourceNodeInteractable source,
            IInventoryContainer destination)
        {
            return access.Owns(destination)
                ? ResourceHarvestTransaction.TryExecute(source, destination)
                : ResourceHarvestResult.UnauthorizedDestination;
        }
    }
}
