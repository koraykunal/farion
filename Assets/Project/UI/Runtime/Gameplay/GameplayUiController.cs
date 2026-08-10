using System;
using Farion.App.Flow;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Input;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.UI.Feedback;
using Farion.UI.Foundation;
using Farion.UI.Input;
using Farion.UI.Loading;
using Farion.UI.Navigation;
using Farion.UI.SaveLoad;
using TMPro;
using UnityEngine;

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayUiController : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] UiScreenId currentScreen = UiScreenId.GameplayHud;

        [Header("References")]
        [SerializeField] InventoryPanelPresenter inventoryPanel;
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

        [Header("Cursor")]
        [SerializeField] bool lockCursorDuringGameplay = true;

        InventoryContainerComponent playerInventory;
        IGameplayCommandEvents observedCommandEvents;
        Action returnToMainMenuAction;
        Action quitGameAction;

        public event Action<UiScreenId> ScreenChanged;
        public UiScreenId CurrentScreen => currentScreen;
        public bool IsUiFocused => screenRouter != null && screenRouter.HasOpenScreen;
        public GameplaySessionController SessionController => sessionController;

        public void SetInteractionRaycaster(PlayerInteractionRaycaster raycaster)
        {
            interactionRaycaster = raycaster;
            RefreshHud();
        }

        public void SetSessionActions(Action returnToMainMenu, Action quitGame)
        {
            returnToMainMenuAction = returnToMainMenu;
            quitGameAction = quitGame;
            GetComponentInChildren<GameplayMenuListPresenter>(true)?.Rebuild();
        }

        public bool IsActionAvailable(GameplayMenuAction action)
        {
            ResolveReferences();
            if (sessionController != null)
            {
                return true;
            }

            return action switch
            {
                GameplayMenuAction.Resume => true,
                GameplayMenuAction.Options => screenRouter != null &&
                    screenRouter.TryGetScreen(UiScreenId.Settings, out _),
                GameplayMenuAction.ExitToMainMenu => returnToMainMenuAction != null,
                GameplayMenuAction.QuitGame => quitGameAction != null,
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
                    SynchronizeScreenStateFromRouter(notify: true);
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

        void SynchronizeScreenStateFromRouter(bool notify)
        {
            if (screenRouter == null)
            {
                return;
            }

            UiScreenId routedState = screenRouter.TopScreenId != UiScreenId.None
                ? screenRouter.TopScreenId
                : UiScreenId.GameplayHud;
            SetCurrentScreen(routedState, notify);
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

            SynchronizeScreenStateFromRouter(notify: true);
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

        public void Handle(GameplayMenuAction action)
        {
            switch (action)
            {
                case GameplayMenuAction.Resume:
                    CloseActiveScreen();
                    break;
                case GameplayMenuAction.Inventory:
                    ShowInventory();
                    break;
                case GameplayMenuAction.ExitToMainMenu:
                    RequestConfirmation(
                        "RETURN TO MAIN MENU",
                        "Unsaved progress may be lost.",
                        "RETURN",
                        BeginReturnToMainMenu);
                    break;
                case GameplayMenuAction.QuitGame:
                    RequestConfirmation(
                        "QUIT TO DESKTOP",
                        "Unsaved progress may be lost.",
                        "QUIT",
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
                case GameplayMenuAction.Blueprints:
                case GameplayMenuAction.Journal:
                case GameplayMenuAction.Ship:
                case GameplayMenuAction.Map:
                    break;
                case GameplayMenuAction.Options:
                    OpenScreen(UiScreenId.Settings);
                    break;
                case GameplayMenuAction.Save:
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
                SetCurrentScreen(UiScreenId.GameplayHud, notify: false);
                RefreshHud();
                return;
            }

            screenRouter.Open(UiScreenId.GameplayHud, animated: false);
            inventoryPanel?.SetInventory(playerInventory);
            SynchronizeScreenStateFromRouter(notify: false);
            RefreshHud();
        }

        void HandleTopScreenChanged(UiScreenId _)
        {
            SynchronizeScreenStateFromRouter(notify: true);
        }

        void BindItemAcquisitionFeedback()
        {
            IGameplayCommandEvents commandEvents = sessionController?.CommandEvents;
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
            }
        }

        void UnbindItemAcquisitionFeedback()
        {
            if (observedCommandEvents != null)
            {
                observedCommandEvents.ItemAcquired -= HandleItemAcquired;
                observedCommandEvents.CargoTransferCompleted -=
                    HandleCargoTransferCompleted;
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
                _ => "CARGO TRANSFER FAILED."
            };
        }

        static string FormatSlots(InventoryContainerSnapshot snapshot)
        {
            return snapshot == null
                ? "--/-- SLOTS"
                : $"{snapshot.Stacks.Count:00}/{snapshot.SlotCapacity:00} SLOTS";
        }

        void SetCurrentScreen(UiScreenId screenId, bool notify)
        {
            if (currentScreen == screenId)
            {
                return;
            }

            currentScreen = screenId;
            if (notify)
            {
                ScreenChanged?.Invoke(currentScreen);
            }
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
                inventoryPanel = GetComponentInChildren<InventoryPanelPresenter>(true);
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

            interactionPromptText.gameObject.SetActive(showPrompt);
            if (!showPrompt)
            {
                interactionPromptText.text = string.Empty;
                return;
            }

            string prompt = interactionRaycaster.CurrentPrompt.Trim();
            string binding = UiBindingDisplay.GetDisplayString(
                FarionInputActions.OnFootInteract,
                inputDeviceService != null
                    ? inputDeviceService.CurrentDevice
                    : UiInputDeviceKind.KeyboardMouse);
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
            if (sessionController == null)
            {
                ShowFeedback(
                    "Save is unavailable because the gameplay session is not ready.",
                    UiFeedbackSeverity.Error);
                return;
            }

            if (saveLoadScreen != null &&
                saveLoadScreen.OpenForSave(
                    sessionController.Save,
                    sessionController.SaveSlotName))
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
