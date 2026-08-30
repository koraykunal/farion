using System;
using System.Collections.Generic;
using System.Text;
using Farion.Gameplay.Inventory;
using Farion.UI.Localization;
using Farion.UI.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class UiInventoryPanelPresenter : MonoBehaviour
    {
        [Header("Inventory")]
        [SerializeField] InventoryContainerComponent inventory;

        [Header("Slots")]
        [SerializeField] Transform slotContainer;
        [SerializeField] UiInventorySlotView slotPrefab;
        [SerializeField] List<UiInventorySlotView> slotViews = new();
        [SerializeField] bool instantiateMissingSlots = true;

        [Header("Screen")]
        [SerializeField] UiScreenView screenView;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text capacityText;

        [Header("Selected Item")]
        [SerializeField] TMP_Text detailLabelText;
        [SerializeField] TMP_Text detailTitleText;
        [SerializeField] TMP_Text detailMetaText;
        [SerializeField] TMP_Text detailStackText;
        [SerializeField] TMP_Text detailDomainText;
        [SerializeField] Image detailIconImage;

        [Header("Quick View")]
        [SerializeField] RectTransform tooltipPrefab;

        UiInventorySlotView focusedSlot;
        UiInventorySlotView tooltipSlot;
        RectTransform tooltipRoot;
        TMP_Text tooltipTitleText;
        TMP_Text tooltipBodyText;
        bool subscribed;

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            ResolveReferences();
            HideTooltip();
            Subscribe();
            Refresh();
        }

        void OnDisable()
        {
            HideTooltip();
            Unsubscribe();
        }

        public void SetInventory(InventoryContainerComponent nextInventory)
        {
            if (inventory == nextInventory)
            {
                Refresh();
                return;
            }

            Unsubscribe();
            inventory = nextInventory;
            Subscribe();
            Refresh();
        }

        public void Refresh()
        {
            HideTooltip();
            EnsureSlotViews();

            IReadOnlyList<InventoryStack> stacks = inventory != null
                ? inventory.Stacks
                : System.Array.Empty<InventoryStack>();

            int visibleSlotCount = inventory != null
                ? Mathf.Max(inventory.SlotCapacity, stacks.Count)
                : slotViews.Count;

            for (int i = 0; i < slotViews.Count; i++)
            {
                UiInventorySlotView slot = slotViews[i];
                if (slot == null)
                {
                    continue;
                }

                bool visible = i < visibleSlotCount;
                slot.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                InventoryStack stack = i < stacks.Count ? stacks[i] : null;
                slot.SetStack(stack);
            }

            RefreshHeader(stacks.Count, visibleSlotCount);
            RefreshFocus();
        }

        void EnsureSlotViews()
        {
            slotViews ??= new List<UiInventorySlotView>();
            slotViews.RemoveAll(slot => slot == null);

            if (instantiateMissingSlots && inventory != null && slotPrefab != null)
            {
                Transform parent = slotContainer != null ? slotContainer : transform;
                while (slotViews.Count < inventory.SlotCapacity)
                {
                    UiInventorySlotView slot = Instantiate(slotPrefab, parent);
                    slotViews.Add(slot);
                }
            }

            for (int i = 0; i < (slotViews?.Count ?? 0); i++)
            {
                UiInventorySlotView slot = slotViews[i];
                if (slot == null)
                {
                    continue;
                }

                slot.Focused -= HandleSlotFocused;
                slot.Focused += HandleSlotFocused;
                slot.ContextRequested -= HandleContextRequested;
                slot.ContextRequested += HandleContextRequested;
                slot.PrimaryClicked -= HandlePrimaryClicked;
                slot.PrimaryClicked += HandlePrimaryClicked;
            }
        }

        void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (inventory != null)
            {
                inventory.Changed += Refresh;
            }

            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
            subscribed = true;
        }

        void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (inventory != null)
            {
                inventory.Changed -= Refresh;
            }

            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
            for (int i = 0; i < slotViews.Count; i++)
            {
                if (slotViews[i] != null)
                {
                    slotViews[i].Focused -= HandleSlotFocused;
                    slotViews[i].ContextRequested -= HandleContextRequested;
                    slotViews[i].PrimaryClicked -= HandlePrimaryClicked;
                }
            }

            subscribed = false;
        }

        void HandleLocaleChanged(Locale _)
        {
            Refresh();
        }

        void HandleSlotFocused(UiInventorySlotView slot)
        {
            if (slot == null)
            {
                return;
            }

            focusedSlot?.SetCurrent(false);
            focusedSlot = slot;
            focusedSlot.SetCurrent(true);
            RefreshDetail(slot.Stack);
        }

        void HandleContextRequested(UiInventorySlotView slot)
        {
            if (slot == null || slot.Stack == null || slot.Stack.IsEmpty)
            {
                HideTooltip();
                return;
            }

            if (tooltipSlot == slot && tooltipRoot != null && tooltipRoot.gameObject.activeSelf)
            {
                HideTooltip();
                return;
            }

            EnsureTooltip();
            if (tooltipRoot == null)
            {
                return;
            }

            tooltipSlot = slot;
            InventoryItemDefinition item = slot.Stack.Item;
            SetText(tooltipTitleText, UiLocalization.ToDisplayUpper(item.DisplayName));
            SetText(
                tooltipBodyText,
                $"{FormatEnum(item.Category)}  /  {FormatEnum(item.Form)}\n" +
                $"{UiLocalization.ToDisplayUpper(UiLocalization.Get(UiTextKeys.InventoryStack))}  " +
                $"{slot.Stack.Quantity} / {item.MaxStackSize}\n" +
                $"{UiLocalization.ToDisplayUpper(UiLocalization.Get(UiTextKeys.InventoryDomain))}  " +
                FormatEnum(item.PrimaryTechDomain));

            tooltipRoot.SetAsLastSibling();
            tooltipRoot.gameObject.SetActive(true);
            PositionTooltip(slot.transform as RectTransform);
        }

        void HandlePrimaryClicked(UiInventorySlotView _)
        {
            HideTooltip();
        }

        void RefreshHeader(int usedSlots, int totalSlots)
        {
            SetText(titleText, UiLocalization.Get(UiTextKeys.InventoryTitle));
            SetText(
                detailLabelText,
                UiLocalization.Get(UiTextKeys.InventoryDetail));
            SetText(
                capacityText,
                $"{usedSlots:00} / {Mathf.Max(0, totalSlots):00} " +
                UiLocalization.ToDisplayUpper(UiLocalization.Get(UiTextKeys.InventorySlots)));
        }

        void RefreshFocus()
        {
            if (focusedSlot == null ||
                !focusedSlot.gameObject.activeInHierarchy)
            {
                focusedSlot?.SetCurrent(false);
                focusedSlot = FindInitialSlot();
            }

            focusedSlot?.SetCurrent(true);
            RefreshDetail(focusedSlot != null ? focusedSlot.Stack : null);
            if (screenView != null && focusedSlot != null)
            {
                screenView.SetFirstSelection(focusedSlot);
            }
        }

        UiInventorySlotView FindInitialSlot()
        {
            UiInventorySlotView firstVisible = null;
            for (int i = 0; i < slotViews.Count; i++)
            {
                UiInventorySlotView slot = slotViews[i];
                if (slot == null || !slot.gameObject.activeInHierarchy)
                {
                    continue;
                }

                firstVisible ??= slot;
                if (slot.Stack != null && !slot.Stack.IsEmpty)
                {
                    return slot;
                }
            }

            return firstVisible;
        }

        void RefreshDetail(InventoryStack stack)
        {
            bool hasItem = stack != null &&
                           !stack.IsEmpty &&
                           stack.Item != null;
            if (!hasItem)
            {
                if (detailIconImage != null)
                {
                    detailIconImage.sprite = null;
                    detailIconImage.enabled = false;
                }

                SetText(
                    detailTitleText,
                    UiLocalization.Get(UiTextKeys.InventoryEmptyTitle));
                SetText(
                    detailMetaText,
                    UiLocalization.Get(UiTextKeys.InventoryEmptyDescription));
                SetText(detailStackText, string.Empty);
                SetText(detailDomainText, string.Empty);
                return;
            }

            InventoryItemDefinition item = stack.Item;
            if (detailIconImage != null)
            {
                detailIconImage.sprite = item.Icon;
                detailIconImage.enabled = item.HasIcon;
                detailIconImage.color = item.AccentColor;
                detailIconImage.raycastTarget = false;
            }

            SetText(detailTitleText, UiLocalization.ToDisplayUpper(item.DisplayName));
            SetText(
                detailMetaText,
                $"{FormatEnum(item.Category)}  /  {FormatEnum(item.Form)}");
            SetText(
                detailStackText,
                $"{UiLocalization.ToDisplayUpper(UiLocalization.Get(UiTextKeys.InventoryStack))}  " +
                $"{stack.Quantity} / {item.MaxStackSize}");
            SetText(
                detailDomainText,
                $"{UiLocalization.ToDisplayUpper(UiLocalization.Get(UiTextKeys.InventoryDomain))}\n" +
                FormatEnum(item.PrimaryTechDomain));
        }

        void ResolveReferences()
        {
            screenView ??= GetComponent<UiScreenView>();
            slotViews ??= new List<UiInventorySlotView>();
            if (slotContainer == null)
            {
                slotContainer = transform.Find("SlotPanel/SlotContainer");
            }
        }

        void EnsureTooltip()
        {
            if (tooltipRoot != null || tooltipPrefab == null)
            {
                return;
            }

            tooltipRoot = Instantiate(tooltipPrefab, transform);
            tooltipRoot.name = tooltipPrefab.name;
            tooltipTitleText = tooltipRoot.Find("Title")?.GetComponent<TMP_Text>();
            tooltipBodyText = tooltipRoot.Find("Body")?.GetComponent<TMP_Text>();
            tooltipRoot.gameObject.SetActive(false);
        }

        void PositionTooltip(RectTransform slot)
        {
            if (slot == null || tooltipRoot == null || transform is not RectTransform screen)
            {
                return;
            }

            Vector3[] corners = new Vector3[4];
            slot.GetWorldCorners(corners);
            Vector2 position = screen.InverseTransformPoint(corners[2]);
            position.x += 12f;

            Rect bounds = screen.rect;
            Vector2 size = tooltipRoot.rect.size;
            position.x = Mathf.Clamp(position.x, bounds.xMin + 24f, bounds.xMax - size.x - 24f);
            position.y = Mathf.Clamp(position.y, bounds.yMin + size.y + 24f, bounds.yMax - 24f);
            tooltipRoot.anchoredPosition = position;
        }

        void HideTooltip()
        {
            tooltipSlot = null;
            if (tooltipRoot != null)
            {
                tooltipRoot.gameObject.SetActive(false);
            }
        }

        static string FormatEnum<T>(T value)
            where T : Enum
        {
            string source = value.ToString();
            if (string.IsNullOrEmpty(source))
            {
                return string.Empty;
            }

            StringBuilder builder = new(source.Length + 4);
            for (int i = 0; i < source.Length; i++)
            {
                char character = source[i];
                if (i > 0 &&
                    char.IsUpper(character) &&
                    !char.IsUpper(source[i - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(char.ToUpperInvariant(character));
            }

            return builder.ToString();
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
