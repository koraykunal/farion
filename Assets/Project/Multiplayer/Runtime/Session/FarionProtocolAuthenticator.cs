using Farion.Core.Identity;
using System;
using Farion.Multiplayer.Spawning;
using FishNet.Authenticating;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace Farion.Multiplayer.Session
{
    public struct ProtocolHandshakeBroadcast : IBroadcast
    {
        public ulong Protocol;

        public ProtocolHandshakeBroadcast(ulong protocol)
        {
            Protocol = protocol;
        }
    }

    public struct ProtocolHandshakeResultBroadcast : IBroadcast
    {
        public bool Accepted;
        public byte Rejection;

        public ProtocolHandshakeResultBroadcast(
            bool accepted,
            MultiplayerFailureReason rejection)
        {
            Accepted = accepted;
            Rejection = (byte)rejection;
        }

        public MultiplayerFailureReason RejectionReason =>
            (MultiplayerFailureReason)Rejection;
    }

    [DisallowMultipleComponent]
    public sealed class FarionProtocolAuthenticator : Authenticator
    {
        const uint ProtocolRevision = 1;

        static ulong cachedProtocol;

        public override event Action<NetworkConnection, bool> OnAuthenticationResult;

        public static ulong LocalProtocol
        {
            get
            {
                if (cachedProtocol == 0UL)
                {
                    cachedProtocol = StableHashUtility.Combine(
                        StableHashUtility.Combine("farion.protocol"),
                        Application.version,
                        ProtocolRevision);
                    if (cachedProtocol == 0UL)
                    {
                        cachedProtocol = 1UL;
                    }
                }

                return cachedProtocol;
            }
        }

        public override void InitializeOnce(NetworkManager networkManager)
        {
            base.InitializeOnce(networkManager);
            networkManager.ServerManager.RegisterBroadcast<ProtocolHandshakeBroadcast>(
                OnServerHandshake,
                requireAuthentication: false);
            networkManager.ClientManager
                .RegisterBroadcast<ProtocolHandshakeResultBroadcast>(
                    OnClientHandshakeResult);
            networkManager.ClientManager.OnClientConnectionState +=
                OnClientConnectionState;
        }

        void OnDestroy()
        {
            if (NetworkManager == null)
            {
                return;
            }

            NetworkManager.ServerManager
                .UnregisterBroadcast<ProtocolHandshakeBroadcast>(OnServerHandshake);
            NetworkManager.ClientManager
                .UnregisterBroadcast<ProtocolHandshakeResultBroadcast>(
                    OnClientHandshakeResult);
            NetworkManager.ClientManager.OnClientConnectionState -=
                OnClientConnectionState;
        }

        void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started)
            {
                return;
            }

            NetworkManager.ClientManager.Broadcast(
                new ProtocolHandshakeBroadcast(LocalProtocol));
        }

        void OnServerHandshake(
            NetworkConnection connection,
            ProtocolHandshakeBroadcast message,
            Channel channel)
        {
            MultiplayerFailureReason rejection = ResolveRejection(
                connection,
                message.Protocol);
            bool accepted = rejection == MultiplayerFailureReason.None;
            NetworkManager.ServerManager.Broadcast(
                connection,
                new ProtocolHandshakeResultBroadcast(accepted, rejection),
                requireAuthenticated: false);
            if (rejection == MultiplayerFailureReason.ProtocolMismatch)
            {
                NetworkManager.Log(
                    $"Rejected a client using protocol {message.Protocol}; this build expects {LocalProtocol}.");
            }
            else if (rejection == MultiplayerFailureReason.ServerFull)
            {
                NetworkManager.Log(
                    $"Rejected a client because the session is full ({MultiplayerPlayerSpawner.MaximumPlayers} players).");
            }

            OnAuthenticationResult?.Invoke(connection, accepted);
        }

        MultiplayerFailureReason ResolveRejection(
            NetworkConnection connection,
            ulong protocol)
        {
            if (protocol != LocalProtocol)
            {
                return MultiplayerFailureReason.ProtocolMismatch;
            }

            int occupied = 0;
            foreach (NetworkConnection other in
                     NetworkManager.ServerManager.Clients.Values)
            {
                if (other != null &&
                    other != connection &&
                    other.IsActive &&
                    other.IsAuthenticated)
                {
                    occupied++;
                }
            }

            int capacity = MultiplayerSessionController.Active != null
                ? MultiplayerSessionController.Active.PlayerCapacity
                : MultiplayerPlayerSpawner.MaximumPlayers;
            return occupied < capacity
                ? MultiplayerFailureReason.None
                : MultiplayerFailureReason.ServerFull;
        }

        void OnClientHandshakeResult(
            ProtocolHandshakeResultBroadcast message,
            Channel channel)
        {
            if (message.Accepted)
            {
                return;
            }

            MultiplayerFailureReason reason =
                message.RejectionReason == MultiplayerFailureReason.None
                    ? MultiplayerFailureReason.ProtocolMismatch
                    : message.RejectionReason;
            MultiplayerSessionController.Active?.ReportFailure(reason);
            NetworkManager.LogError(
                reason == MultiplayerFailureReason.ServerFull
                    ? "The host session is already full; the connection was rejected."
                    : "The host is running a different Farion build; the connection was rejected.");
        }
    }
}
