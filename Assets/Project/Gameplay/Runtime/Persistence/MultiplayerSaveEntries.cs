using System;
using Farion.Gameplay.Inventory;
using UnityEngine;

namespace Farion.Gameplay.Persistence
{
    [Serializable]
    public sealed class MultiplayerPlayerSaveEntry
    {
        [SerializeField] string persistentPlayerId;
        [SerializeField] string displayName;
        [SerializeField] int formationSlot = -1;
        [SerializeField] InventoryContainerSnapshot carriedInventory;

        public MultiplayerPlayerSaveEntry()
        {
        }

        public MultiplayerPlayerSaveEntry(
            string persistentPlayerId,
            string displayName,
            int formationSlot,
            InventoryContainerSnapshot carriedInventory)
        {
            this.persistentPlayerId = persistentPlayerId;
            this.displayName = displayName;
            this.formationSlot = formationSlot;
            this.carriedInventory = carriedInventory;
        }

        public string PersistentPlayerId => persistentPlayerId;
        public string DisplayName => displayName;
        public int FormationSlot => formationSlot;
        public InventoryContainerSnapshot CarriedInventory => carriedInventory;
        public bool IsValid => !string.IsNullOrWhiteSpace(persistentPlayerId);
    }

    [Serializable]
    public sealed class MultiplayerShipCargoSaveEntry
    {
        [SerializeField] string persistentPlayerId;
        [SerializeField] int formationSlot = -1;
        [SerializeField] InventoryContainerSnapshot cargo;

        public MultiplayerShipCargoSaveEntry()
        {
        }

        public MultiplayerShipCargoSaveEntry(
            string persistentPlayerId,
            int formationSlot,
            InventoryContainerSnapshot cargo)
        {
            this.persistentPlayerId = persistentPlayerId;
            this.formationSlot = formationSlot;
            this.cargo = cargo;
        }

        public string PersistentPlayerId => persistentPlayerId;
        public int FormationSlot => formationSlot;
        public InventoryContainerSnapshot Cargo => cargo;
        public bool HasOwner => !string.IsNullOrWhiteSpace(persistentPlayerId);
        public bool IsValid => HasOwner || formationSlot >= 0;
    }
}
