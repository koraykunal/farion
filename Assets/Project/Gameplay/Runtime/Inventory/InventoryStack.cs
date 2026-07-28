using System;
using UnityEngine;

namespace Farion.Gameplay.Inventory
{
    [Serializable]
    public sealed class InventoryStack
    {
        [SerializeField] InventoryItemDefinition item;
        [Min(0)]
        [SerializeField] int quantity;

        public InventoryItemDefinition Item => item;
        public int Quantity => quantity;
        public bool IsEmpty => item == null || quantity <= 0;
        public int RemainingCapacity => item == null ? 0 : Mathf.Max(0, item.MaxStackSize - quantity);

        public InventoryStack(InventoryItemDefinition item, int quantity)
        {
            this.item = item;
            this.quantity = Mathf.Max(0, quantity);
        }

    }
}
