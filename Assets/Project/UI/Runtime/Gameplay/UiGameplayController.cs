using System;
using Farion.App.Flow;
using Farion.Core.Persistence;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Input;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.UI.Feedback;
using Farion.UI.Foundation;
using Farion.UI.Input;
using Farion.UI.Loading;
using Farion.UI.Localization;
using Farion.UI.Navigation;
using Farion.UI.SaveLoad;
using TMPro;
using UnityEngine;

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class UiGameplayController : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] UiScreenId currentScreen = UiScreenId.GameplayHud;

        [Header("References")]
        [SerializeField] UiInventoryPanelPresenter inventoryPanel;
        [SerializeField] GameplaySessionController sessionController;

        [Header("UI Foundation")]
        [SerializeField] UiSystemRoot uiSystemRoot;
        [SerializeField] UiScreenRouter screenRouter;
        [SerializeField] UiInputDeviceService inputDeviceService;
        [SerializeField] UiFeedbackService feedbackService;
        [SerializeField] UiConfirmationDialog confirmationDialog;
        [SerializeField] UiLoadingOverlayPresenter loadingOverlay;
        [SerializeField] UiSaveLoadScreenPresenter saveLoadScreen;

        [Header("HUD")]
        [SerializeField] PlayerInteractionRaycaster interactionRaycaster;
        [SerializeField] TMP_Text interactionPromptText;
        [SerializeField] string interactionPromptPrefix = "E";
        [SerializeField] TMP_Text networkStatusText;

        [Header("Cursor")]
        [SerializeField] bool lockCursorDuringGameplay = true;

        InventoryContainerComponent playerInventory;
        IGameplayCommandEvents observedCommandEvents;
        IGameplayCommandEvents externalCommandEvents;
        Action returnToMainMenuAction;
        Action quitGameAction;
        Func<string, SaveGameOperationResult> externalSaveAction;
        string externalSaveSlotName;
        string displayedInteractionPrompt;
        UiInputDeviceKind displayedPromptDevice = (UiInputDeviceKind)(-1);

        public void SetInteractionRaycaster(PlayerInteractionRaycaster raycaster)
        {
            interactionRaycaster = raycaster;
            RefreshHud();
        }

        public void SetNetworkStatus(string status)
        {
            if (networkStatusText == null)
            {
                return;
            }

            bool visible = !string.IsNullOrEmpty(status);
            if (networkStatusText.gameObject.activeSelf != visible)
            {
                networkStatusText.gameObject.SetActive(visible);
            }

            if (visible)
            {
                networkStatusText.SetText(status);
            }
        }

        public void BindSession(
            GameplaySessionController controller,
            PlayerInteractionRaycaster raycaster)
        {
            UnbindItemAcquisitionFeedback();
            sessionController = controller;
            interactionRaycaster = raycaster;
            playerInventory = null;
            ResolveReferences();
            BindItemAcquisitionFeedback();
            InitializeNavigation();
        }

        public void SetSessionActions(Action returnToMainMenu, Action quitGame)
        {
            returnToMainMenuAction = returnToMainMenu;
            quitGameAction = quitGame;
            GetComponentInChildren<UiGameplayMenuListPresenter>(true)?.Rebuild();
        }

        public void SetSaveAction(
            Func<string, SaveGameOperationResult> action,
            string slotName)
        {
            externalSaveAction = action;
            externalSaveSlotName = slotName;
            GetComponentInChildren<UiGameplayMenuListPresenter>(true)?.Rebuild();
        }

        public bool IsActionAvailable(UiGameplayMenuAction action)
        {
            ResolveReferences();
            if (sessionController != null)
            {
                return true;
            }

            return action switch
            {
                UiGameplayMenuAction.Resume => true,
                UiGameplayMenuAction.Options => screenRouter != null &&
                    screenRouter.TryGetScreen(UiScreenId.Settings, out _),
                UiGameplayMenuAction.Save => externalSaveAction != null,
                UiGameplayMenuAction.ExitToMainMenu => returnToMainMenuAction != null,
                UiGameplayMenuAction.QuitGame => quitGameAction != null,
                _ => false
            };
        }

        void Awake()
        {
            ResolveReferences();
            InitializeNavigation();
        }

        void OnEnable()
        {
            FarionInputActions.Enable();
            ResolveReferences();
            BindItemAcquisitionFeedback();
            if (screenRouter != null)
            {
                screenRouter.TopScreenChanged -= HandleTopScreenChanged;
                screenRouter.TopScreenChanged += HandleTopScreenChanged;
            }

            InitializeNavigation();
        }

        void OnDisable()
        {
            UnbindItemAcquisitionFeedback();
            if (screenRouter != null)
            {
                screenRouter.TopScreenChanged -= HandleTopScreenChanged;
            }
        }

        void Update()
        {
            if (FarionInputActions.UiPause.WasPressedThisFrame())
            {
                if (screenRouter != null && screenRouter.CancelHandledThisFrame)
                {
                    SynchronizeScreenStateFromRouter();
                    return;
                }

                HandlePausePressed();
                return;
            }

            if (FarionInputActions.UiInventory.WasPressedThisFrame())
            {
                ToggleInventory();
            }
        }

        void SynchronizeScreenStateFromRouter()
        {
            if (screenRouter == null)
            {
                return;
            }

            currentScreen = screenRouter.TopScreenId != UiScreenId.None
                ? screenRouter.TopScreenId
                : UiScreenId.GameplayHud;
            ApplyCursorState(screenRouter.HasOpenScreen);
            RefreshHud();
        }

        void LateUpdate()
        {
            RefreshHud();
        }

        public void ShowPauseMenu()
        {
            OpenScreen(UiScreenId.PauseMenu);
        }

        public void ShowInventory()
        {
            OpenScreen(UiScreenId.Inventory);
        }

        public void CloseActiveScreen()
        {
            if (screenRouter != null && screenRouter.CloseTop())
            {
                return;
            }

            SynchronizeScreenStateFromRouter();
        }

        public void ToggleInventory()
        {
            if (screenRouter != null &&
                screenRouter.IsOpen(UiScreenId.Inventory))
            {
                screenRouter.Close(UiScreenId.Inventory);
                return;
            }

            ShowInventory();
        }

        public void SetPlayerInventory(InventoryContainerComponent inventory)
        {
            playerInventory = inventory;
            if (inventoryPanel != null)
            {
                inventoryPanel.SetInventory(playerInventory);
            }
        }

        public void Handle(UiGameplayMenuAction action)
        {
            switch (action)
            {
                case UiGameplayMenuAction.Resume:
                    CloseActiveScreen();
                    break;
                case UiGameplayMenuAction.Inventory:
                    ShowInventory();
                    break;
                case UiGameplayMenuAction.ExitToMainMenu:
                    RequestConfirmation(
                        UiLocalization.Get(UiTextKeys.DialogMainMenuTitle),
                        UiLocalization.Get(UiTextKeys.DialogUnsavedProgressBody),
                        UiLocalization.Get(UiTextKeys.DialogMainMenuConfirm),
                        BeginReturnToMainMenu);
                    break;
                case UiGameplayMenuAction.QuitGame:
                    RequestConfirmation(
                        UiLocalization.Get(UiTextKeys.DialogQuitTitle),
                        UiLocalization.Get(UiTextKeys.DialogUnsavedProgressBody),
                        UiLocalization.Get(UiTextKeys.CommonQuit),
                        () =>
                        {
                            if (quitGameAction != null)
                            {
                                quitGameAction();
                            }
                            else
                            {
                                sessionController?.Quit();
                            }
                        });
                    break;
                case UiGameplayMenuAction.Blueprints:
                case UiGameplayMenuAction.Journal:
                case UiGameplayMenuAction.Ship:
                case UiGameplayMenuAction.Map:
                    break;
                case UiGameplayMenuAction.Options:
                    OpenScreen(UiScreenId.Settings);
                    break;
                case UiGameplayMenuAction.Save:
                    OpenSaveGameScreen();
                    break;
            }
        }

        void HandlePausePressed()
        {
            if (screenRouter == null)
            {
                ShowFeedback(
                    "UI navigation is not configured.",
                    UiFeedbackSeverity.Error);
                return;
            }

            if (screenRouter.HasOpenScreen)
            {
                screenRouter.CloseTop();
                return;
            }

            ShowPauseMenu();
        }

        void OpenScreen(UiScreenId screenId)
        {
            ResolveReferences();
            if (screenRouter != null && screenRouter.Open(screenId))
            {
                return;
            }

            ShowFeedback(
                $"UI screen '{screenId}' is not configured.",
                UiFeedbackSeverity.Error);
        }

        void InitializeNavigation()
        {
            ResolveReferences();
            if (screenRouter == null)
            {
                currentScreen = UiScreenId.GameplayHud;
                RefreshHud();
                return;
            }

            screenRouter.Open(UiScreenId.GameplayHud, animated: false);
            inventoryPanel?.SetInventory(playerInventory);
            SynchronizeScreenStateFromRouter();
            RefreshHud();
        }

        void HandleTopScreenChanged(UiScreenId _)
        {
            SynchronizeScreenStateFromRouter();
        }

        public void BindCommandEvents(IGameplayCommandEvents commandEvents)
        {
            externalCommandEvents = commandEvents;
            BindItemAcquisitionFeedback();
        }

        void BindItemAcquisitionFeedback()
        {
            IGameplayCommandEvents commandEvents =
                externalCommandEvents ?? sessionController?.CommandEvents;
            if (ReferenceEquals(observedCommandEvents, commandEvents))
            {
                return;
            }

            UnbindItemAcquisitionFeedback();
            observedCommandEvents = commandEvents;
            if (observedCommandEvents != null)
            {
                observedCommandEvents.ItemAcquired += HandleItemAcquired;
                observedCommandEvents.CargoTransferCompleted +=
                    HandleCargoTransferCompleted;
                observedCommandEvents.FleetProcessingCompleted +=
                    HandleFleetProcessingCompleted;
            }
        }

        void UnbindItemAcquisitionFeedback()
        {
            if (observedCommandEvents != null)
            {
                observedCommandEvents.ItemAcquired -= HandleItemAcquired;
                observedCommandEvents.CargoTransferCompleted -=
                    HandleCargoTransferCompleted;
                observedCommandEvents.FleetProcessingCompleted -=
                    HandleFleetProcessingCompleted;
                observedCommandEvents = null;
            }
        }

        void HandleItemAcquired(InventoryItemDefinition item, int amount)
        {
            if (item != null && amount > 0)
            {
                feedbackService?.ShowItem(item.DisplayName, amount, item.Icon);
            }
        }

        void HandleCargoTransferCompleted(CargoTransferReceipt receipt)
        {
            bool succeeded = receipt.Result == CargoTransferResult.Succeeded;
            ShowFeedback(
                FormatCargoTransferMessage(receipt),
                succeeded
                    ? UiFeedbackSeverity.Success
                    : receipt.Result == CargoTransferResult.EmptySource ||
                      receipt.Result == CargoTransferResult.InsufficientCapacity
                        ? UiFeedbackSeverity.Caution
                        : UiFeedbackSeverity.Error);
        }

        void HandleFleetProcessingCompleted(FleetProcessingResult result)
        {
            if (result == FleetProcessingResult.Pending)
            {
                return;
            }

            ShowFeedback(
                FormatFleetProcessingMessage(result),
                result == FleetProcessingResult.Succeeded
                    ? UiFeedbackSeverity.Success
                    : result == FleetProcessingResult.Rejected ||
                      result == FleetProcessingResult.OutOfRange
                        ? UiFeedbackSeverity.Caution
                        : UiFeedbackSeverity.Error);
        }

        internal static string FormatFleetProcessingMessage(
            FleetProcessingResult result)
        {
            return result switch
            {
                FleetProcessingResult.Succeeded => "PROCESSING COMPLETE.",
                FleetProcessingResult.Rejected =>
                    "FLEET STORAGE LACKS THE REQUIRED INPUTS.",
                FleetProcessingResult.OutOfRange =>
                    "MOVE CLOSER TO THE FLEET TO PROCESS.",
                FleetProcessingResult.MissingStorage =>
                    "FLEET STORAGE IS UNAVAILABLE.",
                FleetProcessingResult.StaleStorage =>
                    "FLEET STORAGE CHANGED. TRY AGAIN.",
                _ => "PROCESSING FAILED."
            };
        }

        internal static string FormatCargoTransferMessage(
            CargoTransferReceipt receipt)
        {
            if (receipt.Result == CargoTransferResult.Succeeded)
            {
                return receipt.Kind == CargoTransferKind.LoadShuttle
                    ? $"CARGO LOADED  /  EXPLORER {FormatSlots(receipt.Source)}  /  SHUTTLE {FormatSlots(receipt.Destination)}"
                    : $"UNLOAD COMPLETE  /  SHUTTLE {FormatSlots(receipt.Source)}  /  FLEET STORAGE {FormatSlots(receipt.Destination)}";
            }

            return receipt.Result switch
            {
                CargoTransferResult.EmptySource =>
                    receipt.Kind == CargoTransferKind.LoadShuttle
                        ? "NO EXPLORER CARGO TO LOAD."
                        : "SHUTTLE CARGO IS EMPTY.",
                CargoTransferResult.InsufficientCapacity =>
                    receipt.Kind == CargoTransferKind.LoadShuttle
                        ? "SHUTTLE CARGO HAS INSUFFICIENT CAPACITY."
                        : "FLEET STORAGE HAS INSUFFICIENT CAPACITY.",
                CargoTransferResult.DefinitionMismatch =>
                    "CARGO CONTAINS AN INCOMPATIBLE ITEM.",
                CargoTransferResult.StaleState =>
                    "CARGO CHANGED. TRY AGAIN.",
                CargoTransferResult.OutOfRange =>
                    receipt.Kind == CargoTransferKind.LoadShuttle
                        ? "MOVE CLOSER TO YOUR SHUTTLE TO LOAD."
                        : "DOCK CLOSER TO THE FLEET TO UNLOAD.",
                _ => "CARGO TRANSFER FAILED."
            };
        }

        static string FormatSlots(InventoryContainerSnapshot snapshot)
        {
            return snapshot == null
                ? "--/-- SLOTS"
                : $"{snapshot.Stacks.Count:00}/{snapshot.SlotCapacity:00} SLOTS";
        }

        void ApplyCursorState(bool uiFocused)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (uiFocused)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (lockCursorDuringGameplay)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void ResolveReferences()
        {
            if (inventoryPanel == null)
            {
                inventoryPanel = GetComponentInChildren<UiInventoryPanelPresenter>(true);
            }

            uiSystemRoot ??= UiCompositionScope.FindSystemRoot(this);
            uiSystemRoot ??= GetComponentInChildren<UiSystemRoot>(true);
            if (uiSystemRoot != null)
            {
                screenRouter ??= uiSystemRoot.ScreenRouter;
                inputDeviceService ??= uiSystemRoot.InputDeviceService;
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

            if (playerInventory == null &&
                sessionController != null &&
                sessionController.TryGetRuntime(out var sessionRuntime))
            {
                playerInventory = sessionRuntime.LocalInventory;
            }
        }

        void RefreshHud()
        {
            if (interactionPromptText == null)
            {
                return;
            }

            bool showPrompt = currentScreen == UiScreenId.GameplayHud &&
                interactionRaycaster != null &&
                interactionRaycaster.isActiveAndEnabled &&
                interactionRaycaster.HasTarget &&
                !string.IsNullOrWhiteSpace(interactionRaycaster.CurrentPrompt);

            if (interactionPromptText.gameObject.activeSelf != showPrompt)
            {
                interactionPromptText.gameObject.SetActive(showPrompt);
            }

            if (!showPrompt)
            {
                if (displayedInteractionPrompt != null)
                {
                    interactionPromptText.text = string.Empty;
                    displayedInteractionPrompt = null;
                    displayedPromptDevice = (UiInputDeviceKind)(-1);
                }

                return;
            }

            string promptSource = interactionRaycaster.CurrentPrompt;
            UiInputDeviceKind device = inputDeviceService != null
                ? inputDeviceService.CurrentDevice
                : UiInputDeviceKind.KeyboardMouse;
            if (promptSource == displayedInteractionPrompt &&
                device == displayedPromptDevice)
            {
                return;
            }

            displayedInteractionPrompt = promptSource;
            displayedPromptDevice = device;
            string prompt = promptSource.Trim();
            string binding = UiBindingDisplay.GetDisplayString(
                FarionInputActions.OnFootInteract,
                device);
            if (string.IsNullOrWhiteSpace(binding))
            {
                binding = interactionPromptPrefix;
            }

            interactionPromptText.text = string.IsNullOrWhiteSpace(binding)
                ? prompt
                : $"{binding.Trim()}  {prompt}";
        }

        void OpenSaveGameScreen()
        {
            if (externalSaveAction != null)
            {
                SaveGameOperationResult result =
                    externalSaveAction(externalSaveSlotName);
                ShowFeedback(
                    result.Succeeded
                        ? UiLocalization.Get(UiTextKeys.CoopSaveCompleted)
                        : UiLocalization.Get(UiTextKeys.CoopSaveFailed),
                    result.Succeeded
                        ? UiFeedbackSeverity.Success
                        : UiFeedbackSeverity.Error);
                CloseActiveScreen();
                return;
            }

            Func<string, SaveGameOperationResult> save =
                sessionController != null ? sessionController.Save : null;
            string slot = sessionController?.SaveSlotName;
            if (save == null)
            {
                ShowFeedback(
                    "Save is unavailable because the gameplay session is not ready.",
                    UiFeedbackSeverity.Error);
                return;
            }

            if (saveLoadScreen != null &&
                saveLoadScreen.OpenForSave(save, slot))
            {
                return;
            }

            ShowFeedback(
                "The save game screen is not configured.",
                UiFeedbackSeverity.Error);
        }

        void BeginReturnToMainMenu()
        {
            ResolveReferences();
            if (returnToMainMenuAction != null)
            {
                returnToMainMenuAction();
                return;
            }

            if (sessionController == null || loadingOverlay == null)
            {
                ShowFeedback(
                    "The loading screen is not configured.",
                    UiFeedbackSeverity.Error);
                return;
            }

            loadingOverlay.TryBegin(
                sessionController.ExitToMainMenuAsync,
                UiLoadingPresentation.ReturningToMainMenu,
                () => ShowFeedback(
                    "The main menu could not be loaded.",
                    UiFeedbackSeverity.Error));
        }

        void RequestConfirmation(
            string title,
            string body,
            string confirmLabel,
            Action confirmedAction)
        {
            ResolveReferences();
            if (confirmationDialog != null &&
                confirmationDialog.Present(
                    title,
                    body,
                    confirmLabel,
                    confirmedAction))
            {
                return;
            }

            ShowFeedback(
                "Confirmation screen is not configured.",
                UiFeedbackSeverity.Error);
        }

        void ShowFeedback(string message, UiFeedbackSeverity severity)
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

    }
}
