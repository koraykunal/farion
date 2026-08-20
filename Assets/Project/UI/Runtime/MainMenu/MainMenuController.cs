using System;
using System.Collections.Generic;
using Farion.App.Flow;
using Farion.Core.Persistence;
using Farion.UI.Feedback;
using Farion.UI.Foundation;
using Farion.UI.Loading;
using Farion.UI.Localization;
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

        public event Action CoopHostRequested;

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
                    OpenSlotScreen(SaveGameStartupMode.NewGame);
                    break;
                case MainMenuAction.LoadGame:
                    if (HasAnySaveData)
                    {
                        OpenSlotScreen(SaveGameStartupMode.LoadGame);
                    }
                    break;
                case MainMenuAction.Settings:
                    OpenScreen(UiScreenId.Settings);
                    break;
                case MainMenuAction.Credits:
                    OpenScreen(
                        UiScreenId.Credits,
                        UiLocalization.Get(UiTextKeys.FeedbackScreenUnavailable));
                    break;
                case MainMenuAction.Exit:
                    Confirm(
                        UiTextKeys.DialogQuitTitle,
                        UiTextKeys.DialogUnsavedProgressBody,
                        UiTextKeys.CommonQuit,
                        Application.Quit);
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
                case MainMenuAction.JoinCoop:
                    OpenScreen(
                        UiScreenId.Coop,
                        UiLocalization.Get(UiTextKeys.FeedbackScreenUnavailable));
                    break;
            }
        }

        void StartGameplayLoad(
            string sceneName,
            SaveGameStartupMode startupMode,
            string requestedSlotName = null)
        {
            ResolveReferences();
            if (string.IsNullOrWhiteSpace(sceneName) || loadingOverlay == null)
            {
                ShowFeedback(
                    UiLocalization.Get(UiTextKeys.FeedbackGameplayUnavailable),
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
                        UiLocalization.Get(UiTextKeys.FeedbackGameplayUnavailable),
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

        void OpenSlotScreen(SaveGameStartupMode startupMode)
        {
            ResolveReferences();
            void StartSolo(string slotName) => StartGameplayLoad(
                ResolveGameplaySceneName(),
                startupMode,
                slotName);
            void StartCoop(string slotName) => RequestCoopHost(startupMode, slotName);
            Action<string> coopAction = CoopHostRequested != null
                ? StartCoop
                : null;

            if (saveLoadScreen != null &&
                (startupMode == SaveGameStartupMode.NewGame
                    ? saveLoadScreen.OpenForNewGame(StartSolo, coopAction)
                    : saveLoadScreen.OpenForLoad(
                        StartSolo,
                        coopAction,
                        ResolveSaveSlotName())))
            {
                return;
            }

            ShowFeedback(
                UiLocalization.Get(UiTextKeys.FeedbackScreenUnavailable),
                UiFeedbackSeverity.Error);
        }

        void RequestCoopHost(SaveGameStartupMode startupMode, string slotName)
        {
            if (startupMode == SaveGameStartupMode.LoadGame)
            {
                SaveGameStartupRequest.RequestLoad(slotName);
            }
            else
            {
                SaveGameStartupRequest.RequestNewGame(slotName);
            }

            CoopHostRequested?.Invoke();
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

        public bool Confirm(
            string titleKey,
            string bodyKey,
            string confirmKey,
            Action onConfirm)
        {
            ResolveReferences();
            if (confirmationDialog != null &&
                confirmationDialog.Present(
                    UiLocalization.Get(titleKey),
                    UiLocalization.Get(bodyKey),
                    UiLocalization.Get(confirmKey),
                    onConfirm))
            {
                return true;
            }

            ShowFeedback(UiLocalization.Get(UiTextKeys.FeedbackScreenUnavailable));
            return false;
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
