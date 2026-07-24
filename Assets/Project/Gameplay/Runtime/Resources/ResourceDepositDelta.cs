using System;
using Farion.Simulation.World.Identity;

namespace Farion.Gameplay.Resources
{
    public readonly struct ResourceDepositDelta : IEquatable<ResourceDepositDelta>
    {
        public ResourceDepositDelta(GeneratedEntityId depositId, int extractedAmount)
        {
            if (!depositId.IsValid)
            {
                throw new ArgumentException("Resource deposit deltas require a valid deposit id.", nameof(depositId));
            }

            DepositId = depositId;
            ExtractedAmount = Math.Max(0, extractedAmount);
        }

        public GeneratedEntityId DepositId { get; }
        public int ExtractedAmount { get; }
        public bool IsEmpty => ExtractedAmount <= 0;

        public bool Equals(ResourceDepositDelta other)
        {
            return DepositId.Equals(other.DepositId) && ExtractedAmount == other.ExtractedAmount;
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceDepositDelta other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (DepositId.GetHashCode() * 397) ^ ExtractedAmount;
            }
        }
    }
}
