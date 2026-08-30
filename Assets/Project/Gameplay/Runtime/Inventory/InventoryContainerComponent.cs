using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Farion.Core.Identity;
using Farion.Core.Persistence;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Domain.Economy;
using Farion.Gameplay.Domain.Identity;
using UnityEngine;
using UnityEngine.Serialization;

namespace Farion.Gameplay.Inventory
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PersistentObjectId))]
    public abstract class InventoryContainerComponent : MonoBehaviour
    {
        const string DefaultContainerId = "inventory.local";

        [Header("Identity")]
        [FormerlySerializedAs("containerId")]
        [Tooltip("Used only when this object has no valid PersistentObjectId owner.")]
        [SerializeField] string fallbackContainerId = DefaultContainerId;

        [Header("Capacity")]
        [Min(1)]
        [SerializeField] int slotCapacity = 12;

        [Header("Runtime")]
        [SerializeField] List<InventoryStack> stacks = new();

        readonly Dictionary<DefinitionId, InventoryItemDefinition> definitionsById = new();
        readonly List<DefinitionId> definitionOrder = new();

        InventoryContainerState state;
        ReadOnlyCollection<InventoryStack> readOnlyStacks;

        public event Action Changed;

        protected virtual string ContainerIdNamespace => "inventory";

        public PersistentEntityId ContainerId
        {
            get
            {
                EnsureState();
                return state.ContainerId;
            }
        }

        public long Revision
        {
            get
            {
                EnsureState();
                return state.Revision;
            }
        }

        public IReadOnlyList<InventoryStack> Stacks
        {
            get
            {
                EnsureState();
                readOnlyStacks ??= stacks.AsReadOnly();
                return readOnlyStacks;
            }
        }

        public int SlotCapacity
        {
            get
            {
                EnsureState();
                return state.SlotCapacity;
            }
        }

        public bool IsFull
        {
            get
            {
                EnsureState();
                return !state.HasAnyStackCapacity;
            }
        }

        protected virtual void OnValidate()
        {
            fallbackContainerId = NormalizeContainerId(fallbackContainerId);
            slotCapacity = Mathf.Max(1, slotCapacity);
            stacks ??= new List<InventoryStack>();
            stacks.RemoveAll(stack => stack == null || stack.IsEmpty);
            InvalidateRuntimeState();
        }

        public bool CanAdd(InventoryItemDefinition item, int amount)
        {
            InventoryContainerState inventoryState = GetState();
            return amount > 0 &&
                   TryRegisterStackableDefinition(item, out DefinitionId definitionId) &&
                   inventoryState.GetAvailableStackCapacity(definitionId, item.MaxStackSize) >= amount;
        }

        public int TryAdd(InventoryItemDefinition item, int amount)
        {
            InventoryContainerState inventoryState = GetState();
            if (amount <= 0 ||
                !TryRegisterStackableDefinition(item, out DefinitionId definitionId))
            {
                return 0;
            }

            int accepted = Math.Min(
                amount,
                inventoryState.GetAvailableStackCapacity(definitionId, item.MaxStackSize));
            if (accepted <= 0 ||
                inventoryState.TryAddStack(
                    definitionId,
                    accepted,
                    item.MaxStackSize,
                    inventoryState.Revision) != InventoryOperationResult.Succeeded)
            {
                return 0;
            }

            SynchronizeSerializedStacks();
            NotifyChanged();
            return accepted;
        }

        public bool CanRemove(InventoryItemDefinition item, int amount)
        {
            return amount > 0 &&
                   TryGetDefinitionId(item, out DefinitionId definitionId) &&
                   GetState().Count(definitionId) >= amount;
        }

        public int TryRemove(InventoryItemDefinition item, int amount)
        {
            if (amount <= 0 ||
                !TryGetDefinitionId(item, out DefinitionId definitionId))
            {
                return 0;
            }

            InventoryContainerState inventoryState = GetState();
            int removed = Math.Min(amount, inventoryState.Count(definitionId));
            if (removed <= 0 ||
                inventoryState.TryRemoveStack(
                    definitionId,
                    removed,
                    item.MaxStackSize,
                    inventoryState.Revision) != InventoryOperationResult.Succeeded)
            {
                return 0;
            }

            SynchronizeSerializedStacks();
            NotifyChanged();
            return removed;
        }

        public bool CanRemoveAll(IReadOnlyList<ItemStackDefinition> costs)
        {
            if (!TryBuildChanges(costs, null, out List<InventoryStackChange> changes))
            {
                return false;
            }

            return changes.Count == 0 ||
                   GetState().CanApplyStackChanges(changes) ==
                   InventoryOperationResult.Succeeded;
        }

        public bool TryRemoveAll(IReadOnlyList<ItemStackDefinition> costs)
        {
            return TryApplyChanges(costs, null);
        }

        public bool CanExchange(
            IReadOnlyList<ItemStackDefinition> inputs,
            IReadOnlyList<ItemStackDefinition> outputs)
        {
            if (!TryBuildChanges(inputs, outputs, out List<InventoryStackChange> changes))
            {
                return false;
            }

            return changes.Count == 0 ||
                   GetState().CanApplyStackChanges(changes) ==
                   InventoryOperationResult.Succeeded;
        }

        public bool TryExchange(
            IReadOnlyList<ItemStackDefinition> inputs,
            IReadOnlyList<ItemStackDefinition> outputs)
        {
            return TryApplyChanges(inputs, outputs);
        }

        public int Count(InventoryItemDefinition item)
        {
            InventoryContainerState inventoryState = GetState();
            return TryGetDefinitionId(item, out DefinitionId definitionId)
                ? inventoryState.Count(definitionId)
                : 0;
        }

        public int GetAvailableCapacity(InventoryItemDefinition item)
        {
            InventoryContainerState inventoryState = GetState();
            return TryGetDefinitionId(item, out DefinitionId definitionId)
                ? inventoryState.GetAvailableStackCapacity(definitionId, item.MaxStackSize)
                : 0;
        }

        protected List<InventoryStackSnapshot> CaptureStackSnapshot()
        {
            EnsureState();
            List<InventoryStackSnapshot> snapshotStacks = new(definitionOrder.Count);
            for (int i = 0; i < definitionOrder.Count; i++)
            {
                DefinitionId definitionId = definitionOrder[i];
                int quantity = state.Count(definitionId);
                if (quantity <= 0 ||
                    !definitionsById.TryGetValue(
                        definitionId,
                        out InventoryItemDefinition item))
                {
                    continue;
                }

                snapshotStacks.Add(new InventoryStackSnapshot(item.ItemId, quantity));
            }

            return snapshotStacks;
        }

        public InventoryContainerSnapshot CaptureContainerSnapshot()
        {
            EnsureState();
            return new InventoryContainerSnapshot(
                ContainerId.Value,
                SlotCapacity,
                CaptureStackSnapshot());
        }

        public bool CanApplyContainerSnapshot(
            InventoryContainerSnapshot snapshot,
            GameplayDefinitionRegistry definitions)
        {
            return snapshot != null &&
                   SnapshotTargetsThisContainer(snapshot) &&
                   CanApplyStackSnapshot(
                       snapshot.SlotCapacity,
                       snapshot.Stacks,
                       definitions);
        }

        public bool ApplyContainerSnapshot(
            InventoryContainerSnapshot snapshot,
            GameplayDefinitionRegistry definitions,
            bool notify = true)
        {
            return CanApplyContainerSnapshot(snapshot, definitions) &&
                   TryApplyStackSnapshot(
                       snapshot.SlotCapacity,
                       snapshot.Stacks,
                       definitions,
                       notify);
        }

        protected bool TryApplyStackSnapshot(
            int snapshotSlotCapacity,
            IReadOnlyList<InventoryStackSnapshot> snapshotStacks,
            GameplayDefinitionRegistry definitions,
            bool notify)
        {
            if (!TryBuildSnapshotState(
                    snapshotSlotCapacity,
                    snapshotStacks,
                    definitions,
                    out InventoryContainerState candidate,
                    out Dictionary<DefinitionId, InventoryItemDefinition> candidateDefinitions,
                    out List<DefinitionId> candidateOrder))
            {
                return false;
            }

            state = candidate;
            slotCapacity = candidate.SlotCapacity;
            definitionsById.Clear();
            definitionOrder.Clear();
            foreach (KeyValuePair<DefinitionId, InventoryItemDefinition> pair in candidateDefinitions)
            {
                definitionsById.Add(pair.Key, pair.Value);
            }

            definitionOrder.AddRange(candidateOrder);
            SynchronizeSerializedStacks();
            if (notify)
            {
                NotifyChanged();
            }

            return true;
        }

        protected bool CanApplyStackSnapshot(
            int snapshotSlotCapacity,
            IReadOnlyList<InventoryStackSnapshot> snapshotStacks,
            GameplayDefinitionRegistry definitions)
        {
            return TryBuildSnapshotState(
                snapshotSlotCapacity,
                snapshotStacks,
                definitions,
                out _,
                out _,
                out _);
        }

        protected void NotifyChanged()
        {
            Changed?.Invoke();
        }

        internal InventoryContainerState DomainState => GetState();

        internal bool CanReferenceDefinitions(
            IReadOnlyList<InventoryItemDefinition> items)
        {
            EnsureState();
            if (items == null)
            {
                return false;
            }

            for (int i = 0; i < items.Count; i++)
            {
                InventoryItemDefinition item = items[i];
                if (!TryGetDefinitionId(item, out DefinitionId definitionId) ||
                    (definitionsById.TryGetValue(
                         definitionId,
                         out InventoryItemDefinition existing) &&
                     existing != item))
                {
                    return false;
                }
            }

            return true;
        }

        internal void CompleteDomainTransfer(
            IReadOnlyList<InventoryItemDefinition> transferredDefinitions)
        {
            if (transferredDefinitions != null)
            {
                for (int i = 0; i < transferredDefinitions.Count; i++)
                {
                    TryRegisterStackableDefinition(
                        transferredDefinitions[i],
                        out _);
                }
            }

            SynchronizeSerializedStacks();
            NotifyChanged();
        }

        bool TryApplyChanges(
            IReadOnlyList<ItemStackDefinition> inputs,
            IReadOnlyList<ItemStackDefinition> outputs)
        {
            if (!TryBuildChanges(inputs, outputs, out List<InventoryStackChange> changes))
            {
                return false;
            }

            if (changes.Count == 0)
            {
                return true;
            }

            InventoryContainerState inventoryState = GetState();
            long previousRevision = inventoryState.Revision;
            if (inventoryState.TryApplyStackChanges(changes, previousRevision) !=
                InventoryOperationResult.Succeeded)
            {
                return false;
            }

            if (inventoryState.Revision != previousRevision)
            {
                SynchronizeSerializedStacks();
                NotifyChanged();
            }

            return true;
        }

        bool TryBuildChanges(
            IReadOnlyList<ItemStackDefinition> inputs,
            IReadOnlyList<ItemStackDefinition> outputs,
            out List<InventoryStackChange> changes)
        {
            EnsureState();
            changes = new List<InventoryStackChange>(
                (inputs?.Count ?? 0) + (outputs?.Count ?? 0));
            return TryAppendChanges(inputs, -1, changes) &&
                   TryAppendChanges(outputs, 1, changes);
        }

        bool TryAppendChanges(
            IReadOnlyList<ItemStackDefinition> definitions,
            int direction,
            List<InventoryStackChange> changes)
        {
            if (definitions == null)
            {
                return true;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                ItemStackDefinition stack = definitions[i];
                if (stack == null)
                {
                    continue;
                }

                if (!stack.IsValid ||
                    !TryRegisterStackableDefinition(
                        stack.Item,
                        out DefinitionId definitionId))
                {
                    return false;
                }

                changes.Add(
                    new InventoryStackChange(
                        definitionId,
                        direction * stack.Amount,
                        stack.Item.MaxStackSize));
            }

            return true;
        }

        bool TryBuildSnapshotState(
            int snapshotSlotCapacity,
            IReadOnlyList<InventoryStackSnapshot> snapshotStacks,
            GameplayDefinitionRegistry definitions,
            out InventoryContainerState candidate,
            out Dictionary<DefinitionId, InventoryItemDefinition> candidateDefinitions,
            out List<DefinitionId> candidateOrder)
        {
            candidate = null;
            candidateDefinitions = null;
            candidateOrder = null;
            if (snapshotSlotCapacity < 1 || definitions == null)
            {
                return false;
            }

            candidateDefinitions = new Dictionary<DefinitionId, InventoryItemDefinition>();
            candidateOrder = new List<DefinitionId>();
            candidate = new InventoryContainerState(
                ResolveContainerId(),
                snapshotSlotCapacity);

            if (snapshotStacks == null)
            {
                return true;
            }

            for (int i = 0; i < snapshotStacks.Count; i++)
            {
                InventoryStackSnapshot stack = snapshotStacks[i];
                if (!stack.IsValid ||
                    !definitions.TryGetInventoryItem(
                        stack.ItemId,
                        out InventoryItemDefinition item) ||
                    !TryRegisterStackableDefinition(
                        item,
                        candidateDefinitions,
                        candidateOrder,
                        out DefinitionId definitionId) ||
                    candidate.TryAddStack(
                        definitionId,
                        stack.Quantity,
                        item.MaxStackSize) != InventoryOperationResult.Succeeded)
                {
                    candidate = null;
                    candidateDefinitions = null;
                    candidateOrder = null;
                    return false;
                }
            }

            return true;
        }

        InventoryContainerState GetState()
        {
            EnsureState();
            return state;
        }

        void EnsureState()
        {
            if (state != null)
            {
                return;
            }

            stacks ??= new List<InventoryStack>();
            readOnlyStacks ??= stacks.AsReadOnly();
            definitionsById.Clear();
            definitionOrder.Clear();
            state = new InventoryContainerState(
                ResolveContainerId(),
                Mathf.Max(1, slotCapacity));

            for (int i = 0; i < stacks.Count; i++)
            {
                InventoryStack stack = stacks[i];
                if (stack == null ||
                    stack.IsEmpty ||
                    !TryRegisterStackableDefinition(
                        stack.Item,
                        definitionsById,
                        definitionOrder,
                        out DefinitionId definitionId))
                {
                    continue;
                }

                int accepted = Math.Min(
                    stack.Quantity,
                    state.GetAvailableStackCapacity(
                        definitionId,
                        stack.Item.MaxStackSize));
                if (accepted > 0)
                {
                    state.TryAddStack(
                        definitionId,
                        accepted,
                        stack.Item.MaxStackSize);
                }
            }

            SynchronizeSerializedStacks();
        }

        void SynchronizeSerializedStacks()
        {
            stacks.Clear();
            for (int i = 0; i < definitionOrder.Count; i++)
            {
                DefinitionId definitionId = definitionOrder[i];
                if (!definitionsById.TryGetValue(
                        definitionId,
                        out InventoryItemDefinition item))
                {
                    continue;
                }

                int remaining = state.Count(definitionId);
                while (remaining > 0)
                {
                    int quantity = Math.Min(remaining, item.MaxStackSize);
                    stacks.Add(new InventoryStack(item, quantity));
                    remaining -= quantity;
                }
            }
        }

        public void RefreshIdentity()
        {
            InvalidateRuntimeState();
        }

        void InvalidateRuntimeState()
        {
            state = null;
            definitionsById.Clear();
            definitionOrder.Clear();
        }

        bool TryRegisterStackableDefinition(
            InventoryItemDefinition item,
            out DefinitionId definitionId)
        {
            return TryRegisterStackableDefinition(
                item,
                definitionsById,
                definitionOrder,
                out definitionId);
        }

        static bool TryRegisterStackableDefinition(
            InventoryItemDefinition item,
            Dictionary<DefinitionId, InventoryItemDefinition> targetDefinitions,
            List<DefinitionId> targetOrder,
            out DefinitionId definitionId)
        {
            definitionId = default;
            if (!TryGetDefinitionId(item, out definitionId))
            {
                return false;
            }

            if (targetDefinitions.TryGetValue(
                    definitionId,
                    out InventoryItemDefinition existing))
            {
                return existing == item;
            }

            targetDefinitions.Add(definitionId, item);
            targetOrder.Add(definitionId);
            return true;
        }

        static bool TryGetDefinitionId(
            InventoryItemDefinition item,
            out DefinitionId definitionId)
        {
            definitionId = default;
            return item != null &&
                   DefinitionId.TryCreate(item.ItemId, out definitionId);
        }

        PersistentEntityId ResolveContainerId()
        {
            if (TryGetComponent(out PersistentObjectId ownerId) &&
                ownerId.HasId &&
                PersistentEntityId.TryCreate(
                    $"{ContainerIdNamespace}.{ownerId.Id}",
                    out PersistentEntityId derivedId))
            {
                return derivedId;
            }

            string normalized = NormalizeContainerId(fallbackContainerId);
            fallbackContainerId = normalized;
            return new PersistentEntityId(normalized);
        }

        static string NormalizeContainerId(string value)
        {
            return PersistentEntityId.TryCreate(value, out PersistentEntityId id)
                ? id.Value
                : DefaultContainerId;
        }

        bool SnapshotTargetsThisContainer(InventoryContainerSnapshot snapshot)
        {
            return snapshot.HasValidContainerId &&
                   (!snapshot.HasContainerId ||
                    string.Equals(
                        snapshot.ContainerId,
                        ContainerId.Value,
                        StringComparison.Ordinal));
        }
    }
}
