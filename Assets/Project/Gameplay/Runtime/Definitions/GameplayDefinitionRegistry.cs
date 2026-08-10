using System;
using System.Collections.Generic;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Processing;
using Farion.Gameplay.Resources;
using Object = UnityEngine.Object;
using UnityEngine;

namespace Farion.Gameplay.Definitions
{
    [CreateAssetMenu(
        menuName = "Farion/Gameplay/Definition Registry",
        fileName = "SO_GameplayDefinitionRegistry")]
    public sealed class GameplayDefinitionRegistry : ScriptableObject
    {
        [SerializeField] List<InventoryItemDefinition> inventoryItems = new();
        [SerializeField] List<ResourceNodeDefinition> resourceNodes = new();
        [SerializeField] List<ProcessingRecipeDefinition> processingRecipes = new();

        Dictionary<DefinitionId, InventoryItemDefinition> inventoryItemsById;
        Dictionary<DefinitionId, ResourceNodeDefinition> resourceNodesById;
        Dictionary<DefinitionId, ProcessingRecipeDefinition> processingRecipesById;

        public bool TryGetInventoryItem(
            string itemId,
            out InventoryItemDefinition item)
        {
            item = null;
            return DefinitionId.TryCreate(itemId, out DefinitionId id) &&
                   TryGetInventoryItem(id, out item);
        }

        public bool TryGetInventoryItem(
            DefinitionId itemId,
            out InventoryItemDefinition item)
        {
            item = null;
            EnsureBuilt();
            return itemId.IsValid &&
                   inventoryItemsById.TryGetValue(itemId, out item);
        }

        public bool TryGetResourceNode(
            string nodeId,
            out ResourceNodeDefinition node)
        {
            node = null;
            return DefinitionId.TryCreate(nodeId, out DefinitionId id) &&
                   TryGetResourceNode(id, out node);
        }

        public bool TryGetResourceNode(
            DefinitionId nodeId,
            out ResourceNodeDefinition node)
        {
            node = null;
            EnsureBuilt();
            return nodeId.IsValid &&
                   resourceNodesById.TryGetValue(nodeId, out node);
        }

        public bool TryGetProcessingRecipe(
            DefinitionId recipeId,
            out ProcessingRecipeDefinition recipe)
        {
            recipe = null;
            EnsureBuilt();
            return recipeId.IsValid &&
                   processingRecipesById.TryGetValue(recipeId, out recipe);
        }

        void OnValidate()
        {
            RemoveNulls(inventoryItems);
            RemoveNulls(resourceNodes);
            RemoveNulls(processingRecipes);
            ClearLookupCache();
        }

        void EnsureBuilt()
        {
            if (inventoryItemsById != null)
            {
                return;
            }

            inventoryItemsById =
                new Dictionary<DefinitionId, InventoryItemDefinition>();
            resourceNodesById =
                new Dictionary<DefinitionId, ResourceNodeDefinition>();
            processingRecipesById =
                new Dictionary<DefinitionId, ProcessingRecipeDefinition>();

            AddDefinitions(
                inventoryItems,
                inventoryItemsById,
                item => item.ItemId);
            AddDefinitions(
                resourceNodes,
                resourceNodesById,
                node => node.NodeId);
            AddDefinitions(
                processingRecipes,
                processingRecipesById,
                recipe => recipe.RecipeId);
        }

        void ClearLookupCache()
        {
            inventoryItemsById = null;
            resourceNodesById = null;
            processingRecipesById = null;
        }

        static void AddDefinitions<T>(
            List<T> definitions,
            Dictionary<DefinitionId, T> lookup,
            Func<T, string> getId)
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

                if (!DefinitionId.TryCreate(
                        getId(definition),
                        out DefinitionId id))
                {
                    throw new InvalidOperationException(
                        $"{typeof(T).Name} '{definition.name}' has an invalid persistent definition id.");
                }

                if (!lookup.TryAdd(id, definition))
                {
                    throw new InvalidOperationException(
                        $"Duplicate {typeof(T).Name} id '{id}' in {nameof(GameplayDefinitionRegistry)}.");
                }
            }
        }

        static void RemoveNulls<T>(List<T> definitions)
            where T : Object
        {
            definitions?.RemoveAll(definition => definition == null);
        }
    }
}
