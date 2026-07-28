namespace Farion.Gameplay.Crafting
{
    public enum CraftingRecipeResult
    {
        Succeeded = 0,
        MissingRecipe = 1,
        InvalidRecipe = 2,
        MissingInventory = 3,
        MissingResearch = 4,
        MissingIngredients = 5,
        NoOutputCapacity = 6,
        UnauthorizedInventory = 7
    }
}
