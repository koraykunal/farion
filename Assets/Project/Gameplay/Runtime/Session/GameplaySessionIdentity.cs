using Farion.Gameplay.Domain.Identity;

namespace Farion.Gameplay.Session
{
    public readonly struct GameplaySessionIdentity
    {
        GameplaySessionIdentity(
            PersistentEntityId localPlayerId,
            PersistentEntityId explorerActorId,
            PersistentEntityId assignedShuttleId,
            PersistentEntityId carriedInventoryId)
        {
            LocalPlayerId = localPlayerId;
            ExplorerActorId = explorerActorId;
            AssignedShuttleId = assignedShuttleId;
            CarriedInventoryId = carriedInventoryId;
        }

        public PersistentEntityId LocalPlayerId { get; }
        public PersistentEntityId ExplorerActorId { get; }
        public PersistentEntityId AssignedShuttleId { get; }
        public PersistentEntityId CarriedInventoryId { get; }
        public bool IsValid =>
            LocalPlayerId.IsValid &&
            ExplorerActorId.IsValid &&
            AssignedShuttleId.IsValid &&
            CarriedInventoryId.IsValid;

        public static bool TryCreate(
            string localPlayerId,
            string explorerActorId,
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
                    assignedShuttleId,
                    out PersistentEntityId resolvedShipId) ||
                !carriedInventoryId.IsValid)
            {
                return false;
            }

            identity = new GameplaySessionIdentity(
                resolvedPlayerId,
                resolvedExplorerId,
                resolvedShipId,
                carriedInventoryId);
            return true;
        }
    }
}
