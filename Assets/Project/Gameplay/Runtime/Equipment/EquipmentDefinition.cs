using System;
using System.Collections.Generic;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Inventory;
using UnityEngine;

namespace Farion.Gameplay.Equipment
{
    [CreateAssetMenu(
        menuName = "Farion/Gameplay/Equipment/Equipment",
        fileName = "SO_Equipment")]
    public sealed class EquipmentDefinition : ScriptableObject
    {
        [SerializeField] InventoryItemDefinition item;
        [SerializeField] EquipmentSizeClass sizeClass = EquipmentSizeClass.Compact;
        [Min(0f)]
        [SerializeField] float baseMassKilograms = 1f;
        [SerializeField] List<string> compatibleSlotTypeIds = new();

        public InventoryItemDefinition Item => item;
        public string EquipmentId => item != null ? item.ItemId : string.Empty;
        public string DisplayName => item != null ? item.DisplayName : name;
        public EquipmentSizeClass SizeClass => sizeClass;
        public float BaseMassKilograms => Mathf.Max(0f, baseMassKilograms);
        public IReadOnlyList<string> CompatibleSlotTypeIds => compatibleSlotTypeIds;

        public bool IsValid
        {
            get
            {
                if (item == null ||
                    item.StorageMode != InventoryStorageMode.UniqueInstance ||
                    !DefinitionId.TryCreate(item.ItemId, out _) ||
                    !Enum.IsDefined(typeof(EquipmentSizeClass), sizeClass) ||
                    float.IsNaN(baseMassKilograms) ||
                    float.IsInfinity(baseMassKilograms) ||
                    baseMassKilograms < 0f ||
                    compatibleSlotTypeIds == null ||
                    compatibleSlotTypeIds.Count == 0)
                {
                    return false;
                }

                HashSet<DefinitionId> uniqueSlotTypes = new();
                for (int i = 0; i < compatibleSlotTypeIds.Count; i++)
                {
                    if (!DefinitionId.TryCreate(
                            compatibleSlotTypeIds[i],
                            out DefinitionId slotTypeId) ||
                        !uniqueSlotTypes.Add(slotTypeId))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public bool SupportsSlotType(DefinitionId slotTypeId)
        {
            if (!slotTypeId.IsValid || compatibleSlotTypeIds == null)
            {
                return false;
            }

            for (int i = 0; i < compatibleSlotTypeIds.Count; i++)
            {
                if (DefinitionId.TryCreate(
                        compatibleSlotTypeIds[i],
                        out DefinitionId compatibleId) &&
                    compatibleId == slotTypeId)
                {
                    return true;
                }
            }

            return false;
        }

        void OnValidate()
        {
            if (!Enum.IsDefined(typeof(EquipmentSizeClass), sizeClass))
            {
                sizeClass = EquipmentSizeClass.Compact;
            }

            if (float.IsNaN(baseMassKilograms) ||
                float.IsInfinity(baseMassKilograms) ||
                baseMassKilograms < 0f)
            {
                baseMassKilograms = 0f;
            }

            compatibleSlotTypeIds ??= new List<string>();
            for (int i = 0; i < compatibleSlotTypeIds.Count; i++)
            {
                compatibleSlotTypeIds[i] =
                    DefinitionId.Normalize(compatibleSlotTypeIds[i]);
            }
        }
    }
}
