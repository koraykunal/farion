using Farion.Gameplay.Inventory;

namespace Farion.Gameplay.Crafting
{
    public static class CraftingRecipeExecutor
    {
        public static CraftingRecipeResult CanCraft(
            RecipeDefinition recipe,
            CraftingStationType stationType,
            IInventoryContainer inventory,
            bool hasRequiredResearch)
        {
            CraftingRecipeResult validation =
                ValidateRecipeAndInventory(
                    recipe,
                    inventory,
                    hasRequiredResearch);
            if (validation != CraftingRecipeResult.Succeeded)
            {
                return validation;
            }

            return recipe.StationType == stationType
                ? ValidateIngredientsAndCapacity(recipe, inventory)
                : CraftingRecipeResult.WrongStationType;
        }

        public static CraftingRecipeResult CanCraft(
            RecipeDefinition recipe,
            IInventoryContainer inventory,
            bool hasRequiredResearch)
        {
            CraftingRecipeResult validation =
                ValidateRecipeAndInventory(
                    recipe,
                    inventory,
                    hasRequiredResearch);
            return validation == CraftingRecipeResult.Succeeded
                ? ValidateIngredientsAndCapacity(recipe, inventory)
                : validation;
        }

        public static CraftingRecipeResult TryCraft(
            RecipeDefinition recipe,
            CraftingStationType stationType,
            IInventoryContainer inventory,
            bool hasRequiredResearch)
        {
            CraftingRecipeResult result = CanCraft(
                recipe,
                stationType,
                inventory,
                hasRequiredResearch);
            if (result != CraftingRecipeResult.Succeeded)
            {
                return result;
            }

            return inventory.TryExchange(recipe.Inputs, recipe.Outputs)
                ? CraftingRecipeResult.Succeeded
                : CraftingRecipeResult.MissingIngredients;
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

        static CraftingRecipeResult ValidateRecipeAndInventory(
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

            return CraftingRecipeResult.Succeeded;
        }

        static CraftingRecipeResult ValidateIngredientsAndCapacity(
            RecipeDefinition recipe,
            IInventoryContainer inventory)
        {
            if (!inventory.CanRemoveAll(recipe.Inputs))
            {
                return CraftingRecipeResult.MissingIngredients;
            }

            return inventory.CanExchange(recipe.Inputs, recipe.Outputs)
                ? CraftingRecipeResult.Succeeded
                : CraftingRecipeResult.NoOutputCapacity;
        }
    }
}
