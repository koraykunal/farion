using System.Collections.Generic;
using Farion.Multiplayer.Player;
using Farion.Multiplayer.Session;
using Farion.Multiplayer.World;
using Farion.Simulation.World.Identity;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Multiplayer.Spawning
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class NetworkPlayerSpawner : MonoBehaviour
    {
        const int MaximumPlayers = 4;

        [SerializeField] NetworkManager networkManager;
        [SerializeField] NetworkObject sessionPlayerPrefab;
        [SerializeField] NetworkObject playerPrefab;
        [SerializeField] NetworkObject starterShipPrefab;

        readonly SpawnSlotAllocator slots = new(MaximumPlayers);
        readonly Dictionary<int, NetworkObject> sessionPlayers = new();
        readonly Dictionary<int, NetworkObject> players = new();
        readonly Dictionary<SceneHandle, MultiplayerSceneContext> contexts = new();
        readonly Dictionary<SceneHandle, List<NetworkObject>> starterShips = new();
        ulong nextSessionPlayerId;
        NetworkWorldOriginAuthority originAuthority;

        public int SpawnedPlayerCount => players.Count;
        public int SessionPlayerCount => sessionPlayers.Count;

        void Awake()
        {
            networkManager ??= GetComponent<NetworkManager>();
            networkManager.SceneManager.OnClientPresenceChangeEnd +=
                OnClientPresenceChangeEnd;
            networkManager.SceneManager.OnClientLoadedStartScenes +=
                OnClientLoadedStartScenes;
            networkManager.ServerManager.OnRemoteConnectionState +=
                OnRemoteConnectionState;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        void OnDestroy()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.SceneManager.OnClientPresenceChangeEnd -=
                OnClientPresenceChangeEnd;
            networkManager.SceneManager.OnClientLoadedStartScenes -=
                OnClientLoadedStartScenes;
            networkManager.ServerManager.OnRemoteConnectionState -=
                OnRemoteConnectionState;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        public void BindContext(
            MultiplayerSceneContext sceneContext,
            NetworkWorldOriginAuthority worldOriginAuthority)
        {
            contexts[sceneContext.gameObject.scene.handle] = sceneContext;
            originAuthority = worldOriginAuthority;
            foreach (NetworkConnection connection in
                     networkManager.ServerManager.Clients.Values)
            {
                if (connection.Scenes.Contains(sceneContext.gameObject.scene))
                {
                    SpawnFor(connection, sceneContext);
                }
            }

            RefreshStarterShips(sceneContext);
        }

        public void ResetSession()
        {
            sessionPlayers.Clear();
            players.Clear();
            slots.Reset();
            nextSessionPlayerId = 0UL;
            contexts.Clear();
            starterShips.Clear();
            originAuthority = null;
        }

        void OnClientPresenceChangeEnd(
            FishNet.Managing.Scened.ClientPresenceChangeEventArgs args)
        {
            if (!networkManager.IsServerStarted ||
                !contexts.TryGetValue(
                    args.Scene.handle,
                    out MultiplayerSceneContext sceneContext))
            {
                return;
            }

            if (args.Added)
            {
                SpawnFor(args.Connection, sceneContext);
            }
            else
            {
                DespawnAvatar(args.Connection, args.Scene);
            }

            RefreshStarterShips(sceneContext);
        }

        void OnClientLoadedStartScenes(
            NetworkConnection connection,
            bool asServer)
        {
            if (asServer)
            {
                EnsureSessionPlayer(connection);
            }
        }

        void SpawnFor(
            NetworkConnection connection,
            MultiplayerSceneContext sceneContext)
        {
            if (connection == null ||
                !connection.IsActive)
            {
                return;
            }

            if (players.TryGetValue(
                    connection.ClientId,
                    out NetworkObject existingPlayer))
            {
                if (existingPlayer != null &&
                    existingPlayer.gameObject.scene == sceneContext.gameObject.scene)
                {
                    return;
                }

                if (existingPlayer != null && existingPlayer.IsSpawned)
                {
                    networkManager.ServerManager.Despawn(existingPlayer);
                }

                players.Remove(connection.ClientId);
            }

            if (!EnsureSessionPlayer(connection) ||
                playerPrefab == null ||
                !slots.TryReserve(connection.ClientId, out int slot) ||
                !sceneContext.TryGetSpawnPose(
                    slot,
                    out Vector3 position,
                    out Quaternion rotation))
            {
                slots.Release(connection.ClientId);
                connection.Disconnect(immediately: true);
                return;
            }

            originAuthority?.SendCurrentOrigin(connection);
            NetworkObject player = Instantiate(playerPrefab, position, rotation);
            player.GetComponent<NetworkExplorerController>()
                ?.BindScene(sceneContext.CelestialFrameProvider, originAuthority);
            networkManager.ServerManager.Spawn(
                player,
                connection,
                sceneContext.gameObject.scene);
            sceneContext.AttachToShiftedWorld(player.transform);
            players.Add(connection.ClientId, player);
            connection.SetFirstObject(player);

            if (networkManager.ClientManager.Connection != null &&
                connection.ClientId ==
                networkManager.ClientManager.Connection.ClientId)
            {
                originAuthority?.SetServerTrackingTarget(player.transform);
            }
        }

        void OnRemoteConnectionState(
            NetworkConnection connection,
            RemoteConnectionStateArgs args)
        {
            if (connection == null)
            {
                return;
            }

            if (args.ConnectionState != RemoteConnectionState.Stopped)
            {
                return;
            }

            sessionPlayers.Remove(args.ConnectionId);
            players.Remove(args.ConnectionId);
            slots.Release(args.ConnectionId);
        }

        void DespawnAvatar(NetworkConnection connection, Scene scene)
        {
            if (connection == null ||
                !players.TryGetValue(
                    connection.ClientId,
                    out NetworkObject player) ||
                player == null ||
                player.gameObject.scene != scene)
            {
                return;
            }

            players.Remove(connection.ClientId);
            if (player.IsSpawned)
            {
                networkManager.ServerManager.Despawn(player);
            }
        }

        void OnSceneUnloaded(Scene scene)
        {
            contexts.Remove(scene.handle);
            starterShips.Remove(scene.handle);
        }

        void RefreshStarterShips(MultiplayerSceneContext sceneContext)
        {
            if (!networkManager.IsServerStarted ||
                starterShipPrefab == null ||
                sceneContext == null ||
                !sceneContext.ZoneId.IsValid)
            {
                return;
            }

            Scene scene = sceneContext.gameObject.scene;
            int partySize = 0;
            foreach (NetworkConnection connection in
                     networkManager.ServerManager.Clients.Values)
            {
                if (connection.IsActive && connection.Scenes.Contains(scene))
                {
                    partySize++;
                }
            }

            partySize = Mathf.Clamp(partySize, 0, MaximumPlayers);
            if (!starterShips.TryGetValue(
                    scene.handle,
                    out List<NetworkObject> ships))
            {
                ships = new List<NetworkObject>(MaximumPlayers);
                starterShips.Add(scene.handle, ships);
            }

            ships.RemoveAll(ship => ship == null);
            if (ships.Count == partySize)
            {
                return;
            }

            for (int i = 0; i < ships.Count; i++)
            {
                if (ships[i] != null && ships[i].IsSpawned)
                {
                    networkManager.ServerManager.Despawn(ships[i]);
                }
            }

            ships.Clear();
            for (int i = 0; i < partySize; i++)
            {
                if (!sceneContext.TryGetStarterShipPose(
                        partySize,
                        i,
                        out Vector3 position,
                        out Quaternion rotation))
                {
                    Debug.LogError(
                        $"Starter ship formation {partySize} slot {i + 1} is invalid.",
                        sceneContext);
                    break;
                }

                NetworkObject ship = Instantiate(
                    starterShipPrefab,
                    position,
                    rotation);
                GeneratedEntityId shipId = GeneratedEntityId.FromHash(
                    StableHashUtility.Combine(
                        sceneContext.ZoneId.Value,
                        "starter_ship",
                        i));
                ship.GetComponent<NetworkStarterShip>()?.Initialize(shipId, i);
                networkManager.ServerManager.Spawn(ship, null, scene);
                sceneContext.AttachToShiftedWorld(ship.transform);
                ships.Add(ship);
            }
        }

        bool EnsureSessionPlayer(NetworkConnection connection)
        {
            if (sessionPlayers.ContainsKey(connection.ClientId))
            {
                return true;
            }

            if (sessionPlayerPrefab == null)
            {
                connection.Disconnect(immediately: true);
                return false;
            }

            NetworkObject sessionPlayer = Instantiate(sessionPlayerPrefab);
            sessionPlayer.GetComponent<NetworkSessionPlayer>()
                .Initialize(NextSessionPlayerId());
            networkManager.ServerManager.Spawn(sessionPlayer, connection);
            sessionPlayers.Add(connection.ClientId, sessionPlayer);
            return true;
        }

        ulong NextSessionPlayerId()
        {
            nextSessionPlayerId++;
            if (nextSessionPlayerId == 0UL)
            {
                nextSessionPlayerId++;
            }

            return nextSessionPlayerId;
        }
    }
}
