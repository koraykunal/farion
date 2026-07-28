using System;
using Farion.Gameplay.Domain.Identity;
using UnityEngine;

namespace Farion.Gameplay.Equipment
{
    [CreateAssetMenu(
        menuName = "Farion/Gameplay/Equipment/Slot",
        fileName = "SO_EquipmentSlot")]
    public sealed class EquipmentSlotDefinition : ScriptableObject
    {
        [SerializeField] string slotId = "slot.personal.utility";
        [SerializeField] string displayName = "Utility Slot";
        [SerializeField] string slotTypeId = "slot_type.personal.utility";
        [SerializeField] EquipmentSizeClass maximumSize = EquipmentSizeClass.Compact;

        public string SlotId => DefinitionId.Normalize(slotId);
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? SlotId
            : displayName.Trim();
        public string SlotTypeId => DefinitionId.Normalize(slotTypeId);
        public EquipmentSizeClass MaximumSize => maximumSize;

        public bool IsValid =>
            DefinitionId.TryCreate(SlotId, out _) &&
            DefinitionId.TryCreate(SlotTypeId, out _) &&
            Enum.IsDefined(typeof(EquipmentSizeClass), maximumSize);

        public bool Accepts(EquipmentDefinition equipment)
        {
            return equipment != null &&
                   equipment.IsValid &&
                   IsValid &&
                   equipment.SizeClass <= maximumSize &&
                   DefinitionId.TryCreate(SlotTypeId, out DefinitionId typeId) &&
                   equipment.SupportsSlotType(typeId);
        }

        void OnValidate()
        {
            slotId = DefinitionId.Normalize(slotId);
            slotTypeId = DefinitionId.Normalize(slotTypeId);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = slotId;
            }
            else
            {
                displayName = displayName.Trim();
            }

            if (!Enum.IsDefined(typeof(EquipmentSizeClass), maximumSize))
            {
                maximumSize = EquipmentSizeClass.Compact;
            }
        }
    }
}
