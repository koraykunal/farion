using Farion.Core.Identity;
using Farion.Gameplay.Domain.Identity;

namespace Farion.Gameplay.Session
{
    public readonly struct GameplaySessionIdentity
    {
        GameplaySessionIdentity(
            PersistentEntityId localPlayerId,
            PersistentEntityId explorerActorId,
            PersistentEntityId fleetId,
            PersistentEntityId assignedShuttleId,
            PersistentEntityId carriedInventoryId)
        {
            LocalPlayerId = localPlayerId;
            ExplorerActorId = explorerActorId;
            FleetId = fleetId;
            AssignedShuttleId = assignedShuttleId;
            CarriedInventoryId = carriedInventoryId;
        }

        public PersistentEntityId LocalPlayerId { get; }
        public PersistentEntityId ExplorerActorId { get; }
        public PersistentEntityId FleetId { get; }
        public PersistentEntityId AssignedShuttleId { get; }
        public PersistentEntityId CarriedInventoryId { get; }
        public bool IsValid =>
            LocalPlayerId.IsValid &&
            ExplorerActorId.IsValid &&
            FleetId.IsValid &&
            AssignedShuttleId.IsValid &&
            CarriedInventoryId.IsValid;

        public static bool TryCreate(
            string localPlayerId,
            string explorerActorId,
            string fleetId,
            string assignedShuttleId,
            PersistentEntityId carriedInventoryId,
            out GameplaySessionIdentity identity)
        {
            identity = default;
            if (!PersistentEntityId.TryCreate(
                    localPlayerId,
                    out PersistentEntityId resolvedPlayerId) ||
                !PersistentEntityId.TryCreate(
                    explorerActorId,
                    out PersistentEntityId resolvedExplorerId) ||
                !PersistentEntityId.TryCreate(
                    fleetId,
                    out PersistentEntityId resolvedFleetId) ||
                !PersistentEntityId.TryCreate(
                    assignedShuttleId,
                    out PersistentEntityId resolvedShipId) ||
                !carriedInventoryId.IsValid)
            {
                return false;
            }

            identity = new GameplaySessionIdentity(
                resolvedPlayerId,
                resolvedExplorerId,
                resolvedFleetId,
                resolvedShipId,
                carriedInventoryId);
            return true;
        }
    }
}
