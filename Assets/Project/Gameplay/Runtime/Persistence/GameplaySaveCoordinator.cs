using System;
using System.Collections.Generic;
using Farion.Core.Physics;
using Farion.Core.Persistence;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Research;
using Farion.Gameplay.Resources;
using Farion.Gameplay.Session;
using Farion.Simulation.World;
using UnityEngine;

namespace Farion.Gameplay.Persistence
{
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class GameplaySaveCoordinator : MonoBehaviour
    {
        [Header("Slot")]
        [SerializeField] string slotName = SaveGameSlotCatalog.DefaultSlotName;
        [SerializeField] bool consumeStartupRequestOnStart = true;

        [Header("Definitions")]
        [SerializeField] GameplayDefinitionRegistry definitions;

        [Header("Runtime Sources")]
        [SerializeField] GravitySimulation gravitySimulation;
        [SerializeField] WorldOriginRebaser originRebaser;
        [SerializeField] PlayerInventory playerInventory;
        [SerializeField] PlayerPossessionController possessionController;
        [SerializeField] FleetProgressionRuntime fleetProgression;
        [SerializeField] List<ResourceDepositRuntimeSpawner> resourceStreamers = new();

        readonly IGameplaySaveParticipant[] saveParticipants =
        {
            new CelestialSimulationSaveParticipant(),
            new WorldOriginSaveParticipant(),
            new PlayerPossessionSaveParticipant(),
            new PlayerInventorySaveParticipant(),
            new PersonalShipCargoSaveParticipant(),
            new FleetProgressionSaveParticipant(),
            new ResourceDepositSaveParticipant()
        };

        SaveGameOperationResult lastSaveResult;
        SaveGameOperationResult lastLoadResult;
        GameplayRuntimeBindings runtimeBindings;

        public string SlotName => SaveGameSlotCatalog.ResolveSlotName(slotName);
        public GameplayRuntimeBindings RuntimeBindings =>
            runtimeBindings ??= new GameplayRuntimeBindings(
                definitions,
                gravitySimulation,
                originRebaser,
                playerInventory,
                possessionController,
                ResolveFleetProgression(),
                resourceStreamers);
        public SaveGameOperationResult LastSaveResult => lastSaveResult;
        public SaveGameOperationResult LastLoadResult => lastLoadResult;

        void Start()
        {
            if (!consumeStartupRequestOnStart)
            {
                return;
            }

            if (!SaveGameStartupRequest.Consume(out SaveGameStartupMode startupMode, out string requestedSlotName))
            {
                return;
            }

            if (startupMode == SaveGameStartupMode.LoadGame)
            {
                Load(requestedSlotName);
            }
        }

        void OnValidate()
        {
            slotName = SaveGameSlotCatalog.ResolveSlotName(slotName);
            fleetProgression ??= GetComponent<FleetProgressionRuntime>();
            resourceStreamers ??= new List<ResourceDepositRuntimeSpawner>();
            resourceStreamers.RemoveAll(streamer => streamer == null);
            runtimeBindings = null;
        }

        [ContextMenu("Save Game")]
        public SaveGameOperationResult Save()
        {
            return Save(SlotName);
        }

        public SaveGameOperationResult Save(string requestedSlotName)
        {
            string resolvedSlotName =
                SaveGameSlotCatalog.ResolveSlotName(requestedSlotName);
            if (!CanCaptureSaveData())
            {
                lastSaveResult = SaveGameOperationResult.Failure(
                    SaveGameOperationStatus.MissingRuntimeReference,
                    SaveGameSlotCatalog.GetSlotPath(resolvedSlotName));
                return lastSaveResult;
            }

            GameplaySaveData saveData = CaptureSaveData();
            string json = JsonUtility.ToJson(saveData, prettyPrint: true);
            lastSaveResult = SaveGameFileService.WriteText(resolvedSlotName, json);
            if (lastSaveResult.Succeeded)
            {
                slotName = resolvedSlotName;
            }

            return lastSaveResult;
        }

        [ContextMenu("Load Game")]
        public SaveGameOperationResult Load()
        {
            return Load(SlotName);
        }

        public SaveGameOperationResult Load(string requestedSlotName)
        {
            string resolvedSlotName = SaveGameSlotCatalog.ResolveSlotName(requestedSlotName);
            if (!TryReadSaveData(
                    resolvedSlotName,
                    useBackup: false,
                    out GameplaySaveData saveData,
                    out SaveGameOperationResult readResult))
            {
                if (!TryReadSaveData(
                        resolvedSlotName,
                        useBackup: true,
                        out saveData,
                        out SaveGameOperationResult backupReadResult))
                {
                    lastLoadResult = readResult.Status == SaveGameOperationStatus.NoSaveFound
                        ? backupReadResult
                        : readResult;
                    return lastLoadResult;
                }

                readResult = backupReadResult;
            }

            if (!CanApplySaveData(saveData))
            {
                lastLoadResult = SaveGameOperationResult.Failure(
                    SaveGameOperationStatus.MissingRuntimeReference,
                    readResult.Path);
                return lastLoadResult;
            }

            GameplaySaveData rollbackData = CanCaptureSaveData() ? CaptureSaveData() : null;
            if (!ApplySaveData(saveData))
            {
                bool rollbackSucceeded = rollbackData != null && ApplySaveData(rollbackData);
                lastLoadResult = SaveGameOperationResult.Failure(
                    rollbackSucceeded
                        ? SaveGameOperationStatus.ApplyFailed
                        : SaveGameOperationStatus.RollbackFailed,
                    readResult.Path);
                return lastLoadResult;
            }

            lastLoadResult = SaveGameOperationResult.Success(readResult.Path);
            slotName = resolvedSlotName;
            return lastLoadResult;
        }

        GameplaySaveData CaptureSaveData()
        {
            GameplaySaveContext context = CreateContext();
            GameplaySaveCapture capture = new(DateTime.UtcNow.ToString("O"));
            for (int i = 0; i < saveParticipants.Length; i++)
            {
                saveParticipants[i].Capture(capture, context);
            }

            return capture.CreateSnapshot();
        }

        bool CanCaptureSaveData()
        {
            GameplaySaveContext context = CreateContext();
            for (int i = 0; i < saveParticipants.Length; i++)
            {
                if (!saveParticipants[i].CanCapture(context))
                {
                    return false;
                }
            }

            return true;
        }

        bool TryReadSaveData(
            string slotName,
            bool useBackup,
            out GameplaySaveData saveData,
            out SaveGameOperationResult result)
        {
            result = useBackup
                ? SaveGameFileService.ReadBackupText(slotName, out string json)
                : SaveGameFileService.ReadText(slotName, out json);
            saveData = null;
            if (!result.Succeeded)
            {
                return false;
            }

            try
            {
                saveData = JsonUtility.FromJson<GameplaySaveData>(json);
            }
            catch (ArgumentException exception)
            {
                result = SaveGameOperationResult.Failure(
                    SaveGameOperationStatus.InvalidPayload,
                    result.Path,
                    exception);
                return false;
            }

            if (saveData == null)
            {
                result = SaveGameOperationResult.Failure(SaveGameOperationStatus.InvalidPayload, result.Path);
                return false;
            }

            if (!saveData.IsSupported)
            {
                result = SaveGameOperationResult.Failure(SaveGameOperationStatus.UnsupportedVersion, result.Path);
                return false;
            }

            if (!GameplaySaveMigration.TryMigrateToCurrent(
                    saveData,
                    CreateContext(),
                    out saveData))
            {
                result = SaveGameOperationResult.Failure(
                    SaveGameOperationStatus.InvalidPayload,
                    result.Path);
                return false;
            }

            return true;
        }

        bool CanApplySaveData(GameplaySaveData saveData)
        {
            if (saveData == null)
            {
                return false;
            }

            GameplaySaveContext context = CreateContext();
            for (int i = 0; i < saveParticipants.Length; i++)
            {
                if (!saveParticipants[i].CanApply(saveData, context))
                {
                    return false;
                }
            }

            return true;
        }

        bool ApplySaveData(GameplaySaveData saveData)
        {
            GameplaySaveContext context = CreateContext();
            for (int i = 0; i < saveParticipants.Length; i++)
            {
                if (!saveParticipants[i].Apply(saveData, context))
                {
                    return false;
                }
            }

            return true;
        }

        GameplaySaveContext CreateContext()
        {
            return new GameplaySaveContext(RuntimeBindings);
        }

        FleetProgressionRuntime ResolveFleetProgression()
        {
            fleetProgression ??= GetComponent<FleetProgressionRuntime>();
            return fleetProgression;
        }

    }
}
