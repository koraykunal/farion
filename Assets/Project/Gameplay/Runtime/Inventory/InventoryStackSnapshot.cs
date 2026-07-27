using System;
using UnityEngine;

namespace Farion.Gameplay.Inventory
{
    [Serializable]
    public struct InventoryStackSnapshot
    {
        [SerializeField] string itemId;
        [SerializeField] int quantity;

        public InventoryStackSnapshot(string itemId, int quantity)
        {
            this.itemId = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
            this.quantity = Mathf.Max(0, quantity);
        }

        public string ItemId => string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
        public int Quantity => Mathf.Max(0, quantity);
        public bool IsValid => !string.IsNullOrEmpty(ItemId) && Quantity > 0;
    }
}
