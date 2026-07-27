using Farion.Core.Persistence;
using Farion.Gameplay.Persistence;
using UnityEngine;

namespace Farion.App.Flow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameplaySaveCoordinator))]
    public sealed class GameplaySessionController : MonoBehaviour
    {
        [SerializeField] GameFlowSettings flowSettings;
        [SerializeField] GameplaySaveCoordinator saveCoordinator;

        public SaveGameOperationResult Save()
        {
            ResolveReferences();
            return saveCoordinator != null
                ? saveCoordinator.Save()
                : SaveGameOperationResult.Failure(
                    SaveGameOperationStatus.MissingRuntimeReference,
                    string.Empty);
        }

        public bool ExitToMainMenu()
        {
            return GameFlowService.LoadMainMenuAsync(flowSettings) != null;
        }

        public void Quit()
        {
            GameFlowService.Quit();
        }

        void Awake()
        {
            ResolveReferences();
        }

        void OnValidate()
        {
            ResolveReferences();
        }

        void ResolveReferences()
        {
            saveCoordinator ??= GetComponent<GameplaySaveCoordinator>();
        }
    }
}
