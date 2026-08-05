using System;
using System.Collections.Generic;
using System.Globalization;
using Farion.Core.Persistence;
using Farion.UI.Feedback;
using Farion.UI.Foundation;
using Farion.UI.Localization;
using Farion.UI.Navigation;
using Farion.UI.Styling;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using UiNavigation = UnityEngine.UI.Navigation;

namespace Farion.UI.SaveLoad
{
    [DisallowMultipleComponent]
    public sealed class UiSaveLoadScreenPresenter : MonoBehaviour
    {
        [Header("Composition")]
        [SerializeField] UiScreenView screenView;
        [SerializeField] UiSystemRoot systemRoot;
        [SerializeField] List<UiSaveSlotView> slotViews = new();

        [Header("Header")]
        [SerializeField] TMP_Text eyebrowText;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text descriptionText;

        [Header("Detail")]
        [SerializeField] TMP_Text detailSlotText;
        [SerializeField] TMP_Text detailStateText;
        [SerializeField] TMP_Text detailDescriptionText;
        [SerializeField] TMP_Text savedLabelText;
        [SerializeField] TMP_Text savedValueText;
        [SerializeField] TMP_Text versionLabelText;
        [SerializeField] TMP_Text versionValueText;

        [Header("Actions")]
        [SerializeField] Button backButton;
        [SerializeField] TMP_Text backLabelText;
        [SerializeField] Button deleteButton;
        [SerializeField] TMP_Text deleteLabelText;
        [SerializeField] Button primaryButton;
        [SerializeField] TMP_Text primaryLabelText;

        readonly List<SaveGameSlotSummary> summaries = new();
        UiSaveLoadMode mode;
        int selectedIndex;
        bool subscribed;
        Action<string> loadRequested;
        Func<string, SaveGameOperationResult> saveRequested;

        public event Action CatalogChanged;
        public bool HasCompletePresentation =>
            screenView != null &&
            slotViews != null &&
            slotViews.Count == SaveGameSlotCatalog.PlayerSlotNames.Count &&
            titleText != null &&
            detailSlotText != null &&
            backButton != null &&
            deleteButton != null &&
            primaryButton != null;

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
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        public bool OpenForLoad(
            Action<string> onLoadRequested,
            string preferredSlotName = null)
        {
            if (onLoadRequested == null)
            {
                return false;
            }

            mode = UiSaveLoadMode.Load;
            loadRequested = onLoadRequested;
            saveRequested = null;
            ResolveReferences();
            Refresh(preferredSlotName);
            return systemRoot != null &&
                   systemRoot.ScreenRouter != null &&
                   systemRoot.ScreenRouter.Open(UiScreenId.SaveLoad);
        }

        public bool OpenForSave(
            Func<string, SaveGameOperationResult> onSaveRequested,
            string preferredSlotName = null)
        {
            if (onSaveRequested == null)
            {
                return false;
            }

            mode = UiSaveLoadMode.Save;
            saveRequested = onSaveRequested;
            loadRequested = null;
            ResolveReferences();
            Refresh(preferredSlotName);
            return systemRoot != null &&
                   systemRoot.ScreenRouter != null &&
                   systemRoot.ScreenRouter.Open(UiScreenId.SaveLoad);
        }

        public void Close()
        {
            systemRoot?.ScreenRouter?.Close(UiScreenId.SaveLoad);
        }

        void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            for (int i = 0; i < slotViews.Count; i++)
            {
                if (slotViews[i] != null)
                {
                    slotViews[i].Focused += HandleSlotFocused;
                    slotViews[i].Submitted += HandleSlotSubmitted;
                }
            }

            backButton?.onClick.AddListener(Close);
            deleteButton?.onClick.AddListener(RequestDelete);
            primaryButton?.onClick.AddListener(RequestPrimaryAction);
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
            subscribed = true;
        }

        void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            for (int i = 0; i < slotViews.Count; i++)
            {
                if (slotViews[i] != null)
                {
                    slotViews[i].Focused -= HandleSlotFocused;
                    slotViews[i].Submitted -= HandleSlotSubmitted;
                }
            }

            backButton?.onClick.RemoveListener(Close);
            deleteButton?.onClick.RemoveListener(RequestDelete);
            primaryButton?.onClick.RemoveListener(RequestPrimaryAction);
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
            subscribed = false;
        }

        void HandleLocaleChanged(Locale _)
        {
            Refresh(GetSelectedSlotName());
        }

        void HandleSlotFocused(UiSaveSlotView slotView)
        {
            int index = slotViews.IndexOf(slotView);
            if (index < 0 || index >= summaries.Count)
            {
                return;
            }

            selectedIndex = index;
            RefreshCurrentSlot();
            RefreshDetail();
            ConfigureNavigation();
        }

        void HandleSlotSubmitted(UiSaveSlotView slotView)
        {
            HandleSlotFocused(slotView);
            RequestPrimaryAction();
        }

        void Refresh(string preferredSlotName = null)
        {
            ResolveReferences();
            summaries.Clear();
            summaries.AddRange(SaveGameSlotService.GetPlayerSlotSummaries());

            string requestedSlot = string.IsNullOrWhiteSpace(preferredSlotName)
                ? GetSelectedSlotName()
                : SaveGameSlotCatalog.ResolveSlotName(preferredSlotName);
            selectedIndex = ResolveSelectionIndex(requestedSlot);

            SetText(
                eyebrowText,
                mode == UiSaveLoadMode.Save
                    ? UiLocalization.Get("save_load.eyebrow.save", "EXPEDITION RECORD")
                    : UiLocalization.Get("save_load.eyebrow.load", "RETURN TO EXPEDITION"));
            SetText(
                titleText,
                mode == UiSaveLoadMode.Save
                    ? UiLocalization.Get("save_load.title.save", "SAVE GAME")
                    : UiLocalization.Get("save_load.title.load", "LOAD GAME"));
            SetText(
                descriptionText,
                mode == UiSaveLoadMode.Save
                    ? UiLocalization.Get(
                        "save_load.description.save",
                        "Choose a slot for the current expedition state.")
                    : UiLocalization.Get(
                        "save_load.description.load",
                        "Choose a compatible record to continue your expedition."));
            SetText(backLabelText, UiLocalization.Get("common.back", "BACK"));
            SetText(deleteLabelText, UiLocalization.Get("common.delete", "DELETE"));
            SetText(
                primaryLabelText,
                mode == UiSaveLoadMode.Save
                    ? UiLocalization.Get("common.save", "SAVE")
                    : UiLocalization.Get("common.load", "LOAD"));
            SetText(savedLabelText, UiLocalization.Get("save_load.saved", "SAVED"));
            SetText(versionLabelText, UiLocalization.Get("save_load.version", "SAVE VERSION"));

            int count = Mathf.Min(slotViews.Count, summaries.Count);
            for (int i = 0; i < count; i++)
            {
                SaveGameSlotSummary summary = summaries[i];
                slotViews[i].Configure(
                    summary,
                    i + 1,
                    GetSlotTitle(summary.SlotName, i),
                    GetTimestamp(summary),
                    GetStateLabel(summary.State));
            }

            RefreshCurrentSlot();

            for (int i = count; i < slotViews.Count; i++)
            {
                if (slotViews[i] != null)
                {
                    slotViews[i].gameObject.SetActive(false);
                }
            }

            RefreshDetail();
            ConfigureNavigation();
            AssignInitialSelection();
        }

        void RefreshDetail()
        {
            if (summaries.Count == 0)
            {
                return;
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, summaries.Count - 1);
            SaveGameSlotSummary summary = summaries[selectedIndex];
            SetText(detailSlotText, GetSlotTitle(summary.SlotName, selectedIndex));
            SetText(detailStateText, GetDetailState(summary.State));
            SetText(detailDescriptionText, GetDetailDescription(summary.State));
            SetText(savedValueText, GetTimestamp(summary));
            SetText(
                versionValueText,
                summary.SchemaVersion > 0
                    ? $"V{summary.SchemaVersion}"
                    : UiLocalization.Get("save_load.not_available", "NOT AVAILABLE"));

            bool canLoad = mode == UiSaveLoadMode.Load && summary.IsLoadable;
            bool canSave = mode == UiSaveLoadMode.Save;
            if (primaryButton != null)
            {
                primaryButton.interactable = canLoad || canSave;
            }

            if (deleteButton != null)
            {
                deleteButton.gameObject.SetActive(mode == UiSaveLoadMode.Load);
                deleteButton.interactable =
                    mode == UiSaveLoadMode.Load && summary.HasData;
            }
        }

        void RequestPrimaryAction()
        {
            if (summaries.Count == 0)
            {
                return;
            }

            SaveGameSlotSummary summary = summaries[selectedIndex];
            if (mode == UiSaveLoadMode.Load)
            {
                if (!summary.IsLoadable || loadRequested == null)
                {
                    ShowFeedback(
                        UiLocalization.Get(
                            "save_load.feedback.unavailable",
                            "This save cannot be loaded."),
                        UiFeedbackSeverity.Caution);
                    return;
                }

                Action<string> callback = loadRequested;
                Close();
                callback(summary.SlotName);
                return;
            }

            if (saveRequested == null)
            {
                ShowFeedback(
                    UiLocalization.Get(
                        "save_load.feedback.save_unavailable",
                        "Saving is not available."),
                    UiFeedbackSeverity.Error);
                return;
            }

            if (!summary.HasData)
            {
                PerformSave(summary.SlotName);
                return;
            }

            PresentConfirmation(
                UiLocalization.Get("save_load.overwrite.title", "OVERWRITE SAVE"),
                UiLocalization.Get(
                    "save_load.overwrite.body",
                    "The existing record in this slot will be replaced."),
                UiLocalization.Get("save_load.overwrite.confirm", "OVERWRITE"),
                () => PerformSave(summary.SlotName));
        }

        void RequestDelete()
        {
            if (mode != UiSaveLoadMode.Load ||
                summaries.Count == 0 ||
                !summaries[selectedIndex].HasData)
            {
                return;
            }

            string slotName = summaries[selectedIndex].SlotName;
            PresentConfirmation(
                UiLocalization.Get("save_load.delete.title", "DELETE SAVE"),
                UiLocalization.Get(
                    "save_load.delete.body",
                    "This record and its recovery backup will be permanently deleted."),
                UiLocalization.Get("common.delete", "DELETE"),
                () => PerformDelete(slotName));
        }

        void PerformSave(string slotName)
        {
            SaveGameOperationResult result = saveRequested(slotName);
            if (!result.Succeeded)
            {
                ShowFeedback(
                    string.Format(
                        UiLocalization.Get(
                            "save_load.feedback.save_failed",
                            "Save failed: {0}."),
                        result.Status),
                    UiFeedbackSeverity.Error);
                return;
            }

            Refresh(slotName);
            CatalogChanged?.Invoke();
            ShowFeedback(
                UiLocalization.Get("save_load.feedback.saved", "Game saved."),
                UiFeedbackSeverity.Success);
        }

        void PerformDelete(string slotName)
        {
            SaveGameOperationResult result = SaveGameFileService.DeleteSlot(slotName);
            if (!result.Succeeded &&
                result.Status != SaveGameOperationStatus.NoSaveFound)
            {
                ShowFeedback(
                    string.Format(
                        UiLocalization.Get(
                            "save_load.feedback.delete_failed",
                            "Delete failed: {0}."),
                        result.Status),
                    UiFeedbackSeverity.Error);
                return;
            }

            Refresh(slotName);
            CatalogChanged?.Invoke();
            ShowFeedback(
                UiLocalization.Get("save_load.feedback.deleted", "Save deleted."),
                UiFeedbackSeverity.Information);
        }

        void PresentConfirmation(
            string title,
            string body,
            string confirmLabel,
            Action confirmedAction)
        {
            UiConfirmationDialog dialog = systemRoot?.ConfirmationDialog;
            if (dialog != null &&
                dialog.Present(title, body, confirmLabel, confirmedAction))
            {
                return;
            }

            ShowFeedback(
                UiLocalization.Get(
                    "save_load.feedback.confirmation_unavailable",
                    "Confirmation is not available."),
                UiFeedbackSeverity.Error);
        }

        void ShowFeedback(string message, UiFeedbackSeverity severity)
        {
            systemRoot?.FeedbackService?.Show(message, severity);
        }

        int ResolveSelectionIndex(string preferredSlotName)
        {
            if (!string.IsNullOrWhiteSpace(preferredSlotName))
            {
                for (int i = 0; i < summaries.Count; i++)
                {
                    if (string.Equals(
                            summaries[i].SlotName,
                            preferredSlotName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            if (mode == UiSaveLoadMode.Load &&
                SaveGameSlotService.TryGetMostRecentLoadable(
                    out SaveGameSlotSummary recent))
            {
                for (int i = 0; i < summaries.Count; i++)
                {
                    if (string.Equals(
                            summaries[i].SlotName,
                            recent.SlotName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            return 0;
        }

        void ConfigureNavigation()
        {
            if (slotViews.Count == 0)
            {
                return;
            }

            Selectable firstAction = primaryButton != null && primaryButton.IsInteractable()
                ? primaryButton
                : deleteButton != null &&
                  deleteButton.gameObject.activeSelf &&
                  deleteButton.IsInteractable()
                    ? deleteButton
                    : backButton;

            for (int i = 0; i < slotViews.Count; i++)
            {
                UiSaveSlotView slot = slotViews[i];
                if (slot == null || !slot.gameObject.activeSelf)
                {
                    continue;
                }

                slot.navigation = new UiNavigation
                {
                    mode = UiNavigation.Mode.Explicit,
                    selectOnUp = i > 0 ? slotViews[i - 1] : backButton,
                    selectOnDown = i < slotViews.Count - 1
                        ? slotViews[i + 1]
                        : firstAction
                };
            }

            if (backButton != null)
            {
                backButton.navigation = new UiNavigation
                {
                    mode = UiNavigation.Mode.Explicit,
                    selectOnUp = slotViews[^1],
                    selectOnDown = slotViews[0],
                    selectOnRight = firstAction
                };
            }

            if (deleteButton != null)
            {
                deleteButton.navigation = new UiNavigation
                {
                    mode = UiNavigation.Mode.Explicit,
                    selectOnUp = slotViews[^1],
                    selectOnDown = slotViews[0],
                    selectOnLeft = backButton,
                    selectOnRight = primaryButton
                };
            }

            if (primaryButton != null)
            {
                primaryButton.navigation = new UiNavigation
                {
                    mode = UiNavigation.Mode.Explicit,
                    selectOnUp = slotViews[^1],
                    selectOnDown = slotViews[0],
                    selectOnLeft = mode == UiSaveLoadMode.Load
                        ? deleteButton
                        : backButton
                };
            }
        }

        void AssignInitialSelection()
        {
            if (screenView == null ||
                selectedIndex < 0 ||
                selectedIndex >= slotViews.Count ||
                slotViews[selectedIndex] == null)
            {
                return;
            }

            screenView.SetFirstSelection(slotViews[selectedIndex]);
        }

        void RefreshCurrentSlot()
        {
            for (int i = 0; i < slotViews.Count; i++)
            {
                slotViews[i]?.SetCurrent(i == selectedIndex);
            }
        }

        string GetSelectedSlotName()
        {
            return selectedIndex >= 0 && selectedIndex < summaries.Count
                ? summaries[selectedIndex].SlotName
                : string.Empty;
        }

        static string GetSlotTitle(string slotName, int index)
        {
            return string.Equals(
                    slotName,
                    SaveGameSlotCatalog.DefaultSlotName,
                    StringComparison.OrdinalIgnoreCase)
                ? UiLocalization.Get("save_load.slot.primary", "PRIMARY SLOT")
                : string.Format(
                    UiLocalization.Get(
                        "save_load.slot.manual",
                        "MANUAL SLOT {0:00}"),
                    index);
        }

        static string GetTimestamp(SaveGameSlotSummary summary)
        {
            if (!summary.BestTimestampUtc.HasValue)
            {
                return UiLocalization.Get("save_load.empty", "NO SAVE DATA");
            }

            string localeCode = LocalizationSettings.SelectedLocale?.Identifier.Code;
            CultureInfo culture = string.Equals(
                    localeCode,
                    UiLocalization.TurkishLocaleCode,
                    StringComparison.OrdinalIgnoreCase)
                ? CultureInfo.GetCultureInfo("tr-TR")
                : CultureInfo.GetCultureInfo("en-US");
            return summary.BestTimestampUtc.Value
                .ToLocalTime()
                .ToString("dd MMM yyyy · HH:mm", culture)
                .ToUpper(culture);
        }

        static string GetStateLabel(SaveGameSlotState state)
        {
            return state switch
            {
                SaveGameSlotState.Available =>
                    UiLocalization.Get("save_load.state.ready", "READY"),
                SaveGameSlotState.RecoverableBackup =>
                    UiLocalization.Get("save_load.state.backup", "BACKUP"),
                SaveGameSlotState.Unsupported =>
                    UiLocalization.Get("save_load.state.unsupported", "UNSUPPORTED"),
                SaveGameSlotState.Invalid =>
                    UiLocalization.Get("save_load.state.invalid", "DAMAGED"),
                _ => UiLocalization.Get("save_load.state.empty", "EMPTY")
            };
        }

        static string GetDetailState(SaveGameSlotState state)
        {
            return state switch
            {
                SaveGameSlotState.Available =>
                    UiLocalization.Get("save_load.detail.ready", "READY TO LOAD"),
                SaveGameSlotState.RecoverableBackup =>
                    UiLocalization.Get(
                        "save_load.detail.backup",
                        "RECOVERY BACKUP AVAILABLE"),
                SaveGameSlotState.Unsupported =>
                    UiLocalization.Get(
                        "save_load.detail.unsupported",
                        "SAVE VERSION NOT SUPPORTED"),
                SaveGameSlotState.Invalid =>
                    UiLocalization.Get(
                        "save_load.detail.invalid",
                        "SAVE DATA COULD NOT BE READ"),
                _ => UiLocalization.Get(
                    "save_load.detail.empty",
                    "AVAILABLE FOR A NEW SAVE")
            };
        }

        static string GetDetailDescription(SaveGameSlotState state)
        {
            return state switch
            {
                SaveGameSlotState.Available =>
                    UiLocalization.Get(
                        "save_load.detail.ready_description",
                        "This record is compatible with the current game version."),
                SaveGameSlotState.RecoverableBackup =>
                    UiLocalization.Get(
                        "save_load.detail.backup_description",
                        "The primary record is unavailable. A compatible recovery copy will be used."),
                SaveGameSlotState.Unsupported =>
                    UiLocalization.Get(
                        "save_load.detail.unsupported_description",
                        "This record was created by an incompatible game version."),
                SaveGameSlotState.Invalid =>
                    UiLocalization.Get(
                        "save_load.detail.invalid_description",
                        "The record exists but its contents are not valid."),
                _ => UiLocalization.Get(
                    "save_load.detail.empty_description",
                    "No expedition record has been written to this slot.")
            };
        }

        void ResolveReferences()
        {
            screenView ??= GetComponent<UiScreenView>();
            systemRoot ??= UiCompositionScope.FindSystemRoot(this);
            slotViews ??= new List<UiSaveSlotView>();
            if (slotViews.Count == 0)
            {
                slotViews.AddRange(
                    GetComponentsInChildren<UiSaveSlotView>(true));
            }
        }

        void ApplyTypography()
        {
            UiTheme theme = systemRoot != null ? systemRoot.Theme : null;
            if (theme == null)
            {
                return;
            }

            SetFont(eyebrowText, theme.InterfaceMediumFont, FontWeight.Medium);
            SetFont(titleText, theme.InterfaceFont, FontWeight.Regular);
            SetFont(descriptionText, theme.InterfaceFont, FontWeight.Regular);
            SetFont(detailSlotText, theme.InterfaceMediumFont, FontWeight.Medium);
            SetFont(detailStateText, theme.InterfaceMediumFont, FontWeight.Medium);
            SetFont(detailDescriptionText, theme.InterfaceFont, FontWeight.Regular);
            SetFont(savedLabelText, theme.InterfaceMediumFont, FontWeight.Medium);
            SetFont(savedValueText, theme.InstrumentFont, FontWeight.Medium);
            SetFont(versionLabelText, theme.InterfaceMediumFont, FontWeight.Medium);
            SetFont(versionValueText, theme.InstrumentFont, FontWeight.Medium);
            SetFont(backLabelText, theme.InterfaceMediumFont, FontWeight.Medium);
            SetFont(deleteLabelText, theme.InterfaceMediumFont, FontWeight.Medium);
            SetFont(primaryLabelText, theme.InterfaceMediumFont, FontWeight.Medium);
        }

        static void SetFont(TMP_Text target, TMP_FontAsset font, FontWeight weight)
        {
            if (target != null && font != null)
            {
                target.font = font;
                target.fontWeight = weight;
            }
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
