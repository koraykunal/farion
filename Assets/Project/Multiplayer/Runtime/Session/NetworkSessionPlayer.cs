using System;
using Farion.Simulation.World.Identity;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace Farion.Multiplayer.Session
{
    public sealed class NetworkSessionPlayer : NetworkBehaviour
    {
        readonly SyncVar<ulong> sessionPlayerId = new();
        readonly SyncVar<ulong> currentZoneId = new();

        public ulong SessionPlayerId => sessionPlayerId.Value;
        public GeneratedEntityId CurrentZoneId => currentZoneId.Value == 0UL
            ? GeneratedEntityId.None
            : new GeneratedEntityId(currentZoneId.Value);

        internal void Initialize(ulong value)
        {
            if (value == 0UL)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (sessionPlayerId.Value != 0UL)
            {
                throw new InvalidOperationException(
                    "The session player identity is already initialized.");
            }

            sessionPlayerId.Value = value;
        }

        internal void SetCurrentZone(GeneratedEntityId value)
        {
            currentZoneId.Value = value.Value;
        }
    }
}
