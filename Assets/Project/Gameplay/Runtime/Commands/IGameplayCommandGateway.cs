using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;

namespace Farion.Gameplay.Commands
{
    public interface IGameplayCommandGateway
    {
        ResourceHarvestResult CanHarvest(
            ResourceNodeInteractable source,
            IInventoryContainer destination);

        ResourceHarvestResult TryHarvest(
            ResourceNodeInteractable source,
            IInventoryContainer destination);
    }
}
