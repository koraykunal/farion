using Farion.Core.Identity;
using Farion.Gameplay.Domain.Identity;
using Farion.Simulation.World;

namespace Farion.Gameplay.Commands
{
    public readonly struct ResourceHarvestRequest
    {
        public ResourceHarvestRequest(
            GeneratedEntityId depositId,
            PersistentEntityId destinationContainerId,
            long expectedDestinationRevision)
        {
            DepositId = depositId;
            DestinationContainerId = destinationContainerId;
            ExpectedDestinationRevision = expectedDestinationRevision;
        }

        public GeneratedEntityId DepositId { get; }
        public PersistentEntityId DestinationContainerId { get; }
        public long ExpectedDestinationRevision { get; }
        public bool IsAddressed => DepositId.IsValid && DestinationContainerId.IsValid;
    }

    public readonly struct CargoTransferRequest
    {
        public CargoTransferRequest(
            PersistentEntityId shuttleCargoId,
            long expectedShuttleCargoRevision)
        {
            ShuttleCargoId = shuttleCargoId;
            ExpectedShuttleCargoRevision = expectedShuttleCargoRevision;
        }

        public PersistentEntityId ShuttleCargoId { get; }
        public long ExpectedShuttleCargoRevision { get; }
        public bool IsAddressed => ShuttleCargoId.IsValid;
    }

    public readonly struct FleetProcessingRequest
    {
        public FleetProcessingRequest(
            DefinitionId recipeId,
            PersistentEntityId fleetStorageId,
            long expectedFleetStorageRevision)
        {
            RecipeId = recipeId;
            FleetStorageId = fleetStorageId;
            ExpectedFleetStorageRevision = expectedFleetStorageRevision;
        }

        public DefinitionId RecipeId { get; }
        public PersistentEntityId FleetStorageId { get; }
        public long ExpectedFleetStorageRevision { get; }
        public bool IsAddressed => RecipeId.IsValid && FleetStorageId.IsValid;
    }
}
