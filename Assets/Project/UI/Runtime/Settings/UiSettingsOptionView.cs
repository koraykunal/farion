using System;
using Farion.UI.Common;
using Farion.UI.Foundation;
using Farion.UI.Styling;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Farion.UI.Settings
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class UiSettingsOptionView :
        Selectable,
        ISubmitHandler,
        IPointerClickHandler
    {
        [Header("Identity")]
        [SerializeField] UiSettingId settingId;

        [Header("Content")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text valueText;

        [Header("Visuals")]
        [SerializeField] UiTheme theme;
        [SerializeField] Image background;
        [SerializeField] Image selectionFrame;

        UiPointerFocusState focusState;
        string displayTitle = string.Empty;
        string displayDescription = string.Empty;
        string displayValue = string.Empty;

        public event Action<UiSettingsOptionView, int> AdjustmentRequested;
        public event Action<UiSettingsOptionView> Focused;

        public UiSettingId SettingId => settingId;
        public string DisplayTitle => displayTitle;
        public string DisplayDescription => displayDescription;
        public string DisplayValue => displayValue;

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

        public void ConfigureContent(string title, string description, string value)
        {
            displayTitle = title ?? string.Empty;
            displayDescription = description ?? string.Empty;

            if (titleText != null)
            {
                titleText.text = ToLabel(displayTitle);
            }

            SetValue(value);
        }

        public void SetValue(string value)
        {
            displayValue = value ?? string.Empty;
            if (valueText != null)
            {
                valueText.text =
                    $"\u2039  {ToLabel(displayValue)}  \u203A";
            }
        }

        public void SetAvailable(bool available)
        {
            interactable = available;
            RefreshVisual();
        }

        public override void OnMove(AxisEventData eventData)
        {
            switch (eventData.moveDir)
            {
                case MoveDirection.Left:
                    RequestAdjustment(-1);
                    eventData.Use();
                    return;
                case MoveDirection.Right:
                    RequestAdjustment(1);
                    eventData.Use();
                    return;
                default:
                    base.OnMove(eventData);
                    break;
            }
        }

        public void OnSubmit(BaseEventData eventData)
        {
            RequestAdjustment(1);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
            {
                return;
            }

            focusState.PointerClick();
            Select();
            RequestAdjustment(
                eventData.button == PointerEventData.InputButton.Right ? -1 : 1);
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

        void RequestAdjustment(int direction)
        {
            if (IsActive() && IsInteractable())
            {
                AdjustmentRequested?.Invoke(this, direction < 0 ? -1 : 1);
            }
        }

        void ResolveReferences()
        {
            background ??= GetComponent<Image>();
            targetGraphic = background;

            if (theme == null)
            {
                UiSystemRoot root = UiCompositionScope.FindSystemRoot(this);
                theme = root != null ? root.Theme : null;
            }

            if (titleText != null && theme != null && theme.InterfaceMediumFont != null)
            {
                titleText.font = theme.InterfaceMediumFont;
                titleText.fontWeight = FontWeight.Medium;
            }

            if (valueText != null && theme != null && theme.InstrumentFont != null)
            {
                valueText.font = theme.InstrumentFont;
                valueText.fontWeight = FontWeight.Medium;
            }
        }

        void RefreshVisual()
        {
            bool available = IsInteractable();
            bool focused = available && focusState.IsFocused;

            Color panelNormal = theme != null
                ? theme.ButtonSurface
                : new Color(0.035f, 0.052f, 0.072f, 0.72f);
            Color panelFocused = theme != null
                ? theme.ButtonSurfaceHighlighted
                : new Color(0.07f, 0.11f, 0.15f, 0.92f);
            Color primary = theme != null
                ? theme.PrimaryText
                : new Color(0.88f, 0.91f, 0.93f, 1f);
            Color supporting = theme != null
                ? theme.SupportingText
                : new Color(0.68f, 0.76f, 0.81f, 1f);
            Color focus = theme != null
                ? theme.Focus
                : new Color(0.56f, 0.68f, 0.76f, 1f);

            if (!available)
            {
                panelNormal.a *= 0.42f;
                supporting.a = 0.24f;
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
                valueText.color = focused ? focus : supporting;
            }

            if (selectionFrame == null)
            {
                return;
            }

            focus.a *= focused ? 0.9f : available ? 0.12f : 0.05f;
            selectionFrame.color = focus;
            selectionFrame.raycastTarget = false;
        }

        static string ToLabel(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.ToUpperInvariant();
        }
    }
}
