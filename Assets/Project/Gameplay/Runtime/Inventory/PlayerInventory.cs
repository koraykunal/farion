using System;
using System.Collections.Generic;
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
            if (added > 0)
            {
                Changed?.Invoke();
            }

            return added;
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
