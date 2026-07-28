using Farion.Gameplay.Domain.Fleet;

namespace Farion.App.Commands
{
    public readonly struct PersonalShipCommandResult
    {
        public PersonalShipCommandResult(
            PersonalShipCommandStatus status,
            FleetOperationResult operation,
            double appliedAmount,
            long revision)
        {
            Status = status;
            Operation = operation;
            AppliedAmount = appliedAmount;
            Revision = revision;
        }

        public PersonalShipCommandStatus Status { get; }
        public FleetOperationResult Operation { get; }
        public double AppliedAmount { get; }
        public long Revision { get; }
        public bool Succeeded =>
            Status == PersonalShipCommandStatus.Succeeded &&
            Operation == FleetOperationResult.Succeeded;
    }
}
