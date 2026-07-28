using System.Collections.Generic;
using Farion.Gameplay.Crafting;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Inventory;
using UnityEngine;

namespace Farion.Gameplay.Research
{
    [CreateAssetMenu(menuName = "Farion/Gameplay/Research/Research", fileName = "SO_Research")]
    public sealed class ResearchDefinition : ScriptableObject
    {
        [SerializeField] string researchId = "research.id";
        [SerializeField] string displayName = "Research";
        [SerializeField] TechnologyDomain domain = TechnologyDomain.MaterialsEngineering;
        [Min(0)]
        [SerializeField] int tier;
        [SerializeField] List<ItemStackDefinition> requiredItems = new();
        [SerializeField] List<RecipeDefinition> unlockedRecipes = new();
        [SerializeField] List<string> unlockedCapabilityIds = new();

        public string ResearchId => string.IsNullOrWhiteSpace(researchId) ? name : researchId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? ResearchId : displayName.Trim();
        public TechnologyDomain Domain => domain;
        public int Tier => Mathf.Max(0, tier);
        public IReadOnlyList<ItemStackDefinition> RequiredItems => requiredItems;
        public IReadOnlyList<RecipeDefinition> UnlockedRecipes => unlockedRecipes;
        public IReadOnlyList<string> UnlockedCapabilityIds => unlockedCapabilityIds;

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(researchId))
            {
                researchId = name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = researchId;
            }

            tier = Mathf.Max(0, tier);
            requiredItems ??= new List<ItemStackDefinition>();
            unlockedRecipes ??= new List<RecipeDefinition>();
            unlockedCapabilityIds ??= new List<string>();
            for (int i = 0; i < requiredItems.Count; i++)
            {
                requiredItems[i]?.OnValidate();
            }

            for (int i = 0; i < unlockedCapabilityIds.Count; i++)
            {
                unlockedCapabilityIds[i] =
                    DefinitionId.Normalize(unlockedCapabilityIds[i]);
            }
        }
    }
}
