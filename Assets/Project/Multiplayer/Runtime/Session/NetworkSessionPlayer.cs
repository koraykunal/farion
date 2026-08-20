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
        readonly SyncVar<ulong> assignedStarterShipId = new();
        readonly SyncVar<ulong> claimedStarterShipId = new();
        readonly SyncVar<PlayerPossessionMode> possessionMode = new();
        NetworkPlayerSpawner playerSpawner;

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
        public GeneratedEntityId AssignedStarterShipId =>
            assignedStarterShipId.Value == 0UL
                ? GeneratedEntityId.None
                : new GeneratedEntityId(assignedStarterShipId.Value);
        public GeneratedEntityId ClaimedStarterShipId =>
            claimedStarterShipId.Value == 0UL
                ? GeneratedEntityId.None
                : new GeneratedEntityId(claimedStarterShipId.Value);
        public PlayerPossessionMode PossessionMode => possessionMode.Value;
        internal NetworkPlayerSpawner PlayerSpawner => playerSpawner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLocal()
        {
            Local = null;
            activePlayers.Clear();
        }

        void Awake()
        {
            assignedStarterShipId.OnChange += OnAssignedStarterShipChanged;
            if (!activePlayers.Contains(this))
            {
                activePlayers.Add(this);
            }
        }

        void OnDestroy()
        {
            activePlayers.Remove(this);
        }

        internal void Initialize(ulong value, NetworkPlayerSpawner spawner)
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
            assignedStarterShipId.Value = 0UL;
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

        public bool RequestUseStarterShip(GeneratedEntityId shipId)
        {
            if (!IsOwner || !shipId.IsValid)
            {
                return false;
            }

            RequestUseStarterShipServerRpc(shipId.Value);
            return true;
        }

        [ServerRpc]
        void RequestUseStarterShipServerRpc(
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

            playerSpawner?.TryUseStarterShip(
                this,
                new GeneratedEntityId(shipId));
        }

        public bool RequestExitStarterShip(GeneratedEntityId shipId)
        {
            if (!IsOwner || !shipId.IsValid)
            {
                return false;
            }

            RequestExitStarterShipServerRpc(shipId.Value);
            return true;
        }

        [ServerRpc]
        void RequestExitStarterShipServerRpc(
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

            playerSpawner?.TryExitStarterShip(
                this,
                new GeneratedEntityId(shipId));
        }

        internal void SetCurrentZone(GeneratedEntityId value)
        {
            currentZoneId.Value = value.Value;
        }

        internal void SetAssignedStarterShip(GeneratedEntityId value)
        {
            assignedStarterShipId.Value = value.Value;
        }

        internal void SetClaimedStarterShip(GeneratedEntityId value)
        {
            claimedStarterShipId.Value = value.Value;
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
                NotifyAssignedStarterShipReady();
            }
            else if (Local == this)
            {
                Local = null;
            }
        }

        void OnAssignedStarterShipChanged(ulong previous, ulong next, bool asServer)
        {
            NotifyAssignedStarterShipReady();
        }

        void NotifyAssignedStarterShipReady()
        {
            if (IsOwner && AssignedStarterShipId.IsValid)
            {
                MultiplayerSessionController.Active
                    ?.NotifyAssignedStarterShipReady();
            }
        }
    }
}
