using Farion.Core.Persistence;
using UnityEngine;

namespace Farion.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class LocalSaveGameAvailabilityProvider : SaveGameAvailabilityProvider
    {
        [SerializeField] string slotName = SaveGameSlotCatalog.DefaultSlotName;

        public override string SlotName => SaveGameSlotCatalog.ResolveSlotName(slotName);
        public override bool HasSaveGame => HasSupportedSaveGame();

        void OnValidate()
        {
            slotName = SaveGameSlotCatalog.ResolveSlotName(slotName);
        }

        bool HasSupportedSaveGame()
        {
            if (SaveGameFileService.ReadText(slotName, out string payload).Succeeded &&
                SaveGameSchema.PayloadLooksSupported(payload))
            {
                return true;
            }

            return SaveGameFileService.ReadBackupText(slotName, out payload).Succeeded &&
                   SaveGameSchema.PayloadLooksSupported(payload);
        }
    }
}
