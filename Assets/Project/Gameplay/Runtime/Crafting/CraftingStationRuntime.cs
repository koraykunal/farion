using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Farion.Gameplay.Crafting
{
    [DisallowMultipleComponent]
    public sealed class CraftingStationRuntime : MonoBehaviour
    {
        [SerializeField] CraftingStationType stationType = CraftingStationType.Refinery;
        [SerializeField] List<RecipeDefinition> availableRecipes = new();

        ReadOnlyCollection<RecipeDefinition> readOnlyRecipes;

        public CraftingStationType StationType => stationType;
        public IReadOnlyList<RecipeDefinition> AvailableRecipes =>
            readOnlyRecipes ??= availableRecipes.AsReadOnly();

        void OnValidate()
        {
            availableRecipes ??= new List<RecipeDefinition>();
            availableRecipes.RemoveAll(recipe => recipe == null);
            readOnlyRecipes = null;
        }

        public bool CanOffer(RecipeDefinition recipe)
        {
            return recipe != null &&
                   recipe.IsValid &&
                   recipe.StationType == stationType &&
                   availableRecipes.Contains(recipe);
        }

        public bool TryGetRecipe(string recipeId, out RecipeDefinition recipe)
        {
            recipe = null;
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                return false;
            }

            string normalized = recipeId.Trim();
            for (int i = 0; i < availableRecipes.Count; i++)
            {
                RecipeDefinition candidate = availableRecipes[i];
                if (candidate != null &&
                    string.Equals(
                        candidate.RecipeId,
                        normalized,
                        System.StringComparison.Ordinal))
                {
                    recipe = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
