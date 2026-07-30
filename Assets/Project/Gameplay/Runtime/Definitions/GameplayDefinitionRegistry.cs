using System;
using System.Collections.Generic;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Resources;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Farion.Gameplay.Definitions
{
    [CreateAssetMenu(
        menuName = "Farion/Gameplay/Definition Registry",
        fileName = "SO_GameplayDefinitionRegistry")]
    public sealed class GameplayDefinitionRegistry : ScriptableObject
    {
        [SerializeField] List<InventoryItemDefinition> inventoryItems = new();
        [SerializeField] List<ResourceNodeDefinition> resourceNodes = new();

        Dictionary<DefinitionId, InventoryItemDefinition> inventoryItemsById;
        Dictionary<DefinitionId, ResourceNodeDefinition> resourceNodesById;

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

        void OnValidate()
        {
            RemoveNulls(inventoryItems);
            RemoveNulls(resourceNodes);
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

            AddDefinitions(
                inventoryItems,
                inventoryItemsById,
                item => item.ItemId);
            AddDefinitions(
                resourceNodes,
                resourceNodesById,
                node => node.NodeId);
        }

        void ClearLookupCache()
        {
            inventoryItemsById = null;
            resourceNodesById = null;
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
