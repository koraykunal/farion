using Farion.Gameplay.Domain.Identity;

namespace Farion.Gameplay.Domain.Economy
{
    public static class InventoryTransferService
    {
        public static InventoryOperationResult TryTransferStack(
            InventoryContainerState source,
            InventoryContainerState destination,
            DefinitionId definitionId,
            int quantity,
            int stackLimit,
            long expectedSourceRevision = InventoryContainerState.AnyRevision,
            long expectedDestinationRevision = InventoryContainerState.AnyRevision)
        {
            if (!CanUseContainers(source, destination))
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
            InventoryContainerState sourceCandidate = source.Clone();
            InventoryContainerState destinationCandidate = destination.Clone();
            InventoryOperationResult removeResult = sourceCandidate.TryRemoveStack(
                definitionId,
                quantity,
                stackLimit);
            if (removeResult != InventoryOperationResult.Succeeded)
            {
                return removeResult;
            }

            InventoryOperationResult addResult = destinationCandidate.TryAddStack(
                definitionId,
                quantity,
                stackLimit);
            if (addResult != InventoryOperationResult.Succeeded)
            {
                return addResult;
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

        public static InventoryOperationResult TryTransferItemInstance(
            EquipmentRepositoryState repository,
            InventoryContainerState source,
            InventoryContainerState destination,
            PersistentEntityId itemInstanceId,
            long expectedRepositoryRevision = InventoryContainerState.AnyRevision,
            long expectedSourceRevision = InventoryContainerState.AnyRevision,
            long expectedDestinationRevision = InventoryContainerState.AnyRevision)
        {
            if (repository == null)
            {
                return InventoryOperationResult.InvalidRepository;
            }

            if (!CanUseContainers(source, destination))
            {
                return InventoryOperationResult.InvalidContainer;
            }

            if (!repository.MatchesRevision(expectedRepositoryRevision) ||
                !source.MatchesRevision(expectedSourceRevision) ||
                !destination.MatchesRevision(expectedDestinationRevision))
            {
                return InventoryOperationResult.StaleRevision;
            }

            if (!repository.TryGetLocation(
                    itemInstanceId,
                    out EquipmentLocation location))
            {
                return InventoryOperationResult.EquipmentNotRegistered;
            }

            EquipmentLocation sourceLocation =
                EquipmentLocation.InContainer(source.ContainerId);
            if (location != sourceLocation ||
                !source.ContainsItemInstance(itemInstanceId) ||
                destination.ContainsItemInstance(itemInstanceId))
            {
                return InventoryOperationResult.OwnershipConflict;
            }

            long repositoryRevision = repository.Revision;
            long sourceRevision = source.Revision;
            long destinationRevision = destination.Revision;
            EquipmentRepositoryState repositoryCandidate = repository.Clone();
            InventoryContainerState sourceCandidate = source.Clone();
            InventoryContainerState destinationCandidate = destination.Clone();
            InventoryOperationResult removeResult =
                sourceCandidate.TryRemoveItemInstance(itemInstanceId);
            if (removeResult != InventoryOperationResult.Succeeded)
            {
                return removeResult;
            }

            InventoryOperationResult addResult =
                destinationCandidate.TryAddItemInstance(itemInstanceId);
            if (addResult != InventoryOperationResult.Succeeded)
            {
                return addResult;
            }

            EquipmentRepositoryResult moveResult = repositoryCandidate.TryMove(
                itemInstanceId,
                sourceLocation,
                EquipmentLocation.InContainer(destination.ContainerId));
            if (moveResult != EquipmentRepositoryResult.Succeeded)
            {
                return moveResult == EquipmentRepositoryResult.StaleRevision
                    ? InventoryOperationResult.StaleRevision
                    : InventoryOperationResult.OwnershipConflict;
            }

            if (repository.Revision != repositoryRevision ||
                source.Revision != sourceRevision ||
                destination.Revision != destinationRevision)
            {
                return InventoryOperationResult.StaleRevision;
            }

            repository.ReplaceWith(repositoryCandidate);
            source.ReplaceWith(sourceCandidate);
            destination.ReplaceWith(destinationCandidate);
            return InventoryOperationResult.Succeeded;
        }

        static bool CanUseContainers(
            InventoryContainerState source,
            InventoryContainerState destination)
        {
            return source != null &&
                   destination != null &&
                   source.ContainerId != destination.ContainerId;
        }
    }
}
