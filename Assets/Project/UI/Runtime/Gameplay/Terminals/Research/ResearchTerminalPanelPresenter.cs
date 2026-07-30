using System.Collections.Generic;
using Farion.App.Flow;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Research;
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
    public sealed class ResearchTerminalPanelPresenter : MonoBehaviour
    {
        [Header("Session")]
        [SerializeField] GameplaySessionController sessionController;

        [Header("Terminal")]
        [SerializeField] ResearchTerminalRuntime terminal;
        [SerializeField] InventoryContainerComponent inputInventory;

        [Header("Research List")]
        [SerializeField] Transform researchButtonContainer;
        [SerializeField] MenuButtonView researchButtonPrefab;

        [Header("Screen")]
        [SerializeField] UiScreenView screenView;
        [SerializeField] UiSystemRoot systemRoot;
        [SerializeField] UiFeedbackService feedbackService;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text detailTitleText;
        [SerializeField] TMP_Text detailBodyText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] Button completeButton;
        [SerializeField] Button backButton;

        readonly List<MenuButtonView> researchButtons = new();
        ResearchDefinition selectedResearch;

        void Awake()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            RebuildResearchList();
            Refresh();
        }

        void OnDisable()
        {
            Unsubscribe();
            ClearResearchButtons();
        }

        public bool Open(
            ResearchTerminalRuntime nextTerminal,
            InventoryContainerComponent inventory)
        {
            terminal = nextTerminal;
            inputInventory = inventory;
            ResolveReferences();
            RebuildResearchList();
            Refresh();
            return systemRoot != null &&
                   systemRoot.ScreenRouter.Open(UiScreenId.ResearchTerminal);
        }

        public void CompleteSelected()
        {
            ResolveReferences();
            if (selectedResearch == null)
            {
                SetStatus(TerminalUiText.Get(
                    "research.no_selection",
                    "NO RESEARCH SELECTED"));
                return;
            }

            ResearchUnlockResult result =
                sessionController != null &&
                sessionController.Commands != null
                    ? sessionController.Commands.TryCompleteResearch(
                        terminal,
                        selectedResearch,
                        inputInventory)
                    : ResearchUnlockResult.MissingKnowledge;

            Refresh();
            if (result == ResearchUnlockResult.Succeeded)
            {
                SetStatus(TerminalUiText.Format(
                    "research.completed_named",
                    "COMPLETED {0}",
                    selectedResearch.DisplayName));
                return;
            }

            SetStatus(TerminalUiText.Get(
                "research.failed",
                "RESEARCH FAILED"));
            feedbackService?.Show(
                TerminalUiText.Get(
                    "research.failed_detail",
                    "The research could not be completed."),
                UiFeedbackSeverity.Caution);
        }

        public void Close()
        {
            systemRoot?.ScreenRouter?.Close(UiScreenId.ResearchTerminal);
        }

        void RebuildResearchList()
        {
            ClearResearchButtons();
            selectedResearch = null;
            if (terminal == null ||
                researchButtonContainer == null ||
                researchButtonPrefab == null)
            {
                return;
            }

            IReadOnlyList<ResearchDefinition> entries = terminal.AvailableResearch;
            for (int i = 0; i < entries.Count; i++)
            {
                ResearchDefinition research = entries[i];
                if (!terminal.CanOffer(research))
                {
                    continue;
                }

                MenuButtonView button =
                    Instantiate(researchButtonPrefab, researchButtonContainer);
                button.name = $"UI_ResearchButton_{research.ResearchId}";
                button.ConfigureContent(
                    research.DisplayName,
                    BuildResearchSubtitle(research),
                    null);
                ResearchDefinition capturedResearch = research;
                button.Clicked += () => SelectResearch(capturedResearch);
                researchButtons.Add(button);
                selectedResearch ??= research;
            }
        }

        void ClearResearchButtons()
        {
            for (int i = researchButtons.Count - 1; i >= 0; i--)
            {
                MenuButtonView button = researchButtons[i];
                if (button == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(button.gameObject);
                }
                else
                {
                    DestroyImmediate(button.gameObject);
                }
            }

            researchButtons.Clear();
        }

        void SelectResearch(ResearchDefinition research)
        {
            selectedResearch = research;
            Refresh();
        }

        void Subscribe()
        {
            if (completeButton != null)
            {
                completeButton.onClick.RemoveListener(CompleteSelected);
                completeButton.onClick.AddListener(CompleteSelected);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(Close);
                backButton.onClick.AddListener(Close);
            }
        }

        void Unsubscribe()
        {
            completeButton?.onClick.RemoveListener(CompleteSelected);
            backButton?.onClick.RemoveListener(Close);
        }

        void Refresh()
        {
            ResolveInventory();
            ConfigureActionLabels();
            SetText(
                titleText,
                TerminalUiText.Get("research.title", "RESEARCH TERMINAL"));
            if (selectedResearch == null)
            {
                SetText(
                    detailTitleText,
                    TerminalUiText.Get(
                        "research.no_research",
                        "NO RESEARCH"));
                SetText(
                    detailBodyText,
                    TerminalUiText.Get(
                        "research.unconfigured",
                        "This terminal has no available research."));
                SetStatus(TerminalUiText.Get(
                    "research.no_available",
                    "NO RESEARCH AVAILABLE"));
                SetInteractable(completeButton, false);
                return;
            }

            SetText(detailTitleText, selectedResearch.DisplayName.ToUpperInvariant());
            SetText(detailBodyText, BuildResearchDetail(selectedResearch));
            bool completed =
                sessionController != null &&
                sessionController.Commands != null &&
                sessionController.Commands.HasCompletedResearch(selectedResearch);
            ResearchUnlockResult result =
                sessionController != null &&
                sessionController.Commands != null
                    ? sessionController.Commands.CanCompleteResearch(
                        terminal,
                        selectedResearch,
                        inputInventory)
                    : ResearchUnlockResult.MissingKnowledge;
            bool canComplete = result == ResearchUnlockResult.Succeeded;
            SetInteractable(completeButton, canComplete);
            SetStatus(
                completed
                    ? TerminalUiText.Get("common.completed", "COMPLETED")
                    : canComplete
                        ? TerminalUiText.Get("common.ready", "READY")
                        : TerminalUiText.Get(
                            "common.unavailable",
                            "ACTION UNAVAILABLE"));

            if (screenView != null)
            {
                Selectable first = researchButtons.Count > 0
                    ? researchButtons[0].Button
                    : completeButton;
                screenView.SetFirstSelection(first);
            }
        }

        void ConfigureActionLabels()
        {
            ConfigureButton(
                completeButton,
                TerminalUiText.Get("research.action", "COMPLETE"),
                TerminalUiText.Get(
                    "research.action_detail",
                    "Spend requirements and unlock"));
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
            ResolveInventory();
        }

        void ResolveInventory()
        {
            if (inputInventory != null ||
                sessionController == null ||
                !sessionController.TryGetRuntime(out var runtime))
            {
                return;
            }

            inputInventory = runtime.PersonalShipBinding != null
                ? runtime.PersonalShipBinding.Cargo
                : runtime.LocalInventory;
        }

        static string BuildResearchSubtitle(ResearchDefinition research)
        {
            return research == null
                ? string.Empty
                : TerminalUiText.Format(
                    "research.subtitle",
                    "{0}  /  TIER {1}",
                    research.Domain,
                    research.Tier);
        }

        static string BuildResearchDetail(ResearchDefinition research)
        {
            if (research == null)
            {
                return string.Empty;
            }

            return
                $"{TerminalUiText.Get("research.required", "REQUIRED")}\n" +
                $"{FormatStacks(research.RequiredItems)}\n\n" +
                $"{TerminalUiText.Get("research.blueprints", "BLUEPRINTS")}\n" +
                $"{FormatRecipes(research)}\n\n" +
                $"{TerminalUiText.Get("research.capabilities", "CAPABILITIES")}\n" +
                FormatCapabilities(research.UnlockedCapabilityIds);
        }

        static string FormatStacks(IReadOnlyList<ItemStackDefinition> stacks)
        {
            if (stacks == null || stacks.Count == 0)
            {
                return TerminalUiText.Get("common.none", "NONE");
            }

            System.Text.StringBuilder builder = new();
            for (int i = 0; i < stacks.Count; i++)
            {
                ItemStackDefinition stack = stacks[i];
                if (stack == null || !stack.IsValid)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(stack.Amount);
                builder.Append(" x ");
                builder.Append(stack.Item.DisplayName);
            }

            return builder.Length > 0
                ? builder.ToString()
                : TerminalUiText.Get("common.none", "NONE");
        }

        static string FormatRecipes(ResearchDefinition research)
        {
            if (research.UnlockedRecipes == null ||
                research.UnlockedRecipes.Count == 0)
            {
                return TerminalUiText.Get("common.none", "NONE");
            }

            System.Text.StringBuilder builder = new();
            for (int i = 0; i < research.UnlockedRecipes.Count; i++)
            {
                if (research.UnlockedRecipes[i] == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(research.UnlockedRecipes[i].DisplayName);
            }

            return builder.Length > 0
                ? builder.ToString()
                : TerminalUiText.Get("common.none", "NONE");
        }

        static string FormatCapabilities(IReadOnlyList<string> capabilities)
        {
            if (capabilities == null || capabilities.Count == 0)
            {
                return TerminalUiText.Get("common.none", "NONE");
            }

            System.Text.StringBuilder builder = new();
            for (int i = 0; i < capabilities.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(capabilities[i]))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(capabilities[i].Trim());
            }

            return builder.Length > 0
                ? builder.ToString()
                : TerminalUiText.Get("common.none", "NONE");
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
