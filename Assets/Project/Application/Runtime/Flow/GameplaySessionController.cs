using Farion.App.Commands;
using Farion.Core.Persistence;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Persistence;
using Farion.Gameplay.Session;
using UnityEngine;

namespace Farion.App.Flow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameplaySaveCoordinator))]
    public sealed class GameplaySessionController : MonoBehaviour
    {
        const string DefaultLocalPlayerId = "player.local";

        [Header("Identity")]
        [SerializeField] string localPlayerId = DefaultLocalPlayerId;

        [Header("Flow")]
        [SerializeField] GameFlowSettings flowSettings;
        [SerializeField] GameplaySaveCoordinator saveCoordinator;

        GameplaySessionRuntime runtime;
        GameplayCommandService commands;

        public PersistentEntityId LocalPlayerId =>
            PersistentEntityId.TryCreate(localPlayerId, out PersistentEntityId id)
                ? id
                : PersistentEntityId.None;
        public GameplaySessionRuntime Runtime =>
            TryGetRuntime(out GameplaySessionRuntime currentRuntime)
                ? currentRuntime
                : null;
        public GameplayCommandService Commands =>
            TryGetRuntime(out GameplaySessionRuntime currentRuntime)
                ? ResolveCommands(currentRuntime)
                : null;
        public string SaveSlotName =>
            saveCoordinator != null
                ? saveCoordinator.SlotName
                : SaveGameSlotCatalog.DefaultSlotName;

        public SaveGameOperationResult Save()
        {
            ResolveReferences();
            return saveCoordinator != null && TryGetRuntime(out _)
                ? saveCoordinator.Save()
                : SaveGameOperationResult.Failure(
                    SaveGameOperationStatus.MissingRuntimeReference,
                    string.Empty);
        }

        public SaveGameOperationResult Save(string slotName)
        {
            ResolveReferences();
            return saveCoordinator != null && TryGetRuntime(out _)
                ? saveCoordinator.Save(slotName)
                : SaveGameOperationResult.Failure(
                    SaveGameOperationStatus.MissingRuntimeReference,
                    string.Empty);
        }

        public bool ExitToMainMenu()
        {
            return ExitToMainMenuAsync() != null;
        }

        public AsyncOperation ExitToMainMenuAsync()
        {
            return GameFlowService.LoadMainMenuAsync(flowSettings);
        }

        public void Quit()
        {
            GameFlowService.Quit();
        }

        void Awake()
        {
            ResolveReferences();
            TryGetRuntime(out _);
        }

        void OnValidate()
        {
            localPlayerId = string.IsNullOrWhiteSpace(localPlayerId)
                ? DefaultLocalPlayerId
                : localPlayerId.Trim();
            runtime = null;
            commands = null;
            ResolveReferences();
        }

        public bool TryGetRuntime(out GameplaySessionRuntime currentRuntime)
        {
            ResolveReferences();
            if (saveCoordinator == null)
            {
                currentRuntime = null;
                return false;
            }

            if (runtime == null ||
                !runtime.Matches(
                    localPlayerId,
                    saveCoordinator.RuntimeBindings))
            {
                GameplaySessionRuntime.TryCreate(
                    localPlayerId,
                    saveCoordinator.RuntimeBindings,
                    out runtime);
            }

            currentRuntime = runtime;
            if (currentRuntime != null)
            {
                GameplayCommandService currentCommands =
                    ResolveCommands(currentRuntime);
                currentRuntime.Possession.ExplorerInteractionRaycaster
                    ?.SetCommandGateway(currentCommands);
            }

            return currentRuntime != null;
        }

        GameplayCommandService ResolveCommands(
            GameplaySessionRuntime currentRuntime)
        {
            if (currentRuntime == null)
            {
                commands = null;
                return null;
            }

            if (commands == null || !commands.Matches(currentRuntime))
            {
                commands = new GameplayCommandService(currentRuntime);
            }

            return commands;
        }

        void ResolveReferences()
        {
            saveCoordinator ??= GetComponent<GameplaySaveCoordinator>();
        }
    }
}
