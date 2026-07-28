using Farion.Core.Persistence;
using UnityEngine;

namespace Farion.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class LocalSaveGameAvailabilityProvider : SaveGameAvailabilityProvider
    {
        string mostRecentSlotName = SaveGameSlotCatalog.DefaultSlotName;

        public override string SlotName
        {
            get
            {
                Refresh();
                return mostRecentSlotName;
            }
        }

        public override bool HasSaveGame
        {
            get
            {
                return Refresh();
            }
        }

        public override bool HasAnySaveData
        {
            get
            {
                var summaries = SaveGameSlotService.GetPlayerSlotSummaries();
                for (int i = 0; i < summaries.Count; i++)
                {
                    if (summaries[i].HasData)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        bool Refresh()
        {
            if (!SaveGameSlotService.TryGetMostRecentLoadable(
                    out SaveGameSlotSummary summary))
            {
                mostRecentSlotName = SaveGameSlotCatalog.DefaultSlotName;
                return false;
            }

            mostRecentSlotName = summary.SlotName;
            return true;
        }
    }
}
