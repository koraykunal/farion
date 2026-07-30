using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Ships;

namespace Farion.Gameplay.Session
{
    public sealed class GameplaySessionRuntime
    {
        GameplaySessionRuntime(
            GameplaySessionIdentity identity,
            GameplayRuntimeBindings bindings)
        {
            Identity = identity;
            Bindings = bindings;
        }

        public GameplaySessionIdentity Identity { get; }
        public GameplayRuntimeBindings Bindings { get; }
        public InventoryContainerComponent LocalInventory =>
            Bindings.LocalPlayerInventory;
        public PlayerPossessionController Possession => Bindings.Possession;
        public FleetKnowledgeRuntime FleetKnowledge => Bindings.FleetKnowledge;
        public ShuttleRuntimeBinding ShuttleBinding =>
            Bindings.AssignedShuttle;
        public static bool TryCreate(
            string localPlayerId,
            GameplayRuntimeBindings bindings,
            out GameplaySessionRuntime runtime)
        {
            runtime = null;
            if (bindings == null ||
                !bindings.IsValid ||
                !GameplaySessionIdentity.TryCreate(
                    localPlayerId,
                    bindings.Possession.ExplorerPersistentId,
                    bindings.AssignedShuttle.ShipId.Value,
                    bindings.LocalPlayerInventory.ContainerId,
                    out GameplaySessionIdentity identity))
            {
                return false;
            }

            runtime = new GameplaySessionRuntime(
                identity,
                bindings);
            return true;
        }

        public bool Matches(
            string localPlayerId,
            GameplayRuntimeBindings bindings)
        {
            return bindings != null &&
                   bindings.IsValid &&
                   Bindings.Matches(bindings) &&
                   PersistentEntityId.TryCreate(
                       localPlayerId,
                       out PersistentEntityId resolvedPlayerId) &&
                   Identity.LocalPlayerId == resolvedPlayerId &&
                   Identity.CarriedInventoryId ==
                   bindings.LocalPlayerInventory.ContainerId &&
                   PersistentEntityId.TryCreate(
                       bindings.Possession.ExplorerPersistentId,
                       out PersistentEntityId explorerId) &&
                   Identity.ExplorerActorId == explorerId &&
                   Identity.AssignedShuttleId ==
                   bindings.AssignedShuttle.ShipId;
        }
    }
}
