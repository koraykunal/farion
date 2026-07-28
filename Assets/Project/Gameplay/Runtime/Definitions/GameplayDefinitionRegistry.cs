using System;
using System.Collections.Generic;
using Farion.Gameplay.Crafting;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Equipment;
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
        [SerializeField] List<EquipmentDefinition> equipment = new();
        [SerializeField] List<EquipmentSlotDefinition> equipmentSlots = new();

        Dictionary<DefinitionId, InventoryItemDefinition> inventoryItemsById;
        Dictionary<DefinitionId, ResourceNodeDefinition> resourceNodesById;
        Dictionary<DefinitionId, RecipeDefinition> recipesById;
        Dictionary<DefinitionId, ResearchDefinition> researchById;
        Dictionary<DefinitionId, EquipmentDefinition> equipmentById;
        Dictionary<DefinitionId, EquipmentSlotDefinition> equipmentSlotsById;

        public bool TryGetInventoryItem(string itemId, out InventoryItemDefinition item)
        {
            item = null;
            return DefinitionId.TryCreate(itemId, out DefinitionId definitionId) &&
                   TryGetInventoryItem(definitionId, out item);
        }

        public bool TryGetInventoryItem(
            DefinitionId itemId,
            out InventoryItemDefinition item)
        {
            item = null;
            EnsureBuilt();
            return itemId.IsValid && inventoryItemsById.TryGetValue(itemId, out item);
        }

        public bool TryGetResourceNode(string nodeId, out ResourceNodeDefinition node)
        {
            node = null;
            return DefinitionId.TryCreate(nodeId, out DefinitionId definitionId) &&
                   TryGetResourceNode(definitionId, out node);
        }

        public bool TryGetResourceNode(
            DefinitionId nodeId,
            out ResourceNodeDefinition node)
        {
            node = null;
            EnsureBuilt();
            return nodeId.IsValid && resourceNodesById.TryGetValue(nodeId, out node);
        }

        public bool TryGetRecipe(string recipeId, out RecipeDefinition recipe)
        {
            recipe = null;
            return DefinitionId.TryCreate(recipeId, out DefinitionId definitionId) &&
                   TryGetRecipe(definitionId, out recipe);
        }

        public bool TryGetRecipe(DefinitionId recipeId, out RecipeDefinition recipe)
        {
            recipe = null;
            EnsureBuilt();
            return recipeId.IsValid && recipesById.TryGetValue(recipeId, out recipe);
        }

        public bool TryGetResearch(string researchId, out ResearchDefinition definition)
        {
            definition = null;
            return DefinitionId.TryCreate(researchId, out DefinitionId definitionId) &&
                   TryGetResearch(definitionId, out definition);
        }

        public bool TryGetResearch(
            DefinitionId researchId,
            out ResearchDefinition definition)
        {
            definition = null;
            EnsureBuilt();
            return researchId.IsValid && researchById.TryGetValue(researchId, out definition);
        }

        public bool TryGetEquipment(
            string equipmentId,
            out EquipmentDefinition definition)
        {
            definition = null;
            return DefinitionId.TryCreate(
                       equipmentId,
                       out DefinitionId definitionId) &&
                   TryGetEquipment(definitionId, out definition);
        }

        public bool TryGetEquipment(
            DefinitionId equipmentId,
            out EquipmentDefinition definition)
        {
            definition = null;
            EnsureBuilt();
            return equipmentId.IsValid &&
                   equipmentById.TryGetValue(equipmentId, out definition);
        }

        public bool TryGetEquipmentSlot(
            string slotId,
            out EquipmentSlotDefinition definition)
        {
            definition = null;
            return DefinitionId.TryCreate(slotId, out DefinitionId definitionId) &&
                   TryGetEquipmentSlot(definitionId, out definition);
        }

        public bool TryGetEquipmentSlot(
            DefinitionId slotId,
            out EquipmentSlotDefinition definition)
        {
            definition = null;
            EnsureBuilt();
            return slotId.IsValid &&
                   equipmentSlotsById.TryGetValue(slotId, out definition);
        }

        void OnValidate()
        {
            RemoveNulls(inventoryItems);
            RemoveNulls(resourceNodes);
            RemoveNulls(recipes);
            RemoveNulls(research);
            RemoveNulls(equipment);
            RemoveNulls(equipmentSlots);
            ClearLookupCache();
        }

        void EnsureBuilt()
        {
            if (inventoryItemsById != null)
            {
                return;
            }

            inventoryItemsById = new Dictionary<DefinitionId, InventoryItemDefinition>();
            resourceNodesById = new Dictionary<DefinitionId, ResourceNodeDefinition>();
            recipesById = new Dictionary<DefinitionId, RecipeDefinition>();
            researchById = new Dictionary<DefinitionId, ResearchDefinition>();
            equipmentById = new Dictionary<DefinitionId, EquipmentDefinition>();
            equipmentSlotsById =
                new Dictionary<DefinitionId, EquipmentSlotDefinition>();

            AddDefinitions(inventoryItems, inventoryItemsById, item => item.ItemId);
            AddDefinitions(resourceNodes, resourceNodesById, node => node.NodeId);
            AddDefinitions(recipes, recipesById, recipe => recipe.RecipeId);
            AddDefinitions(research, researchById, definition => definition.ResearchId);
            AddDefinitions(equipment, equipmentById, definition => definition.EquipmentId);
            AddDefinitions(
                equipmentSlots,
                equipmentSlotsById,
                definition => definition.SlotId);
        }

        void ClearLookupCache()
        {
            inventoryItemsById = null;
            resourceNodesById = null;
            recipesById = null;
            researchById = null;
            equipmentById = null;
            equipmentSlotsById = null;
        }

        static void AddDefinitions<T>(
            List<T> definitions,
            Dictionary<DefinitionId, T> lookup,
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

                string rawId = getId(definition);
                if (!DefinitionId.TryCreate(rawId, out DefinitionId id))
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
