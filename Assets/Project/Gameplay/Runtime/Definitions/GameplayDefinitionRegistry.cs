using System;
using System.Collections.Generic;
using Farion.Gameplay.Crafting;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Research;
using Farion.Gameplay.Resources;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Farion.Gameplay.Definitions
{
    [CreateAssetMenu(menuName = "Farion/Gameplay/Definition Registry", fileName = "SO_GameplayDefinitionRegistry")]
    public sealed class GameplayDefinitionRegistry : ScriptableObject
    {
        [SerializeField] List<InventoryItemDefinition> inventoryItems = new();
        [SerializeField] List<ResourceNodeDefinition> resourceNodes = new();
        [SerializeField] List<RecipeDefinition> recipes = new();
        [SerializeField] List<ResearchDefinition> research = new();

        Dictionary<string, InventoryItemDefinition> inventoryItemsById;
        Dictionary<string, ResourceNodeDefinition> resourceNodesById;
        Dictionary<string, RecipeDefinition> recipesById;
        Dictionary<string, ResearchDefinition> researchById;

        public bool TryGetInventoryItem(string itemId, out InventoryItemDefinition item)
        {
            EnsureBuilt();
            return inventoryItemsById.TryGetValue(NormalizeId(itemId), out item);
        }

        public bool TryGetResourceNode(string nodeId, out ResourceNodeDefinition node)
        {
            EnsureBuilt();
            return resourceNodesById.TryGetValue(NormalizeId(nodeId), out node);
        }

        public bool TryGetRecipe(string recipeId, out RecipeDefinition recipe)
        {
            EnsureBuilt();
            return recipesById.TryGetValue(NormalizeId(recipeId), out recipe);
        }

        public bool TryGetResearch(string researchId, out ResearchDefinition definition)
        {
            EnsureBuilt();
            return researchById.TryGetValue(NormalizeId(researchId), out definition);
        }

        void OnValidate()
        {
            RemoveNulls(inventoryItems);
            RemoveNulls(resourceNodes);
            RemoveNulls(recipes);
            RemoveNulls(research);
            ClearLookupCache();
        }

        void EnsureBuilt()
        {
            if (inventoryItemsById != null)
            {
                return;
            }

            inventoryItemsById = new Dictionary<string, InventoryItemDefinition>();
            resourceNodesById = new Dictionary<string, ResourceNodeDefinition>();
            recipesById = new Dictionary<string, RecipeDefinition>();
            researchById = new Dictionary<string, ResearchDefinition>();

            AddDefinitions(inventoryItems, inventoryItemsById, item => item.ItemId);
            AddDefinitions(resourceNodes, resourceNodesById, node => node.NodeId);
            AddDefinitions(recipes, recipesById, recipe => recipe.RecipeId);
            AddDefinitions(research, researchById, definition => definition.ResearchId);
        }

        void ClearLookupCache()
        {
            inventoryItemsById = null;
            resourceNodesById = null;
            recipesById = null;
            researchById = null;
        }

        static void AddDefinitions<T>(
            List<T> definitions,
            Dictionary<string, T> lookup,
            System.Func<T, string> getId)
            where T : Object
        {
            if (definitions == null)
            {
                return;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                T definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                string id = NormalizeId(getId(definition));
                if (string.IsNullOrEmpty(id))
                {
                    throw new InvalidOperationException(
                        $"{typeof(T).Name} '{definition.name}' has an empty persistent definition id.");
                }

                if (!lookup.TryAdd(id, definition))
                {
                    throw new InvalidOperationException(
                        $"Duplicate {typeof(T).Name} id '{id}' in {nameof(GameplayDefinitionRegistry)}.");
                }
            }
        }

        static string NormalizeId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        }

        static void RemoveNulls<T>(List<T> definitions)
            where T : Object
        {
            definitions?.RemoveAll(definition => definition == null);
        }
    }
}
