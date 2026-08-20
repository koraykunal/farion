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

        Callback<LobbyCreated_t> lobbyCreated;
        Callback<GameLobbyJoinRequested_t> joinRequested;
        Callback<LobbyEnter_t> lobbyEntered;
        Action<bool> hostCompleted;
        CSteamID currentLobby;

        public bool IsAvailable => FarionSteamRuntime.IsReady;
        public bool IsInLobby => currentLobby.IsValid();
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

            hostCompleted = completed;
            SteamMatchmaking.CreateLobby(
                ELobbyType.k_ELobbyTypeFriendsOnly,
                Mathf.Max(1, maximumPlayers));
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
            completed?.Invoke(succeeded);
        }

        void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
        {
            SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
        }

        void OnLobbyEntered(LobbyEnter_t callback)
        {
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
    }
}
