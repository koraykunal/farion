using System;
using System.Globalization;
using Farion.UI.Foundation;
using Farion.UI.Localization;
using Farion.UI.Navigation;
using Farion.UI.Styling;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Farion.UI.Feedback
{
    [DisallowMultipleComponent]
    public sealed class UiConfirmationDialog : MonoBehaviour
    {
        [Header("Navigation")]
        [SerializeField] UiScreenRouter router;
        [SerializeField] UiScreenId screenId = UiScreenId.Confirmation;

        [Header("Content")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text bodyText;
        [SerializeField] TMP_Text confirmLabelText;
        [SerializeField] TMP_Text cancelLabelText;

        [Header("Design")]
        [SerializeField] UiTheme theme;

        [Header("Actions")]
        [SerializeField] Button confirmButton;
        [SerializeField] Button cancelButton;

        Action pendingConfirmation;
        Action pendingCancellation;
        string countdownBodyFormat;
        float countdownDeadline;
        int countdownDisplayedSeconds = -1;

        void Reset()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            ResolveReferences();
            ApplyTypography();
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(Confirm);
                confirmButton.onClick.AddListener(Confirm);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(Cancel);
                cancelButton.onClick.AddListener(Cancel);
            }
        }

        void OnDisable()
        {
            pendingConfirmation = null;
            pendingCancellation = null;
            countdownBodyFormat = null;

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(Confirm);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(Cancel);
            }
        }

        public bool Present(
            string title,
            string body,
            string confirmLabel,
            Action onConfirm,
            string cancelLabel = null,
            Action onCancel = null,
            float autoCancelSeconds = 0f)
        {
            ResolveReferences();
            if (router == null || onConfirm == null || pendingConfirmation != null)
            {
                return false;
            }

            pendingCancellation = onCancel;
            countdownBodyFormat = autoCancelSeconds > 0f ? body : null;
            countdownDeadline = autoCancelSeconds > 0f
                ? Time.unscaledTime + autoCancelSeconds
                : 0f;
            countdownDisplayedSeconds = -1;
            if (countdownBodyFormat != null)
            {
                body = FormatCountdownBody(Mathf.CeilToInt(autoCancelSeconds));
            }

            SetText(titleText, title);
            SetText(bodyText, body);
            SetText(confirmLabelText, confirmLabel);
            SetText(
                cancelLabelText,
                string.IsNullOrWhiteSpace(cancelLabel)
                    ? UiLocalization.Get(UiTextKeys.CommonCancel)
                    : cancelLabel);
            pendingConfirmation = onConfirm;

            if (router.Open(screenId))
            {
                return true;
            }

            pendingConfirmation = null;
            pendingCancellation = null;
            countdownBodyFormat = null;
            return false;
        }

        public void Confirm()
        {
            Action action = pendingConfirmation;
            ClearPending();
            router?.Close(screenId);
            action?.Invoke();
        }

        public void Cancel()
        {
            Action action = pendingCancellation;
            ClearPending();
            router?.Close(screenId);
            action?.Invoke();
        }

        void Update()
        {
            if (countdownBodyFormat == null)
            {
                return;
            }

            int remaining = Mathf.Max(
                0,
                Mathf.CeilToInt(countdownDeadline - Time.unscaledTime));
            if (remaining != countdownDisplayedSeconds)
            {
                countdownDisplayedSeconds = remaining;
                SetText(bodyText, FormatCountdownBody(remaining));
            }

            if (remaining <= 0)
            {
                Cancel();
            }
        }

        string FormatCountdownBody(int remainingSeconds)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                countdownBodyFormat,
                remainingSeconds);
        }

        void ClearPending()
        {
            pendingConfirmation = null;
            pendingCancellation = null;
            countdownBodyFormat = null;
        }

        void ResolveReferences()
        {
            UiSystemRoot root = UiCompositionScope.FindSystemRoot(this);
            if (router == null)
            {
                router = root != null ? root.ScreenRouter : null;
            }

            theme = UiTheme.Resolve(theme != null ? theme : root != null ? root.Theme : null);
        }

        void ApplyTypography()
        {

            SetFont(titleText, theme.InterfaceMediumFont, FontWeight.Medium);
            SetFont(bodyText, theme.InterfaceFont, FontWeight.Regular);
            SetFont(confirmLabelText, theme.InterfaceMediumFont, FontWeight.Medium);
            SetFont(cancelLabelText, theme.InterfaceMediumFont, FontWeight.Medium);
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
