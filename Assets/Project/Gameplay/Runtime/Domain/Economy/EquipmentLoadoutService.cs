using Farion.Gameplay.Domain.Fleet;
using Farion.Gameplay.Domain.Identity;

namespace Farion.Gameplay.Domain.Economy
{
    public static class EquipmentLoadoutService
    {
        public static EquipmentLoadoutResult TryInstallFromContainer(
            EquipmentRepositoryState repository,
            InventoryContainerState source,
            PersonalShipState ship,
            IEquipmentInstallationPolicy installationPolicy,
            DefinitionId slotId,
            PersistentEntityId equipmentInstanceId,
            out PersistentEntityId displacedInstanceId,
            long expectedRepositoryRevision = InventoryContainerState.AnyRevision,
            long expectedSourceRevision = InventoryContainerState.AnyRevision,
            long expectedShipRevision = InventoryContainerState.AnyRevision)
        {
            displacedInstanceId = PersistentEntityId.None;
            EquipmentLoadoutResult validationResult = ValidateCommon(
                repository,
                source,
                ship,
                installationPolicy,
                slotId,
                equipmentInstanceId,
                expectedRepositoryRevision,
                expectedSourceRevision,
                expectedShipRevision);
            if (validationResult != EquipmentLoadoutResult.Succeeded)
            {
                return validationResult;
            }

            if (!repository.TryGet(
                    equipmentInstanceId,
                    out EquipmentInstanceState equipment))
            {
                return EquipmentLoadoutResult.EquipmentNotRegistered;
            }

            EquipmentLocation sourceLocation =
                EquipmentLocation.InContainer(source.ContainerId);
            if (!repository.TryGetLocation(
                    equipmentInstanceId,
                    out EquipmentLocation equipmentLocation))
            {
                return EquipmentLoadoutResult.EquipmentNotRegistered;
            }

            if (equipmentLocation != sourceLocation)
            {
                return EquipmentLoadoutResult.EquipmentNotStored;
            }

            if (!source.ContainsItemInstance(equipmentInstanceId))
            {
                return EquipmentLoadoutResult.ConflictingOwnership;
            }

            if (ship.IsEquipmentInstalled(equipmentInstanceId))
            {
                return EquipmentLoadoutResult.EquipmentAlreadyInstalled;
            }

            if (!installationPolicy.CanInstall(equipment.DefinitionId, slotId))
            {
                return EquipmentLoadoutResult.IncompatibleEquipment;
            }

            EquipmentLocation slotLocation =
                EquipmentLocation.InPersonalShipSlot(ship.ShipId, slotId);
            if (ship.TryGetInstalledModule(slotId, out PersistentEntityId existing))
            {
                if (!repository.TryGetLocation(
                        existing,
                        out EquipmentLocation existingLocation))
                {
                    return EquipmentLoadoutResult.EquipmentNotRegistered;
                }

                if (existingLocation != slotLocation)
                {
                    return EquipmentLoadoutResult.ConflictingOwnership;
                }
            }

            long repositoryRevision = repository.Revision;
            long sourceRevision = source.Revision;
            long shipRevision = ship.Revision;
            EquipmentRepositoryState repositoryCandidate = repository.Clone();
            InventoryContainerState sourceCandidate = source.Clone();
            PersonalShipState shipCandidate = ship.Clone();

            InventoryOperationResult removeResult =
                sourceCandidate.TryRemoveItemInstance(equipmentInstanceId);
            if (removeResult != InventoryOperationResult.Succeeded)
            {
                return MapInventoryResult(removeResult);
            }

            FleetOperationResult installResult = shipCandidate.TryInstallModule(
                slotId,
                equipmentInstanceId,
                out displacedInstanceId);
            if (installResult != FleetOperationResult.Succeeded)
            {
                displacedInstanceId = PersistentEntityId.None;
                return MapFleetResult(installResult);
            }

            EquipmentRepositoryResult equipmentMoveResult =
                repositoryCandidate.TryMove(
                    equipmentInstanceId,
                    sourceLocation,
                    slotLocation);
            if (equipmentMoveResult != EquipmentRepositoryResult.Succeeded)
            {
                displacedInstanceId = PersistentEntityId.None;
                return MapRepositoryResult(equipmentMoveResult);
            }

            if (displacedInstanceId.IsValid)
            {
                InventoryOperationResult storeResult =
                    sourceCandidate.TryAddItemInstance(displacedInstanceId);
                if (storeResult != InventoryOperationResult.Succeeded)
                {
                    displacedInstanceId = PersistentEntityId.None;
                    return MapInventoryResult(storeResult);
                }

                EquipmentRepositoryResult displacedMoveResult =
                    repositoryCandidate.TryMove(
                        displacedInstanceId,
                        slotLocation,
                        sourceLocation);
                if (displacedMoveResult != EquipmentRepositoryResult.Succeeded)
                {
                    displacedInstanceId = PersistentEntityId.None;
                    return MapRepositoryResult(displacedMoveResult);
                }
            }

            if (repository.Revision != repositoryRevision ||
                source.Revision != sourceRevision ||
                ship.Revision != shipRevision)
            {
                displacedInstanceId = PersistentEntityId.None;
                return EquipmentLoadoutResult.StaleRevision;
            }

            repository.ReplaceWith(repositoryCandidate);
            source.ReplaceWith(sourceCandidate);
            ship.ReplaceWith(shipCandidate);
            return EquipmentLoadoutResult.Succeeded;
        }

        public static EquipmentLoadoutResult TryUninstallToContainer(
            EquipmentRepositoryState repository,
            PersonalShipState ship,
            InventoryContainerState destination,
            DefinitionId slotId,
            out PersistentEntityId removedInstanceId,
            long expectedRepositoryRevision = InventoryContainerState.AnyRevision,
            long expectedShipRevision = InventoryContainerState.AnyRevision,
            long expectedDestinationRevision = InventoryContainerState.AnyRevision)
        {
            removedInstanceId = PersistentEntityId.None;
            if (repository == null)
            {
                return EquipmentLoadoutResult.InvalidRepository;
            }

            if (ship == null)
            {
                return EquipmentLoadoutResult.InvalidShip;
            }

            if (destination == null)
            {
                return EquipmentLoadoutResult.InvalidContainer;
            }

            if (!slotId.IsValid)
            {
                return EquipmentLoadoutResult.InvalidIdentifier;
            }

            if (!repository.MatchesRevision(expectedRepositoryRevision) ||
                !ship.MatchesRevision(expectedShipRevision) ||
                !destination.MatchesRevision(expectedDestinationRevision))
            {
                return EquipmentLoadoutResult.StaleRevision;
            }

            if (!ship.TryGetInstalledModule(slotId, out PersistentEntityId installedId))
            {
                return EquipmentLoadoutResult.SlotEmpty;
            }

            EquipmentLocation slotLocation =
                EquipmentLocation.InPersonalShipSlot(ship.ShipId, slotId);
            if (!repository.TryGetLocation(
                    installedId,
                    out EquipmentLocation currentLocation))
            {
                return EquipmentLoadoutResult.EquipmentNotRegistered;
            }

            if (currentLocation != slotLocation ||
                destination.ContainsItemInstance(installedId))
            {
                return EquipmentLoadoutResult.ConflictingOwnership;
            }

            long repositoryRevision = repository.Revision;
            long shipRevision = ship.Revision;
            long destinationRevision = destination.Revision;
            EquipmentRepositoryState repositoryCandidate = repository.Clone();
            PersonalShipState shipCandidate = ship.Clone();
            InventoryContainerState destinationCandidate = destination.Clone();

            FleetOperationResult uninstallResult =
                shipCandidate.TryUninstallModule(slotId, out removedInstanceId);
            if (uninstallResult != FleetOperationResult.Succeeded)
            {
                removedInstanceId = PersistentEntityId.None;
                return MapFleetResult(uninstallResult);
            }

            InventoryOperationResult addResult =
                destinationCandidate.TryAddItemInstance(removedInstanceId);
            if (addResult != InventoryOperationResult.Succeeded)
            {
                removedInstanceId = PersistentEntityId.None;
                return MapInventoryResult(addResult);
            }

            EquipmentRepositoryResult moveResult = repositoryCandidate.TryMove(
                removedInstanceId,
                slotLocation,
                EquipmentLocation.InContainer(destination.ContainerId));
            if (moveResult != EquipmentRepositoryResult.Succeeded)
            {
                removedInstanceId = PersistentEntityId.None;
                return MapRepositoryResult(moveResult);
            }

            if (repository.Revision != repositoryRevision ||
                ship.Revision != shipRevision ||
                destination.Revision != destinationRevision)
            {
                removedInstanceId = PersistentEntityId.None;
                return EquipmentLoadoutResult.StaleRevision;
            }

            repository.ReplaceWith(repositoryCandidate);
            ship.ReplaceWith(shipCandidate);
            destination.ReplaceWith(destinationCandidate);
            return EquipmentLoadoutResult.Succeeded;
        }

        static EquipmentLoadoutResult ValidateCommon(
            EquipmentRepositoryState repository,
            InventoryContainerState container,
            PersonalShipState ship,
            IEquipmentInstallationPolicy installationPolicy,
            DefinitionId slotId,
            PersistentEntityId equipmentInstanceId,
            long expectedRepositoryRevision,
            long expectedContainerRevision,
            long expectedShipRevision)
        {
            if (repository == null)
            {
                return EquipmentLoadoutResult.InvalidRepository;
            }

            if (container == null)
            {
                return EquipmentLoadoutResult.InvalidContainer;
            }

            if (ship == null)
            {
                return EquipmentLoadoutResult.InvalidShip;
            }

            if (installationPolicy == null)
            {
                return EquipmentLoadoutResult.InvalidPolicy;
            }

            if (!slotId.IsValid || !equipmentInstanceId.IsValid)
            {
                return EquipmentLoadoutResult.InvalidIdentifier;
            }

            return repository.MatchesRevision(expectedRepositoryRevision) &&
                   container.MatchesRevision(expectedContainerRevision) &&
                   ship.MatchesRevision(expectedShipRevision)
                ? EquipmentLoadoutResult.Succeeded
                : EquipmentLoadoutResult.StaleRevision;
        }

        static EquipmentLoadoutResult MapInventoryResult(
            InventoryOperationResult result)
        {
            return result switch
            {
                InventoryOperationResult.InsufficientCapacity =>
                    EquipmentLoadoutResult.InsufficientCapacity,
                InventoryOperationResult.MissingItemInstance =>
                    EquipmentLoadoutResult.EquipmentNotStored,
                InventoryOperationResult.DuplicateItemInstance =>
                    EquipmentLoadoutResult.ConflictingOwnership,
                InventoryOperationResult.StaleRevision =>
                    EquipmentLoadoutResult.StaleRevision,
                _ => EquipmentLoadoutResult.InvalidContainer
            };
        }

        static EquipmentLoadoutResult MapFleetResult(FleetOperationResult result)
        {
            return result switch
            {
                FleetOperationResult.StaleRevision =>
                    EquipmentLoadoutResult.StaleRevision,
                FleetOperationResult.Conflict =>
                    EquipmentLoadoutResult.ConflictingOwnership,
                FleetOperationResult.NotFound =>
                    EquipmentLoadoutResult.SlotEmpty,
                _ => EquipmentLoadoutResult.InvalidShip
            };
        }

        static EquipmentLoadoutResult MapRepositoryResult(
            EquipmentRepositoryResult result)
        {
            return result switch
            {
                EquipmentRepositoryResult.StaleRevision =>
                    EquipmentLoadoutResult.StaleRevision,
                EquipmentRepositoryResult.OwnershipConflict =>
                    EquipmentLoadoutResult.ConflictingOwnership,
                _ => EquipmentLoadoutResult.EquipmentNotRegistered
            };
        }
    }
}
