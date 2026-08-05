using System;
using Farion.UI.Foundation;
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
            string cancelLabel = "CANCEL")
        {
            ResolveReferences();
            if (router == null || onConfirm == null)
            {
                return false;
            }

            SetText(titleText, title);
            SetText(bodyText, body);
            SetText(confirmLabelText, confirmLabel);
            SetText(cancelLabelText, cancelLabel);
            pendingConfirmation = onConfirm;

            if (router.Open(screenId))
            {
                return true;
            }

            pendingConfirmation = null;
            return false;
        }

        public void Confirm()
        {
            Action action = pendingConfirmation;
            pendingConfirmation = null;
            router?.Close(screenId);
            action?.Invoke();
        }

        public void Cancel()
        {
            pendingConfirmation = null;
            router?.Close(screenId);
        }

        void ResolveReferences()
        {
            UiSystemRoot root = UiCompositionScope.FindSystemRoot(this);
            if (router == null)
            {
                router = root != null ? root.ScreenRouter : null;
            }

            theme ??= root != null ? root.Theme : null;
        }

        void ApplyTypography()
        {
            if (theme == null)
            {
                return;
            }

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
