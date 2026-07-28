using System;
using Farion.UI.Feedback;
using Farion.UI.Input;
using Farion.UI.Loading;
using Farion.UI.Navigation;
using Farion.UI.Settings;
using Farion.UI.Styling;
using UnityEngine;

namespace Farion.UI.Foundation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UiScreenRouter))]
    [RequireComponent(typeof(UiFocusController))]
    [RequireComponent(typeof(UiInputDeviceService))]
    public sealed class UiSystemRoot : MonoBehaviour
    {
        [Header("Design")]
        [SerializeField] UiTheme theme;

        [Header("Accessibility")]
        [SerializeField] bool reducedMotion;

        [Header("Services")]
        [SerializeField] UiScreenRouter screenRouter;
        [SerializeField] UiFocusController focusController;
        [SerializeField] UiInputDeviceService inputDeviceService;
        UiPreferencesService preferencesService;
        public UiTheme Theme => theme;
        public Canvas OwningCanvas => UiCompositionScope.FindOwningCanvas(this);
        public bool ReducedMotion => reducedMotion;
        public event Action<bool> ReducedMotionChanged;
        public UiScreenRouter ScreenRouter
        {
            get
            {
                ResolveReferences();
                return screenRouter;
            }
        }

        public UiFocusController FocusController
        {
            get
            {
                ResolveReferences();
                return focusController;
            }
        }

        public UiInputDeviceService InputDeviceService
        {
            get
            {
                ResolveReferences();
                return inputDeviceService;
            }
        }

        public UiPreferencesService PreferencesService
        {
            get
            {
                preferencesService ??= GetComponent<UiPreferencesService>();
                return preferencesService;
            }
        }

        public UiFeedbackService FeedbackService
        {
            get => UiCompositionScope.FindFirstInScope<UiFeedbackService>(this);
        }

        public UiConfirmationDialog ConfirmationDialog
        {
            get => UiCompositionScope.FindFirstInScope<UiConfirmationDialog>(this);
        }

        public UiLoadingOverlayPresenter LoadingOverlay
        {
            get => UiCompositionScope.FindFirstInScope<UiLoadingOverlayPresenter>(this);
        }

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
        }

        void OnValidate()
        {
            ResolveReferences();
        }

        public void ResolveReferences()
        {
            screenRouter ??= GetComponent<UiScreenRouter>();
            focusController ??= GetComponent<UiFocusController>();
            inputDeviceService ??= GetComponent<UiInputDeviceService>();
        }

        public void SetReducedMotion(bool value)
        {
            if (reducedMotion == value)
            {
                return;
            }

            reducedMotion = value;
            ReducedMotionChanged?.Invoke(reducedMotion);
        }
    }
}
