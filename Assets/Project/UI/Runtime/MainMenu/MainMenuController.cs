using System.Collections;
using Farion.App.Flow;
using Farion.Core.Persistence;
using UnityEngine;

namespace Farion.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Scene Flow")]
        [SerializeField] GameFlowSettings flowSettings;
        [SerializeField] SaveGameAvailabilityProvider saveGameAvailability;

        [Header("Panels")]
        [SerializeField] MainMenuPanelSwitcher panelSwitcher;

        [Header("Buttons")]
        [SerializeField] MainMenuButton continueButton;
        [SerializeField] MainMenuButton loadGameButton;

        Coroutine loadRoutine;

        public bool HasSaveGame => saveGameAvailability != null && saveGameAvailability.HasSaveGame;

        void Awake()
        {
            ResolveReferences();
            ApplySaveAvailability();
            panelSwitcher?.ShowMain();
        }

        public void Handle(MainMenuAction action)
        {
            switch (action)
            {
                case MainMenuAction.Continue:
                    if (HasSaveGame)
                    {
                        StartGameplayLoad(ResolveContinueSceneName(), SaveGameStartupMode.LoadGame, ResolveSaveSlotName());
                    }
                    break;
                case MainMenuAction.NewGame:
                    StartGameplayLoad(ResolveNewGameSceneName(), SaveGameStartupMode.NewGame);
                    break;
                case MainMenuAction.LoadGame:
                    if (HasSaveGame)
                    {
                        StartGameplayLoad(ResolveLoadGameSceneName(), SaveGameStartupMode.LoadGame, ResolveSaveSlotName());
                    }
                    break;
                case MainMenuAction.Settings:
                    panelSwitcher?.ShowSettings();
                    break;
                case MainMenuAction.Credits:
                    panelSwitcher?.ShowCredits();
                    break;
                case MainMenuAction.Exit:
                    panelSwitcher?.ShowConfirmDialog();
                    break;
                case MainMenuAction.Back:
                case MainMenuAction.CancelExit:
                    panelSwitcher?.ShowMain();
                    break;
                case MainMenuAction.ConfirmExit:
                    GameFlowService.Quit();
                    break;
            }
        }

        void StartGameplayLoad(
            string sceneName,
            SaveGameStartupMode startupMode,
            string requestedSlotName = null)
        {
            if (loadRoutine != null || string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            loadRoutine = StartCoroutine(LoadSceneRoutine(sceneName, startupMode, requestedSlotName));
        }

        IEnumerator LoadSceneRoutine(
            string sceneName,
            SaveGameStartupMode startupMode,
            string requestedSlotName)
        {
            panelSwitcher?.ShowLoading();
            yield return null;

            AsyncOperation operation = GameFlowService.LoadGameplaySceneAsync(
                sceneName,
                startupMode,
                requestedSlotName);
            if (operation == null)
            {
                panelSwitcher?.ShowMain();
                loadRoutine = null;
                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }
        }

        void ResolveReferences()
        {
            if (panelSwitcher == null)
            {
                panelSwitcher = GetComponent<MainMenuPanelSwitcher>();
            }

            if (saveGameAvailability == null)
            {
                saveGameAvailability = GetComponent<SaveGameAvailabilityProvider>();
            }
        }

        void ApplySaveAvailability()
        {
            if (continueButton != null)
            {
                continueButton.SetAvailable(HasSaveGame);
            }

            if (loadGameButton != null)
            {
                loadGameButton.SetAvailable(HasSaveGame);
            }
        }

        string ResolveNewGameSceneName()
        {
            return flowSettings != null ? flowSettings.NewGameSceneName : string.Empty;
        }

        string ResolveContinueSceneName()
        {
            return flowSettings != null ? flowSettings.ContinueSceneName : string.Empty;
        }

        string ResolveLoadGameSceneName()
        {
            return flowSettings != null ? flowSettings.LoadGameSceneName : string.Empty;
        }

        string ResolveSaveSlotName()
        {
            return saveGameAvailability != null ? saveGameAvailability.SlotName : SaveGameSlotCatalog.DefaultSlotName;
        }
    }
}
