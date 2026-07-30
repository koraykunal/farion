using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Research;
using UnityEngine;

namespace Farion.Gameplay.Inventory
{
    [CreateAssetMenu(menuName = "Farion/Gameplay/Inventory Item", fileName = "SO_Item")]
    public sealed class InventoryItemDefinition : ScriptableObject
    {
        [SerializeField] string itemId = "item.resource";
        [SerializeField] string displayName = "Resource Item";
        [SerializeField] InventoryStorageMode storageMode = InventoryStorageMode.Stackable;
        [Min(1)]
        [SerializeField] int maxStackSize = 20;
        [SerializeField] InventoryItemCategory category = InventoryItemCategory.Structural;
        [SerializeField] InventoryItemForm form = InventoryItemForm.Raw;
        [SerializeField] TechnologyDomain primaryTechDomain = TechnologyDomain.MaterialsEngineering;
        [SerializeField] bool requiresIdentification;
        [SerializeField] string unidentifiedDisplayName = "Unknown Material";

        [Header("Presentation")]
        [SerializeField] Sprite icon;
        [SerializeField] GameObject worldPrefab;
        [SerializeField] Color accentColor = Color.white;

        public string ItemId => string.IsNullOrWhiteSpace(itemId) ? name : itemId.Trim();
        public DefinitionId DomainId => new(ItemId);
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? ItemId : displayName.Trim();
        public InventoryStorageMode StorageMode => storageMode;
        public int MaxStackSize => storageMode == InventoryStorageMode.UniqueInstance
            ? 1
            : Mathf.Max(1, maxStackSize);
        public InventoryItemCategory Category => category;
        public InventoryItemForm Form => form;
        public TechnologyDomain PrimaryTechDomain => primaryTechDomain;
        public bool RequiresIdentification => requiresIdentification;
        public string UnidentifiedDisplayName => string.IsNullOrWhiteSpace(unidentifiedDisplayName)
            ? "Unknown Material"
            : unidentifiedDisplayName.Trim();
        public Sprite Icon => icon;
        public GameObject WorldPrefab => worldPrefab;
        public Color AccentColor => accentColor;
        public bool HasIcon => icon != null;
        public bool HasWorldPrefab => worldPrefab != null;

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

            maxStackSize = storageMode == InventoryStorageMode.UniqueInstance
                ? 1
                : Mathf.Max(1, maxStackSize);
            if (string.IsNullOrWhiteSpace(unidentifiedDisplayName))
            {
                unidentifiedDisplayName = "Unknown Material";
            }

            accentColor.a = Mathf.Clamp01(accentColor.a);
        }
    }
}
