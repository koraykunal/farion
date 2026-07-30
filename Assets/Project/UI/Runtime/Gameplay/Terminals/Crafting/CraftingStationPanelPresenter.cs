using System.Collections.Generic;
using Farion.App.Flow;
using Farion.Gameplay.Crafting;
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
    public sealed class CraftingStationPanelPresenter : MonoBehaviour
    {
        [Header("Session")]
        [SerializeField] GameplaySessionController sessionController;

        [Header("Station")]
        [SerializeField] CraftingStationRuntime station;
        [SerializeField] InventoryContainerComponent inventory;

        [Header("Recipe List")]
        [SerializeField] Transform recipeButtonContainer;
        [SerializeField] MenuButtonView recipeButtonPrefab;

        [Header("Screen")]
        [SerializeField] UiScreenView screenView;
        [SerializeField] UiSystemRoot systemRoot;
        [SerializeField] UiFeedbackService feedbackService;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text detailTitleText;
        [SerializeField] TMP_Text detailBodyText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] Button craftButton;
        [SerializeField] Button backButton;

        readonly List<MenuButtonView> recipeButtons = new();
        RecipeDefinition selectedRecipe;

        void Awake()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            RebuildRecipeList();
            Refresh();
        }

        void OnDisable()
        {
            Unsubscribe();
            ClearRecipeButtons();
        }

        public bool Open(
            CraftingStationRuntime nextStation,
            InventoryContainerComponent nextInventory)
        {
            station = nextStation;
            inventory = nextInventory;
            ResolveReferences();
            RebuildRecipeList();
            Refresh();
            return systemRoot != null &&
                   systemRoot.ScreenRouter.Open(UiScreenId.CraftingTerminal);
        }

        public void CraftSelected()
        {
            ResolveReferences();
            if (selectedRecipe == null)
            {
                SetStatus(TerminalUiText.Get(
                    "crafting.no_selection",
                    "NO RECIPE SELECTED"));
                return;
            }

            if (station == null || inventory == null)
            {
                SetStatus(TerminalUiText.Get(
                    "crafting.not_ready",
                    "STATION IS NOT READY"));
                return;
            }

            CraftingRecipeResult result =
                sessionController != null &&
                sessionController.Commands != null
                    ? sessionController.Commands.TryCraft(
                        station,
                        selectedRecipe,
                        inventory)
                    : CraftingRecipeResult.UnauthorizedInventory;

            Refresh();
            if (result == CraftingRecipeResult.Succeeded)
            {
                SetStatus(TerminalUiText.Format(
                    "crafting.crafted",
                    "CRAFTED {0}",
                    selectedRecipe.DisplayName));
                return;
            }

            SetStatus(TerminalUiText.Get(
                "crafting.failed",
                "CRAFT FAILED"));
            feedbackService?.Show(
                TerminalUiText.Get(
                    "crafting.failed_detail",
                    "The recipe could not be produced."),
                UiFeedbackSeverity.Caution);
        }

        public void Close()
        {
            systemRoot?.ScreenRouter?.Close(UiScreenId.CraftingTerminal);
        }

        void RebuildRecipeList()
        {
            ClearRecipeButtons();
            selectedRecipe = null;
            if (station == null ||
                recipeButtonContainer == null ||
                recipeButtonPrefab == null)
            {
                return;
            }

            IReadOnlyList<RecipeDefinition> recipes = station.AvailableRecipes;
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDefinition recipe = recipes[i];
                if (!station.CanOffer(recipe))
                {
                    continue;
                }

                MenuButtonView button =
                    Instantiate(recipeButtonPrefab, recipeButtonContainer);
                button.name = $"UI_RecipeButton_{recipe.RecipeId}";
                button.ConfigureContent(
                    recipe.DisplayName,
                    BuildRecipeSubtitle(recipe),
                    null);
                RecipeDefinition capturedRecipe = recipe;
                button.Clicked += () => SelectRecipe(capturedRecipe);
                recipeButtons.Add(button);
                selectedRecipe ??= recipe;
            }
        }

        void ClearRecipeButtons()
        {
            for (int i = recipeButtons.Count - 1; i >= 0; i--)
            {
                MenuButtonView button = recipeButtons[i];
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

            recipeButtons.Clear();
        }

        void SelectRecipe(RecipeDefinition recipe)
        {
            selectedRecipe = recipe;
            Refresh();
        }

        void Subscribe()
        {
            if (craftButton != null)
            {
                craftButton.onClick.RemoveListener(CraftSelected);
                craftButton.onClick.AddListener(CraftSelected);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(Close);
                backButton.onClick.AddListener(Close);
            }
        }

        void Unsubscribe()
        {
            craftButton?.onClick.RemoveListener(CraftSelected);
            backButton?.onClick.RemoveListener(Close);
        }

        void Refresh()
        {
            ResolveInventory();
            ConfigureActionLabels();
            SetText(
                titleText,
                station != null
                    ? TerminalUiText.Format(
                        "crafting.station_title",
                        "{0} TERMINAL",
                        station.StationType).ToUpperInvariant()
                    : TerminalUiText.Get(
                        "crafting.title",
                        "CRAFTING TERMINAL"));

            if (selectedRecipe == null)
            {
                SetText(
                    detailTitleText,
                    TerminalUiText.Get("crafting.no_recipe", "NO RECIPE"));
                SetText(
                    detailBodyText,
                    TerminalUiText.Get(
                        "crafting.unconfigured",
                        "This station has no available recipes."));
                SetStatus(TerminalUiText.Get(
                    "crafting.no_available",
                    "NO RECIPE AVAILABLE"));
                SetInteractable(craftButton, false);
                return;
            }

            SetText(detailTitleText, selectedRecipe.DisplayName.ToUpperInvariant());
            SetText(detailBodyText, BuildRecipeDetail(selectedRecipe));
            CraftingRecipeResult result =
                sessionController != null &&
                sessionController.Commands != null &&
                station != null
                    ? sessionController.Commands.CanCraft(
                        station,
                        selectedRecipe,
                        inventory)
                    : CraftingRecipeResult.UnauthorizedInventory;
            bool canCraft = result == CraftingRecipeResult.Succeeded;
            SetInteractable(craftButton, canCraft);
            SetStatus(canCraft
                ? TerminalUiText.Get("common.ready", "READY")
                : TerminalUiText.Get(
                    "common.unavailable",
                    "ACTION UNAVAILABLE"));

            if (screenView != null)
            {
                Selectable first = recipeButtons.Count > 0
                    ? recipeButtons[0].Button
                    : craftButton;
                screenView.SetFirstSelection(first);
            }
        }

        void ConfigureActionLabels()
        {
            ConfigureButton(
                craftButton,
                TerminalUiText.Get("crafting.action", "FABRICATE"),
                TerminalUiText.Get(
                    "crafting.action_detail",
                    "Consume selected materials"));
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
            if (inventory != null ||
                sessionController == null ||
                !sessionController.TryGetRuntime(out var runtime))
            {
                return;
            }

            inventory = runtime.PersonalShipBinding != null
                ? runtime.PersonalShipBinding.Cargo
                : runtime.LocalInventory;
        }

        static string BuildRecipeSubtitle(RecipeDefinition recipe)
        {
            return recipe == null
                ? string.Empty
                : $"{recipe.StationType}  /  {recipe.ProcessingDurationSeconds:0.#}s";
        }

        static string BuildRecipeDetail(RecipeDefinition recipe)
        {
            if (recipe == null)
            {
                return string.Empty;
            }

            return
                $"{TerminalUiText.Get("crafting.inputs", "INPUTS")}\n" +
                $"{FormatStacks(recipe.Inputs)}\n\n" +
                $"{TerminalUiText.Get("crafting.outputs", "OUTPUTS")}\n" +
                FormatStacks(recipe.Outputs);
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
