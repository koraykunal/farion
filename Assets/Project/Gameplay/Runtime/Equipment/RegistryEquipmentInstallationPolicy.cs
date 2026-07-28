using System;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Domain.Economy;
using Farion.Gameplay.Domain.Identity;

namespace Farion.Gameplay.Equipment
{
    public sealed class RegistryEquipmentInstallationPolicy :
        IEquipmentInstallationPolicy
    {
        readonly GameplayDefinitionRegistry definitions;

        public RegistryEquipmentInstallationPolicy(
            GameplayDefinitionRegistry definitions)
        {
            this.definitions = definitions != null
                ? definitions
                : throw new ArgumentNullException(nameof(definitions));
        }

        public bool CanInstall(
            DefinitionId equipmentDefinitionId,
            DefinitionId slotDefinitionId)
        {
            return definitions.TryGetEquipment(
                       equipmentDefinitionId,
                       out EquipmentDefinition equipment) &&
                   definitions.TryGetEquipmentSlot(
                       slotDefinitionId,
                       out EquipmentSlotDefinition slot) &&
                   slot.Accepts(equipment);
        }
    }
}
