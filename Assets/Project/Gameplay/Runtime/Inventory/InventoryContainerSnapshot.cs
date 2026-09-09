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
        [SerializeField] long revision = NoRevision;

        public const long NoRevision = -1L;

        public InventoryContainerSnapshot(
            string containerId,
            int slotCapacity,
            IReadOnlyList<InventoryStackSnapshot> stacks,
            long revision = NoRevision)
        {
            this.revision = revision < 0L ? NoRevision : revision;
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
        public long Revision => revision < 0L ? NoRevision : revision;
        public bool HasRevision => revision >= 0L;
        public bool HasContainerId => !string.IsNullOrEmpty(ContainerId);

        public InventoryContainerSnapshot WithContainerId(string targetContainerId)
        {
            return new InventoryContainerSnapshot(targetContainerId, SlotCapacity, stacks, revision);
        }
        public bool HasValidContainerId =>
            !HasContainerId ||
            PersistentEntityId.TryCreate(ContainerId, out _);
    }
}
