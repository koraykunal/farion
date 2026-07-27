using System;
using System.Collections.Generic;
using Farion.Gameplay.Definitions;
using UnityEngine;

namespace Farion.Gameplay.Inventory
{
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour
    {
        [Header("Capacity")]
        [Min(1)]
        [SerializeField] int slotCapacity = 12;

        [Header("Runtime")]
        [SerializeField] List<InventoryStack> stacks = new();

        public event Action Changed;
        public IReadOnlyList<InventoryStack> Stacks => stacks;
        public int SlotCapacity => slotCapacity;
        public bool IsFull => CountUsedSlots() >= slotCapacity && !HasPartialStackCapacity();

        void OnValidate()
        {
            slotCapacity = Mathf.Max(1, slotCapacity);
            Compact();
        }

        public bool CanAdd(InventoryItemDefinition item, int amount)
        {
            return item != null && amount > 0 && GetAvailableCapacity(item) >= amount;
        }

        public int TryAdd(InventoryItemDefinition item, int amount)
        {
            return TryAddInternal(item, amount, notify: true);
        }

        public bool CanRemove(InventoryItemDefinition item, int amount)
        {
            return item != null && amount > 0 && Count(item) >= amount;
        }

        public int TryRemove(InventoryItemDefinition item, int amount)
        {
            return TryRemoveInternal(item, amount, notify: true);
        }

        public bool CanRemoveAll(IReadOnlyList<ItemStackDefinition> costs)
        {
            return InventoryTransactionPlanner.CanRemoveAll(stacks, costs);
        }

        public bool TryRemoveAll(IReadOnlyList<ItemStackDefinition> costs)
        {
            if (!InventoryTransactionPlanner.TryBuildStackMap(costs, out Dictionary<InventoryItemDefinition, int> required) ||
                !InventoryTransactionPlanner.ContainsAll(stacks, required))
            {
                return false;
            }

            foreach (KeyValuePair<InventoryItemDefinition, int> pair in required)
            {
                TryRemoveInternal(pair.Key, pair.Value, notify: false);
            }

            Compact();
            if (required.Count > 0)
            {
                Changed?.Invoke();
            }

            return true;
        }

        public bool CanExchange(
            IReadOnlyList<ItemStackDefinition> inputs,
            IReadOnlyList<ItemStackDefinition> outputs)
        {
            return InventoryTransactionPlanner.CanExchange(stacks, slotCapacity, inputs, outputs);
        }

        public bool TryExchange(
            IReadOnlyList<ItemStackDefinition> inputs,
            IReadOnlyList<ItemStackDefinition> outputs)
        {
            if (!InventoryTransactionPlanner.TryBuildStackMap(inputs, out Dictionary<InventoryItemDefinition, int> inputMap) ||
                !InventoryTransactionPlanner.TryBuildStackMap(outputs, out Dictionary<InventoryItemDefinition, int> outputMap) ||
                !InventoryTransactionPlanner.CanExchange(stacks, slotCapacity, inputMap, outputMap))
            {
                return false;
            }

            foreach (KeyValuePair<InventoryItemDefinition, int> pair in inputMap)
            {
                TryRemoveInternal(pair.Key, pair.Value, notify: false);
            }

            foreach (KeyValuePair<InventoryItemDefinition, int> pair in outputMap)
            {
                TryAddInternal(pair.Key, pair.Value, notify: false);
            }

            Compact();
            if (inputMap.Count > 0 || outputMap.Count > 0)
            {
                Changed?.Invoke();
            }

            return true;
        }

        int TryAddInternal(InventoryItemDefinition item, int amount, bool notify)
        {
            if (item == null || amount <= 0)
            {
                return 0;
            }

            int remaining = amount;
            for (int i = 0; i < stacks.Count && remaining > 0; i++)
            {
                InventoryStack stack = stacks[i];
                if (stack != null && stack.Item == item)
                {
                    remaining = stack.Add(remaining);
                }
            }

            while (remaining > 0 && CountUsedSlots() < slotCapacity)
            {
                int accepted = Mathf.Min(remaining, item.MaxStackSize);
                stacks.Add(new InventoryStack(item, accepted));
                remaining -= accepted;
            }

            Compact();

            int added = amount - remaining;
            if (notify && added > 0)
            {
                Changed?.Invoke();
            }

            return added;
        }

        int TryRemoveInternal(InventoryItemDefinition item, int amount, bool notify)
        {
            if (item == null || amount <= 0)
            {
                return 0;
            }

            int remaining = amount;
            for (int i = stacks.Count - 1; i >= 0 && remaining > 0; i--)
            {
                InventoryStack stack = stacks[i];
                if (stack == null || stack.Item != item)
                {
                    continue;
                }

                remaining -= stack.Remove(remaining);
            }

            Compact();

            int removed = amount - remaining;
            if (notify && removed > 0)
            {
                Changed?.Invoke();
            }

            return removed;
        }

        public int Count(InventoryItemDefinition item)
        {
            if (item == null)
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

        public int GetAvailableCapacity(InventoryItemDefinition item)
        {
            if (item == null)
            {
                return 0;
            }

            int capacity = 0;
            for (int i = 0; i < stacks.Count; i++)
            {
                InventoryStack stack = stacks[i];
                if (stack != null && stack.Item == item)
                {
                    capacity += stack.RemainingCapacity;
                }
            }

            int freeSlots = Mathf.Max(0, slotCapacity - CountUsedSlots());
            capacity += freeSlots * item.MaxStackSize;
            return capacity;
        }

        public PlayerInventorySnapshot CaptureSnapshot()
        {
            List<InventoryStackSnapshot> snapshotStacks = new(stacks.Count);
            for (int i = 0; i < stacks.Count; i++)
            {
                InventoryStack stack = stacks[i];
                if (stack == null || stack.IsEmpty)
                {
                    continue;
                }

                snapshotStacks.Add(new InventoryStackSnapshot(stack.Item.ItemId, stack.Quantity));
            }

            return new PlayerInventorySnapshot(slotCapacity, snapshotStacks);
        }

        public void ApplySnapshot(PlayerInventorySnapshot snapshot, GameplayDefinitionRegistry definitions)
        {
            stacks.Clear();
            if (snapshot == null || definitions == null)
            {
                Changed?.Invoke();
                return;
            }

            slotCapacity = Mathf.Max(1, snapshot.SlotCapacity);
            IReadOnlyList<InventoryStackSnapshot> snapshotStacks = snapshot.Stacks;
            for (int i = 0; i < snapshotStacks.Count; i++)
            {
                InventoryStackSnapshot stack = snapshotStacks[i];
                if (!stack.IsValid || !definitions.TryGetInventoryItem(stack.ItemId, out InventoryItemDefinition item))
                {
                    continue;
                }

                TryAddInternal(item, stack.Quantity, notify: false);
            }

            Compact();
            Changed?.Invoke();
        }

        public bool CanApplySnapshot(PlayerInventorySnapshot snapshot, GameplayDefinitionRegistry definitions)
        {
            if (snapshot == null || definitions == null)
            {
                return false;
            }

            IReadOnlyList<InventoryStackSnapshot> snapshotStacks = snapshot.Stacks;
            for (int i = 0; i < snapshotStacks.Count; i++)
            {
                InventoryStackSnapshot stack = snapshotStacks[i];
                if (!stack.IsValid || !definitions.TryGetInventoryItem(stack.ItemId, out _))
                {
                    return false;
                }
            }

            return true;
        }

        int CountUsedSlots()
        {
            int used = 0;
            for (int i = 0; i < stacks.Count; i++)
            {
                if (stacks[i] != null && !stacks[i].IsEmpty)
                {
                    used++;
                }
            }

            return used;
        }

        bool HasPartialStackCapacity()
        {
            for (int i = 0; i < stacks.Count; i++)
            {
                InventoryStack stack = stacks[i];
                if (stack != null && !stack.IsEmpty && stack.RemainingCapacity > 0)
                {
                    return true;
                }
            }

            return false;
        }

        void Compact()
        {
            stacks ??= new List<InventoryStack>();
            stacks.RemoveAll(stack => stack == null || stack.IsEmpty);
        }
    }
}
