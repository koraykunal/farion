using System;
using System.Collections.Generic;
using Farion.Core.Identity;
using Farion.Gameplay.Domain.Identity;
using UnityEngine;

namespace Farion.Gameplay.Inventory
{
    [Serializable]
    public sealed class InventoryContainerSnapshot
    {
        [SerializeField] string containerId;
        [SerializeField] int slotCapacity;
        [SerializeField] List<InventoryStackSnapshot> stacks = new();

        public InventoryContainerSnapshot(
            string containerId,
            int slotCapacity,
            IReadOnlyList<InventoryStackSnapshot> stacks)
        {
            this.containerId = string.IsNullOrWhiteSpace(containerId)
                ? string.Empty
                : containerId.Trim();
            this.slotCapacity = Mathf.Max(1, slotCapacity);
            this.stacks = stacks != null
                ? new List<InventoryStackSnapshot>(stacks)
                : new List<InventoryStackSnapshot>();
        }

        public string ContainerId => string.IsNullOrWhiteSpace(containerId)
            ? string.Empty
            : containerId.Trim();
        public int SlotCapacity => Mathf.Max(1, slotCapacity);
        public IReadOnlyList<InventoryStackSnapshot> Stacks => stacks;
        public bool HasContainerId => !string.IsNullOrEmpty(ContainerId);
        public bool HasValidContainerId =>
            !HasContainerId ||
            PersistentEntityId.TryCreate(ContainerId, out _);
    }
}
