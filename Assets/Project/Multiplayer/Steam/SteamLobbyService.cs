using Farion.Multiplayer.Session;
using Steamworks;
using System;
using UnityEngine;

namespace Farion.Multiplayer.Steam
{
    [DisallowMultipleComponent]
    public sealed class SteamLobbyService : MonoBehaviour, IMultiplayerLobbyService
    {
        const string HostAddressKey = "farion.host";
        const string GameNameKey = "farion.game";
        const string GameName = "Farion";
        const uint LobbyEnterSuccess = 1;

        Callback<LobbyCreated_t> lobbyCreated;
        Callback<GameLobbyJoinRequested_t> joinRequested;
        Callback<LobbyEnter_t> lobbyEntered;
        Action<bool> hostCompleted;
        CSteamID currentLobby;
        bool launchLobbyConsumed;

        public bool IsAvailable => FarionSteamRuntime.IsReady;
        public bool HasLobby => currentLobby.IsValid();
        public string LocalDisplayName => IsAvailable
            ? SteamFriends.GetPersonaName()
            : string.Empty;
        public string LocalPersistentId => IsAvailable
            ? SteamUser.GetSteamID().m_SteamID.ToString()
            : string.Empty;

        public event Action<string> JoinRequested;

        void OnEnable()
        {
            lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            joinRequested =
                Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            MultiplayerLobbyGateway.Register(this);
        }

        void Start()
        {
            if (launchLobbyConsumed ||
                !MultiplayerCommandLine.TryParseSteamLobby(
                    Environment.GetCommandLineArgs(),
                    out ulong lobbyId))
            {
                return;
            }

            launchLobbyConsumed = true;
            JoinLobby(lobbyId);
        }

        void OnDisable()
        {
            MultiplayerLobbyGateway.Unregister(this);
            Leave();
            lobbyCreated?.Dispose();
            joinRequested?.Dispose();
            lobbyEntered?.Dispose();
            lobbyCreated = null;
            joinRequested = null;
            lobbyEntered = null;
        }

        public void HostLobby(int maximumPlayers, Action<bool> completed)
        {
            if (!IsAvailable)
            {
                completed?.Invoke(false);
                return;
            }

            Leave();
            hostCompleted = completed;
            SteamMatchmaking.CreateLobby(
                ELobbyType.k_ELobbyTypeFriendsOnly,
                Mathf.Max(1, maximumPlayers));
        }

        public void JoinLobby(ulong lobbyId)
        {
            if (!IsAvailable || lobbyId == 0UL || !CanJoinAnotherSession())
            {
                return;
            }

            Leave();
            SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
        }

        public void Leave()
        {
            if (!currentLobby.IsValid())
            {
                return;
            }

            if (IsAvailable)
            {
                SteamMatchmaking.LeaveLobby(currentLobby);
                SteamFriends.ClearRichPresence();
            }

            currentLobby = CSteamID.Nil;
        }

        public void OpenInviteOverlay()
        {
            if (IsAvailable && currentLobby.IsValid())
            {
                SteamFriends.ActivateGameOverlayInviteDialog(currentLobby);
            }
        }

        void OnLobbyCreated(LobbyCreated_t callback)
        {
            bool succeeded = callback.m_eResult == EResult.k_EResultOK;
            if (succeeded)
            {
                currentLobby = new CSteamID(callback.m_ulSteamIDLobby);
                string hostAddress = SteamUser.GetSteamID().m_SteamID.ToString();
                SteamMatchmaking.SetLobbyData(
                    currentLobby,
                    HostAddressKey,
                    hostAddress);
                SteamMatchmaking.SetLobbyData(currentLobby, GameNameKey, GameName);
                SteamFriends.SetRichPresence("connect", hostAddress);
            }

            Action<bool> completed = hostCompleted;
            hostCompleted = null;
            if (completed == null)
            {
                Leave();
                return;
            }

            completed(succeeded);
        }

        void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
        {
            JoinLobby(callback.m_steamIDLobby.m_SteamID);
        }

        void OnLobbyEntered(LobbyEnter_t callback)
        {
            if (callback.m_EChatRoomEnterResponse != LobbyEnterSuccess)
            {
                Debug.LogWarning(
                    $"[Steam] Could not enter lobby {callback.m_ulSteamIDLobby} (response {callback.m_EChatRoomEnterResponse}).");
                return;
            }

            CSteamID lobby = new(callback.m_ulSteamIDLobby);
            currentLobby = lobby;
            string hostAddress =
                SteamMatchmaking.GetLobbyData(lobby, HostAddressKey);
            if (string.IsNullOrEmpty(hostAddress) ||
                hostAddress == SteamUser.GetSteamID().m_SteamID.ToString())
            {
                return;
            }

            SteamFriends.SetRichPresence("connect", hostAddress);
            JoinRequested?.Invoke(hostAddress);
        }

        static bool CanJoinAnotherSession()
        {
            MultiplayerSessionController session = MultiplayerSessionController.Active;
            return session == null || session.CanStartSession;
        }
    }
}
