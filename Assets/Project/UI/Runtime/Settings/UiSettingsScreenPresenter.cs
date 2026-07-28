using System.Collections.Generic;
using Farion.UI.Foundation;
using Farion.UI.Localization;
using Farion.UI.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using UiNavigation = UnityEngine.UI.Navigation;

namespace Farion.UI.Settings
{
    [DisallowMultipleComponent]
    public sealed class UiSettingsScreenPresenter : MonoBehaviour
    {
        [Header("Composition")]
        [SerializeField] UiScreenView screenView;
        [SerializeField] UiSystemRoot systemRoot;
        [SerializeField] UiPreferencesService preferences;
        [SerializeField] List<UiSettingsOptionView> options = new();
        [SerializeField] List<UiSettingsCategoryView> categories = new();
        [SerializeField] GameObject interfaceGroup;
        [SerializeField] GameObject accessibilityGroup;

        [Header("Header")]
        [SerializeField] TMP_Text titleText;

        [Header("Context")]
        [SerializeField] TMP_Text categoryTitleText;
        [SerializeField] TMP_Text detailTitleText;
        [SerializeField] TMP_Text detailDescriptionText;
        [SerializeField] TMP_Text detailValueText;

        [Header("Actions")]
        [SerializeField] TMP_Text backLabelText;
        [SerializeField] Button backButton;

        bool subscribed;
        UiSettingsCategory activeCategory = UiSettingsCategory.Interface;
        UiSettingsOptionView focusedOption;

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            ResolveReferences();
            if (!Application.isPlaying)
            {
                return;
            }

            Subscribe();
            Refresh();
            AssignInitialSelection();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        public void Close()
        {
            ResolveReferences();
            systemRoot?.ScreenRouter?.Close(UiScreenId.Settings);
        }

        void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (preferences != null)
            {
                preferences.Changed += Refresh;
            }

            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;

            for (int i = 0; i < options.Count; i++)
            {
                UiSettingsOptionView option = options[i];
                if (option == null)
                {
                    continue;
                }

                option.AdjustmentRequested += HandleAdjustment;
                option.Focused += HandleOptionFocused;
            }

            for (int i = 0; i < categories.Count; i++)
            {
                UiSettingsCategoryView category = categories[i];
                if (category != null)
                {
                    category.Chosen += HandleCategoryChosen;
                }
            }

            if (backButton != null)
            {
                backButton.onClick.AddListener(Close);
            }

            subscribed = true;
        }

        void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (preferences != null)
            {
                preferences.Changed -= Refresh;
            }

            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;

            for (int i = 0; i < options.Count; i++)
            {
                UiSettingsOptionView option = options[i];
                if (option == null)
                {
                    continue;
                }

                option.AdjustmentRequested -= HandleAdjustment;
                option.Focused -= HandleOptionFocused;
            }

            for (int i = 0; i < categories.Count; i++)
            {
                UiSettingsCategoryView category = categories[i];
                if (category != null)
                {
                    category.Chosen -= HandleCategoryChosen;
                }
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(Close);
            }

            subscribed = false;
        }

        void HandleLocaleChanged(Locale _)
        {
            Refresh();
        }

        void HandleCategoryChosen(UiSettingsCategoryView category)
        {
            if (category != null)
            {
                SetCategory(category.Category, moveFocusToOption: true);
            }
        }

        void HandleOptionFocused(UiSettingsOptionView option)
        {
            focusedOption = option;
            RefreshDetail();
        }

        void HandleAdjustment(UiSettingsOptionView option, int direction)
        {
            if (preferences == null || option == null)
            {
                return;
            }

            switch (option.SettingId)
            {
                case UiSettingId.Language:
                    preferences.CycleLocale(direction);
                    break;
                case UiSettingId.UiScale:
                    preferences.CycleUiScale(direction);
                    break;
                case UiSettingId.ReducedMotion:
                    preferences.SetReducedMotion(!preferences.ReducedMotion);
                    break;
                case UiSettingId.Subtitles:
                    preferences.SetSubtitlesEnabled(!preferences.SubtitlesEnabled);
                    break;
                case UiSettingId.SubtitleSize:
                    preferences.SetSubtitleSize(
                        preferences.SubtitleSize == UiSubtitleSize.Standard
                            ? UiSubtitleSize.Large
                            : UiSubtitleSize.Standard);
                    break;
            }
        }

        void Refresh()
        {
            SetText(titleText, "settings.title", "SETTINGS");
            SetText(backLabelText, "common.back", "BACK");

            for (int i = 0; i < categories.Count; i++)
            {
                UiSettingsCategoryView category = categories[i];
                if (category != null)
                {
                    category.ConfigureContent(GetCategoryLabel(category.Category));
                }
            }

            for (int i = 0; i < options.Count; i++)
            {
                UiSettingsOptionView option = options[i];
                if (option != null)
                {
                    RefreshOption(option);
                }
            }

            SetCategory(activeCategory, moveFocusToOption: false);
        }

        void RefreshOption(UiSettingsOptionView option)
        {
            if (preferences == null)
            {
                return;
            }

            switch (option.SettingId)
            {
                case UiSettingId.Language:
                    option.ConfigureContent(
                        UiLocalization.Get("settings.language.title", "Language"),
                        UiLocalization.Get(
                            "settings.language.description",
                            "Choose the language used by menus and system messages."),
                        preferences.LocaleCode == UiLocalization.TurkishLocaleCode
                            ? UiLocalization.Get("language.turkish", "Turkish")
                            : UiLocalization.Get("language.english", "English"));
                    option.SetAvailable(true);
                    break;
                case UiSettingId.UiScale:
                    option.ConfigureContent(
                        UiLocalization.Get("settings.scale.title", "Interface scale"),
                        UiLocalization.Get(
                            "settings.scale.description",
                            "Increase the size of menus and operational readouts."),
                        $"{Mathf.RoundToInt(preferences.UiScale * 100f)}%");
                    option.SetAvailable(true);
                    break;
                case UiSettingId.ReducedMotion:
                    option.ConfigureContent(
                        UiLocalization.Get("settings.motion.title", "Reduced motion"),
                        UiLocalization.Get(
                            "settings.motion.description",
                            "Replace most interface transitions with immediate state changes."),
                        ResolveBoolean(preferences.ReducedMotion));
                    option.SetAvailable(true);
                    break;
                case UiSettingId.Subtitles:
                    option.ConfigureContent(
                        UiLocalization.Get("settings.subtitles.title", "Subtitles"),
                        UiLocalization.Get(
                            "settings.subtitles.description",
                            "Display spoken dialogue and important radio communication."),
                        ResolveBoolean(preferences.SubtitlesEnabled));
                    option.SetAvailable(true);
                    break;
                case UiSettingId.SubtitleSize:
                    option.ConfigureContent(
                        UiLocalization.Get("settings.subtitle_size.title", "Subtitle size"),
                        UiLocalization.Get(
                            "settings.subtitle_size.description",
                            "Set the reading size used by dialogue captions."),
                        preferences.SubtitleSize == UiSubtitleSize.Large
                            ? UiLocalization.Get("value.large", "Large")
                            : UiLocalization.Get("value.standard", "Standard"));
                    option.SetAvailable(true);
                    break;
            }
        }

        void SetCategory(
            UiSettingsCategory category,
            bool moveFocusToOption)
        {
            UiSettingsOptionView previousFocus = focusedOption;
            activeCategory = category;

            if (interfaceGroup != null)
            {
                interfaceGroup.SetActive(category == UiSettingsCategory.Interface);
            }

            if (accessibilityGroup != null)
            {
                accessibilityGroup.SetActive(
                    category == UiSettingsCategory.Accessibility);
            }

            for (int i = 0; i < categories.Count; i++)
            {
                UiSettingsCategoryView categoryView = categories[i];
                if (categoryView != null)
                {
                    categoryView.SetCurrent(categoryView.Category == category);
                }
            }

            if (categoryTitleText != null)
            {
                categoryTitleText.text = GetCategoryLabel(category).ToUpperInvariant();
            }

            ConfigureNavigation();
            focusedOption =
                previousFocus != null &&
                previousFocus.IsActive() &&
                previousFocus.IsInteractable() &&
                BelongsToCategory(previousFocus, category)
                    ? previousFocus
                    : FindFirstOption(category);
            RefreshDetail();

            if (moveFocusToOption && focusedOption != null)
            {
                focusedOption.Select();
            }
        }

        void ConfigureNavigation()
        {
            UiSettingsCategoryView currentCategory = categories.Find(view =>
                view != null && view.Category == activeCategory);
            UiSettingsCategoryView interfaceCategory = categories.Find(view =>
                view != null && view.Category == UiSettingsCategory.Interface);
            UiSettingsCategoryView accessibilityCategory = categories.Find(view =>
                view != null && view.Category == UiSettingsCategory.Accessibility);

            if (interfaceCategory != null)
            {
                interfaceCategory.navigation = new UiNavigation
                {
                    mode = UiNavigation.Mode.Explicit,
                    selectOnUp = backButton,
                    selectOnDown = accessibilityCategory != null
                        ? (Selectable)accessibilityCategory
                        : backButton
                };
            }

            if (accessibilityCategory != null)
            {
                accessibilityCategory.navigation = new UiNavigation
                {
                    mode = UiNavigation.Mode.Explicit,
                    selectOnUp = interfaceCategory != null
                        ? (Selectable)interfaceCategory
                        : backButton,
                    selectOnDown = backButton
                };
            }

            List<UiSettingsOptionView> activeOptions = new();
            for (int i = 0; i < options.Count; i++)
            {
                UiSettingsOptionView option = options[i];
                if (option != null && option.IsActive() && option.IsInteractable())
                {
                    activeOptions.Add(option);
                }
            }

            for (int i = 0; i < activeOptions.Count; i++)
            {
                activeOptions[i].navigation = new UiNavigation
                {
                    mode = UiNavigation.Mode.Explicit,
                    selectOnUp = i == 0
                        ? currentCategory
                        : activeOptions[i - 1],
                    selectOnDown = i == activeOptions.Count - 1
                        ? backButton
                        : activeOptions[i + 1]
                };
            }

            if (backButton != null)
            {
                backButton.navigation = new UiNavigation
                {
                    mode = UiNavigation.Mode.Explicit,
                    selectOnUp = activeOptions.Count > 0
                        ? activeOptions[^1]
                        : currentCategory,
                    selectOnDown = currentCategory
                };
            }
        }

        UiSettingsOptionView FindFirstOption(UiSettingsCategory category)
        {
            for (int i = 0; i < options.Count; i++)
            {
                UiSettingsOptionView option = options[i];
                if (option == null ||
                    !option.IsActive() ||
                    !option.IsInteractable())
                {
                    continue;
                }

                if (BelongsToCategory(option, category))
                {
                    return option;
                }
            }

            return null;
        }

        static bool BelongsToCategory(
            UiSettingsOptionView option,
            UiSettingsCategory category)
        {
            if (option == null)
            {
                return false;
            }

            return category switch
            {
                UiSettingsCategory.Interface =>
                    option.SettingId is UiSettingId.Language or UiSettingId.UiScale,
                UiSettingsCategory.Accessibility =>
                    option.SettingId is UiSettingId.ReducedMotion or
                        UiSettingId.Subtitles or
                        UiSettingId.SubtitleSize,
                _ => false
            };
        }

        void RefreshDetail()
        {
            UiSettingsOptionView option =
                focusedOption != null && focusedOption.IsActive()
                    ? focusedOption
                    : FindFirstOption(activeCategory);
            if (option == null)
            {
                return;
            }

            if (detailTitleText != null)
            {
                detailTitleText.text = option.DisplayTitle.ToUpperInvariant();
            }

            if (detailDescriptionText != null)
            {
                detailDescriptionText.text = option.DisplayDescription;
            }

            if (detailValueText != null)
            {
                detailValueText.text = option.DisplayValue.ToUpperInvariant();
            }
        }

        string ResolveBoolean(bool value)
        {
            return value
                ? UiLocalization.Get("value.on", "On")
                : UiLocalization.Get("value.off", "Off");
        }

        static string GetCategoryLabel(UiSettingsCategory category)
        {
            return category == UiSettingsCategory.Accessibility
                ? UiLocalization.Get("settings.subtitle", "Accessibility")
                : UiLocalization.Get("settings.section.interface", "Interface");
        }

        void ResolveReferences()
        {
            screenView ??= GetComponent<UiScreenView>();
            systemRoot ??= UiCompositionScope.FindSystemRoot(this);
            preferences ??= systemRoot != null
                ? systemRoot.PreferencesService
                : UiCompositionScope.FindFirstInScope<UiPreferencesService>(this);

            options ??= new List<UiSettingsOptionView>();
            if (options.Count == 0)
            {
                options.AddRange(GetComponentsInChildren<UiSettingsOptionView>(true));
            }

            categories ??= new List<UiSettingsCategoryView>();
            if (categories.Count == 0)
            {
                categories.AddRange(
                    GetComponentsInChildren<UiSettingsCategoryView>(true));
            }
        }

        void AssignInitialSelection()
        {
            if (screenView == null)
            {
                return;
            }

            UiSettingsCategoryView firstCategory = categories.Find(category =>
                category != null &&
                category.Category == UiSettingsCategory.Interface &&
                category.IsActive() &&
                category.IsInteractable());
            if (firstCategory != null)
            {
                screenView.SetFirstSelection(firstCategory);
                return;
            }

            UiSettingsOptionView firstOption = FindFirstOption(activeCategory);
            if (firstOption != null)
            {
                screenView.SetFirstSelection(firstOption);
            }
        }

        static void SetText(TMP_Text target, string key, string fallback)
        {
            if (target != null)
            {
                target.text = UiLocalization.Get(key, fallback);
            }
        }
    }
}
