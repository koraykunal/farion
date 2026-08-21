using System;
using Farion.Core.Persistence;
using Farion.Gameplay.Domain.Systems;
using Farion.Gameplay.Interaction;
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
        [SerializeField] TransformPoseSnapshot explorerPose;
        [SerializeField] bool hasExplorerPose;
        [SerializeField] PlayerPossessionMode possessionMode =
            PlayerPossessionMode.OnFoot;

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
        public TransformPoseSnapshot ExplorerPose => explorerPose;
        public bool HasExplorerPose => hasExplorerPose;
        public PlayerPossessionMode PossessionMode => possessionMode;
        public bool IsValid => !string.IsNullOrWhiteSpace(persistentPlayerId);

        public void SetExplorerPose(TransformPoseSnapshot pose)
        {
            explorerPose = pose;
            hasExplorerPose = true;
        }

        public void SetPossessionMode(PlayerPossessionMode mode)
        {
            possessionMode = mode;
        }

        public void ShiftPose(Vector3 originOffset)
        {
            if (hasExplorerPose)
            {
                explorerPose = explorerPose.Translated(-originOffset);
            }
        }
    }

    [Serializable]
    public sealed class MultiplayerShipSaveEntry
    {
        [SerializeField] string persistentPlayerId;
        [SerializeField] int formationSlot = -1;
        [SerializeField] InventoryContainerSnapshot cargo;
        [SerializeField] ResourcePool fuel;
        [SerializeField] ResourcePool hull;
        [SerializeField] TransformPoseSnapshot shipPose;
        [SerializeField] bool hasShipPose;

        public MultiplayerShipSaveEntry()
        {
        }

        public MultiplayerShipSaveEntry(
            string persistentPlayerId,
            int formationSlot,
            InventoryContainerSnapshot cargo,
            ResourcePool fuel,
            ResourcePool hull)
        {
            this.persistentPlayerId = persistentPlayerId;
            this.formationSlot = formationSlot;
            this.cargo = cargo;
            this.fuel = fuel;
            this.hull = hull;
        }

        public string PersistentPlayerId => persistentPlayerId;
        public int FormationSlot => formationSlot;
        public InventoryContainerSnapshot Cargo => cargo;
        public ResourcePool Fuel => fuel;
        public bool HasFuel => fuel.Capacity > 0f;
        public ResourcePool Hull => hull;
        public bool HasHull => hull.Capacity > 0f;
        public bool HasOwner => !string.IsNullOrWhiteSpace(persistentPlayerId);
        public bool IsValid => HasOwner || formationSlot >= 0;
        public TransformPoseSnapshot ShipPose => shipPose;
        public bool HasShipPose => hasShipPose;

        public void SetShipPose(TransformPoseSnapshot pose)
        {
            shipPose = pose;
            hasShipPose = true;
        }

        public void ShiftPose(Vector3 originOffset)
        {
            if (hasShipPose)
            {
                shipPose = shipPose.Translated(-originOffset);
            }
        }
    }
}
