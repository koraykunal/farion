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
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class UiSettingsCategoryView :
        Selectable,
        ISubmitHandler,
        IPointerClickHandler
    {
        [Header("Identity")]
        [SerializeField] UiSettingsCategory category;

        [Header("Content")]
        [SerializeField] TMP_Text labelText;

        [Header("Visuals")]
        [SerializeField] UiTheme theme;
        [SerializeField] Image background;
        [SerializeField] Image selectionFrame;

        bool current;
        UiPointerFocusState focusState;

        UiTheme Theme => theme = UiTheme.Resolve(theme);

        public event Action<UiSettingsCategoryView> Chosen;

        public UiSettingsCategory Category => category;

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

        public void ConfigureContent(string label)
        {
            if (labelText != null)
            {
                labelText.text = ToLabel(label);
            }
        }

        public void SetCurrent(bool value)
        {
            current = value;
            RefreshVisual();
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Right)
            {
                Choose();
                eventData.Use();
                return;
            }

            base.OnMove(eventData);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            Choose();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
            {
                return;
            }

            focusState.PointerClick();
            Select();
            Choose();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            focusState.PointerEnter();
            RefreshVisual();
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

            if (labelText != null && Theme.InterfaceMediumFont != null)
            {
                labelText.font = theme.InterfaceMediumFont;
                labelText.fontWeight = FontWeight.Medium;
            }
        }

        void RefreshVisual()
        {
            bool available = IsInteractable();
            bool focused = available && focusState.IsFocused;

            Color normalSurface = Theme.ButtonSurface;
            Color raisedSurface = Theme.ButtonSurfaceHighlighted;
            Color primary = Theme.PrimaryText;
            Color secondary = Theme.SecondaryText;
            Color focus = Theme.Focus;

            if (background != null)
            {
                Color surface = current || focused ? raisedSurface : normalSurface;
                if (!available)
                {
                    surface.a *= 0.42f;
                }

                background.color = surface;
                background.raycastTarget = true;
            }

            if (labelText != null)
            {
                Color labelColor = current || focused ? primary : secondary;
                if (!available)
                {
                    labelColor.a *= 0.42f;
                }

                labelText.color = labelColor;
            }

            if (selectionFrame != null)
            {
                Color frameColor = focus;
                frameColor.a *= focused ? 0.9f : current ? 0.42f : 0.12f;
                selectionFrame.color = frameColor;
                selectionFrame.raycastTarget = false;
            }
        }

        static string ToLabel(string value)
        {
            return UiLocalization.ToDisplayUpper(value);
        }
    }
}
