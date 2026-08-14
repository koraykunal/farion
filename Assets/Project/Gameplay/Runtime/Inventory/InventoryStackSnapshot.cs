using System;
using Farion.Core.Identity;
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
            this.itemId = IdentifierText.Normalize(itemId);
            this.quantity = Mathf.Max(0, quantity);
        }

        public string ItemId => IdentifierText.Normalize(itemId);
        public int Quantity => Mathf.Max(0, quantity);
        public bool IsValid => !string.IsNullOrEmpty(ItemId) && Quantity > 0;
    }
}
