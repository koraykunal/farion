using System.Collections.Generic;

namespace Farion.Gameplay.Domain.Economy
{
    public static class InventoryTransferService
    {
        public static InventoryOperationResult CanTransferAllStacks(
            InventoryContainerState source,
            InventoryContainerState destination,
            long expectedSourceRevision = InventoryContainerState.AnyRevision,
            long expectedDestinationRevision = InventoryContainerState.AnyRevision)
        {
            return EvaluateTransfer(
                source,
                destination,
                expectedSourceRevision,
                expectedDestinationRevision,
                apply: false);
        }

        public static InventoryOperationResult TryTransferAllStacks(
            InventoryContainerState source,
            InventoryContainerState destination,
            long expectedSourceRevision = InventoryContainerState.AnyRevision,
            long expectedDestinationRevision = InventoryContainerState.AnyRevision)
        {
            return EvaluateTransfer(
                source,
                destination,
                expectedSourceRevision,
                expectedDestinationRevision,
                apply: true);
        }

        static InventoryOperationResult EvaluateTransfer(
            InventoryContainerState source,
            InventoryContainerState destination,
            long expectedSourceRevision,
            long expectedDestinationRevision,
            bool apply)
        {
            if (source == null || destination == null ||
                source.ContainerId == destination.ContainerId)
            {
                return InventoryOperationResult.InvalidContainer;
            }

            if (!source.MatchesRevision(expectedSourceRevision) ||
                !destination.MatchesRevision(expectedDestinationRevision))
            {
                return InventoryOperationResult.StaleRevision;
            }

            long sourceRevision = source.Revision;
            long destinationRevision = destination.Revision;
            List<InventoryStackChange> removals = new();
            List<InventoryStackChange> additions = new();
            foreach (InventoryStackState stack in source.Stacks)
            {
                removals.Add(
                    new InventoryStackChange(
                        stack.DefinitionId,
                        -stack.Quantity,
                        stack.StackLimit));
                additions.Add(
                    new InventoryStackChange(
                        stack.DefinitionId,
                        stack.Quantity,
                        stack.StackLimit));
            }

            if (removals.Count == 0)
            {
                return InventoryOperationResult.EmptyContainer;
            }

            InventoryContainerState sourceCandidate = source.Clone();
            InventoryContainerState destinationCandidate = destination.Clone();
            InventoryOperationResult sourceResult =
                sourceCandidate.TryApplyStackChanges(removals);
            if (sourceResult != InventoryOperationResult.Succeeded)
            {
                return sourceResult;
            }

            InventoryOperationResult destinationResult =
                destinationCandidate.TryApplyStackChanges(additions);
            if (destinationResult != InventoryOperationResult.Succeeded)
            {
                return destinationResult;
            }

            if (!apply)
            {
                return InventoryOperationResult.Succeeded;
            }

            if (source.Revision != sourceRevision ||
                destination.Revision != destinationRevision)
            {
                return InventoryOperationResult.StaleRevision;
            }

            source.ReplaceWith(sourceCandidate);
            destination.ReplaceWith(destinationCandidate);
            return InventoryOperationResult.Succeeded;
        }
    }
}
