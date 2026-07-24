using System.Collections.Generic;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Research;
using UnityEngine;

namespace Farion.Gameplay.Crafting
{
    [CreateAssetMenu(menuName = "Farion/Gameplay/Crafting/Recipe", fileName = "SO_Recipe")]
    public sealed class RecipeDefinition : ScriptableObject
    {
        [SerializeField] string recipeId = "recipe.id";
        [SerializeField] string displayName = "Recipe";
        [SerializeField] CraftingStationType stationType = CraftingStationType.Refinery;
        [Min(0f)]
        [SerializeField] float processingDurationSeconds;
        [SerializeField] ResearchDefinition requiredResearch;
        [SerializeField] List<ItemStackDefinition> inputs = new();
        [SerializeField] List<ItemStackDefinition> outputs = new();

        public string RecipeId => string.IsNullOrWhiteSpace(recipeId) ? name : recipeId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? RecipeId : displayName.Trim();
        public CraftingStationType StationType => stationType;
        public float ProcessingDurationSeconds => Mathf.Max(0f, processingDurationSeconds);
        public ResearchDefinition RequiredResearch => requiredResearch;
        public IReadOnlyList<ItemStackDefinition> Inputs => inputs;
        public IReadOnlyList<ItemStackDefinition> Outputs => outputs;
        public bool HasRequiredResearch => requiredResearch != null;
        public bool IsValid => outputs != null && outputs.Exists(output => output != null && output.IsValid);

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                recipeId = name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = recipeId;
            }

            processingDurationSeconds = Mathf.Max(0f, processingDurationSeconds);
            inputs ??= new List<ItemStackDefinition>();
            outputs ??= new List<ItemStackDefinition>();
            ValidateStacks(inputs);
            ValidateStacks(outputs);
        }

        static void ValidateStacks(List<ItemStackDefinition> stacks)
        {
            if (stacks == null)
            {
                return;
            }

            for (int i = 0; i < stacks.Count; i++)
            {
                stacks[i]?.OnValidate();
            }
        }
    }
}
