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
    public sealed class UiSettingsOptionView :
        Slider,
        IUiValueControl,
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
        bool pending;
        string displayTitle = string.Empty;
        string displayDescription = string.Empty;
        string displayValue = string.Empty;
        bool rangeDragging;

        UiTheme Theme => theme = UiTheme.Resolve(theme);

        public event Action<UiSettingsOptionView, int> AdjustmentRequested;
        public event Action<UiSettingsOptionView, float> RangeValueChanged;
        public event Action<IUiValueControl> Focused;

        public UiSettingId SettingId => settingId;
        public string DisplayTitle => displayTitle;
        public string DisplayDescription => displayDescription;
        public string DisplayValue => displayValue;
        public Selectable Selectable => this;
        public bool HasRange => fillRect != null;

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
            transition = Transition.None;
            onValueChanged.AddListener(HandleRangeValueChanged);
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

        protected override void OnDestroy()
        {
            onValueChanged.RemoveListener(HandleRangeValueChanged);
            base.OnDestroy();
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

        public void ConfigureRange(float normalizedValue)
        {
            if (!HasRange)
            {
                return;
            }

            minValue = 0f;
            maxValue = 1f;
            wholeNumbers = false;
            direction = Direction.LeftToRight;
            SetValueWithoutNotify(Mathf.Clamp01(normalizedValue));
            RefreshVisual();
        }

        public void SetAvailable(bool available)
        {
            interactable = available;
            RefreshVisual();
        }

        public void SetPending(bool value)
        {
            if (pending == value)
            {
                return;
            }

            pending = value;
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
            if (HasRange && eventData.button == PointerEventData.InputButton.Left)
            {
                Focused?.Invoke(this);
                return;
            }

            RequestAdjustment(
                eventData.button == PointerEventData.InputButton.Right ? -1 : 1);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            rangeDragging = HasRange &&
                eventData.button == PointerEventData.InputButton.Left &&
                RectTransformUtility.RectangleContainsScreenPoint(
                    fillRect.parent as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera);
            if (HasRange && !rangeDragging)
            {
                Select();
                return;
            }

            base.OnPointerDown(eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (!HasRange || rangeDragging)
            {
                base.OnDrag(eventData);
            }
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

        void HandleRangeValueChanged(float normalizedValue)
        {
            if (!Application.isPlaying || !HasRange)
            {
                return;
            }

            Select();
            RangeValueChanged?.Invoke(this, normalizedValue);
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
            bool focused = available && focusState.IsFocused;

            Color panelNormal = Theme.ButtonSurface;
            Color panelFocused = Theme.ButtonSurfaceHighlighted;
            Color primary = Theme.PrimaryText;
            Color supporting = Theme.SupportingText;
            Color focus = Theme.Focus;

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
                valueText.color = pending && available
                    ? Theme.Caution
                    : focused
                        ? focus
                        : supporting;
            }

            if (selectionFrame == null)
            {
                RefreshRangeVisual(available, focused);
                return;
            }

            focus.a *= focused ? 0.9f : available ? 0.12f : 0.05f;
            selectionFrame.color = focus;
            selectionFrame.raycastTarget = false;
            RefreshRangeVisual(available, focused);
        }

        void RefreshRangeVisual(bool available, bool focused)
        {
            if (!HasRange)
            {
                return;
            }

            Image fillImage = fillRect.GetComponent<Image>();
            Image trackImage = fillRect.parent.GetComponent<Image>();
            if (trackImage != null)
            {
                trackImage.color = focused ? Theme.RaisedSurface : Theme.PanelSurface;
                trackImage.raycastTarget = false;
            }

            if (fillImage != null)
            {
                Color color = pending && available
                    ? Theme.Caution
                    : focused
                        ? Theme.Focus
                        : Theme.SupportingText;
                if (!available)
                {
                    color.a *= 0.35f;
                }

                fillImage.color = color;
                fillImage.raycastTarget = false;
            }
        }

        static string ToLabel(string value)
        {
            return UiLocalization.ToDisplayUpper(value);
        }
    }
}
