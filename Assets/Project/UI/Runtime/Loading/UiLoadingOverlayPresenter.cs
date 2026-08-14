using System;
using System.Collections;
using Farion.UI.Foundation;
using Farion.UI.Localization;
using Farion.UI.Navigation;
using Farion.UI.Styling;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Farion.UI.Loading
{
    public enum UiLoadingPresentation
    {
        PreparingExpedition = 0,
        ReturningToMainMenu = 10
    }

    [DisallowMultipleComponent]
    public sealed class UiLoadingOverlayPresenter : MonoBehaviour
    {
        const float SceneActivationProgress = 0.9f;

        [Header("Composition")]
        [SerializeField] UiScreenRouter screenRouter;

        [Header("Content")]
        [SerializeField] TMP_Text systemLabelText;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text progressText;
        [SerializeField] Image progressFill;

        [Header("Design")]
        [SerializeField] UiTheme theme;

        UiLoadingPresentation presentation =
            UiLoadingPresentation.PreparingExpedition;
        Coroutine transitionRoutine;

        public bool IsTransitionActive => transitionRoutine != null;
        public bool HasCompletePresentation =>
            systemLabelText != null &&
            titleText != null &&
            statusText != null &&
            progressText != null &&
            progressFill != null;

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
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
            RefreshContent();
        }

        void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        }

        void OnValidate()
        {
            ResolveReferences();
        }

        public bool TryBegin(
            Func<AsyncOperation> beginOperation,
            UiLoadingPresentation nextPresentation,
            Action onFailed = null)
        {
            if (beginOperation == null || transitionRoutine != null)
            {
                return false;
            }

            ResolveReferences();
            presentation = nextPresentation;
            SetProgress(0f);
            RefreshContent();

            if (screenRouter == null ||
                !screenRouter.Open(UiScreenId.Loading))
            {
                onFailed?.Invoke();
                return false;
            }

            transitionRoutine = StartCoroutine(
                RunTransition(beginOperation, onFailed));
            return true;
        }

        IEnumerator RunTransition(
            Func<AsyncOperation> beginOperation,
            Action onFailed)
        {
            // Give the fully opaque system layer one rendered frame before
            // scene deserialization starts.
            yield return null;

            AsyncOperation operation = null;
            try
            {
                operation = beginOperation();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            if (operation == null)
            {
                HandleFailure(onFailed);
                yield break;
            }

            while (!operation.isDone)
            {
                float normalized = operation.progress >= SceneActivationProgress
                    ? 1f
                    : operation.progress / SceneActivationProgress;
                SetProgress(normalized);
                yield return null;
            }

            SetProgress(1f);
            transitionRoutine = null;
        }

        void HandleFailure(Action onFailed)
        {
            transitionRoutine = null;
            onFailed?.Invoke();
            if (screenRouter != null &&
                screenRouter.IsOpen(UiScreenId.Loading))
            {
                screenRouter.Close(UiScreenId.Loading);
            }
        }

        void HandleLocaleChanged(Locale _)
        {
            RefreshContent();
        }

        void RefreshContent()
        {
            SetText(
                systemLabelText,
                UiLocalization.Get(UiTextKeys.LoadingSystem));

            switch (presentation)
            {
                case UiLoadingPresentation.ReturningToMainMenu:
                    SetText(
                        titleText,
                        UiLocalization.Get(UiTextKeys.LoadingMainMenuTitle));
                    SetText(
                        statusText,
                        UiLocalization.Get(UiTextKeys.LoadingMainMenuStatus));
                    break;
                default:
                    SetText(
                        titleText,
                        UiLocalization.Get(UiTextKeys.LoadingGameplayTitle));
                    SetText(
                        statusText,
                        UiLocalization.Get(UiTextKeys.LoadingGameplayStatus));
                    break;
            }
        }

        void SetProgress(float value)
        {
            float normalized = Mathf.Clamp01(value);
            if (progressFill != null)
            {
                progressFill.fillAmount = normalized;
            }

            SetText(progressText, $"{Mathf.RoundToInt(normalized * 100f):00}%");
        }

        void ResolveReferences()
        {
            UiSystemRoot root = UiCompositionScope.FindSystemRoot(this);
            if (screenRouter == null)
            {
                screenRouter = root != null ? root.ScreenRouter : null;
            }

            theme = UiTheme.Resolve(theme != null ? theme : root != null ? root.Theme : null);
        }

        void ApplyTypography()
        {

            SetFont(systemLabelText, theme.InstrumentFont, FontWeight.Medium);
            SetFont(titleText, theme.InterfaceMediumFont, FontWeight.Medium);
            SetFont(statusText, theme.InterfaceFont, FontWeight.Regular);
            SetFont(progressText, theme.InstrumentFont, FontWeight.Medium);
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
