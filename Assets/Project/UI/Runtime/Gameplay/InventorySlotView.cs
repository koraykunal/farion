using System;
using Farion.Gameplay.Inventory;
using Farion.UI.Common;
using Farion.UI.Foundation;
using Farion.UI.Styling;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class InventorySlotView :
        Selectable,
        ISubmitHandler,
        IPointerClickHandler
    {
        [Header("Content")]
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text detailText;
        [SerializeField] TMP_Text quantityText;
        [SerializeField] Image iconImage;

        [Header("Visuals")]
        [SerializeField] UiTheme theme;
        [SerializeField] Image background;
        [SerializeField] Image selectionFrame;

        UiPointerFocusState focusState;
        bool current;
        bool hasItem;

        UiTheme Theme => theme = UiTheme.Resolve(theme);

        public event Action<InventorySlotView> Focused;
        public event Action<InventorySlotView> ContextRequested;
        public event Action<InventorySlotView> PrimaryClicked;

        public InventoryStack Stack { get; private set; }

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

        public void SetStack(InventoryStack stack)
        {
            Stack = stack;
            hasItem = stack != null && !stack.IsEmpty && stack.Item != null;
            InventoryItemDefinition item = hasItem ? stack.Item : null;

            SetText(
                nameText,
                item != null
                    ? item.DisplayName
                    : string.Empty);
            SetText(detailText, item != null ? $"{item.Category} / {item.Form}" : string.Empty);
            SetText(quantityText, hasItem ? $"\u00D7{stack.Quantity}" : string.Empty);

            if (iconImage != null)
            {
                iconImage.sprite = item != null ? item.Icon : null;
                iconImage.enabled = item != null && item.HasIcon;
                iconImage.color = item != null
                    ? item.AccentColor
                    : Color.white;
                iconImage.raycastTarget = false;
            }

            RefreshVisual();
        }

        public void SetCurrent(bool value)
        {
            current = value;
            RefreshVisual();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
            {
                return;
            }

            ContextRequested?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
            {
                return;
            }

            focusState.PointerClick();
            Select();
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                ContextRequested?.Invoke(this);
                return;
            }

            PrimaryClicked?.Invoke(this);
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

        void ResolveReferences()
        {
            background ??= GetComponent<Image>();
            targetGraphic = background;

            if (theme == null)
            {
                UiSystemRoot root = UiCompositionScope.FindSystemRoot(this);
                theme = UiTheme.Resolve(root != null ? root.Theme : null);
            }


            SetFont(nameText, theme.InterfaceMediumFont, FontWeight.Medium);
            SetFont(detailText, theme.InterfaceFont, FontWeight.Regular);
            SetFont(quantityText, theme.InstrumentFont, FontWeight.Medium);
        }

        void RefreshVisual()
        {
            bool focused = IsInteractable() && focusState.IsFocused;
            bool emphasized = focused || current;

            Color panelNormal = Theme.ButtonSurface;
            Color panelFocused = Theme.ButtonSurfaceHighlighted;
            Color primary = Theme.PrimaryText;
            Color secondary = Theme.SecondaryText;
            Color supporting = Theme.SupportingText;
            Color focus = Theme.Focus;

            if (background != null)
            {
                background.color = emphasized ? panelFocused : panelNormal;
                background.raycastTarget = true;
            }

            if (nameText != null)
            {
                nameText.color = hasItem
                    ? emphasized
                        ? primary
                        : new Color(primary.r, primary.g, primary.b, 0.84f)
                    : secondary;
            }

            if (detailText != null)
            {
                detailText.color = emphasized ? supporting : secondary;
            }

            if (quantityText != null)
            {
                quantityText.color = emphasized ? focus : supporting;
            }

            if (selectionFrame == null)
            {
                return;
            }

            focus.a *= focused ? 0.92f : current ? 0.58f : hasItem ? 0.14f : 0.06f;
            selectionFrame.color = focus;
            selectionFrame.raycastTarget = false;
        }

        static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        static void SetFont(TMP_Text target, TMP_FontAsset font, FontWeight weight)
        {
            if (target != null && font != null)
            {
                target.font = font;
                target.fontWeight = weight;
            }
        }
    }
}
