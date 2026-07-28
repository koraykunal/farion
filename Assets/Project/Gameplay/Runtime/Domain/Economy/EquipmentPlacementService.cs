using Farion.Gameplay.Domain.Identity;

namespace Farion.Gameplay.Domain.Economy
{
    public static class EquipmentPlacementService
    {
        public static EquipmentPlacementResult TryStoreUnassigned(
            EquipmentRepositoryState repository,
            InventoryContainerState destination,
            PersistentEntityId equipmentInstanceId,
            long expectedRepositoryRevision = InventoryContainerState.AnyRevision,
            long expectedDestinationRevision = InventoryContainerState.AnyRevision)
        {
            if (repository == null)
            {
                return EquipmentPlacementResult.InvalidRepository;
            }

            if (destination == null)
            {
                return EquipmentPlacementResult.InvalidContainer;
            }

            if (!equipmentInstanceId.IsValid)
            {
                return EquipmentPlacementResult.InvalidIdentifier;
            }

            if (!repository.MatchesRevision(expectedRepositoryRevision) ||
                !destination.MatchesRevision(expectedDestinationRevision))
            {
                return EquipmentPlacementResult.StaleRevision;
            }

            if (!repository.TryGetLocation(
                    equipmentInstanceId,
                    out EquipmentLocation currentLocation))
            {
                return EquipmentPlacementResult.EquipmentNotRegistered;
            }

            if (currentLocation != EquipmentLocation.Unassigned)
            {
                return EquipmentPlacementResult.EquipmentAlreadyAssigned;
            }

            long repositoryRevision = repository.Revision;
            long destinationRevision = destination.Revision;
            EquipmentRepositoryState repositoryCandidate = repository.Clone();
            InventoryContainerState destinationCandidate = destination.Clone();

            InventoryOperationResult storeResult =
                destinationCandidate.TryAddItemInstance(equipmentInstanceId);
            if (storeResult != InventoryOperationResult.Succeeded)
            {
                return storeResult == InventoryOperationResult.InsufficientCapacity
                    ? EquipmentPlacementResult.InsufficientCapacity
                    : EquipmentPlacementResult.InvalidContainer;
            }

            EquipmentRepositoryResult moveResult = repositoryCandidate.TryMove(
                equipmentInstanceId,
                EquipmentLocation.Unassigned,
                EquipmentLocation.InContainer(destination.ContainerId));
            if (moveResult != EquipmentRepositoryResult.Succeeded)
            {
                return moveResult == EquipmentRepositoryResult.StaleRevision
                    ? EquipmentPlacementResult.StaleRevision
                    : EquipmentPlacementResult.EquipmentAlreadyAssigned;
            }

            if (repository.Revision != repositoryRevision ||
                destination.Revision != destinationRevision)
            {
                return EquipmentPlacementResult.StaleRevision;
            }

            repository.ReplaceWith(repositoryCandidate);
            destination.ReplaceWith(destinationCandidate);
            return EquipmentPlacementResult.Succeeded;
        }
    }
}
