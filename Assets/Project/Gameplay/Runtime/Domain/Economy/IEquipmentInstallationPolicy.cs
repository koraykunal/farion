using Farion.Gameplay.Domain.Identity;

namespace Farion.Gameplay.Domain.Economy
{
    public interface IEquipmentInstallationPolicy
    {
        bool CanInstall(
            DefinitionId equipmentDefinitionId,
            DefinitionId slotDefinitionId);
    }
}
