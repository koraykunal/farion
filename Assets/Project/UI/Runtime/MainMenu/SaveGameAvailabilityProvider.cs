using Farion.Core.Persistence;
using UnityEngine;

namespace Farion.UI.MainMenu
{
    public abstract class SaveGameAvailabilityProvider : MonoBehaviour
    {
        public virtual string SlotName => SaveGameSlotCatalog.DefaultSlotName;
        public abstract bool HasSaveGame { get; }
        public virtual bool HasAnySaveData => HasSaveGame;
    }
}
