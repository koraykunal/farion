using Farion.Gameplay.Domain.Identity;
using Farion.Gameplay.Domain.Fleet;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
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
        public PersonalShipRuntimeBinding PersonalShipBinding =>
            Bindings.PersonalShip;
        public PersonalShipState PersonalShip =>
            PersonalShipBinding != null
                ? PersonalShipBinding.State
                : null;

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
                    bindings.Possession.SpacecraftPersistentId,
                    bindings.LocalPlayerInventory.ContainerId,
                    out GameplaySessionIdentity identity))
            {
                return false;
            }

            if (!bindings.PersonalShip.TryInitialize(identity.LocalPlayerId) ||
                bindings.PersonalShip.State == null ||
                bindings.PersonalShip.State.ShipId !=
                identity.PersonalShipId)
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
                   PersistentEntityId.TryCreate(
                       bindings.Possession.SpacecraftPersistentId,
                       out PersistentEntityId shipId) &&
                   Identity.PersonalShipId == shipId;
        }
    }
}
