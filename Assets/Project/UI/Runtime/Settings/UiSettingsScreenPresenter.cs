using System.Collections.Generic;
using System.Globalization;
using Farion.Audio.Direction;
using Farion.UI.Common;
using Farion.UI.Feedback;
using Farion.UI.Foundation;
using Farion.UI.Localization;
using Farion.UI.Navigation;
using Farion.UI.Styling;
using TMPro;
using UiNavigation = UnityEngine.UI.Navigation;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

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
        [SerializeField] GameObject audioGroup;
        [SerializeField] GameObject displayGroup;
        [SerializeField] GameObject controlsGroup;

        [Header("Header")]
        [SerializeField] TMP_Text titleText;

        [Header("Context")]
        [SerializeField] TMP_Text categoryTitleText;
        [SerializeField] TMP_Text detailTitleText;
        [SerializeField] TMP_Text detailDescriptionText;
        [SerializeField] TMP_Text detailValueText;

        [Header("Display")]
        [SerializeField] UiDropdownView displayModeDropdown;
        [SerializeField] UiDropdownView resolutionDropdown;
        [SerializeField] UiDropdownView refreshRateDropdown;

        [Header("Actions")]
        [SerializeField] UiConfirmationDialog displayConfirmationDialog;
        [SerializeField, Min(3f)] float displayRevertSeconds = 12f;
        [SerializeField] UiSettingsActionView applyActionView;
        [SerializeField] TMP_Text applyLabelText;
        [SerializeField] Button applyButton;
        [SerializeField] TMP_Text backLabelText;
        [SerializeField] Button backButton;

        bool subscribed;
        AudioDirector audioDirector;
        UiSettingsCategory activeCategory = UiSettingsCategory.Display;
        IUiValueControl focusedControl;
        readonly List<IUiValueControl> activeControls = new();
        readonly List<IUiValueControl> lookupControls = new();
        readonly List<string> optionLabels = new();

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
            ApplyTypography();
        }

        void OnEnable()
        {
            ResolveReferences();
            ApplyTypography();
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
            preferences?.DiscardPendingDisplayPreferences();
        }

        public bool HasCompleteDisplayControls =>
            displayModeDropdown != null &&
            resolutionDropdown != null &&
            refreshRateDropdown != null;

        public void Close()
        {
            ResolveReferences();
            systemRoot?.ScreenRouter?.Close(UiScreenId.Settings);
        }

        public void ApplyDisplayPreferences()
        {
            if (preferences == null || !preferences.HasPendingDisplayChanges)
            {
                return;
            }

            preferences.ApplyDisplayPreferences();
            RequestDisplayConfirmation();
            if (activeCategory == UiSettingsCategory.Display)
            {
                focusedControl = FindFirstControl(activeCategory);
                focusedControl?.Selectable.Select();
            }
        }

        void RequestDisplayConfirmation()
        {
            ResolveConfirmationDialog();
            bool presented = displayConfirmationDialog != null &&
                displayConfirmationDialog.Present(
                    UiLocalization.ToDisplayUpper(
                        UiLocalization.Get(UiTextKeys.SettingsDisplayConfirmTitle)),
                    UiLocalization.Get(UiTextKeys.SettingsDisplayConfirmBody),
                    UiLocalization.Get(UiTextKeys.SettingsDisplayConfirmKeep),
                    preferences.ConfirmDisplayPreferences,
                    UiLocalization.Get(UiTextKeys.SettingsDisplayConfirmRevert),
                    preferences.RevertDisplayPreferences,
                    displayRevertSeconds);
            if (!presented)
            {
                preferences.ConfirmDisplayPreferences();
            }
        }

        void ResolveConfirmationDialog()
        {
            if (displayConfirmationDialog != null)
            {
                return;
            }

            UiSystemRoot root = systemRoot != null
                ? systemRoot
                : UiCompositionScope.FindSystemRoot(this);
            if (root != null)
            {
                displayConfirmationDialog =
                    root.GetComponentInChildren<UiConfirmationDialog>(true);
            }
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

            audioDirector = AudioDirector.Current;
            if (audioDirector != null)
            {
                audioDirector.Changed += Refresh;
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
                option.Focused += HandleControlFocused;
            }

            BindDropdown(displayModeDropdown, HandleDisplayModeChanged, true);
            BindDropdown(resolutionDropdown, HandleResolutionChanged, true);
            BindDropdown(refreshRateDropdown, HandleRefreshRateChanged, true);

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

            if (applyButton != null)
            {
                applyButton.onClick.AddListener(ApplyDisplayPreferences);
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

            if (audioDirector != null)
            {
                audioDirector.Changed -= Refresh;
                audioDirector = null;
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
                option.Focused -= HandleControlFocused;
            }

            BindDropdown(displayModeDropdown, HandleDisplayModeChanged, false);
            BindDropdown(resolutionDropdown, HandleResolutionChanged, false);
            BindDropdown(refreshRateDropdown, HandleRefreshRateChanged, false);

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

            if (applyButton != null)
            {
                applyButton.onClick.RemoveListener(ApplyDisplayPreferences);
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

        void BindDropdown(
            UiDropdownView dropdown,
            System.Action<UiDropdownView, int> handler,
            bool subscribe)
        {
            if (dropdown == null)
            {
                return;
            }

            dropdown.Focused -= HandleControlFocused;
            dropdown.SelectionChanged -= handler;
            if (subscribe)
            {
                dropdown.Focused += HandleControlFocused;
                dropdown.SelectionChanged += handler;
            }
        }

        void HandleControlFocused(IUiValueControl control)
        {
            focusedControl = control;
            RefreshDetail();
        }

        void HandleDisplayModeChanged(UiDropdownView dropdown, int index)
        {
            preferences?.SetDisplayModeIndex(index);
        }

        void HandleResolutionChanged(UiDropdownView dropdown, int index)
        {
            preferences?.SetResolutionIndex(index);
        }

        void HandleRefreshRateChanged(UiDropdownView dropdown, int index)
        {
            preferences?.SetRefreshRateIndex(index);
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
                case UiSettingId.MasterVolume:
                    audioDirector?.AdjustBusVolume(AudioBusId.Master, direction);
                    break;
                case UiSettingId.MusicVolume:
                    audioDirector?.AdjustBusVolume(AudioBusId.Music, direction);
                    break;
                case UiSettingId.AmbienceVolume:
                    audioDirector?.AdjustBusVolume(AudioBusId.Ambience, direction);
                    break;
                case UiSettingId.SfxVolume:
                    audioDirector?.AdjustBusVolume(AudioBusId.Sfx, direction);
                    break;
                case UiSettingId.UiVolume:
                    audioDirector?.AdjustBusVolume(AudioBusId.Ui, direction);
                    break;
                case UiSettingId.VSync:
                    preferences.SetVSyncEnabled(!preferences.VSyncEnabled);
                    break;
                case UiSettingId.FieldOfView:
                    preferences.AdjustFieldOfView(direction);
                    break;
                case UiSettingId.MouseSensitivity:
                    preferences.AdjustMouseSensitivity(direction);
                    break;
                case UiSettingId.InvertLookY:
                    preferences.SetInvertLookY(!preferences.InvertLookY);
                    break;
            }
        }

        void Refresh()
        {
            SetText(titleText, UiTextKeys.SettingsTitle);
            SetText(applyLabelText, UiTextKeys.CommonApply);
            SetText(backLabelText, UiTextKeys.CommonBack);

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

            RefreshDisplayControls();
            SetCategory(activeCategory, moveFocusToOption: false);
        }

        void RefreshDisplayControls()
        {
            if (preferences == null)
            {
                return;
            }

            if (displayModeDropdown != null)
            {
                IReadOnlyList<FullScreenMode> modes = preferences.DisplayModes;
                optionLabels.Clear();
                for (int i = 0; i < modes.Count; i++)
                {
                    optionLabels.Add(
                        UiLocalization.Get(ResolveDisplayModeKey(modes[i])));
                }

                displayModeDropdown.ConfigureContent(
                    UiLocalization.Get(UiTextKeys.SettingsDisplayModeTitle),
                    UiLocalization.Get(UiTextKeys.SettingsDisplayModeDescription));
                displayModeDropdown.SetOptions(
                    optionLabels,
                    preferences.DisplayModeIndex);
                displayModeDropdown.SetAvailable(true);
                displayModeDropdown.SetPending(preferences.HasPendingDisplayMode);
            }

            if (resolutionDropdown != null)
            {
                IReadOnlyList<Vector2Int> resolutions = preferences.Resolutions;
                optionLabels.Clear();
                for (int i = 0; i < resolutions.Count; i++)
                {
                    optionLabels.Add(
                        $"{resolutions[i].x} \u00D7 {resolutions[i].y}");
                }

                resolutionDropdown.ConfigureContent(
                    UiLocalization.Get(UiTextKeys.SettingsResolutionTitle),
                    UiLocalization.Get(UiTextKeys.SettingsResolutionDescription));
                resolutionDropdown.SetOptions(
                    optionLabels,
                    preferences.ResolutionIndex);
                resolutionDropdown.SetAvailable(preferences.HasSelectableResolutions);
                resolutionDropdown.SetPending(preferences.HasPendingResolution);
            }

            if (refreshRateDropdown != null)
            {
                IReadOnlyList<RefreshRate> rates = preferences.RefreshRates;
                optionLabels.Clear();
                for (int i = 0; i < rates.Count; i++)
                {
                    optionLabels.Add(FormatRefreshRate(rates[i]));
                }

                refreshRateDropdown.ConfigureContent(
                    UiLocalization.Get(UiTextKeys.SettingsRefreshRateTitle),
                    UiLocalization.Get(UiTextKeys.SettingsRefreshRateDescription));
                refreshRateDropdown.SetOptions(
                    optionLabels,
                    preferences.RefreshRateIndex);
                refreshRateDropdown.SetAvailable(
                    preferences.SupportsRefreshRateSelection);
                refreshRateDropdown.SetPending(preferences.HasPendingRefreshRate);
            }
        }

        static string FormatRefreshRate(RefreshRate rate)
        {
            CultureInfo culture = UiLocalization.ResolveCulture();
            double value = rate.value;
            string number = System.Math.Abs(value - System.Math.Round(value)) < 0.01d
                ? value.ToString("0", culture)
                : value.ToString("0.00", culture);
            return number + " Hz";
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
                        UiLocalization.Get(UiTextKeys.SettingsLanguageTitle),
                        UiLocalization.Get(UiTextKeys.SettingsLanguageDescription),
                        preferences.LocaleCode == UiLocalization.TurkishLocaleCode
                            ? UiLocalization.Get(UiTextKeys.LanguageTurkish)
                            : UiLocalization.Get(UiTextKeys.LanguageEnglish));
                    option.SetAvailable(true);
                    break;
                case UiSettingId.UiScale:
                    option.ConfigureContent(
                        UiLocalization.Get(UiTextKeys.SettingsScaleTitle),
                        UiLocalization.Get(UiTextKeys.SettingsScaleDescription),
                        $"{Mathf.RoundToInt(preferences.UiScale * 100f)}%");
                    option.SetAvailable(true);
                    break;
                case UiSettingId.ReducedMotion:
                    option.ConfigureContent(
                        UiLocalization.Get(UiTextKeys.SettingsMotionTitle),
                        UiLocalization.Get(UiTextKeys.SettingsMotionDescription),
                        ResolveBoolean(preferences.ReducedMotion));
                    option.SetAvailable(true);
                    break;
                case UiSettingId.Subtitles:
                    option.ConfigureContent(
                        UiLocalization.Get(UiTextKeys.SettingsSubtitlesTitle),
                        UiLocalization.Get(UiTextKeys.SettingsSubtitlesDescription),
                        ResolveBoolean(preferences.SubtitlesEnabled));
                    option.SetAvailable(true);
                    break;
                case UiSettingId.SubtitleSize:
                    option.ConfigureContent(
                        UiLocalization.Get(UiTextKeys.SettingsSubtitleSizeTitle),
                        UiLocalization.Get(UiTextKeys.SettingsSubtitleSizeDescription),
                        preferences.SubtitleSize == UiSubtitleSize.Large
                            ? UiLocalization.Get(UiTextKeys.ValueLarge)
                            : UiLocalization.Get(UiTextKeys.ValueStandard));
                    option.SetAvailable(true);
                    break;
                case UiSettingId.MasterVolume:
                    ConfigureVolumeOption(
                        option,
                        AudioBusId.Master,
                        UiTextKeys.SettingsAudioMasterTitle,
                        "Master volume",
                        UiTextKeys.SettingsAudioMasterDescription,
                        "Control the complete game mix.");
                    break;
                case UiSettingId.MusicVolume:
                    ConfigureVolumeOption(
                        option,
                        AudioBusId.Music,
                        UiTextKeys.SettingsAudioMusicTitle,
                        "Music volume",
                        UiTextKeys.SettingsAudioMusicDescription,
                        "Control the adaptive score.");
                    break;
                case UiSettingId.AmbienceVolume:
                    ConfigureVolumeOption(
                        option,
                        AudioBusId.Ambience,
                        UiTextKeys.SettingsAudioAmbienceTitle,
                        "Ambience volume",
                        UiTextKeys.SettingsAudioAmbienceDescription,
                        "Control space, atmosphere, and interior ambience.");
                    break;
                case UiSettingId.SfxVolume:
                    ConfigureVolumeOption(
                        option,
                        AudioBusId.Sfx,
                        UiTextKeys.SettingsAudioSfxTitle,
                        "SFX volume",
                        UiTextKeys.SettingsAudioSfxDescription,
                        "Control spacecraft and world sound effects.");
                    break;
                case UiSettingId.UiVolume:
                    ConfigureVolumeOption(
                        option,
                        AudioBusId.Ui,
                        UiTextKeys.SettingsAudioUiTitle,
                        "UI volume",
                        UiTextKeys.SettingsAudioUiDescription,
                        "Control interface navigation and feedback sounds.");
                    break;
                case UiSettingId.VSync:
                    option.ConfigureContent(
                        UiLocalization.Get(UiTextKeys.SettingsVSyncTitle),
                        UiLocalization.Get(UiTextKeys.SettingsVSyncDescription),
                        ResolveBoolean(preferences.VSyncEnabled));
                    option.SetAvailable(true);
                    break;
                case UiSettingId.FieldOfView:
                    option.ConfigureContent(
                        UiLocalization.Get(UiTextKeys.SettingsFieldOfViewTitle),
                        UiLocalization.Get(UiTextKeys.SettingsFieldOfViewDescription),
                        preferences.FieldOfView.ToString(
                            "0",
                            CultureInfo.InvariantCulture));
                    option.SetAvailable(true);
                    break;
                case UiSettingId.MouseSensitivity:
                    option.ConfigureContent(
                        UiLocalization.Get(UiTextKeys.SettingsMouseSensitivityTitle),
                        UiLocalization.Get(
                            UiTextKeys.SettingsMouseSensitivityDescription),
                        preferences.MouseSensitivity.ToString(
                            "0.0",
                            CultureInfo.InvariantCulture));
                    option.SetAvailable(true);
                    break;
                case UiSettingId.InvertLookY:
                    option.ConfigureContent(
                        UiLocalization.Get(UiTextKeys.SettingsInvertLookTitle),
                        UiLocalization.Get(UiTextKeys.SettingsInvertLookDescription),
                        ResolveBoolean(preferences.InvertLookY));
                    option.SetAvailable(true);
                    break;
            }
        }

        void ConfigureVolumeOption(
            UiSettingsOptionView option,
            AudioBusId bus,
            string titleKey,
            string titleFallback,
            string descriptionKey,
            string descriptionFallback)
        {
            if (audioDirector == null)
            {
                option.SetAvailable(false);
                return;
            }

            option.ConfigureContent(
                UiLocalization.Get(titleKey),
                UiLocalization.Get(descriptionKey),
                $"{Mathf.RoundToInt(audioDirector.GetBusVolume(bus) * 100f)}%");
            option.SetAvailable(true);
        }

        void SetCategory(
            UiSettingsCategory category,
            bool moveFocusToOption)
        {
            IUiValueControl previousFocus = focusedControl;
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

            if (audioGroup != null)
            {
                audioGroup.SetActive(category == UiSettingsCategory.Audio);
            }

            if (displayGroup != null)
            {
                displayGroup.SetActive(category == UiSettingsCategory.Display);
            }

            if (controlsGroup != null)
            {
                controlsGroup.SetActive(category == UiSettingsCategory.Controls);
            }

            applyActionView?.SetAvailable(
                category == UiSettingsCategory.Display &&
                preferences != null &&
                preferences.HasPendingDisplayChanges);

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
                categoryTitleText.text =
                    UiLocalization.ToDisplayUpper(GetCategoryLabel(category));
            }

            ConfigureNavigation();
            focusedControl =
                IsUsable(previousFocus) && BelongsToCategory(previousFocus, category)
                    ? previousFocus
                    : FindFirstControl(category);
            RefreshDetail();

            if (moveFocusToOption)
            {
                focusedControl?.Selectable.Select();
            }
        }

        void ConfigureNavigation()
        {
            UiSettingsCategoryView currentCategory = categories.Find(view =>
                view != null && view.Category == activeCategory);
            List<UiSettingsCategoryView> activeCategories = categories.FindAll(view =>
                view != null && view.IsActive() && view.IsInteractable());
            for (int i = 0; i < activeCategories.Count; i++)
            {
                activeCategories[i].navigation = new UiNavigation
                {
                    mode = UiNavigation.Mode.Explicit,
                    selectOnUp = i > 0 ? activeCategories[i - 1] : backButton,
                    selectOnDown = i < activeCategories.Count - 1
                        ? activeCategories[i + 1]
                        : backButton
                };
            }

            CollectControls(activeCategory, activeControls);
            bool canApply = applyButton != null &&
                applyButton.gameObject.activeInHierarchy &&
                applyButton.IsInteractable();
            Selectable lastControl = activeControls.Count > 0
                ? activeControls[^1].Selectable
                : currentCategory;

            for (int i = 0; i < activeControls.Count; i++)
            {
                activeControls[i].Selectable.navigation = new UiNavigation
                {
                    mode = UiNavigation.Mode.Explicit,
                    selectOnUp = i == 0
                        ? currentCategory
                        : activeControls[i - 1].Selectable,
                    selectOnDown = i == activeControls.Count - 1
                        ? canApply ? applyButton : backButton
                        : activeControls[i + 1].Selectable
                };
            }

            if (canApply)
            {
                applyButton.navigation = new UiNavigation
                {
                    mode = UiNavigation.Mode.Explicit,
                    selectOnUp = lastControl,
                    selectOnDown = backButton
                };
            }

            if (backButton != null)
            {
                backButton.navigation = new UiNavigation
                {
                    mode = UiNavigation.Mode.Explicit,
                    selectOnUp = canApply ? applyButton : lastControl,
                    selectOnDown = currentCategory
                };
            }
        }

        void CollectControls(
            UiSettingsCategory category,
            List<IUiValueControl> result)
        {
            result.Clear();
            GameObject group = ResolveGroup(category);
            if (group == null)
            {
                return;
            }

            Selectable[] candidates =
                group.GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] is IUiValueControl control && IsUsable(control))
                {
                    result.Add(control);
                }
            }
        }

        GameObject ResolveGroup(UiSettingsCategory category)
        {
            return category switch
            {
                UiSettingsCategory.Controls => controlsGroup,
                UiSettingsCategory.Display => displayGroup,
                UiSettingsCategory.Audio => audioGroup,
                UiSettingsCategory.Interface => interfaceGroup,
                UiSettingsCategory.Accessibility => accessibilityGroup,
                _ => null
            };
        }

        static bool IsUsable(IUiValueControl control)
        {
            return control?.Selectable != null &&
                   control.Selectable.IsActive() &&
                   control.Selectable.IsInteractable();
        }

        IUiValueControl FindFirstControl(UiSettingsCategory category)
        {
            CollectControls(category, lookupControls);
            return lookupControls.Count > 0 ? lookupControls[0] : null;
        }

        bool BelongsToCategory(
            IUiValueControl control,
            UiSettingsCategory category)
        {
            GameObject group = ResolveGroup(category);
            return control?.Selectable != null &&
                   group != null &&
                   control.Selectable.transform.IsChildOf(group.transform);
        }

        void RefreshDetail()
        {
            IUiValueControl control = IsUsable(focusedControl)
                ? focusedControl
                : FindFirstControl(activeCategory);
            if (control == null)
            {
                return;
            }

            if (detailTitleText != null)
            {
                detailTitleText.text =
                    UiLocalization.ToDisplayUpper(control.DisplayTitle);
            }

            if (detailDescriptionText != null)
            {
                detailDescriptionText.text = control.DisplayDescription;
            }

            if (detailValueText != null)
            {
                detailValueText.text =
                    UiLocalization.ToDisplayUpper(control.DisplayValue);
            }
        }

        string ResolveBoolean(bool value)
        {
            return value
                ? UiLocalization.Get(UiTextKeys.ValueOn)
                : UiLocalization.Get(UiTextKeys.ValueOff);
        }

        static string ResolveDisplayModeKey(FullScreenMode mode)
        {
            return mode switch
            {
                FullScreenMode.Windowed => UiTextKeys.ValueWindowed,
                FullScreenMode.FullScreenWindow => UiTextKeys.ValueBorderless,
                _ => UiTextKeys.ValueFullscreen
            };
        }

        static string GetCategoryLabel(UiSettingsCategory category)
        {
            return category switch
            {
                UiSettingsCategory.Accessibility =>
                    UiLocalization.Get(UiTextKeys.SettingsSubtitle),
                UiSettingsCategory.Audio =>
                    UiLocalization.Get(UiTextKeys.SettingsSectionAudio),
                UiSettingsCategory.Display =>
                    UiLocalization.Get(UiTextKeys.SettingsSectionDisplay),
                UiSettingsCategory.Controls =>
                    UiLocalization.Get(UiTextKeys.SettingsSectionControls),
                _ => UiLocalization.Get(UiTextKeys.SettingsSectionInterface)
            };
        }

        void ResolveReferences()
        {
            screenView ??= GetComponent<UiScreenView>();
            systemRoot ??= UiCompositionScope.FindSystemRoot(this);
            preferences ??= systemRoot != null
                ? systemRoot.PreferencesService
                : UiCompositionScope.FindFirstInScope<UiPreferencesService>(this);
            applyActionView ??= applyButton != null
                ? applyButton.GetComponent<UiSettingsActionView>()
                : null;

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
                category.Category == activeCategory &&
                category.IsActive() &&
                category.IsInteractable());
            if (firstCategory != null)
            {
                screenView.SetFirstSelection(firstCategory);
                return;
            }

            IUiValueControl firstControl = FindFirstControl(activeCategory);
            if (firstControl != null)
            {
                screenView.SetFirstSelection(firstControl.Selectable);
            }
        }

        void ApplyTypography()
        {
            UiTheme theme = UiTheme.Resolve(systemRoot != null ? systemRoot.Theme : null);

            SetFont(titleText, theme.InterfaceFont, FontWeight.Regular);
            SetFont(categoryTitleText, theme.InterfaceMediumFont, FontWeight.Medium);
            SetFont(detailTitleText, theme.InterfaceMediumFont, FontWeight.Medium);
            SetFont(detailDescriptionText, theme.InterfaceFont, FontWeight.Regular);
            SetFont(detailValueText, theme.InstrumentFont, FontWeight.Medium);
            SetFont(applyLabelText, theme.InterfaceMediumFont, FontWeight.Medium);
            SetFont(backLabelText, theme.InterfaceMediumFont, FontWeight.Medium);
        }

        static void SetFont(TMP_Text target, TMP_FontAsset font, FontWeight weight)
        {
            if (target != null && font != null)
            {
                target.font = font;
                target.fontWeight = weight;
            }
        }

        static void SetText(TMP_Text target, string key)
        {
            if (target != null)
            {
                target.text = UiLocalization.Get(key);
            }
        }
    }
}
