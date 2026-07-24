using System.Collections.Generic;
using Farion.Gameplay.Inventory;
using UnityEngine;

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class InventoryPanelPresenter : MonoBehaviour
    {
        [Header("Inventory")]
        [SerializeField] PlayerInventory inventory;

        [Header("Slots")]
        [SerializeField] Transform slotContainer;
        [SerializeField] InventorySlotView slotPrefab;
        [SerializeField] List<InventorySlotView> slotViews = new();
        [SerializeField] bool instantiateMissingSlots = true;

        public PlayerInventory Inventory => inventory;

        void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        public void SetInventory(PlayerInventory nextInventory)
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
        }

        void EnsureSlotViews()
        {
            slotViews ??= new List<InventorySlotView>();
            slotViews.RemoveAll(slot => slot == null);

            if (!instantiateMissingSlots || inventory == null || slotPrefab == null)
            {
                return;
            }

            Transform parent = slotContainer != null ? slotContainer : transform;
            while (slotViews.Count < inventory.SlotCapacity)
            {
                InventorySlotView slot = Instantiate(slotPrefab, parent);
                slotViews.Add(slot);
            }
        }

        void Subscribe()
        {
            if (inventory != null)
            {
                inventory.Changed += Refresh;
            }
        }

        void Unsubscribe()
        {
            if (inventory != null)
            {
                inventory.Changed -= Refresh;
            }
        }
    }
}
