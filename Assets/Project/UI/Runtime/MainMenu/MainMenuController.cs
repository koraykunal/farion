using System;
using System.Collections.Generic;
using Farion.App.Flow;
using Farion.Core.Persistence;
using Farion.UI.Feedback;
using Farion.UI.Foundation;
using Farion.UI.Loading;
using Farion.UI.Navigation;
using Farion.UI.SaveLoad;
using UnityEngine;

namespace Farion.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Scene Flow")]
        [SerializeField] GameFlowSettings flowSettings;

        [Header("UI Foundation")]
        [SerializeField] UiSystemRoot uiSystemRoot;
        [SerializeField] UiScreenRouter screenRouter;
        [SerializeField] UiFeedbackService feedbackService;
        [SerializeField] UiConfirmationDialog confirmationDialog;
        [SerializeField] UiLoadingOverlayPresenter loadingOverlay;
        [SerializeField] UiSaveLoadScreenPresenter saveLoadScreen;

        [Header("Buttons")]
        [SerializeField] MainMenuButton continueButton;
        [SerializeField] MainMenuButton loadGameButton;

        public bool HasSaveGame => SaveGameSlotService.TryGetMostRecentLoadable(out _);

        public bool HasAnySaveData
        {
            get
            {
                IReadOnlyList<SaveGameSlotSummary> summaries = SaveGameSlotService.GetPlayerSlotSummaries();
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

        public event Action<MainMenuAction> CoopActionRequested;

        void Awake()
        {
            ResolveReferences();
            ApplySaveAvailability();
            OpenScreen(UiScreenId.MainMenu);
        }

        void OnEnable()
        {
            ResolveReferences();
            if (saveLoadScreen != null)
            {
                saveLoadScreen.CatalogChanged -= ApplySaveAvailability;
                saveLoadScreen.CatalogChanged += ApplySaveAvailability;
            }
        }

        void OnDisable()
        {
            if (saveLoadScreen != null)
            {
                saveLoadScreen.CatalogChanged -= ApplySaveAvailability;
            }
        }

        public void Handle(MainMenuAction action)
        {
            switch (action)
            {
                case MainMenuAction.Continue:
                    if (HasSaveGame)
                    {
                        StartGameplayLoad(ResolveGameplaySceneName(), SaveGameStartupMode.LoadGame, ResolveSaveSlotName());
                    }
                    break;
                case MainMenuAction.NewGame:
                    StartGameplayLoad(ResolveGameplaySceneName(), SaveGameStartupMode.NewGame);
                    break;
                case MainMenuAction.LoadGame:
                    if (HasAnySaveData)
                    {
                        OpenLoadGameScreen();
                    }
                    break;
                case MainMenuAction.Settings:
                    OpenScreen(UiScreenId.Settings);
                    break;
                case MainMenuAction.Credits:
                    OpenScreen(
                        UiScreenId.Credits,
                        "Credits are not available yet.");
                    break;
                case MainMenuAction.Exit:
                    RequestExitConfirmation();
                    break;
                case MainMenuAction.Back:
                case MainMenuAction.CancelExit:
                    if (screenRouter == null || !screenRouter.TryHandleCancel())
                    {
                        OpenScreen(UiScreenId.MainMenu);
                    }
                    break;
                case MainMenuAction.ConfirmExit:
                    Application.Quit();
                    break;
                case MainMenuAction.HostGame:
                case MainMenuAction.JoinLocalhost:
                    CoopActionRequested?.Invoke(action);
                    break;
            }
        }

        void StartGameplayLoad(
            string sceneName,
            SaveGameStartupMode startupMode,
            string requestedSlotName = null)
        {
            ResolveReferences();
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                ShowFeedback(
                    "The gameplay scene is not configured.",
                    UiFeedbackSeverity.Error);
                return;
            }

            if (loadingOverlay == null)
            {
                ShowFeedback(
                    "The loading screen is not configured.",
                    UiFeedbackSeverity.Error);
                return;
            }

            loadingOverlay.TryBegin(
                () => GameFlowService.LoadGameplaySceneAsync(
                    sceneName,
                    startupMode,
                    requestedSlotName),
                UiLoadingPresentation.PreparingExpedition,
                () =>
                {
                    OpenScreen(UiScreenId.MainMenu);
                    ShowFeedback(
                        "The gameplay scene could not be loaded.",
                        UiFeedbackSeverity.Error);
                });
        }

        void ResolveReferences()
        {
            uiSystemRoot ??= UiCompositionScope.FindSystemRoot(this);
            uiSystemRoot ??= GetComponentInChildren<UiSystemRoot>(true);
            if (uiSystemRoot != null)
            {
                screenRouter ??= uiSystemRoot.ScreenRouter;
                feedbackService ??= uiSystemRoot.FeedbackService;
                confirmationDialog ??= uiSystemRoot.ConfirmationDialog;
                loadingOverlay ??= uiSystemRoot.LoadingOverlay;
                saveLoadScreen ??=
                    UiCompositionScope.FindFirstInScope<UiSaveLoadScreenPresenter>(
                        uiSystemRoot);
            }

            if (confirmationDialog == null)
            {
                confirmationDialog = GetComponentInChildren<UiConfirmationDialog>(true);
            }
        }

        void OpenLoadGameScreen()
        {
            ResolveReferences();
            if (saveLoadScreen != null &&
                saveLoadScreen.OpenForLoad(
                    slotName => StartGameplayLoad(
                        ResolveGameplaySceneName(),
                        SaveGameStartupMode.LoadGame,
                        slotName),
                    ResolveSaveSlotName()))
            {
                return;
            }

            ShowFeedback(
                "The load game screen is not configured.",
                UiFeedbackSeverity.Error);
        }

        void ApplySaveAvailability()
        {
            if (continueButton != null)
            {
                continueButton.SetAvailable(HasSaveGame);
            }

            if (loadGameButton != null)
            {
                loadGameButton.SetAvailable(HasAnySaveData);
            }
        }

        string ResolveGameplaySceneName()
        {
            return flowSettings != null ? flowSettings.GameplaySceneName : string.Empty;
        }

        string ResolveSaveSlotName()
        {
            return SaveGameSlotService.TryGetMostRecentLoadable(out SaveGameSlotSummary summary)
                ? summary.SlotName
                : SaveGameSlotCatalog.DefaultSlotName;
        }

        void RequestExitConfirmation()
        {
            ResolveReferences();
            if (confirmationDialog != null &&
                confirmationDialog.Present(
                    "QUIT TO DESKTOP",
                    "Any unsaved progress will be lost.",
                    "QUIT",
                    Application.Quit))
            {
                return;
            }

            ShowFeedback("Confirmation screen is not configured.");
        }

        public void ShowFeedback(
            string message,
            UiFeedbackSeverity severity = UiFeedbackSeverity.Information)
        {
            if (feedbackService != null)
            {
                feedbackService.Show(message, severity);
                return;
            }

#if UNITY_EDITOR
            Debug.LogWarning(message, this);
#endif
        }

        bool OpenScreen(
            UiScreenId screenId,
            string unavailableMessage = null)
        {
            ResolveReferences();
            if (screenRouter != null && screenRouter.Open(screenId))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(unavailableMessage))
            {
                ShowFeedback(unavailableMessage);
            }

            return false;
        }
    }
}
