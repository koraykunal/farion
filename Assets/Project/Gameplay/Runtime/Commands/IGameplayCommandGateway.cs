using Farion.Gameplay.Crafting;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Research;

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

        CraftingRecipeResult CanCraft(
            CraftingStationRuntime station,
            RecipeDefinition recipe,
            IInventoryContainer inventory);

        CraftingRecipeResult TryCraft(
            CraftingStationRuntime station,
            RecipeDefinition recipe,
            IInventoryContainer inventory);

        InventoryTransferResult CanTransfer(
            IInventoryContainer source,
            IInventoryContainer destination,
            InventoryItemDefinition item,
            int amount);

        InventoryTransferResult TryTransfer(
            IInventoryContainer source,
            IInventoryContainer destination,
            InventoryItemDefinition item,
            int amount);

        bool HasCompletedResearch(ResearchDefinition research);

        ResearchUnlockResult CanCompleteResearch(
            ResearchTerminalRuntime terminal,
            ResearchDefinition research,
            IInventoryContainer inventory);

        ResearchUnlockResult TryCompleteResearch(
            ResearchTerminalRuntime terminal,
            ResearchDefinition research,
            IInventoryContainer inventory);
    }
}
