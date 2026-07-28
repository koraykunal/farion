using System;
using Farion.Gameplay.Inventory;
using Farion.UI.Foundation;
using Farion.UI.Localization;
using Farion.UI.Styling;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class InventorySlotView : Selectable
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

        bool pointerInside;
        bool selected;
        bool hasItem;

        public event Action<InventorySlotView> Focused;

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
            pointerInside = false;
            selected = false;
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
                    : UiLocalization.Get("inventory.empty.title", "EMPTY SLOT"));
            SetText(detailText, item != null ? $"{item.Category} / {item.Form}" : string.Empty);
            SetText(quantityText, hasItem ? stack.Quantity.ToString() : string.Empty);

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            RefreshVisual();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            pointerInside = true;
            RefreshVisual();
            Focused?.Invoke(this);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            pointerInside = false;
            RefreshVisual();
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            selected = true;
            RefreshVisual();
            Focused?.Invoke(this);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            selected = false;
            RefreshVisual();
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
        }

        void RefreshVisual()
        {
            bool focused = IsInteractable() && (selected || pointerInside);

            Color panelNormal = theme != null
                ? theme.ButtonSurface
                : new Color(0.024f, 0.037f, 0.052f, 0.78f);
            Color panelFocused = theme != null
                ? theme.ButtonSurfaceHighlighted
                : new Color(0.055f, 0.086f, 0.118f, 0.94f);
            Color primary = theme != null
                ? theme.PrimaryText
                : new Color(0.88f, 0.91f, 0.93f, 1f);
            Color secondary = theme != null
                ? theme.SecondaryText
                : new Color(0.54f, 0.6f, 0.65f, 0.9f);
            Color supporting = theme != null
                ? theme.SupportingText
                : new Color(0.68f, 0.76f, 0.81f, 1f);
            Color focus = theme != null
                ? theme.Focus
                : new Color(0.56f, 0.68f, 0.76f, 1f);

            if (background != null)
            {
                background.color = focused ? panelFocused : panelNormal;
                background.raycastTarget = true;
            }

            if (nameText != null)
            {
                nameText.color = hasItem
                    ? focused
                        ? primary
                        : new Color(primary.r, primary.g, primary.b, 0.84f)
                    : new Color(secondary.r, secondary.g, secondary.b, 0.52f);
            }

            if (detailText != null)
            {
                detailText.color = focused ? supporting : secondary;
            }

            if (quantityText != null)
            {
                quantityText.color = focused ? focus : supporting;
            }

            if (selectionFrame == null)
            {
                return;
            }

            focus.a *= focused ? 0.92f : hasItem ? 0.14f : 0.08f;
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
    }
}
