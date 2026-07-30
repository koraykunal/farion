using Farion.Gameplay.Crafting;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Session;

namespace Farion.App.Commands.Handlers
{
    internal sealed class CraftingCommandHandler
    {
        readonly GameplaySessionRuntime session;
        readonly SessionInventoryAccess access;

        public CraftingCommandHandler(
            GameplaySessionRuntime session,
            SessionInventoryAccess access)
        {
            this.session = session;
            this.access = access;
        }

        public CraftingRecipeResult CanCraft(
            CraftingStationRuntime station,
            RecipeDefinition recipe,
            IInventoryContainer inventory)
        {
            CraftingRecipeResult authorization =
                ValidateRequest(station, recipe, inventory);
            return authorization == CraftingRecipeResult.Succeeded
                ? CraftingRecipeExecutor.CanCraft(
                    recipe,
                    station.StationType,
                    inventory,
                    HasRequiredResearch(recipe))
                : authorization;
        }

        public CraftingRecipeResult TryCraft(
            CraftingStationRuntime station,
            RecipeDefinition recipe,
            IInventoryContainer inventory)
        {
            CraftingRecipeResult authorization =
                ValidateRequest(station, recipe, inventory);
            return authorization == CraftingRecipeResult.Succeeded
                ? CraftingRecipeExecutor.TryCraft(
                    recipe,
                    station.StationType,
                    inventory,
                    HasRequiredResearch(recipe))
                : authorization;
        }

        CraftingRecipeResult ValidateRequest(
            CraftingStationRuntime station,
            RecipeDefinition recipe,
            IInventoryContainer inventory)
        {
            if (!access.Owns(inventory))
            {
                return CraftingRecipeResult.UnauthorizedInventory;
            }

            return station != null && station.CanOffer(recipe)
                ? CraftingRecipeResult.Succeeded
                : CraftingRecipeResult.RecipeNotOffered;
        }

        bool HasRequiredResearch(RecipeDefinition recipe)
        {
            return recipe == null ||
                   !recipe.HasRequiredResearch ||
                   session?.FleetProgression != null &&
                   session.FleetProgression.HasCompletedResearch(
                       recipe.RequiredResearch);
        }
    }
}
