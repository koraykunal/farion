using System.Collections.Generic;
using Farion.Audio;
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
        AudioDirector audioDirector;
        UiSettingsCategory activeCategory = UiSettingsCategory.Interface;
        UiSettingsOptionView focusedOption;

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
            }
        }

        void Refresh()
        {
            SetText(titleText, UiTextKeys.SettingsTitle);
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

            if (audioGroup != null)
            {
                audioGroup.SetActive(category == UiSettingsCategory.Audio);
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
                UiSettingsCategory.Audio =>
                    option.SettingId is UiSettingId.MasterVolume or
                        UiSettingId.MusicVolume or
                        UiSettingId.AmbienceVolume or
                        UiSettingId.SfxVolume or
                        UiSettingId.UiVolume,
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
                ? UiLocalization.Get(UiTextKeys.ValueOn)
                : UiLocalization.Get(UiTextKeys.ValueOff);
        }

        static string GetCategoryLabel(UiSettingsCategory category)
        {
            return category switch
            {
                UiSettingsCategory.Accessibility =>
                    UiLocalization.Get(UiTextKeys.SettingsSubtitle),
                UiSettingsCategory.Audio =>
                    UiLocalization.Get(UiTextKeys.SettingsSectionAudio),
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

        void ApplyTypography()
        {
            UiTheme theme = systemRoot != null ? systemRoot.Theme : null;
            if (theme == null)
            {
                return;
            }

            SetFont(titleText, theme.InterfaceFont, FontWeight.Regular);
            SetFont(categoryTitleText, theme.InterfaceMediumFont, FontWeight.Medium);
            SetFont(detailTitleText, theme.InterfaceMediumFont, FontWeight.Medium);
            SetFont(detailDescriptionText, theme.InterfaceFont, FontWeight.Regular);
            SetFont(detailValueText, theme.InstrumentFont, FontWeight.Medium);
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
