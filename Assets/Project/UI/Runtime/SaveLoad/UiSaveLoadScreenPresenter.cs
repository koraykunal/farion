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
using UiNavigation = UnityEngine.UI.Navigation;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

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
        [SerializeField] Button coopButton;
        [SerializeField] TMP_Text coopLabelText;

        readonly List<SaveGameSlotSummary> summaries = new();
        UiSaveLoadMode mode;
        int selectedIndex;
        bool subscribed;
        Action<string> loadRequested;
        Action<string> coopRequested;
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
            primaryButton != null &&
            coopButton != null;

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
            Action<string> onCoopRequested = null,
            string preferredSlotName = null)
        {
            if (onLoadRequested == null)
            {
                return false;
            }

            mode = UiSaveLoadMode.Load;
            loadRequested = onLoadRequested;
            coopRequested = onCoopRequested;
            saveRequested = null;
            return OpenScreen(preferredSlotName);
        }

        public bool OpenForNewGame(
            Action<string> onStartRequested,
            Action<string> onCoopRequested = null)
        {
            if (onStartRequested == null)
            {
                return false;
            }

            mode = UiSaveLoadMode.NewGame;
            loadRequested = onStartRequested;
            coopRequested = onCoopRequested;
            saveRequested = null;
            return OpenScreen(ResolveFirstEmptySlotName());
        }

        bool OpenScreen(string preferredSlotName)
        {
            ResolveReferences();
            Refresh(preferredSlotName);
            return systemRoot != null &&
                   systemRoot.ScreenRouter != null &&
                   systemRoot.ScreenRouter.Open(UiScreenId.SaveLoad);
        }

        static string ResolveFirstEmptySlotName()
        {
            IReadOnlyList<SaveGameSlotSummary> slots =
                SaveGameSlotService.GetPlayerSlotSummaries();
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].HasData)
                {
                    return slots[i].SlotName;
                }
            }

            return null;
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
            coopRequested = null;
            return OpenScreen(preferredSlotName);
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
            coopButton?.onClick.AddListener(RequestCoopAction);
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
            coopButton?.onClick.RemoveListener(RequestCoopAction);
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

            SetText(eyebrowText, UiLocalization.Get(ResolveKey(
                UiTextKeys.SaveLoadEyebrowLoad,
                UiTextKeys.SaveLoadEyebrowSave,
                UiTextKeys.SaveLoadEyebrowNewGame)));
            SetText(titleText, UiLocalization.Get(ResolveKey(
                UiTextKeys.SaveLoadTitleLoad,
                UiTextKeys.SaveLoadTitleSave,
                UiTextKeys.SaveLoadTitleNewGame)));
            SetText(descriptionText, UiLocalization.Get(ResolveKey(
                UiTextKeys.SaveLoadDescriptionLoad,
                UiTextKeys.SaveLoadDescriptionSave,
                UiTextKeys.SaveLoadDescriptionNewGame)));
            SetText(backLabelText, UiLocalization.Get(UiTextKeys.CommonBack));
            SetText(deleteLabelText, UiLocalization.Get(UiTextKeys.CommonDelete));
            SetText(primaryLabelText, UiLocalization.Get(ResolveKey(
                UiTextKeys.CommonLoad,
                UiTextKeys.CommonSave,
                UiTextKeys.CommonStart)));
            SetText(coopLabelText, UiLocalization.Get(UiTextKeys.SaveLoadHostCoop));
            SetText(savedLabelText, UiLocalization.Get(UiTextKeys.SaveLoadSaved));
            SetText(versionLabelText, UiLocalization.Get(UiTextKeys.SaveLoadVersion));

            int count = Mathf.Min(slotViews.Count, summaries.Count);
            for (int i = 0; i < count; i++)
            {
                SaveGameSlotSummary summary = summaries[i];
                slotViews[i].Configure(
                    summary,
                    i + 1,
                    GetSlotTitle(summary.SlotName, i),
                    GetTimestamp(summary),
                    GetStateLabel(summary));
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
                    : UiLocalization.Get(UiTextKeys.SaveLoadNotAvailable));

            bool canStart = mode switch
            {
                UiSaveLoadMode.Load => summary.IsLoadable,
                _ => true
            };
            if (primaryButton != null)
            {
                primaryButton.interactable = canStart;
            }

            if (coopButton != null)
            {
                coopButton.gameObject.SetActive(coopRequested != null);
                coopButton.interactable = canStart;
            }

            if (deleteButton != null)
            {
                deleteButton.gameObject.SetActive(mode == UiSaveLoadMode.Load);
                deleteButton.interactable =
                    mode == UiSaveLoadMode.Load && summary.HasData;
            }
        }

        string ResolveKey(string load, string save, string newGame)
        {
            return mode switch
            {
                UiSaveLoadMode.Save => save,
                UiSaveLoadMode.NewGame => newGame,
                _ => load
            };
        }

        void RequestCoopAction()
        {
            StartSelectedSlot(coopRequested);
        }

        void RequestPrimaryAction()
        {
            if (mode != UiSaveLoadMode.Save)
            {
                StartSelectedSlot(loadRequested);
                return;
            }

            if (summaries.Count == 0)
            {
                return;
            }

            SaveGameSlotSummary summary = summaries[selectedIndex];

            if (saveRequested == null)
            {
                ShowFeedback(
                    UiLocalization.Get(UiTextKeys.SaveLoadFeedbackSaveUnavailable),
                    UiFeedbackSeverity.Error);
                return;
            }

            if (!summary.HasData)
            {
                PerformSave(summary.SlotName);
                return;
            }

            PresentConfirmation(
                UiLocalization.Get(UiTextKeys.SaveLoadOverwriteTitle),
                UiLocalization.Get(UiTextKeys.SaveLoadOverwriteBody),
                UiLocalization.Get(UiTextKeys.SaveLoadOverwriteConfirm),
                () => PerformSave(summary.SlotName));
        }

        void StartSelectedSlot(Action<string> callback)
        {
            if (summaries.Count == 0)
            {
                return;
            }

            SaveGameSlotSummary summary = summaries[selectedIndex];
            bool unavailable = callback == null ||
                (mode == UiSaveLoadMode.Load && !summary.IsLoadable);
            if (unavailable)
            {
                ShowFeedback(
                    UiLocalization.Get(UiTextKeys.SaveLoadFeedbackUnavailable),
                    UiFeedbackSeverity.Caution);
                return;
            }

            if (mode == UiSaveLoadMode.NewGame && summary.HasData)
            {
                PresentConfirmation(
                    UiLocalization.Get(UiTextKeys.SaveLoadOverwriteTitle),
                    UiLocalization.Get(UiTextKeys.SaveLoadOverwriteBody),
                    UiLocalization.Get(UiTextKeys.SaveLoadOverwriteConfirm),
                    () => InvokeAndClose(callback, summary.SlotName));
                return;
            }

            InvokeAndClose(callback, summary.SlotName);
        }

        void InvokeAndClose(Action<string> callback, string slotName)
        {
            Close();
            callback(slotName);
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
                UiLocalization.Get(UiTextKeys.SaveLoadDeleteTitle),
                UiLocalization.Get(UiTextKeys.SaveLoadDeleteBody),
                UiLocalization.Get(UiTextKeys.CommonDelete),
                () => PerformDelete(slotName));
        }

        void PerformSave(string slotName)
        {
            SaveGameOperationResult result = saveRequested(slotName);
            if (!result.Succeeded)
            {
                ShowFeedback(
                    string.Format(
                        UiLocalization.Get(UiTextKeys.SaveLoadFeedbackSaveFailed),
                        result.Status),
                    UiFeedbackSeverity.Error);
                return;
            }

            Refresh(slotName);
            CatalogChanged?.Invoke();
            ShowFeedback(
                UiLocalization.Get(UiTextKeys.SaveLoadFeedbackSaved),
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
                        UiLocalization.Get(UiTextKeys.SaveLoadFeedbackDeleteFailed),
                        result.Status),
                    UiFeedbackSeverity.Error);
                return;
            }

            Refresh(slotName);
            CatalogChanged?.Invoke();
            ShowFeedback(
                UiLocalization.Get(UiTextKeys.SaveLoadFeedbackDeleted),
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
                UiLocalization.Get(UiTextKeys.SaveLoadFeedbackConfirmationUnavailable),
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

            if (mode != UiSaveLoadMode.Save &&
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

            Selectable coopSelectable = coopButton != null &&
                coopButton.gameObject.activeSelf
                    ? coopButton
                    : null;
            if (primaryButton != null)
            {
                primaryButton.navigation = new UiNavigation
                {
                    mode = UiNavigation.Mode.Explicit,
                    selectOnUp = slotViews[^1],
                    selectOnDown = slotViews[0],
                    selectOnLeft = mode == UiSaveLoadMode.Load
                        ? deleteButton
                        : backButton,
                    selectOnRight = coopSelectable
                };
            }

            if (coopSelectable != null)
            {
                coopButton.navigation = new UiNavigation
                {
                    mode = UiNavigation.Mode.Explicit,
                    selectOnUp = slotViews[^1],
                    selectOnDown = slotViews[0],
                    selectOnLeft = primaryButton
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
                ? UiLocalization.Get(UiTextKeys.SaveLoadSlotPrimary)
                : string.Format(
                    UiLocalization.Get(UiTextKeys.SaveLoadSlotManual),
                    index);
        }

        static string GetTimestamp(SaveGameSlotSummary summary)
        {
            if (!summary.BestTimestampUtc.HasValue)
            {
                return UiLocalization.Get(UiTextKeys.SaveLoadEmpty);
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

        static string GetStateLabel(SaveGameSlotSummary summary)
        {
            string label = GetStateLabel(summary.State);
            return summary.MultiplayerSession && summary.IsLoadable
                ? $"{label} · {UiLocalization.Get(UiTextKeys.SaveLoadStateCoop)}"
                : label;
        }

        static string GetStateLabel(SaveGameSlotState state)
        {
            return state switch
            {
                SaveGameSlotState.Available =>
                    UiLocalization.Get(UiTextKeys.SaveLoadStateReady),
                SaveGameSlotState.RecoverableBackup =>
                    UiLocalization.Get(UiTextKeys.SaveLoadStateBackup),
                SaveGameSlotState.Unsupported =>
                    UiLocalization.Get(UiTextKeys.SaveLoadStateUnsupported),
                SaveGameSlotState.Invalid =>
                    UiLocalization.Get(UiTextKeys.SaveLoadStateInvalid),
                _ => UiLocalization.Get(UiTextKeys.SaveLoadStateEmpty)
            };
        }

        static string GetDetailState(SaveGameSlotState state)
        {
            return state switch
            {
                SaveGameSlotState.Available =>
                    UiLocalization.Get(UiTextKeys.SaveLoadDetailReady),
                SaveGameSlotState.RecoverableBackup =>
                    UiLocalization.Get(UiTextKeys.SaveLoadDetailBackup),
                SaveGameSlotState.Unsupported =>
                    UiLocalization.Get(UiTextKeys.SaveLoadDetailUnsupported),
                SaveGameSlotState.Invalid =>
                    UiLocalization.Get(UiTextKeys.SaveLoadDetailInvalid),
                _ => UiLocalization.Get(UiTextKeys.SaveLoadDetailEmpty)
            };
        }

        static string GetDetailDescription(SaveGameSlotState state)
        {
            return state switch
            {
                SaveGameSlotState.Available =>
                    UiLocalization.Get(UiTextKeys.SaveLoadDetailReadyDescription),
                SaveGameSlotState.RecoverableBackup =>
                    UiLocalization.Get(UiTextKeys.SaveLoadDetailBackupDescription),
                SaveGameSlotState.Unsupported =>
                    UiLocalization.Get(UiTextKeys.SaveLoadDetailUnsupportedDescription),
                SaveGameSlotState.Invalid =>
                    UiLocalization.Get(UiTextKeys.SaveLoadDetailInvalidDescription),
                _ => UiLocalization.Get(UiTextKeys.SaveLoadDetailEmptyDescription)
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
            UiTheme theme = UiTheme.Resolve(systemRoot != null ? systemRoot.Theme : null);

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
