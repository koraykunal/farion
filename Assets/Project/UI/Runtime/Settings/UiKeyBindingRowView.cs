using System;
using Farion.UI.Common;
using Farion.UI.Foundation;
using Farion.UI.Localization;
using Farion.UI.Styling;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Farion.UI.Settings
{
    public enum UiKeyBindingState
    {
        Default = 0,
        Customized = 10,
        Unbound = 20,
        Listening = 30,
        Conflict = 40
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class UiKeyBindingRowView :
        Selectable,
        IUiValueControl,
        ISubmitHandler,
        IPointerClickHandler
    {
        [Header("Content")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text valueText;

        [Header("Visuals")]
        [SerializeField] UiTheme theme;
        [SerializeField] Image background;
        [SerializeField] Image selectionFrame;

        UiPointerFocusState focusState;
        UiKeyBindingState state = UiKeyBindingState.Default;
        string displayTitle = string.Empty;
        string displayDescription = string.Empty;
        string displayValue = string.Empty;

        UiTheme Theme => theme = UiTheme.Resolve(theme);

        public event Action<UiKeyBindingRowView> RebindRequested;
        public event Action<IUiValueControl> Focused;

        public string DisplayTitle => displayTitle;
        public string DisplayDescription => displayDescription;
        public string DisplayValue => displayValue;
        public Selectable Selectable => this;

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
            transition = Transition.None;
            if (Application.isPlaying)
            {
                RefreshVisual();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ResolveReferences();
            if (Application.isPlaying)
            {
                RefreshVisual();
            }
        }

        protected override void OnDisable()
        {
            focusState.Reset();
            base.OnDisable();
        }

        public void Configure(
            string actionLabel,
            string description,
            string binding,
            UiKeyBindingState bindingState)
        {
            displayTitle = actionLabel ?? string.Empty;
            displayDescription = description ?? string.Empty;
            if (titleText != null)
            {
                titleText.text = UiLocalization.ToDisplayUpper(displayTitle);
            }

            SetBinding(binding, bindingState);
        }

        public void SetBinding(string binding, UiKeyBindingState bindingState)
        {
            state = bindingState;
            displayValue = binding ?? string.Empty;
            if (valueText != null)
            {
                valueText.text = UiLocalization.ToDisplayUpper(displayValue);
            }

            RefreshVisual();
        }

        public void SetAvailable(bool available)
        {
            interactable = available;
            RefreshVisual();
        }

        public void SetPending(bool value)
        {
        }

        public void OnSubmit(BaseEventData eventData)
        {
            RequestRebind();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
            {
                return;
            }

            focusState.PointerClick();
            Select();
            RequestRebind();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            focusState.PointerEnter();
            RefreshVisual();
            Focused?.Invoke(this);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            focusState.PointerExit();
            RefreshVisual();
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            focusState.Select();
            RefreshVisual();
            Focused?.Invoke(this);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            focusState.Deselect();
            RefreshVisual();
        }

        void RequestRebind()
        {
            if (IsActive() && IsInteractable())
            {
                RebindRequested?.Invoke(this);
            }
        }

        void ResolveReferences()
        {
            background ??= GetComponent<Image>();
            targetGraphic = background;

            if (theme == null)
            {
                UiSystemRoot root = UiCompositionScope.FindSystemRoot(this);
                theme = UiTheme.Resolve(root != null ? root.Theme : null);
            }

            if (titleText != null && Theme.InterfaceMediumFont != null)
            {
                titleText.font = theme.InterfaceMediumFont;
                titleText.fontWeight = FontWeight.Medium;
            }

            if (valueText != null && Theme.InstrumentFont != null)
            {
                valueText.font = theme.InstrumentFont;
                valueText.fontWeight = FontWeight.Medium;
            }
        }

        void RefreshVisual()
        {
            bool available = IsInteractable();
            bool listening = state == UiKeyBindingState.Listening;
            bool focused = available && (focusState.IsFocused || listening);

            Color panelNormal = Theme.ButtonSurface;
            Color panelFocused = Theme.ButtonSurfaceHighlighted;
            Color primary = Theme.PrimaryText;

            if (!available)
            {
                panelNormal.a *= 0.42f;
            }

            if (background != null)
            {
                background.color = focused ? panelFocused : panelNormal;
                background.raycastTarget = true;
            }

            if (titleText != null)
            {
                titleText.color = focused
                    ? primary
                    : new Color(primary.r, primary.g, primary.b, 0.82f);
            }

            if (valueText != null)
            {
                valueText.color = ResolveValueColor(available);
            }

            if (selectionFrame == null)
            {
                return;
            }

            Color signal = ResolveFrameColor();
            signal.a *= focused ? 0.9f : available ? 0.12f : 0.05f;
            selectionFrame.color = signal;
            selectionFrame.raycastTarget = false;
        }

        Color ResolveValueColor(bool available)
        {
            if (!available)
            {
                Color muted = Theme.SupportingText;
                muted.a = 0.24f;
                return muted;
            }

            switch (state)
            {
                case UiKeyBindingState.Listening:
                    return Theme.Caution;
                case UiKeyBindingState.Conflict:
                    return Theme.Critical;
                case UiKeyBindingState.Customized:
                    return Theme.Focus;
                case UiKeyBindingState.Unbound:
                    Color unbound = Theme.SupportingText;
                    unbound.a *= 0.55f;
                    return unbound;
                default:
                    return Theme.PrimaryText;
            }
        }

        Color ResolveFrameColor()
        {
            return state switch
            {
                UiKeyBindingState.Listening => Theme.Caution,
                UiKeyBindingState.Conflict => Theme.Critical,
                _ => Theme.Focus
            };
        }
    }
}
