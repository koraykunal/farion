using Farion.Core.Identity;
using System;
using System.Collections.Generic;
using Farion.Gameplay.Interaction;
using Farion.Multiplayer.Spawning;
using Farion.Simulation.World;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Farion.Multiplayer.Session
{
    public sealed class NetworkSessionPlayer : NetworkBehaviour
    {
        const int MaximumDisplayNameLength = 24;

        readonly SyncVar<ulong> sessionPlayerId = new();
        readonly SyncVar<string> displayName = new(string.Empty);
        string persistentPlayerId = string.Empty;
        readonly SyncVar<ulong> currentZoneId = new();
        readonly SyncVar<ulong> assignedStarterShuttleId = new();
        readonly SyncVar<ulong> claimedStarterShuttleId = new();
        readonly SyncVar<PlayerPossessionMode> possessionMode = new();
        MultiplayerPlayerSpawner playerSpawner;

        static readonly List<NetworkSessionPlayer> activePlayers = new();

        public static NetworkSessionPlayer Local { get; private set; }

        public static IReadOnlyList<NetworkSessionPlayer> ActivePlayers =>
            activePlayers;

        public ulong SessionPlayerId => sessionPlayerId.Value;
        public string DisplayName => displayName.Value;
        internal string PersistentPlayerId => persistentPlayerId;
        public GeneratedEntityId CurrentZoneId => currentZoneId.Value == 0UL
            ? GeneratedEntityId.None
            : new GeneratedEntityId(currentZoneId.Value);
        public GeneratedEntityId AssignedStarterShuttleId =>
            assignedStarterShuttleId.Value == 0UL
                ? GeneratedEntityId.None
                : new GeneratedEntityId(assignedStarterShuttleId.Value);
        public GeneratedEntityId ClaimedStarterShuttleId =>
            claimedStarterShuttleId.Value == 0UL
                ? GeneratedEntityId.None
                : new GeneratedEntityId(claimedStarterShuttleId.Value);
        public PlayerPossessionMode PossessionMode => possessionMode.Value;
        internal MultiplayerPlayerSpawner PlayerSpawner => playerSpawner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLocal()
        {
            Local = null;
            activePlayers.Clear();
        }

        void Awake()
        {
            assignedStarterShuttleId.OnChange += OnAssignedStarterShuttleChanged;
            if (!activePlayers.Contains(this))
            {
                activePlayers.Add(this);
            }
        }

        void OnDestroy()
        {
            activePlayers.Remove(this);
        }

        internal void Initialize(ulong value, MultiplayerPlayerSpawner spawner)
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
            assignedStarterShuttleId.Value = 0UL;
            possessionMode.Value = PlayerPossessionMode.OnFoot;
            playerSpawner = spawner;
        }

        public override void OnStartClient()
        {
            RefreshLocalBinding();
            if (IsOwner)
            {
                SubmitProfileServerRpc(
                    MultiplayerPlayerProfile.DisplayName,
                    MultiplayerPlayerProfile.PersistentPlayerId);
            }
        }

        [ServerRpc]
        void SubmitProfileServerRpc(
            string requestedName,
            string requestedPersistentId,
            NetworkConnection sender = null)
        {
            if (sender == null ||
                !sender.IsActive ||
                sender.ClientId != OwnerId)
            {
                return;
            }

            displayName.Value = MultiplayerPlayerProfile.Sanitize(
                requestedName,
                MaximumDisplayNameLength,
                sessionPlayerId.Value);
            if (persistentPlayerId.Length == 0 &&
                !string.IsNullOrWhiteSpace(requestedPersistentId))
            {
                persistentPlayerId = requestedPersistentId.Trim();
                playerSpawner?.RestorePlayerState(this);
            }
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            RefreshLocalBinding();
        }

        public override void OnStopClient()
        {
            if (Local == this)
            {
                Local = null;
            }
        }

        public bool RequestUseStarterShuttle(GeneratedEntityId shipId)
        {
            if (!IsOwner || !shipId.IsValid)
            {
                return false;
            }

            RequestUseStarterShuttleServerRpc(shipId.Value);
            return true;
        }

        [ServerRpc]
        void RequestUseStarterShuttleServerRpc(
            ulong shipId,
            NetworkConnection sender = null)
        {
            if (sender == null ||
                !sender.IsActive ||
                sender.ClientId != OwnerId ||
                shipId == 0UL)
            {
                return;
            }

            playerSpawner?.TryUseStarterShuttle(
                this,
                new GeneratedEntityId(shipId));
        }

        public bool RequestExitStarterShuttle(GeneratedEntityId shipId)
        {
            if (!IsOwner || !shipId.IsValid)
            {
                return false;
            }

            RequestExitStarterShuttleServerRpc(shipId.Value);
            return true;
        }

        [ServerRpc]
        void RequestExitStarterShuttleServerRpc(
            ulong shipId,
            NetworkConnection sender = null)
        {
            if (sender == null ||
                !sender.IsActive ||
                sender.ClientId != OwnerId ||
                shipId == 0UL)
            {
                return;
            }

            playerSpawner?.TryExitStarterShuttle(
                this,
                new GeneratedEntityId(shipId));
        }

        internal void SetCurrentZone(GeneratedEntityId value)
        {
            currentZoneId.Value = value.Value;
        }

        internal void SetAssignedStarterShuttle(GeneratedEntityId value)
        {
            assignedStarterShuttleId.Value = value.Value;
        }

        internal void SetClaimedStarterShuttle(GeneratedEntityId value)
        {
            claimedStarterShuttleId.Value = value.Value;
        }

        internal void SetPossessionMode(PlayerPossessionMode value)
        {
            possessionMode.Value = value;
        }

        void RefreshLocalBinding()
        {
            if (IsOwner)
            {
                Local = this;
                NotifyAssignedStarterShuttleReady();
            }
            else if (Local == this)
            {
                Local = null;
            }
        }

        void OnAssignedStarterShuttleChanged(ulong previous, ulong next, bool asServer)
        {
            NotifyAssignedStarterShuttleReady();
        }

        void NotifyAssignedStarterShuttleReady()
        {
            if (IsOwner && AssignedStarterShuttleId.IsValid)
            {
                MultiplayerSessionController.Active
                    ?.NotifyAssignedStarterShuttleReady();
            }
        }
    }
}
