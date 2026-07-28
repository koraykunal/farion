using System;
using System.Collections.Generic;
using Farion.Gameplay.Domain.Identity;

namespace Farion.Gameplay.Domain.Economy
{
    public sealed class InventoryContainerState
    {
        public const long AnyRevision = -1L;

        readonly Dictionary<DefinitionId, InventoryStackState> stacks = new();
        readonly HashSet<PersistentEntityId> itemInstanceIds = new();

        public InventoryContainerState(PersistentEntityId containerId, int slotCapacity)
        {
            if (!containerId.IsValid)
            {
                throw new ArgumentException("Inventory containers require a valid persistent id.", nameof(containerId));
            }

            if (slotCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCapacity));
            }

            ContainerId = containerId;
            SlotCapacity = slotCapacity;
        }

        InventoryContainerState(
            PersistentEntityId containerId,
            int slotCapacity,
            long revision,
            Dictionary<DefinitionId, InventoryStackState> stacks,
            HashSet<PersistentEntityId> itemInstanceIds)
        {
            ContainerId = containerId;
            SlotCapacity = slotCapacity;
            Revision = revision;
            this.stacks = stacks;
            this.itemInstanceIds = itemInstanceIds;
        }

        public PersistentEntityId ContainerId { get; }
        public int SlotCapacity { get; private set; }
        public long Revision { get; private set; }
        public IEnumerable<InventoryStackState> Stacks
        {
            get
            {
                foreach (InventoryStackState stack in stacks.Values)
                {
                    yield return stack;
                }
            }
        }

        public IEnumerable<PersistentEntityId> ItemInstanceIds
        {
            get
            {
                foreach (PersistentEntityId itemInstanceId in itemInstanceIds)
                {
                    yield return itemInstanceId;
                }
            }
        }
        public int UsedSlots
        {
            get
            {
                long usedSlots = CountUsedStackSlots(stacks) + itemInstanceIds.Count;
                return usedSlots >= int.MaxValue ? int.MaxValue : (int)usedSlots;
            }
        }

        public int FreeSlots => Math.Max(0, SlotCapacity - UsedSlots);
        public bool HasAnyStackCapacity => FreeSlots > 0 || HasPartialStack();

        public bool MatchesRevision(long expectedRevision)
        {
            return expectedRevision == AnyRevision || expectedRevision == Revision;
        }

        public int Count(DefinitionId definitionId)
        {
            return definitionId.IsValid && stacks.TryGetValue(definitionId, out InventoryStackState stack)
                ? stack.Quantity
                : 0;
        }

        public bool ContainsItemInstance(PersistentEntityId instanceId)
        {
            return instanceId.IsValid && itemInstanceIds.Contains(instanceId);
        }

        public int GetAvailableStackCapacity(DefinitionId definitionId, int stackLimit)
        {
            if (!definitionId.IsValid || stackLimit < 1)
            {
                return 0;
            }

            long capacity = (long)FreeSlots * stackLimit;
            if (stacks.TryGetValue(definitionId, out InventoryStackState stack))
            {
                if (stack.StackLimit != stackLimit)
                {
                    return 0;
                }

                capacity += stack.RemainingCapacityInOccupiedSlots;
            }

            return capacity >= int.MaxValue ? int.MaxValue : (int)capacity;
        }

        public InventoryOperationResult CanApplyStackChanges(
            IReadOnlyList<InventoryStackChange> changes,
            long expectedRevision = AnyRevision)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return InventoryOperationResult.StaleRevision;
            }

            return EvaluateStackChanges(changes, apply: false);
        }

        public InventoryOperationResult TryApplyStackChanges(
            IReadOnlyList<InventoryStackChange> changes,
            long expectedRevision = AnyRevision)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return InventoryOperationResult.StaleRevision;
            }

            return EvaluateStackChanges(changes, apply: true);
        }

        public InventoryOperationResult TryAddStack(
            DefinitionId definitionId,
            int quantity,
            int stackLimit,
            long expectedRevision = AnyRevision)
        {
            if (quantity <= 0)
            {
                return InventoryOperationResult.InvalidQuantity;
            }

            return TryApplyStackChanges(
                new[] { new InventoryStackChange(definitionId, quantity, stackLimit) },
                expectedRevision);
        }

        public InventoryOperationResult TryRemoveStack(
            DefinitionId definitionId,
            int quantity,
            int stackLimit,
            long expectedRevision = AnyRevision)
        {
            if (quantity <= 0)
            {
                return InventoryOperationResult.InvalidQuantity;
            }

            return TryApplyStackChanges(
                new[] { new InventoryStackChange(definitionId, -quantity, stackLimit) },
                expectedRevision);
        }

        internal InventoryOperationResult TryAddItemInstance(
            PersistentEntityId instanceId,
            long expectedRevision = AnyRevision)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return InventoryOperationResult.StaleRevision;
            }

            if (!instanceId.IsValid)
            {
                return InventoryOperationResult.InvalidItemInstance;
            }

            if (itemInstanceIds.Contains(instanceId))
            {
                return InventoryOperationResult.DuplicateItemInstance;
            }

            if (UsedSlots >= SlotCapacity)
            {
                return InventoryOperationResult.InsufficientCapacity;
            }

            IncrementRevision();
            itemInstanceIds.Add(instanceId);
            return InventoryOperationResult.Succeeded;
        }

        internal InventoryOperationResult TryRemoveItemInstance(
            PersistentEntityId instanceId,
            long expectedRevision = AnyRevision)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return InventoryOperationResult.StaleRevision;
            }

            if (!instanceId.IsValid)
            {
                return InventoryOperationResult.InvalidItemInstance;
            }

            if (!itemInstanceIds.Contains(instanceId))
            {
                return InventoryOperationResult.MissingItemInstance;
            }

            IncrementRevision();
            itemInstanceIds.Remove(instanceId);
            return InventoryOperationResult.Succeeded;
        }

        public InventoryOperationResult TrySetSlotCapacity(
            int slotCapacity,
            long expectedRevision = AnyRevision)
        {
            if (!MatchesRevision(expectedRevision))
            {
                return InventoryOperationResult.StaleRevision;
            }

            if (slotCapacity < 1 || slotCapacity < UsedSlots)
            {
                return InventoryOperationResult.InsufficientCapacity;
            }

            if (slotCapacity == SlotCapacity)
            {
                return InventoryOperationResult.Succeeded;
            }

            IncrementRevision();
            SlotCapacity = slotCapacity;
            return InventoryOperationResult.Succeeded;
        }

        internal InventoryContainerState Clone()
        {
            Dictionary<DefinitionId, InventoryStackState> clonedStacks = new(stacks.Count);
            foreach (KeyValuePair<DefinitionId, InventoryStackState> pair in stacks)
            {
                clonedStacks.Add(pair.Key, pair.Value);
            }

            return new InventoryContainerState(
                ContainerId,
                SlotCapacity,
                Revision,
                clonedStacks,
                new HashSet<PersistentEntityId>(itemInstanceIds));
        }

        internal void ReplaceWith(InventoryContainerState replacement)
        {
            if (replacement == null || replacement.ContainerId != ContainerId)
            {
                throw new InvalidOperationException("Inventory state replacement must target the same container.");
            }

            stacks.Clear();
            foreach (KeyValuePair<DefinitionId, InventoryStackState> pair in replacement.stacks)
            {
                stacks.Add(pair.Key, pair.Value);
            }

            itemInstanceIds.Clear();
            itemInstanceIds.UnionWith(replacement.itemInstanceIds);
            SlotCapacity = replacement.SlotCapacity;
            Revision = replacement.Revision;
        }

        InventoryOperationResult EvaluateStackChanges(
            IReadOnlyList<InventoryStackChange> changes,
            bool apply)
        {
            if (changes == null || changes.Count == 0)
            {
                return InventoryOperationResult.InvalidQuantity;
            }

            Dictionary<DefinitionId, ProjectedStackChange> projectedChanges = new();
            for (int i = 0; i < changes.Count; i++)
            {
                InventoryStackChange change = changes[i];
                if (!change.DefinitionId.IsValid)
                {
                    return InventoryOperationResult.InvalidDefinition;
                }

                if (change.QuantityDelta == 0)
                {
                    return InventoryOperationResult.InvalidQuantity;
                }

                if (change.StackLimit < 1)
                {
                    return InventoryOperationResult.InvalidStackLimit;
                }

                if (projectedChanges.TryGetValue(
                        change.DefinitionId,
                        out ProjectedStackChange projected))
                {
                    if (projected.StackLimit != change.StackLimit)
                    {
                        return InventoryOperationResult.DefinitionPolicyMismatch;
                    }

                    projected.QuantityDelta += change.QuantityDelta;
                    projectedChanges[change.DefinitionId] = projected;
                }
                else
                {
                    projectedChanges.Add(
                        change.DefinitionId,
                        new ProjectedStackChange(change.QuantityDelta, change.StackLimit));
                }
            }

            List<DefinitionId> cancelledChanges = null;
            foreach (KeyValuePair<DefinitionId, ProjectedStackChange> pair in projectedChanges)
            {
                if (pair.Value.QuantityDelta != 0L)
                {
                    continue;
                }

                cancelledChanges ??= new List<DefinitionId>();
                cancelledChanges.Add(pair.Key);
            }

            if (cancelledChanges != null)
            {
                for (int i = 0; i < cancelledChanges.Count; i++)
                {
                    projectedChanges.Remove(cancelledChanges[i]);
                }
            }

            if (projectedChanges.Count == 0)
            {
                return InventoryOperationResult.Succeeded;
            }

            Dictionary<DefinitionId, InventoryStackState> candidate = new(stacks.Count);
            foreach (KeyValuePair<DefinitionId, InventoryStackState> pair in stacks)
            {
                candidate.Add(pair.Key, pair.Value);
            }

            foreach (KeyValuePair<DefinitionId, ProjectedStackChange> pair in projectedChanges)
            {
                bool hasCurrent = candidate.TryGetValue(
                    pair.Key,
                    out InventoryStackState current);
                if (hasCurrent && current.StackLimit != pair.Value.StackLimit)
                {
                    return InventoryOperationResult.DefinitionPolicyMismatch;
                }

                long currentQuantity = hasCurrent ? current.Quantity : 0;
                long nextQuantity = currentQuantity + pair.Value.QuantityDelta;
                if (nextQuantity < 0)
                {
                    return InventoryOperationResult.InsufficientQuantity;
                }

                if (nextQuantity > int.MaxValue)
                {
                    return InventoryOperationResult.InsufficientCapacity;
                }

                if (nextQuantity == 0)
                {
                    candidate.Remove(pair.Key);
                }
                else if (!hasCurrent)
                {
                    candidate.Add(
                        pair.Key,
                        new InventoryStackState(pair.Key, (int)nextQuantity, pair.Value.StackLimit));
                }
                else
                {
                    candidate[pair.Key] = current.WithQuantity((int)nextQuantity);
                }
            }

            if (CountUsedStackSlots(candidate) + itemInstanceIds.Count > SlotCapacity)
            {
                return InventoryOperationResult.InsufficientCapacity;
            }

            if (!apply)
            {
                return InventoryOperationResult.Succeeded;
            }

            IncrementRevision();
            stacks.Clear();
            foreach (KeyValuePair<DefinitionId, InventoryStackState> pair in candidate)
            {
                stacks.Add(pair.Key, pair.Value);
            }

            return InventoryOperationResult.Succeeded;
        }

        bool HasPartialStack()
        {
            foreach (InventoryStackState stack in stacks.Values)
            {
                if (stack.RemainingCapacityInOccupiedSlots > 0)
                {
                    return true;
                }
            }

            return false;
        }

        static long CountUsedStackSlots(
            IReadOnlyDictionary<DefinitionId, InventoryStackState> source)
        {
            long usedSlots = 0L;
            foreach (InventoryStackState stack in source.Values)
            {
                usedSlots += stack.OccupiedSlots;
            }

            return usedSlots;
        }

        void IncrementRevision()
        {
            if (Revision == long.MaxValue)
            {
                throw new InvalidOperationException("Inventory revision capacity was exhausted.");
            }

            Revision++;
        }

        struct ProjectedStackChange
        {
            public ProjectedStackChange(long quantityDelta, int stackLimit)
            {
                QuantityDelta = quantityDelta;
                StackLimit = stackLimit;
            }

            public long QuantityDelta;
            public int StackLimit;
        }
    }
}
