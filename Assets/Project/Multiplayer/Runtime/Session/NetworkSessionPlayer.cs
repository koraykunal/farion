using Farion.Core.Identity;
using System;
using System.Collections.Generic;
using Farion.Gameplay.Interaction;
using Farion.Multiplayer.Spawning;
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
        readonly SyncVar<PlayerPossessionMode> possessionMode = new(PlayerPossessionMode.OnFoot);
        MultiplayerPlayerSpawner playerSpawner;

        static readonly List<NetworkSessionPlayer> activePlayers = new();

        public static NetworkSessionPlayer Local { get; private set; }

        public static IReadOnlyList<NetworkSessionPlayer> ActivePlayers =>
            activePlayers;

        public ulong SessionPlayerId => sessionPlayerId.Value;
        public string DisplayName => displayName.Value;
        internal string PersistentPlayerId => persistentPlayerId;
        public GeneratedEntityId CurrentZoneId => ToEntityId(currentZoneId.Value);
        public GeneratedEntityId AssignedStarterShuttleId =>
            ToEntityId(assignedStarterShuttleId.Value);
        public GeneratedEntityId ClaimedStarterShuttleId =>
            ToEntityId(claimedStarterShuttleId.Value);
        public PlayerPossessionMode PossessionMode => possessionMode.Value;
        internal MultiplayerPlayerSpawner PlayerSpawner => playerSpawner;

        public event Action<PlayerPossessionMode> PossessionModeChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLocal()
        {
            Local = null;
            activePlayers.Clear();
        }

        void Awake()
        {
            assignedStarterShuttleId.OnChange += OnAssignedStarterShuttleChanged;
            possessionMode.OnChange += OnPossessionModeChanged;
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
            if (!IsRequestFromOwner(sender))
            {
                return;
            }

            displayName.Value = MultiplayerPlayerProfile.Sanitize(
                requestedName,
                MaximumDisplayNameLength,
                sessionPlayerId.Value);
            if (persistentPlayerId.Length > 0 ||
                string.IsNullOrWhiteSpace(requestedPersistentId))
            {
                return;
            }

            string trimmed = requestedPersistentId.Trim();
            if (IsPersistentIdInUse(trimmed))
            {
                Debug.LogWarning(
                    $"Session player {sessionPlayerId.Value} requested a persistent id that is already active; it will not restore saved state.",
                    this);
                return;
            }

            persistentPlayerId = trimmed;
            playerSpawner?.RestorePlayerState(this);
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

        public bool RequestBoardStarterShuttle(GeneratedEntityId shipId)
        {
            if (!IsOwner || !shipId.IsValid)
            {
                return false;
            }

            BoardStarterShuttleServerRpc(shipId.Value);
            return true;
        }

        public bool RequestPilotStarterShuttle(GeneratedEntityId shipId)
        {
            if (!IsOwner || !shipId.IsValid)
            {
                return false;
            }

            PilotStarterShuttleServerRpc(shipId.Value);
            return true;
        }

        public bool RequestLeavePilotSeat(GeneratedEntityId shipId)
        {
            if (!IsOwner || !shipId.IsValid)
            {
                return false;
            }

            LeavePilotSeatServerRpc(shipId.Value);
            return true;
        }

        [ServerRpc]
        void BoardStarterShuttleServerRpc(ulong shipId, NetworkConnection sender = null)
        {
            if (IsRequestFromOwner(sender) && shipId != 0UL)
            {
                playerSpawner?.TryBoardStarterShuttle(this, new GeneratedEntityId(shipId));
            }
        }

        [ServerRpc]
        void PilotStarterShuttleServerRpc(ulong shipId, NetworkConnection sender = null)
        {
            if (IsRequestFromOwner(sender) && shipId != 0UL)
            {
                playerSpawner?.TryPilotStarterShuttle(this, new GeneratedEntityId(shipId));
            }
        }

        [ServerRpc]
        void LeavePilotSeatServerRpc(ulong shipId, NetworkConnection sender = null)
        {
            if (IsRequestFromOwner(sender) && shipId != 0UL)
            {
                playerSpawner?.TryLeavePilotSeat(this, new GeneratedEntityId(shipId));
            }
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

        bool IsRequestFromOwner(NetworkConnection sender)
        {
            return sender != null && sender.IsActive && sender.ClientId == OwnerId;
        }

        static bool IsPersistentIdInUse(string candidate)
        {
            for (int i = 0; i < activePlayers.Count; i++)
            {
                NetworkSessionPlayer other = activePlayers[i];
                if (other != null &&
                    other.IsSpawned &&
                    string.Equals(other.persistentPlayerId, candidate, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        static GeneratedEntityId ToEntityId(ulong value)
        {
            return value == 0UL ? GeneratedEntityId.None : new GeneratedEntityId(value);
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

        void OnPossessionModeChanged(
            PlayerPossessionMode previous,
            PlayerPossessionMode next,
            bool asServer)
        {
            PossessionModeChanged?.Invoke(next);
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
