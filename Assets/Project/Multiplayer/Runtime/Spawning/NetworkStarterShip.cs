using System;
using Farion.Core.Persistence;
using Farion.Simulation.World.Identity;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace Farion.Multiplayer.Spawning
{
    public sealed class NetworkStarterShip : NetworkBehaviour
    {
        readonly SyncVar<ulong> entityId = new();
        readonly SyncVar<byte> formationSlot = new();

        PersistentObjectId persistentObjectId;

        public GeneratedEntityId EntityId => entityId.Value == 0UL
            ? GeneratedEntityId.None
            : new GeneratedEntityId(entityId.Value);
        public int FormationSlot => formationSlot.Value;

        void Awake()
        {
            persistentObjectId = GetComponent<PersistentObjectId>();
        }

        internal void Initialize(GeneratedEntityId id, int slot)
        {
            if (!id.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            if (slot < 0 || slot > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            entityId.Value = id.Value;
            formationSlot.Value = (byte)slot;
            ApplyPersistentId();
        }

        public override void OnStartClient()
        {
            ApplyPersistentId();
        }

        void ApplyPersistentId()
        {
            if (persistentObjectId != null && entityId.Value != 0UL)
            {
                persistentObjectId.SetId($"ship.generated.{entityId.Value:X16}");
            }
        }

    }
}
