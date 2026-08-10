using Farion.Core.Identity;
using System.Collections.Generic;
using Farion.Gameplay.Interaction;
using Farion.Multiplayer.Player;
using Farion.Multiplayer.Session;
using Farion.Multiplayer.World;
using Farion.Simulation.World;
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
        readonly Dictionary<SceneHandle, Dictionary<int, NetworkObject>> starterShips = new();
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

            if (IsHostConnection(connection))
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

            ReleaseStarterShipClaims(args.ConnectionId);
            RemoveStarterShip(args.ConnectionId);
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
            if (!starterShips.TryGetValue(
                    scene.handle,
                    out Dictionary<int, NetworkObject> ships))
            {
                ships = new Dictionary<int, NetworkObject>(MaximumPlayers);
                starterShips.Add(scene.handle, ships);
            }

            HashSet<int> activeSlots = new();
            List<NetworkConnection> failedConnections = new();
            foreach (NetworkConnection connection in
                     networkManager.ServerManager.Clients.Values)
            {
                if (!connection.IsActive ||
                    !connection.Scenes.Contains(scene) ||
                    !slots.TryReserve(connection.ClientId, out int slot))
                {
                    continue;
                }

                activeSlots.Add(slot);
                if (ships.TryGetValue(slot, out NetworkObject existing) &&
                    existing != null)
                {
                    if (!AssignStarterShip(
                            connection,
                            existing.GetComponent<NetworkStarterShip>()))
                    {
                        failedConnections.Add(connection);
                    }

                    continue;
                }

                if (!sceneContext.TryGetStarterShipPose(
                        MaximumPlayers,
                        slot,
                        out Vector3 position,
                        out Quaternion rotation))
                {
                    Debug.LogError(
                        $"Starter ship formation {MaximumPlayers} slot {slot + 1} is invalid.",
                        sceneContext);
                    failedConnections.Add(connection);
                    continue;
                }

                NetworkObject ship = Instantiate(
                    starterShipPrefab,
                    position,
                    rotation);
                GeneratedEntityId shipId = GeneratedEntityId.FromHash(
                    StableHashUtility.Combine(
                        sceneContext.ZoneId.Value,
                        "starter_ship",
                        slot));
                NetworkStarterShip starterShip =
                    ship.GetComponent<NetworkStarterShip>();
                starterShip?.Initialize(shipId, slot);
                starterShip?.BindScene(
                    sceneContext.GravitySimulation,
                    sceneContext.CelestialFrameProvider,
                    originAuthority);
                networkManager.ServerManager.Spawn(ship, null, scene);
                sceneContext.AttachToShiftedWorld(ship.transform);
                ships[slot] = ship;
                if (!AssignStarterShip(connection, starterShip))
                {
                    failedConnections.Add(connection);
                }
            }

            for (int i = 0; i < failedConnections.Count; i++)
            {
                failedConnections[i].Disconnect(immediately: true);
            }

            List<int> staleSlots = new();
            foreach (KeyValuePair<int, NetworkObject> pair in ships)
            {
                if (!activeSlots.Contains(pair.Key))
                {
                    staleSlots.Add(pair.Key);
                }
            }

            for (int i = 0; i < staleSlots.Count; i++)
            {
                DespawnStarterShip(ships, staleSlots[i]);
            }
        }

        void RemoveStarterShip(int connectionId)
        {
            if (!slots.TryGetReserved(connectionId, out int slot))
            {
                return;
            }

            foreach (Dictionary<int, NetworkObject> ships in starterShips.Values)
            {
                DespawnStarterShip(ships, slot);
            }
        }

        internal bool TryUseStarterShip(
            NetworkSessionPlayer sessionPlayer,
            GeneratedEntityId requestedShipId)
        {
            if (!TryResolveStarterShip(
                    sessionPlayer,
                    requestedShipId,
                    out NetworkConnection connection,
                    out NetworkObject player,
                    out NetworkStarterShip starterShip))
            {
                return false;
            }

            if (sessionPlayer.PossessionMode == PlayerPossessionMode.OnFoot)
            {
                return TryClaimStarterShip(
                    sessionPlayer,
                    requestedShipId,
                    connection,
                    player,
                    starterShip);
            }

            if (sessionPlayer.PossessionMode != PlayerPossessionMode.ShipInterior ||
                sessionPlayer.ClaimedStarterShipId != requestedShipId ||
                !starterShip.TryBeginPiloting(sessionPlayer, player))
            {
                return false;
            }

            player.GetComponent<NetworkExplorerController>()
                ?.SetPossessionActive(false);
            if (IsHostConnection(connection))
            {
                originAuthority?.SetServerTrackingTarget(starterShip.transform);
            }

            if (starterShip.Rig != null &&
                starterShip.Rig.PilotSeatPoint != null)
            {
                PlaceExplorer(
                    player,
                    starterShip.Rig.PilotSeatPoint.position,
                    starterShip.Rig.PilotSeatPoint.rotation);
            }

            sessionPlayer.SetPossessionMode(PlayerPossessionMode.Spacecraft);
            return true;
        }

        bool AssignStarterShip(
            NetworkConnection connection,
            NetworkStarterShip starterShip)
        {
            if (connection == null ||
                starterShip == null ||
                !sessionPlayers.TryGetValue(
                    connection.ClientId,
                    out NetworkObject sessionObject) ||
                sessionObject == null)
            {
                return false;
            }

            NetworkSessionPlayer sessionPlayer =
                sessionObject.GetComponent<NetworkSessionPlayer>();
            if (sessionPlayer == null || !starterShip.EntityId.IsValid)
            {
                return false;
            }

            sessionPlayer.SetAssignedStarterShip(starterShip.EntityId);
            return true;
        }

        bool TryClaimStarterShip(
            NetworkSessionPlayer sessionPlayer,
            GeneratedEntityId requestedShipId,
            NetworkConnection connection,
            NetworkObject player,
            NetworkStarterShip starterShip)
        {
            if (sessionPlayer.ClaimedStarterShipId.IsValid ||
                !starterShip.TryGetInteriorPose(
                    out Vector3 interiorPosition,
                    out Quaternion interiorRotation) ||
                !starterShip.TryClaim(
                    sessionPlayer,
                    connection,
                    player.transform.position))
            {
                return false;
            }

            PlaceExplorer(
                player,
                interiorPosition,
                interiorRotation);
            sessionPlayer.SetClaimedStarterShip(requestedShipId);
            sessionPlayer.SetPossessionMode(
                PlayerPossessionMode.ShipInterior);
            return true;
        }

        internal bool TryExitStarterShip(
            NetworkSessionPlayer sessionPlayer,
            GeneratedEntityId requestedShipId)
        {
            if (!TryResolveStarterShip(
                    sessionPlayer,
                    requestedShipId,
                    out _,
                    out NetworkObject player,
                    out NetworkStarterShip starterShip) ||
                sessionPlayer.PossessionMode != PlayerPossessionMode.Spacecraft ||
                sessionPlayer.ClaimedStarterShipId != requestedShipId ||
                !starterShip.IsPiloted ||
                !starterShip.IsClaimedBy(sessionPlayer.SessionPlayerId) ||
                !starterShip.TryGetExitPose(
                    out Vector3 exitPosition,
                    out Quaternion exitRotation))
            {
                return false;
            }

            PlaceExplorer(player, exitPosition, exitRotation);
            player.GetComponent<NetworkExplorerController>()
                ?.SetPossessionActive(true);
            if (IsHostConnection(sessionPlayer.Owner))
            {
                originAuthority?.SetServerTrackingTarget(player.transform);
            }

            sessionPlayer.SetClaimedStarterShip(GeneratedEntityId.None);
            sessionPlayer.SetPossessionMode(PlayerPossessionMode.OnFoot);
            starterShip.ClearClaim();
            return true;
        }

        bool TryResolveStarterShip(
            NetworkSessionPlayer sessionPlayer,
            GeneratedEntityId requestedShipId,
            out NetworkConnection connection,
            out NetworkObject player,
            out NetworkStarterShip starterShip)
        {
            connection = sessionPlayer?.Owner;
            player = null;
            starterShip = null;
            if (connection == null ||
                !connection.IsActive ||
                !requestedShipId.IsValid ||
                !sessionPlayers.TryGetValue(
                    connection.ClientId,
                    out NetworkObject registeredSessionPlayer) ||
                registeredSessionPlayer != sessionPlayer.NetworkObject ||
                !players.TryGetValue(connection.ClientId, out player) ||
                player == null ||
                !slots.TryGetReserved(connection.ClientId, out int slot) ||
                !starterShips.TryGetValue(
                    player.gameObject.scene.handle,
                    out Dictionary<int, NetworkObject> ships) ||
                !ships.TryGetValue(slot, out NetworkObject ship) ||
                ship == null)
            {
                return false;
            }

            starterShip = ship.GetComponent<NetworkStarterShip>();
            return starterShip != null &&
                sessionPlayer.AssignedStarterShipId == requestedShipId &&
                starterShip.EntityId == requestedShipId;
        }

        bool IsHostConnection(NetworkConnection connection)
        {
            NetworkConnection local = networkManager.ClientManager.Connection;
            return connection != null &&
                local != null &&
                connection.ClientId == local.ClientId;
        }

        static void PlaceExplorer(
            NetworkObject player,
            Vector3 position,
            Quaternion rotation)
        {
            if (player.TryGetComponent(out Rigidbody body))
            {
                body.position = position;
                body.rotation = rotation;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                return;
            }

            player.transform.SetPositionAndRotation(position, rotation);
        }

        void ReleaseStarterShipClaims(int connectionId)
        {
            foreach (Dictionary<int, NetworkObject> ships in starterShips.Values)
            {
                foreach (NetworkObject ship in ships.Values)
                {
                    NetworkStarterShip starterShip =
                        ship != null
                            ? ship.GetComponent<NetworkStarterShip>()
                            : null;
                    if (starterShip != null)
                    {
                        ulong claimedSessionPlayerId =
                            starterShip.ClaimedBySessionPlayerId;
                        if (starterShip.ReleaseClaim(connectionId))
                        {
                            ClearSessionClaim(claimedSessionPlayerId);
                        }
                    }
                }
            }
        }

        void ClearSessionClaim(ulong sessionPlayerId)
        {
            if (sessionPlayerId == 0UL)
            {
                return;
            }

            foreach (NetworkObject sessionObject in sessionPlayers.Values)
            {
                NetworkSessionPlayer sessionPlayer =
                    sessionObject != null
                        ? sessionObject.GetComponent<NetworkSessionPlayer>()
                        : null;
                if (sessionPlayer != null &&
                    sessionPlayer.SessionPlayerId == sessionPlayerId)
                {
                    sessionPlayer.SetClaimedStarterShip(
                        GeneratedEntityId.None);
                    sessionPlayer.SetPossessionMode(
                        PlayerPossessionMode.OnFoot);
                    return;
                }
            }
        }

        void DespawnStarterShip(
            Dictionary<int, NetworkObject> ships,
            int slot)
        {
            if (!ships.Remove(slot, out NetworkObject ship) ||
                ship == null)
            {
                return;
            }

            NetworkStarterShip starterShip =
                ship.GetComponent<NetworkStarterShip>();
            if (starterShip != null && starterShip.IsClaimed)
            {
                ulong claimedSessionPlayerId =
                    starterShip.ClaimedBySessionPlayerId;
                starterShip.ClearClaim();
                ClearSessionClaim(claimedSessionPlayerId);
            }

            if (ship.IsSpawned)
            {
                networkManager.ServerManager.Despawn(ship);
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
                .Initialize(NextSessionPlayerId(), this);
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
