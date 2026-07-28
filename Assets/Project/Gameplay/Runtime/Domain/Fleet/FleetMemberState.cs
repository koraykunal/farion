using Farion.Gameplay.Domain.Identity;

namespace Farion.Gameplay.Domain.Fleet
{
    public readonly struct FleetMemberState
    {
        public FleetMemberState(
            PersistentEntityId playerId,
            PersistentEntityId personalShipId)
        {
            PlayerId = playerId;
            PersonalShipId = personalShipId;
        }

        public PersistentEntityId PlayerId { get; }
        public PersistentEntityId PersonalShipId { get; }
        public bool IsValid => PlayerId.IsValid && PersonalShipId.IsValid;

        public FleetMemberState WithPersonalShip(PersistentEntityId personalShipId)
        {
            return new FleetMemberState(PlayerId, personalShipId);
        }
    }
}
