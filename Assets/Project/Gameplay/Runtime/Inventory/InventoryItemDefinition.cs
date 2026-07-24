using UnityEngine;

namespace Farion.Gameplay.Inventory
{
    [CreateAssetMenu(menuName = "Farion/Gameplay/Inventory Item", fileName = "SO_Item")]
    public sealed class InventoryItemDefinition : ScriptableObject
    {
        [SerializeField] string itemId = "item.resource";
        [SerializeField] string displayName = "Resource Item";
        [Min(1)]
        [SerializeField] int maxStackSize = 20;

        public string ItemId => string.IsNullOrWhiteSpace(itemId) ? name : itemId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? ItemId : displayName.Trim();
        public int MaxStackSize => Mathf.Max(1, maxStackSize);

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                itemId = name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = itemId;
            }

            maxStackSize = Mathf.Max(1, maxStackSize);
        }
    }
}
