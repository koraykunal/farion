using Farion.Gameplay.Domain.Fleet;
using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Session;
using Farion.Gameplay.Ships;

namespace Farion.App.Commands.Handlers
{
    internal sealed class PersonalShipCommandHandler
    {
        readonly GameplaySessionRuntime session;

        public PersonalShipCommandHandler(GameplaySessionRuntime session)
        {
            this.session = session;
        }

        public PersonalShipCommandResult Rename(
            PersistentEntityId shipId,
            string displayName,
            long expectedRevision)
        {
            if (!TryResolve(
                    shipId,
                    out PersonalShipRuntimeBinding binding,
                    out PersonalShipCommandResult failure))
            {
                return failure;
            }

            return BuildResult(
                binding,
                binding.TryRename(displayName, expectedRevision),
                0d);
        }

        public PersonalShipCommandResult Refuel(
            PersistentEntityId shipId,
            double amount,
            long expectedRevision)
        {
            if (!TryResolve(
                    shipId,
                    out PersonalShipRuntimeBinding binding,
                    out PersonalShipCommandResult failure))
            {
                return failure;
            }

            FleetOperationResult operation =
                binding.TryRefuel(amount, out double accepted, expectedRevision);
            return BuildResult(binding, operation, accepted);
        }

        public PersonalShipCommandResult Repair(
            PersistentEntityId shipId,
            double amount,
            long expectedRevision)
        {
            if (!TryResolve(
                    shipId,
                    out PersonalShipRuntimeBinding binding,
                    out PersonalShipCommandResult failure))
            {
                return failure;
            }

            FleetOperationResult operation =
                binding.TryRepair(amount, out double accepted, expectedRevision);
            return BuildResult(binding, operation, accepted);
        }

        public PersonalShipCommandResult ApplyDamage(
            PersistentEntityId shipId,
            double amount,
            long expectedRevision)
        {
            if (!TryResolve(
                    shipId,
                    out PersonalShipRuntimeBinding binding,
                    out PersonalShipCommandResult failure))
            {
                return failure;
            }

            FleetOperationResult operation =
                binding.TryApplyDamage(
                    amount,
                    out double applied,
                    expectedRevision);
            return BuildResult(binding, operation, applied);
        }

        public PersonalShipCommandResult ConsumeFuel(
            PersistentEntityId shipId,
            double amount,
            long expectedRevision)
        {
            if (!TryResolve(
                    shipId,
                    out PersonalShipRuntimeBinding binding,
                    out PersonalShipCommandResult failure))
            {
                return failure;
            }

            FleetOperationResult operation =
                binding.TryConsumeFuel(amount, expectedRevision);
            return BuildResult(
                binding,
                operation,
                operation == FleetOperationResult.Succeeded ? amount : 0d);
        }

        bool TryResolve(
            PersistentEntityId shipId,
            out PersonalShipRuntimeBinding binding,
            out PersonalShipCommandResult failure)
        {
            binding = null;
            if (session == null)
            {
                failure = Failure(
                    PersonalShipCommandStatus.MissingRuntime,
                    FleetOperationResult.NotFound);
                return false;
            }

            if (!shipId.IsValid)
            {
                failure = Failure(
                    PersonalShipCommandStatus.InvalidShip,
                    FleetOperationResult.InvalidIdentifier);
                return false;
            }

            if (shipId != session.Identity.PersonalShipId)
            {
                failure = Failure(
                    PersonalShipCommandStatus.UnauthorizedShip,
                    FleetOperationResult.Conflict,
                    session.PersonalShip?.Revision ?? 0L);
                return false;
            }

            binding = session.PersonalShipBinding;
            if (binding == null ||
                binding.State == null ||
                binding.State.OwnerPlayerId != session.Identity.LocalPlayerId ||
                binding.State.ShipId != shipId)
            {
                failure = Failure(
                    PersonalShipCommandStatus.MissingRuntime,
                    FleetOperationResult.NotFound);
                binding = null;
                return false;
            }

            failure = default;
            return true;
        }

        static PersonalShipCommandResult Failure(
            PersonalShipCommandStatus status,
            FleetOperationResult operation,
            long revision = 0L)
        {
            return new PersonalShipCommandResult(
                status,
                operation,
                0d,
                revision);
        }

        static PersonalShipCommandResult BuildResult(
            PersonalShipRuntimeBinding binding,
            FleetOperationResult operation,
            double appliedAmount)
        {
            return new PersonalShipCommandResult(
                operation == FleetOperationResult.Succeeded
                    ? PersonalShipCommandStatus.Succeeded
                    : PersonalShipCommandStatus.Rejected,
                operation,
                appliedAmount,
                binding.State?.Revision ?? 0L);
        }
    }
}
