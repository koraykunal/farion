using System.Collections.Generic;
using UnityEngine;

namespace Farion.Gameplay.Inventory
{
    static class InventoryTransactionPlanner
    {
        public static bool CanRemoveAll(
            IReadOnlyList<InventoryStack> stacks,
            IReadOnlyList<ItemStackDefinition> costs)
        {
            return TryBuildStackMap(costs, out Dictionary<InventoryItemDefinition, int> required) &&
                   ContainsAll(stacks, required);
        }

        public static bool CanExchange(
            IReadOnlyList<InventoryStack> stacks,
            int slotCapacity,
            IReadOnlyList<ItemStackDefinition> inputs,
            IReadOnlyList<ItemStackDefinition> outputs)
        {
            return TryBuildStackMap(inputs, out Dictionary<InventoryItemDefinition, int> inputMap) &&
                   TryBuildStackMap(outputs, out Dictionary<InventoryItemDefinition, int> outputMap) &&
                   CanApplyExchange(stacks, slotCapacity, inputMap, outputMap);
        }

        public static bool CanExchange(
            IReadOnlyList<InventoryStack> stacks,
            int slotCapacity,
            Dictionary<InventoryItemDefinition, int> inputs,
            Dictionary<InventoryItemDefinition, int> outputs)
        {
            return CanApplyExchange(stacks, slotCapacity, inputs, outputs);
        }

        public static bool TryBuildStackMap(
            IReadOnlyList<ItemStackDefinition> definitions,
            out Dictionary<InventoryItemDefinition, int> quantities)
        {
            quantities = new Dictionary<InventoryItemDefinition, int>();
            if (definitions == null)
            {
                return true;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                ItemStackDefinition definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                if (!definition.IsValid)
                {
                    return false;
                }

                quantities.TryGetValue(definition.Item, out int current);
                quantities[definition.Item] = current + definition.Amount;
            }

            return true;
        }

        public static bool ContainsAll(
            IReadOnlyList<InventoryStack> stacks,
            Dictionary<InventoryItemDefinition, int> required)
        {
            foreach (KeyValuePair<InventoryItemDefinition, int> pair in required)
            {
                if (Count(stacks, pair.Key) < pair.Value)
                {
                    return false;
                }
            }

            return true;
        }

        static bool CanApplyExchange(
            IReadOnlyList<InventoryStack> stacks,
            int slotCapacity,
            Dictionary<InventoryItemDefinition, int> inputs,
            Dictionary<InventoryItemDefinition, int> outputs)
        {
            Dictionary<InventoryItemDefinition, int> projected = BuildCurrentQuantityMap(stacks);
            foreach (KeyValuePair<InventoryItemDefinition, int> pair in inputs)
            {
                if (!projected.TryGetValue(pair.Key, out int current) || current < pair.Value)
                {
                    return false;
                }

                int remaining = current - pair.Value;
                if (remaining > 0)
                {
                    projected[pair.Key] = remaining;
                }
                else
                {
                    projected.Remove(pair.Key);
                }
            }

            foreach (KeyValuePair<InventoryItemDefinition, int> pair in outputs)
            {
                projected.TryGetValue(pair.Key, out int current);
                projected[pair.Key] = current + pair.Value;
            }

            return CountProjectedSlots(projected) <= Mathf.Max(1, slotCapacity);
        }

        static int Count(IReadOnlyList<InventoryStack> stacks, InventoryItemDefinition item)
        {
            if (item == null || stacks == null)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < stacks.Count; i++)
            {
                InventoryStack stack = stacks[i];
                if (stack != null && stack.Item == item)
                {
                    total += stack.Quantity;
                }
            }

            return total;
        }

        static Dictionary<InventoryItemDefinition, int> BuildCurrentQuantityMap(IReadOnlyList<InventoryStack> stacks)
        {
            Dictionary<InventoryItemDefinition, int> quantities = new();
            if (stacks == null)
            {
                return quantities;
            }

            for (int i = 0; i < stacks.Count; i++)
            {
                InventoryStack stack = stacks[i];
                if (stack == null || stack.IsEmpty)
                {
                    continue;
                }

                quantities.TryGetValue(stack.Item, out int current);
                quantities[stack.Item] = current + stack.Quantity;
            }

            return quantities;
        }

        static int CountProjectedSlots(Dictionary<InventoryItemDefinition, int> quantities)
        {
            int slots = 0;
            foreach (KeyValuePair<InventoryItemDefinition, int> pair in quantities)
            {
                if (pair.Key == null || pair.Value <= 0)
                {
                    continue;
                }

                slots += Mathf.CeilToInt(pair.Value / (float)pair.Key.MaxStackSize);
            }

            return slots;
        }
    }
}
