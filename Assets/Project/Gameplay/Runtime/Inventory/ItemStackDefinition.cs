using System;
using UnityEngine;

namespace Farion.Gameplay.Inventory
{
    [Serializable]
    public sealed class ItemStackDefinition
    {
        [SerializeField] InventoryItemDefinition item;
        [Min(1)]
        [SerializeField] int amount = 1;

        public InventoryItemDefinition Item => item;
        public int Amount => Mathf.Max(1, amount);
        public bool IsValid => item != null && Amount > 0;

        public void OnValidate()
        {
            amount = Mathf.Max(1, amount);
        }
    }
}
