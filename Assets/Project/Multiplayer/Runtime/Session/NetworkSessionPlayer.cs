using Farion.Core.Identity;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
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
        const int MaximumPersistentIdLength = 64;
        const int MaximumSecretLength = 128;
        internal const string PlatformOwnerProof = "platform";

        readonly SyncVar<ulong> sessionPlayerId = new();
        readonly SyncVar<string> displayName = new(string.Empty);
        string persistentPlayerId = string.Empty;
        string ownerProof = string.Empty;
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
        public string PersistentPlayerId => persistentPlayerId;
        internal string OwnerProof => ownerProof;
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
                    MultiplayerPlayerProfile.PersistentPlayerId,
                    MultiplayerPlayerProfile.ReconnectSecret);
            }
        }

        [ServerRpc]
        void SubmitProfileServerRpc(
            string requestedName,
            string requestedPersistentId,
            string requestedSecret,
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
            if (persistentPlayerId.Length > 0)
            {
                return;
            }

            if (!TryResolveIdentity(
                    sender,
                    requestedPersistentId,
                    requestedSecret,
                    out string resolvedId,
                    out string proof))
            {
                Debug.LogWarning(
                    $"Session player {sessionPlayerId.Value} submitted an unusable identity; it will not restore saved state.",
                    this);
                return;
            }

            if (playerSpawner != null)
            {
                resolvedId = playerSpawner.ResolveOwnedIdentity(resolvedId, proof);
            }

            if (IsPersistentIdInUse(resolvedId))
            {
                Debug.LogWarning(
                    $"Session player {sessionPlayerId.Value} requested a persistent id that is already active; it will not restore saved state.",
                    this);
                return;
            }

            persistentPlayerId = resolvedId;
            ownerProof = proof;
            playerSpawner?.RestorePlayerState(this);
        }

        bool TryResolveIdentity(
            NetworkConnection sender,
            string requestedPersistentId,
            string requestedSecret,
            out string persistentId,
            out string proof)
        {
            if (sender.IsLocalClient)
            {
                persistentId = NormalizePersistentId(requestedPersistentId);
                proof = MultiplayerPlayerProfile.HasPlatformIdentity
                    ? PlatformOwnerProof
                    : HashSecret(requestedSecret);
                return persistentId.Length > 0 && proof.Length > 0;
            }

            string address = NetworkManager.TransportManager.Transport
                .GetConnectionAddress(sender.ClientId);
            if (IsSteamId(address))
            {
                persistentId = address;
                proof = PlatformOwnerProof;
                return true;
            }

            persistentId = NormalizePersistentId(requestedPersistentId);
            proof = HashSecret(requestedSecret);
            return persistentId.Length > 0 && proof.Length > 0;
        }

        internal static string NormalizePersistentId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            if (trimmed.Length > MaximumPersistentIdLength)
            {
                return string.Empty;
            }

            for (int i = 0; i < trimmed.Length; i++)
            {
                char current = trimmed[i];
                if (!char.IsLetterOrDigit(current) &&
                    current != '-' &&
                    current != '_' &&
                    current != '.')
                {
                    return string.Empty;
                }
            }

            return trimmed;
        }

        internal static bool IsSteamId(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length != 17 ||
                !value.StartsWith("7656", StringComparison.Ordinal))
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsDigit(value[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal static string HashSecret(string secret)
        {
            if (string.IsNullOrWhiteSpace(secret) || secret.Length > MaximumSecretLength)
            {
                return string.Empty;
            }

            using SHA256 sha = SHA256.Create();
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(secret.Trim()));
            StringBuilder builder = new(digest.Length * 2);
            for (int i = 0; i < digest.Length; i++)
            {
                builder.Append(digest[i].ToString("x2"));
            }

            return builder.ToString();
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
