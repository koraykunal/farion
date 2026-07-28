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

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class InventoryPanelPresenter : MonoBehaviour
    {
        [Header("Inventory")]
        [SerializeField] InventoryContainerComponent inventory;

        [Header("Slots")]
        [SerializeField] Transform slotContainer;
        [SerializeField] InventorySlotView slotPrefab;
        [SerializeField] List<InventorySlotView> slotViews = new();
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

        InventorySlotView focusedSlot;
        bool subscribed;

        public InventoryContainerComponent Inventory => inventory;

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
            Subscribe();
            Refresh();
        }

        void OnDisable()
        {
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
            EnsureSlotViews();

            IReadOnlyList<InventoryStack> stacks = inventory != null
                ? inventory.Stacks
                : System.Array.Empty<InventoryStack>();

            int visibleSlotCount = inventory != null
                ? Mathf.Max(inventory.SlotCapacity, stacks.Count)
                : slotViews.Count;

            for (int i = 0; i < slotViews.Count; i++)
            {
                InventorySlotView slot = slotViews[i];
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
            slotViews ??= new List<InventorySlotView>();
            slotViews.RemoveAll(slot => slot == null);

            if (instantiateMissingSlots && inventory != null && slotPrefab != null)
            {
                Transform parent = slotContainer != null ? slotContainer : transform;
                while (slotViews.Count < inventory.SlotCapacity)
                {
                    InventorySlotView slot = Instantiate(slotPrefab, parent);
                    slotViews.Add(slot);
                }
            }

            for (int i = 0; i < (slotViews?.Count ?? 0); i++)
            {
                InventorySlotView slot = slotViews[i];
                if (slot == null)
                {
                    continue;
                }

                slot.Focused -= HandleSlotFocused;
                slot.Focused += HandleSlotFocused;
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
                }
            }

            subscribed = false;
        }

        void HandleLocaleChanged(Locale _)
        {
            Refresh();
        }

        void HandleSlotFocused(InventorySlotView slot)
        {
            if (slot == null)
            {
                return;
            }

            focusedSlot = slot;
            RefreshDetail(slot.Stack);
        }

        void RefreshHeader(int usedSlots, int totalSlots)
        {
            SetText(titleText, UiLocalization.Get("inventory.title", "INVENTORY"));
            SetText(
                detailLabelText,
                UiLocalization.Get("inventory.detail", "ITEM DETAIL"));
            SetText(
                capacityText,
                $"{usedSlots:00} / {Mathf.Max(0, totalSlots):00} " +
                UiLocalization.Get("inventory.slots", "SLOTS").ToUpperInvariant());
        }

        void RefreshFocus()
        {
            if (focusedSlot == null ||
                !focusedSlot.gameObject.activeInHierarchy)
            {
                focusedSlot = FindInitialSlot();
            }

            RefreshDetail(focusedSlot != null ? focusedSlot.Stack : null);
            if (screenView != null && focusedSlot != null)
            {
                screenView.SetFirstSelection(focusedSlot);
            }
        }

        InventorySlotView FindInitialSlot()
        {
            InventorySlotView firstVisible = null;
            for (int i = 0; i < slotViews.Count; i++)
            {
                InventorySlotView slot = slotViews[i];
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
                SetText(
                    detailTitleText,
                    UiLocalization.Get("inventory.empty.title", "EMPTY SLOT"));
                SetText(
                    detailMetaText,
                    UiLocalization.Get(
                        "inventory.empty.description",
                        "Available for collected materials and components."));
                SetText(detailStackText, string.Empty);
                SetText(detailDomainText, string.Empty);
                return;
            }

            InventoryItemDefinition item = stack.Item;
            SetText(detailTitleText, item.DisplayName.ToUpperInvariant());
            SetText(
                detailMetaText,
                $"{FormatEnum(item.Category)}  /  {FormatEnum(item.Form)}");
            SetText(
                detailStackText,
                $"{UiLocalization.Get("inventory.stack", "STACK").ToUpperInvariant()}  " +
                $"{stack.Quantity} / {item.MaxStackSize}");
            SetText(
                detailDomainText,
                $"{UiLocalization.Get("inventory.domain", "RESEARCH DOMAIN").ToUpperInvariant()}\n" +
                FormatEnum(item.PrimaryTechDomain));
        }

        void ResolveReferences()
        {
            screenView ??= GetComponent<UiScreenView>();
            slotViews ??= new List<InventorySlotView>();
            if (slotContainer == null)
            {
                slotContainer = transform.Find("SlotPanel/SlotContainer");
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
