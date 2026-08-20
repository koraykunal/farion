using System.Collections.Generic;
using Farion.Core.Persistence;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Persistence;
using Farion.Multiplayer.Spawning;
using FishNet.Managing;
using UnityEngine;

namespace Farion.Multiplayer.Session
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class MultiplayerSaveBridge : MonoBehaviour, IMultiplayerSaveSource
    {
        [SerializeField] NetworkManager networkManager;
        [SerializeField] MultiplayerPlayerSpawner playerSpawner;

        GameplaySaveCoordinator coordinator;
        string slotName = SaveGameSlotCatalog.DefaultSlotName;

        public string SlotName => slotName;

        public bool CanSave => networkManager != null &&
            networkManager.IsServerStarted &&
            coordinator != null;

        void Awake()
        {
            networkManager ??= GetComponent<NetworkManager>();
            playerSpawner ??= GetComponent<MultiplayerPlayerSpawner>();
        }

        public void SetSlot(string requestedSlotName)
        {
            slotName = SaveGameSlotCatalog.ResolveSlotName(requestedSlotName);
        }

        public void BindZone(GameplaySaveCoordinator zoneCoordinator)
        {
            if (coordinator == zoneCoordinator)
            {
                return;
            }

            coordinator?.SetMultiplayerSource(null);
            coordinator = zoneCoordinator;
            coordinator?.SetMultiplayerSource(this);
        }

        public void UnbindZone()
        {
            coordinator?.SetMultiplayerSource(null);
            coordinator = null;
        }

        public SaveGameOperationResult Save(string requestedSlotName)
        {
            return CanSave
                ? coordinator.Save(requestedSlotName)
                : SaveGameOperationResult.Failure(
                    SaveGameOperationStatus.MissingRuntimeReference,
                    SaveGameSlotCatalog.GetSlotPath(
                        SaveGameSlotCatalog.ResolveSlotName(requestedSlotName)));
        }

        public SaveGameOperationResult Load(string requestedSlotName)
        {
            return CanSave
                ? coordinator.Load(requestedSlotName)
                : SaveGameOperationResult.Failure(
                    SaveGameOperationStatus.MissingRuntimeReference,
                    SaveGameSlotCatalog.GetSlotPath(
                        SaveGameSlotCatalog.ResolveSlotName(requestedSlotName)));
        }

        public void Capture(
            List<MultiplayerPlayerSaveEntry> players,
            List<MultiplayerShipCargoSaveEntry> shipCargo)
        {
            playerSpawner?.CaptureState(players, shipCargo);
        }

        public void Apply(
            IReadOnlyList<MultiplayerPlayerSaveEntry> players,
            IReadOnlyList<MultiplayerShipCargoSaveEntry> shipCargo,
            GameplayDefinitionRegistry definitions)
        {
            if (playerSpawner == null)
            {
                return;
            }

            playerSpawner.LoadRestoredState(players, definitions);
            playerSpawner.LoadRestoredShipCargo(shipCargo);
        }
    }
}
