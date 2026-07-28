using Farion.Gameplay.Inventory;

namespace Farion.Gameplay.Crafting
{
    public static class CraftingRecipeExecutor
    {
        public static CraftingRecipeResult CanCraft(
            RecipeDefinition recipe,
            IInventoryContainer inventory,
            bool hasRequiredResearch)
        {
            if (recipe == null)
            {
                return CraftingRecipeResult.MissingRecipe;
            }

            if (!recipe.IsValid)
            {
                return CraftingRecipeResult.InvalidRecipe;
            }

            if (inventory == null)
            {
                return CraftingRecipeResult.MissingInventory;
            }

            if (recipe.HasRequiredResearch && !hasRequiredResearch)
            {
                return CraftingRecipeResult.MissingResearch;
            }

            if (!inventory.CanRemoveAll(recipe.Inputs))
            {
                return CraftingRecipeResult.MissingIngredients;
            }

            return inventory.CanExchange(recipe.Inputs, recipe.Outputs)
                ? CraftingRecipeResult.Succeeded
                : CraftingRecipeResult.NoOutputCapacity;
        }

        public static CraftingRecipeResult TryCraft(
            RecipeDefinition recipe,
            IInventoryContainer inventory,
            bool hasRequiredResearch)
        {
            CraftingRecipeResult result = CanCraft(recipe, inventory, hasRequiredResearch);
            if (result != CraftingRecipeResult.Succeeded)
            {
                return result;
            }

            return inventory.TryExchange(recipe.Inputs, recipe.Outputs)
                ? CraftingRecipeResult.Succeeded
                : CraftingRecipeResult.MissingIngredients;
        }
    }
}
