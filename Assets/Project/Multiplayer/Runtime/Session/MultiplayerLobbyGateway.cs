using System;
using UnityEngine;

namespace Farion.Multiplayer.Session
{
    public interface IMultiplayerLobbyService
    {
        bool IsAvailable { get; }
        string LocalDisplayName { get; }
        string LocalPersistentId { get; }

        void HostLobby(int maximumPlayers, Action<bool> completed);
        void Leave();
        void OpenInviteOverlay();

        event Action<string> JoinRequested;
    }

    public static class MultiplayerLobbyGateway
    {
        public static IMultiplayerLobbyService Service { get; private set; }

        public static bool IsAvailable => Service != null && Service.IsAvailable;

        public static event Action<string> JoinRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetGateway()
        {
            Service = null;
            JoinRequested = null;
        }

        public static void Register(IMultiplayerLobbyService service)
        {
            if (Service == service)
            {
                return;
            }

            if (Service != null)
            {
                Service.JoinRequested -= RaiseJoinRequested;
            }

            Service = service;
            if (Service != null)
            {
                Service.JoinRequested += RaiseJoinRequested;
            }
        }

        public static void Unregister(IMultiplayerLobbyService service)
        {
            if (Service == service)
            {
                Register(null);
            }
        }

        static void RaiseJoinRequested(string hostAddress)
        {
            JoinRequested?.Invoke(hostAddress);
        }
    }
}
