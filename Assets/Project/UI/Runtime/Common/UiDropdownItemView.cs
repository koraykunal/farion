using System;
using Farion.UI.Foundation;
using Farion.UI.Localization;
using Farion.UI.Styling;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Farion.UI.Common
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class UiDropdownItemView :
        Selectable,
        ISubmitHandler,
        IPointerClickHandler
    {
        [Header("Content")]
        [SerializeField] TMP_Text labelText;

        [Header("Visuals")]
        [SerializeField] UiTheme theme;
        [SerializeField] Image background;
        [SerializeField] Image currentMarker;

        UiPointerFocusState focusState;
        bool current;

        UiTheme Theme => theme = UiTheme.Resolve(theme);

        public event Action<UiDropdownItemView> Chosen;
        public event Action<UiDropdownItemView> Highlighted;

        public int Index { get; private set; }

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

        public void Configure(int index, string label, bool isCurrent)
        {
            Index = index;
            current = isCurrent;
            if (labelText != null)
            {
                labelText.text = UiLocalization.ToDisplayUpper(label);
            }

            RefreshVisual();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            Choose();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Choose();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            focusState.PointerEnter();
            RefreshVisual();
            if (IsActive() && IsInteractable())
            {
                Select();
            }
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
            Highlighted?.Invoke(this);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            focusState.Deselect();
            RefreshVisual();
        }

        void Choose()
        {
            if (IsActive() && IsInteractable())
            {
                Chosen?.Invoke(this);
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

            if (labelText != null && Theme.InterfaceFont != null)
            {
                labelText.font = theme.InterfaceFont;
                labelText.fontWeight = FontWeight.Regular;
            }
        }

        void RefreshVisual()
        {
            bool focused = IsInteractable() && focusState.IsFocused;

            if (background != null)
            {
                Color surface = Theme.Focus;
                surface.a = focused ? 0.22f : 0f;
                background.color = surface;
                background.raycastTarget = true;
            }

            if (labelText != null)
            {
                labelText.color = focused
                    ? Theme.PrimaryText
                    : current
                        ? Theme.SupportingText
                        : Theme.SecondaryText;
            }

            if (currentMarker != null)
            {
                Color signal = Theme.Focus;
                signal.a *= current ? 0.9f : 0f;
                currentMarker.color = signal;
                currentMarker.raycastTarget = false;
            }
        }
    }
}
