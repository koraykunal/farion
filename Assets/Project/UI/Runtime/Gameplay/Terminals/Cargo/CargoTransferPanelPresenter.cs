using Farion.App.Flow;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Inventory;
using Farion.UI.Common;
using Farion.UI.Feedback;
using Farion.UI.Foundation;
using Farion.UI.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CargoTransferPanelPresenter : MonoBehaviour
    {
        [Header("Session")]
        [SerializeField] GameplaySessionController sessionController;

        [Header("Inventories")]
        [SerializeField] InventoryContainerComponent sourceInventory;
        [SerializeField] InventoryContainerComponent destinationInventory;
        [SerializeField] InventoryPanelPresenter sourcePanel;
        [SerializeField] InventoryPanelPresenter destinationPanel;

        [Header("Screen")]
        [SerializeField] UiScreenView screenView;
        [SerializeField] UiSystemRoot systemRoot;
        [SerializeField] UiFeedbackService feedbackService;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] Button transferOneButton;
        [SerializeField] Button transferStackButton;
        [SerializeField] Button swapButton;
        [SerializeField] Button backButton;

        InventoryPanelPresenter activePanel;

        void Awake()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            Refresh();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        public bool Open(
            InventoryContainerComponent source,
            InventoryContainerComponent destination)
        {
            SetInventories(source, destination);
            ResolveReferences();
            return systemRoot != null &&
                   systemRoot.ScreenRouter.Open(UiScreenId.CargoTransfer);
        }

        public void SetInventories(
            InventoryContainerComponent source,
            InventoryContainerComponent destination)
        {
            sourceInventory = source;
            destinationInventory = destination;
            ResolveDefaultInventories();
            if (sourcePanel != null)
            {
                sourcePanel.SetInventory(sourceInventory);
            }

            if (destinationPanel != null)
            {
                destinationPanel.SetInventory(destinationInventory);
            }

            activePanel = sourcePanel;
            Refresh();
        }

        public void TransferFocusedStack()
        {
            TransferFocused(int.MaxValue);
        }

        public void TransferFocusedOne()
        {
            TransferFocused(1);
        }

        public void SwapDirection()
        {
            (sourceInventory, destinationInventory) =
                (destinationInventory, sourceInventory);
            SetInventories(sourceInventory, destinationInventory);
            SetStatus(TerminalUiText.Get(
                "cargo.direction_swapped",
                "TRANSFER DIRECTION SWAPPED"));
        }

        public void Close()
        {
            systemRoot?.ScreenRouter?.Close(UiScreenId.CargoTransfer);
        }

        void TransferFocused(int requestedAmount)
        {
            ResolveReferences();
            if (activePanel == null || activePanel.FocusedStack == null)
            {
                SetStatus(TerminalUiText.Get(
                    "cargo.no_selection",
                    "NO ITEM SELECTED"));
                return;
            }

            InventoryContainerComponent source =
                activePanel == destinationPanel
                    ? destinationInventory
                    : sourceInventory;
            InventoryContainerComponent destination =
                activePanel == destinationPanel
                    ? sourceInventory
                    : destinationInventory;
            InventoryStack stack = activePanel.FocusedStack;
            int amount = Mathf.Min(stack.Quantity, requestedAmount);
            InventoryTransferResult result =
                sessionController != null &&
                sessionController.Commands != null
                    ? sessionController.Commands.TryTransfer(
                        source,
                        destination,
                        stack.Item,
                        amount)
                    : InventoryTransferResult.UnauthorizedSource;

            Refresh();
            if (result == InventoryTransferResult.Succeeded)
            {
                SetStatus(TerminalUiText.Format(
                    "cargo.transferred",
                    "TRANSFERRED {0} {1}",
                    amount,
                    stack.Item.DisplayName));
                return;
            }

            SetStatus(TerminalUiText.Get(
                "cargo.failed",
                "TRANSFER FAILED"));
            feedbackService?.Show(
                TerminalUiText.Get(
                    "cargo.failed_detail",
                    "The transfer could not be completed."),
                UiFeedbackSeverity.Caution);
        }

        void Subscribe()
        {
            if (sourcePanel != null)
            {
                sourcePanel.FocusedStackChanged -= HandleFocusedStackChanged;
                sourcePanel.FocusedStackChanged += HandleFocusedStackChanged;
            }

            if (destinationPanel != null)
            {
                destinationPanel.FocusedStackChanged -= HandleFocusedStackChanged;
                destinationPanel.FocusedStackChanged += HandleFocusedStackChanged;
            }

            if (transferOneButton != null)
            {
                transferOneButton.onClick.RemoveListener(TransferFocusedOne);
                transferOneButton.onClick.AddListener(TransferFocusedOne);
            }

            if (transferStackButton != null)
            {
                transferStackButton.onClick.RemoveListener(TransferFocusedStack);
                transferStackButton.onClick.AddListener(TransferFocusedStack);
            }

            if (swapButton != null)
            {
                swapButton.onClick.RemoveListener(SwapDirection);
                swapButton.onClick.AddListener(SwapDirection);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(Close);
                backButton.onClick.AddListener(Close);
            }
        }

        void Unsubscribe()
        {
            if (sourcePanel != null)
            {
                sourcePanel.FocusedStackChanged -= HandleFocusedStackChanged;
            }

            if (destinationPanel != null)
            {
                destinationPanel.FocusedStackChanged -= HandleFocusedStackChanged;
            }

            transferOneButton?.onClick.RemoveListener(TransferFocusedOne);
            transferStackButton?.onClick.RemoveListener(TransferFocusedStack);
            swapButton?.onClick.RemoveListener(SwapDirection);
            backButton?.onClick.RemoveListener(Close);
        }

        void HandleFocusedStackChanged(
            InventoryPanelPresenter panel,
            InventoryStack stack)
        {
            activePanel = panel;
            Refresh();
        }

        void Refresh()
        {
            ConfigureActionLabels();
            sourcePanel?.SetInventory(sourceInventory);
            destinationPanel?.SetInventory(destinationInventory);
            SetText(
                titleText,
                TerminalUiText.Get("cargo.title", "CARGO TRANSFER"));
            if (string.IsNullOrWhiteSpace(statusText?.text))
            {
                SetStatus(TerminalUiText.Get(
                    "cargo.instruction",
                    "SELECT AN ITEM AND TRANSFER"));
            }

            bool canTransfer =
                activePanel != null &&
                activePanel.FocusedStack != null &&
                activePanel.FocusedStack.Item != null;
            SetInteractable(transferOneButton, canTransfer);
            SetInteractable(transferStackButton, canTransfer);
            SetInteractable(swapButton, sourceInventory != null && destinationInventory != null);
            if (screenView != null && transferStackButton != null)
            {
                screenView.SetFirstSelection(transferStackButton);
            }
        }

        void ConfigureActionLabels()
        {
            ConfigureButton(
                transferOneButton,
                TerminalUiText.Get("cargo.action_one", "TRANSFER ONE"),
                TerminalUiText.Get(
                    "cargo.action_one_detail",
                    "Move one unit"));
            ConfigureButton(
                transferStackButton,
                TerminalUiText.Get(
                    "cargo.action_stack",
                    "TRANSFER STACK"),
                TerminalUiText.Get(
                    "cargo.action_stack_detail",
                    "Move selected stack"));
            ConfigureButton(
                swapButton,
                TerminalUiText.Get("cargo.action_swap", "SWAP"),
                TerminalUiText.Get(
                    "cargo.action_swap_detail",
                    "Reverse direction"));
            ConfigureButton(
                backButton,
                TerminalUiText.Get("common.back", "BACK"),
                TerminalUiText.Get("common.close", "Close terminal"));
        }

        void ResolveReferences()
        {
            screenView ??= GetComponent<UiScreenView>();
            systemRoot ??= UiCompositionScope.FindSystemRoot(this);
            feedbackService ??= systemRoot != null ? systemRoot.FeedbackService : null;
            GameplayUiController gameplayUi =
                GetComponentInParent<GameplayUiController>();
            sessionController ??= gameplayUi != null
                ? gameplayUi.SessionController
                : null;
            ResolveDefaultInventories();
        }

        void ResolveDefaultInventories()
        {
            if (sessionController == null ||
                !sessionController.TryGetRuntime(out var runtime))
            {
                return;
            }

            sourceInventory ??= runtime.LocalInventory;
            destinationInventory ??= runtime.PersonalShipBinding != null
                ? runtime.PersonalShipBinding.Cargo
                : null;
        }

        void SetStatus(string value)
        {
            SetText(statusText, value);
        }

        static void SetInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        static void ConfigureButton(
            Button button,
            string title,
            string subtitle)
        {
            button?.GetComponent<MenuButtonView>()
                ?.ConfigureContent(title, subtitle, null);
        }

        static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }
    }
}
