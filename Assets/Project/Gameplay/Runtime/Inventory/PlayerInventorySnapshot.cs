using System;
using System.Collections.Generic;
using UnityEngine;

namespace Farion.Gameplay.Inventory
{
    [Serializable]
    public sealed class PlayerInventorySnapshot
    {
        [SerializeField] int slotCapacity;
        [SerializeField] List<InventoryStackSnapshot> stacks = new();

        public PlayerInventorySnapshot(int slotCapacity, IReadOnlyList<InventoryStackSnapshot> stacks)
        {
            this.slotCapacity = Mathf.Max(1, slotCapacity);
            this.stacks = stacks != null ? new List<InventoryStackSnapshot>(stacks) : new List<InventoryStackSnapshot>();
        }

        public int SlotCapacity => Mathf.Max(1, slotCapacity);
        public IReadOnlyList<InventoryStackSnapshot> Stacks => stacks;
    }
}
