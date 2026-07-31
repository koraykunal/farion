using System.Collections.Generic;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Inventory;
using UnityEngine;

namespace Farion.Gameplay.Processing
{
    [CreateAssetMenu(
        menuName = "Farion/Gameplay/Processing Recipe",
        fileName = "SO_ProcessRecipe")]
    public sealed class ProcessingRecipeDefinition : ScriptableObject
    {
        [SerializeField] string recipeId = "process.recipe";
        [SerializeField] string displayName = "Processing Recipe";
        [SerializeField] List<ItemStackDefinition> inputs = new();
        [SerializeField] List<ItemStackDefinition> outputs = new();

        public string RecipeId => recipeId?.Trim();
        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
        public IReadOnlyList<ItemStackDefinition> Inputs => inputs;
        public IReadOnlyList<ItemStackDefinition> Outputs => outputs;
        public bool IsValid =>
            DefinitionId.TryCreate(RecipeId, out _) &&
            HasValidEntries(inputs) &&
            HasValidEntries(outputs);

        void OnValidate()
        {
            recipeId = RecipeId;
            displayName = DisplayName;
            inputs ??= new List<ItemStackDefinition>();
            outputs ??= new List<ItemStackDefinition>();
            ValidateEntries(inputs);
            ValidateEntries(outputs);
        }

        static bool HasValidEntries(IReadOnlyList<ItemStackDefinition> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] == null || !entries[i].IsValid)
                {
                    return false;
                }
            }

            return true;
        }

        static void ValidateEntries(List<ItemStackDefinition> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i]?.OnValidate();
            }
        }
    }
}
