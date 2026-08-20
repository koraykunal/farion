using System;
using Farion.UI.Common;
using Farion.UI.Feedback;
using Farion.UI.Foundation;
using Farion.UI.Localization;
using Farion.UI.Navigation;
using Farion.UI.Styling;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Farion.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class UiCoopScreenPresenter : MonoBehaviour, IUiCancelConsumer
    {
        const string AddressPreferenceKey = "farion.coop.address";
        const int MaximumAddressLength = 64;

        [Header("View")]
        [SerializeField] TMP_InputField addressInput;
        [SerializeField] UiMenuButtonView joinButton;
        [SerializeField] UiMenuButtonView backButton;
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text titleText;

        [Header("Navigation")]
        [SerializeField] UiScreenRouter screenRouter;

        [Header("Style")]
        [SerializeField] UiTheme theme;

        bool busy;

        public event Action<string> JoinRequested;
        public event Action Closed;

        public string Address => addressInput != null
            ? addressInput.text
            : string.Empty;

        void Awake()
        {
            if (addressInput != null)
            {
                addressInput.characterLimit = MaximumAddressLength;
                addressInput.text = PlayerPrefs.GetString(
                    AddressPreferenceKey,
                    string.Empty);
            }
        }

        void OnEnable()
        {
            if (joinButton != null)
            {
                joinButton.Clicked -= RequestJoin;
                joinButton.Clicked += RequestJoin;
            }

            if (backButton != null)
            {
                backButton.Clicked -= RequestClose;
                backButton.Clicked += RequestClose;
            }

            if (addressInput != null)
            {
                addressInput.onSubmit.RemoveListener(OnAddressSubmitted);
                addressInput.onSubmit.AddListener(OnAddressSubmitted);
            }

            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            ResolveRouter();
            screenRouter?.RegisterCancelConsumer(this);
            SetBusy(false);
            SetStatus(string.Empty, UiFeedbackSeverity.Information);
            RefreshLocalizedContent();
        }

        void OnDisable()
        {
            if (joinButton != null)
            {
                joinButton.Clicked -= RequestJoin;
            }

            if (backButton != null)
            {
                backButton.Clicked -= RequestClose;
            }

            if (addressInput != null)
            {
                addressInput.onSubmit.RemoveListener(OnAddressSubmitted);
            }

            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            ResolveRouter();
            screenRouter?.UnregisterCancelConsumer(this);
        }

        public void SetBusy(bool value)
        {
            busy = value;
            joinButton?.SetAvailable(!value);
            if (addressInput != null)
            {
                addressInput.interactable = !value;
            }
        }

        public void SetStatus(string message, UiFeedbackSeverity severity)
        {
            if (statusText == null)
            {
                return;
            }

            statusText.SetText(message ?? string.Empty);
            statusText.color = ResolveSeverityColor(severity);
        }

        public bool TryConsumeCancel()
        {
            Closed?.Invoke();
            return false;
        }

        void RequestClose()
        {
            ResolveRouter();
            if (screenRouter != null)
            {
                screenRouter.TryHandleCancel();
            }
        }

        void ResolveRouter()
        {
            if (screenRouter != null)
            {
                return;
            }

            UiSystemRoot systemRoot = UiCompositionScope.FindSystemRoot(this);
            if (systemRoot != null)
            {
                screenRouter = systemRoot.ScreenRouter;
            }
        }

        void OnAddressSubmitted(string _)
        {
            RequestJoin();
        }

        void RequestJoin()
        {
            if (busy)
            {
                return;
            }

            string address = Address.Trim();
            if (address.Length == 0)
            {
                SetStatus(
                    UiLocalization.Get(UiTextKeys.CoopErrorAddress),
                    UiFeedbackSeverity.Error);
                return;
            }

            PlayerPrefs.SetString(AddressPreferenceKey, address);
            PlayerPrefs.Save();
            JoinRequested?.Invoke(address);
        }

        void OnLocaleChanged(Locale _)
        {
            RefreshLocalizedContent();
        }

        void RefreshLocalizedContent()
        {
            if (titleText != null)
            {
                titleText.SetText(
                    UiLocalization.ToDisplayUpper(
                        UiLocalization.Get(UiTextKeys.CoopTitle)));
            }

            joinButton?.SetTitle(UiLocalization.Get(UiTextKeys.CommonJoin));
            backButton?.SetTitle(UiLocalization.Get(UiTextKeys.CommonBack));
            if (addressInput != null &&
                addressInput.placeholder is TMP_Text placeholder)
            {
                placeholder.SetText(
                    UiLocalization.Get(UiTextKeys.CoopAddressPlaceholder));
            }
        }

        Color ResolveSeverityColor(UiFeedbackSeverity severity)
        {
            UiTheme resolved = theme = UiTheme.Resolve(theme);
            return severity switch
            {
                UiFeedbackSeverity.Error => resolved.Critical,
                UiFeedbackSeverity.Caution => resolved.Caution,
                UiFeedbackSeverity.Success => resolved.Focus,
                _ => resolved.SecondaryText
            };
        }
    }
}
