using System;
using Farion.Gameplay.Domain.Identity;

namespace Farion.Gameplay.Domain.Economy
{
    public readonly struct EquipmentLocation : IEquatable<EquipmentLocation>
    {
        EquipmentLocation(
            EquipmentLocationKind kind,
            PersistentEntityId ownerId,
            DefinitionId slotId)
        {
            Kind = kind;
            OwnerId = ownerId;
            SlotId = slotId;
        }

        public EquipmentLocationKind Kind { get; }
        public PersistentEntityId OwnerId { get; }
        public DefinitionId SlotId { get; }

        public bool IsValid => Kind switch
        {
            EquipmentLocationKind.Unassigned =>
                !OwnerId.IsValid && !SlotId.IsValid,
            EquipmentLocationKind.Container =>
                OwnerId.IsValid && !SlotId.IsValid,
            EquipmentLocationKind.PersonalShipSlot =>
                OwnerId.IsValid && SlotId.IsValid,
            _ => false
        };

        public static EquipmentLocation Unassigned => default;

        public static EquipmentLocation InContainer(PersistentEntityId containerId)
        {
            if (!containerId.IsValid)
            {
                throw new ArgumentException(
                    "Equipment container locations require a valid container id.",
                    nameof(containerId));
            }

            return new EquipmentLocation(
                EquipmentLocationKind.Container,
                containerId,
                default);
        }

        public static EquipmentLocation InPersonalShipSlot(
            PersistentEntityId shipId,
            DefinitionId slotId)
        {
            if (!shipId.IsValid || !slotId.IsValid)
            {
                throw new ArgumentException(
                    "Installed equipment locations require valid ship and slot ids.");
            }

            return new EquipmentLocation(
                EquipmentLocationKind.PersonalShipSlot,
                shipId,
                slotId);
        }

        public static bool operator ==(EquipmentLocation left, EquipmentLocation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(EquipmentLocation left, EquipmentLocation right)
        {
            return !left.Equals(right);
        }

        public bool Equals(EquipmentLocation other)
        {
            return Kind == other.Kind &&
                   OwnerId == other.OwnerId &&
                   SlotId == other.SlotId;
        }

        public override bool Equals(object obj)
        {
            return obj is EquipmentLocation other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ OwnerId.GetHashCode();
                hash = (hash * 397) ^ SlotId.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return Kind switch
            {
                EquipmentLocationKind.Container => $"Container:{OwnerId}",
                EquipmentLocationKind.PersonalShipSlot =>
                    $"PersonalShip:{OwnerId}/{SlotId}",
                _ => "Unassigned"
            };
        }
    }
}
