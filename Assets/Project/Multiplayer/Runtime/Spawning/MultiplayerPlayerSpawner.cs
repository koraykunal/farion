using Farion.Core.Identity;
using System.Collections.Generic;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Gameplay.Persistence;
using Farion.Gameplay.Session;
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
using Farion.Multiplayer.Spacecraft;

namespace Farion.Multiplayer.Spawning
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class MultiplayerPlayerSpawner : MonoBehaviour
    {
        public const int MaximumPlayers = 4;

        [SerializeField] NetworkManager networkManager;
        [SerializeField] NetworkObject sessionPlayerPrefab;
        [SerializeField] NetworkObject playerPrefab;
        [SerializeField] NetworkObject starterShuttlePrefab;

        readonly SpawnSlotAllocator slots = new(MaximumPlayers);
        readonly Dictionary<int, NetworkObject> sessionPlayers = new();
        readonly Dictionary<int, NetworkObject> players = new();
        readonly Dictionary<SceneHandle, MultiplayerSceneContext> contexts = new();
        readonly Dictionary<SceneHandle, Dictionary<int, NetworkObject>> starterShuttles = new();
        readonly Dictionary<string, MultiplayerPlayerSaveEntry> restoredPlayers = new();
        readonly List<MultiplayerShipSaveEntry> restoredShipCargo = new();
        ulong nextSessionPlayerId;
        MultiplayerWorldOriginAuthority originAuthority;
        GameplayDefinitionRegistry definitions;

        public int SpawnedPlayerCount => players.Count;
        public int SessionPlayerCount => sessionPlayers.Count;

        internal bool TryGetSpawnedExplorer(
            NetworkSessionPlayer player,
            out NetworkExplorerController explorer)
        {
            explorer = null;
            if (player == null ||
                player.Owner == null ||
                !players.TryGetValue(
                    player.Owner.ClientId,
                    out NetworkObject playerObject) ||
                playerObject == null)
            {
                return false;
            }

            explorer = playerObject.GetComponent<NetworkExplorerController>();
            return explorer != null;
        }

        bool TryResolveDefinitions(out GameplayDefinitionRegistry registry)
        {
            registry = definitions;
            if (registry != null)
            {
                return true;
            }

            if (TryGetRuntimeBindings(out GameplayRuntimeBindings bindings))
            {
                registry = bindings.Definitions;
            }

            return registry != null;
        }

        internal bool TryGetRuntimeBindings(out GameplayRuntimeBindings bindings)
        {
            bindings = null;
            foreach (MultiplayerSceneContext context in contexts.Values)
            {
                if (context != null && context.RuntimeRoot != null)
                {
                    bindings = context.RuntimeRoot.Bindings;
                    break;
                }
            }

            return bindings != null;
        }

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
            MultiplayerWorldOriginAuthority worldOriginAuthority)
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

            RefreshStarterShuttles(sceneContext);
        }

        public void LoadRestoredState(
            IReadOnlyList<MultiplayerPlayerSaveEntry> players,
            GameplayDefinitionRegistry definitionRegistry)
        {
            restoredPlayers.Clear();
            definitions = definitionRegistry;
            if (players == null)
            {
                return;
            }

            for (int i = 0; i < players.Count; i++)
            {
                MultiplayerPlayerSaveEntry entry = players[i];
                if (entry != null && entry.IsValid)
                {
                    restoredPlayers[entry.PersistentPlayerId] = entry;
                }
            }
        }

        internal void RestorePlayerState(NetworkSessionPlayer sessionPlayer)
        {
            if (!networkManager.IsServerStarted ||
                sessionPlayer == null ||
                !TryResolveDefinitions(out GameplayDefinitionRegistry registry))
            {
                return;
            }

            ApplyRestoredShipCargo(restoredShipCargo);

            if (string.IsNullOrEmpty(sessionPlayer.PersistentPlayerId) ||
                !restoredPlayers.TryGetValue(
                    sessionPlayer.PersistentPlayerId,
                    out MultiplayerPlayerSaveEntry entry) ||
                !TryGetSpawnedExplorer(
                    sessionPlayer,
                    out NetworkExplorerController explorer))
            {
                return;
            }

            InventoryContainerComponent inventory =
                explorer.GetComponentInChildren<InventoryContainerComponent>(true);
            if (inventory != null && entry.CarriedInventory != null)
            {
                inventory.ApplyContainerSnapshot(entry.CarriedInventory, registry);
                sessionPlayer.GetComponent<NetworkGameplayCommands>()
                    ?.PushOwnerInventory(inventory);
            }

            restoredPlayers.Remove(sessionPlayer.PersistentPlayerId);
        }

        void PreservePlayerState(int connectionId)
        {
            if (!sessionPlayers.TryGetValue(
                    connectionId,
                    out NetworkObject sessionObject) ||
                sessionObject == null)
            {
                return;
            }

            NetworkSessionPlayer sessionPlayer =
                sessionObject.GetComponent<NetworkSessionPlayer>();
            if (sessionPlayer == null ||
                string.IsNullOrEmpty(sessionPlayer.PersistentPlayerId) ||
                !TryCaptureEntry(
                    sessionPlayer,
                    connectionId,
                    out MultiplayerPlayerSaveEntry entry))
            {
                return;
            }

            restoredPlayers[sessionPlayer.PersistentPlayerId] = entry;
        }

        bool TryCaptureEntry(
            NetworkSessionPlayer sessionPlayer,
            int connectionId,
            out MultiplayerPlayerSaveEntry entry)
        {
            entry = null;
            if (!TryGetSpawnedExplorer(
                    sessionPlayer,
                    out NetworkExplorerController explorer))
            {
                return false;
            }

            InventoryContainerComponent inventory =
                explorer.GetComponentInChildren<InventoryContainerComponent>(true);
            entry = new MultiplayerPlayerSaveEntry(
                sessionPlayer.PersistentPlayerId,
                sessionPlayer.DisplayName,
                slots.TryGetReserved(connectionId, out int slot) ? slot : -1,
                inventory != null ? inventory.CaptureContainerSnapshot() : null);
            return true;
        }

        public void CaptureState(
            List<MultiplayerPlayerSaveEntry> players,
            List<MultiplayerShipSaveEntry> shipCargo)
        {
            if (players == null || shipCargo == null)
            {
                return;
            }

            foreach (MultiplayerPlayerSaveEntry pending in restoredPlayers.Values)
            {
                players.Add(pending);
            }

            foreach (KeyValuePair<int, NetworkObject> pair in sessionPlayers)
            {
                NetworkSessionPlayer sessionPlayer = pair.Value != null
                    ? pair.Value.GetComponent<NetworkSessionPlayer>()
                    : null;
                if (sessionPlayer != null &&
                    !string.IsNullOrEmpty(sessionPlayer.PersistentPlayerId) &&
                    TryCaptureEntry(
                        sessionPlayer,
                        pair.Key,
                        out MultiplayerPlayerSaveEntry entry))
                {
                    players.Add(entry);
                }
            }

            HashSet<string> capturedOwners = new();
            HashSet<int> capturedSlots = new();
            foreach (Dictionary<int, NetworkObject> ships in starterShuttles.Values)
            {
                foreach (KeyValuePair<int, NetworkObject> pair in ships)
                {
                    NetworkStarterShuttle ship = pair.Value != null
                        ? pair.Value.GetComponent<NetworkStarterShuttle>()
                        : null;
                    if (ship == null || ship.Cargo == null || ship.Motor == null)
                    {
                        continue;
                    }

                    string owner = ResolveSlotOwner(pair.Key);
                    if (!string.IsNullOrEmpty(owner))
                    {
                        capturedOwners.Add(owner);
                    }

                    capturedSlots.Add(pair.Key);
                    shipCargo.Add(new MultiplayerShipSaveEntry(
                        owner,
                        pair.Key,
                        ship.Cargo.CaptureContainerSnapshot(),
                        ship.Motor.Fuel));
                }
            }

            for (int i = 0; i < restoredShipCargo.Count; i++)
            {
                MultiplayerShipSaveEntry pending = restoredShipCargo[i];
                bool alreadyCaptured = pending.HasOwner
                    ? capturedOwners.Contains(pending.PersistentPlayerId)
                    : capturedSlots.Contains(pending.FormationSlot);
                if (!alreadyCaptured)
                {
                    shipCargo.Add(pending);
                }
            }
        }

        public void LoadRestoredShipCargo(
            IReadOnlyList<MultiplayerShipSaveEntry> shipCargo)
        {
            restoredShipCargo.Clear();
            if (shipCargo == null)
            {
                return;
            }

            for (int i = 0; i < shipCargo.Count; i++)
            {
                if (shipCargo[i] != null && shipCargo[i].IsValid)
                {
                    restoredShipCargo.Add(shipCargo[i]);
                }
            }

            ApplyRestoredShipCargo(restoredShipCargo);
        }

        void ApplyRestoredShipCargo(List<MultiplayerShipSaveEntry> shipCargo)
        {
            if (shipCargo.Count == 0 ||
                !TryResolveDefinitions(out GameplayDefinitionRegistry registry))
            {
                return;
            }

            for (int i = shipCargo.Count - 1; i >= 0; i--)
            {
                MultiplayerShipSaveEntry entry = shipCargo[i];
                if (!TryResolveCargoSlot(entry, out int slot) ||
                    !TryGetStarterShuttleInSlot(slot, out NetworkStarterShuttle starterShuttle))
                {
                    continue;
                }

                starterShuttle.Cargo.ApplyContainerSnapshot(entry.Cargo, registry);
                if (entry.HasFuel && starterShuttle.Motor != null)
                {
                    starterShuttle.Motor.RestoreFuel(entry.Fuel);
                }

                shipCargo.RemoveAt(i);
            }
        }

        bool TryResolveCargoSlot(
            MultiplayerShipSaveEntry entry,
            out int slot)
        {
            if (entry.HasOwner)
            {
                return TryGetSlotForPersistentId(entry.PersistentPlayerId, out slot);
            }

            slot = entry.FormationSlot;
            return slot >= 0;
        }

        bool TryGetSlotForPersistentId(string persistentPlayerId, out int slot)
        {
            slot = -1;
            if (string.IsNullOrWhiteSpace(persistentPlayerId))
            {
                return false;
            }

            foreach (KeyValuePair<int, NetworkObject> pair in sessionPlayers)
            {
                NetworkSessionPlayer sessionPlayer = pair.Value != null
                    ? pair.Value.GetComponent<NetworkSessionPlayer>()
                    : null;
                if (sessionPlayer != null &&
                    sessionPlayer.PersistentPlayerId == persistentPlayerId)
                {
                    return slots.TryGetReserved(pair.Key, out slot);
                }
            }

            return false;
        }

        string ResolveSlotOwner(int slot)
        {
            foreach (KeyValuePair<int, NetworkObject> pair in sessionPlayers)
            {
                if (!slots.TryGetReserved(pair.Key, out int reserved) ||
                    reserved != slot)
                {
                    continue;
                }

                NetworkSessionPlayer sessionPlayer = pair.Value != null
                    ? pair.Value.GetComponent<NetworkSessionPlayer>()
                    : null;
                return sessionPlayer != null
                    ? sessionPlayer.PersistentPlayerId
                    : string.Empty;
            }

            return string.Empty;
        }

        bool TryGetStarterShuttleInSlot(int slot, out NetworkStarterShuttle starterShuttle)
        {
            starterShuttle = null;
            if (slot < 0)
            {
                return false;
            }

            foreach (Dictionary<int, NetworkObject> ships in starterShuttles.Values)
            {
                if (!ships.TryGetValue(slot, out NetworkObject ship) ||
                    ship == null)
                {
                    continue;
                }

                NetworkStarterShuttle candidate =
                    ship.GetComponent<NetworkStarterShuttle>();
                if (candidate != null && candidate.Cargo != null)
                {
                    starterShuttle = candidate;
                    return true;
                }
            }

            return false;
        }

        void PreserveStarterShuttleState(NetworkStarterShuttle starterShuttle, int slot)
        {
            if (starterShuttle == null ||
                starterShuttle.Cargo == null ||
                starterShuttle.Motor == null)
            {
                return;
            }

            MultiplayerShipSaveEntry entry = new(
                ResolveSlotOwner(slot),
                slot,
                starterShuttle.Cargo.CaptureContainerSnapshot(),
                starterShuttle.Motor.Fuel);
            if (!entry.IsValid)
            {
                return;
            }

            for (int i = restoredShipCargo.Count - 1; i >= 0; i--)
            {
                MultiplayerShipSaveEntry existing = restoredShipCargo[i];
                bool duplicate = entry.HasOwner && existing.HasOwner
                    ? existing.PersistentPlayerId == entry.PersistentPlayerId
                    : existing.FormationSlot == slot;
                if (duplicate)
                {
                    restoredShipCargo.RemoveAt(i);
                }
            }

            restoredShipCargo.Add(entry);
        }

        public void ResetSession()
        {
            sessionPlayers.Clear();
            players.Clear();
            slots.Reset();
            nextSessionPlayerId = 0UL;
            contexts.Clear();
            starterShuttles.Clear();
            restoredPlayers.Clear();
            restoredShipCargo.Clear();
            definitions = null;
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

            RefreshStarterShuttles(sceneContext);
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
            NetworkExplorerController explorer =
                player.GetComponent<NetworkExplorerController>();
            NetworkSessionPlayer sessionPlayer =
                sessionPlayers[connection.ClientId]
                    .GetComponent<NetworkSessionPlayer>();
            explorer?.InitializeIdentity(sessionPlayer.SessionPlayerId);
            explorer?.BindScene(
                sceneContext.CelestialFrameProvider,
                originAuthority);
            networkManager.ServerManager.Spawn(
                player,
                connection,
                sceneContext.gameObject.scene);
            sceneContext.AttachToShiftedWorld(player.transform);
            players.Add(connection.ClientId, player);
            connection.SetFirstObject(player);
            RestorePlayerState(
                sessionPlayers[connection.ClientId]
                    .GetComponent<NetworkSessionPlayer>());

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

            PreservePlayerState(args.ConnectionId);
            ReleaseStarterShuttleClaims(args.ConnectionId);
            RemoveStarterShuttle(args.ConnectionId);
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
            starterShuttles.Remove(scene.handle);
        }

        void RefreshStarterShuttles(MultiplayerSceneContext sceneContext)
        {
            if (!networkManager.IsServerStarted ||
                starterShuttlePrefab == null ||
                sceneContext == null ||
                !sceneContext.ZoneId.IsValid)
            {
                return;
            }

            Scene scene = sceneContext.gameObject.scene;
            if (!starterShuttles.TryGetValue(
                    scene.handle,
                    out Dictionary<int, NetworkObject> ships))
            {
                ships = new Dictionary<int, NetworkObject>(MaximumPlayers);
                starterShuttles.Add(scene.handle, ships);
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
                    if (!AssignStarterShuttle(
                            connection,
                            existing.GetComponent<NetworkStarterShuttle>()))
                    {
                        failedConnections.Add(connection);
                    }

                    continue;
                }

                if (!sceneContext.TryGetStarterShuttlePose(
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
                    starterShuttlePrefab,
                    position,
                    rotation);
                GeneratedEntityId shipId = GeneratedEntityId.FromHash(
                    StableHashUtility.Combine(
                        sceneContext.ZoneId.Value,
                        "starter_ship",
                        slot));
                NetworkStarterShuttle starterShuttle =
                    ship.GetComponent<NetworkStarterShuttle>();
                starterShuttle?.Initialize(shipId, slot);
                starterShuttle?.BindScene(
                    sceneContext.GravitySimulation,
                    sceneContext.CelestialFrameProvider,
                    originAuthority);
                networkManager.ServerManager.Spawn(ship, null, scene);
                sceneContext.AttachToShiftedWorld(ship.transform);
                ships[slot] = ship;
                if (!AssignStarterShuttle(connection, starterShuttle))
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
                DespawnStarterShuttle(ships, staleSlots[i]);
            }

            ApplyRestoredShipCargo(restoredShipCargo);
        }

        void RemoveStarterShuttle(int connectionId)
        {
            if (!slots.TryGetReserved(connectionId, out int slot))
            {
                return;
            }

            foreach (Dictionary<int, NetworkObject> ships in starterShuttles.Values)
            {
                DespawnStarterShuttle(ships, slot);
            }
        }

        internal bool TryUseStarterShuttle(
            NetworkSessionPlayer sessionPlayer,
            GeneratedEntityId requestedShipId)
        {
            if (!TryResolveStarterShuttle(
                    sessionPlayer,
                    requestedShipId,
                    out NetworkConnection connection,
                    out NetworkObject player,
                    out NetworkStarterShuttle starterShuttle))
            {
                return false;
            }

            if (sessionPlayer.PossessionMode == PlayerPossessionMode.OnFoot)
            {
                return TryClaimStarterShuttle(
                    sessionPlayer,
                    requestedShipId,
                    connection,
                    player,
                    starterShuttle);
            }

            if (sessionPlayer.PossessionMode != PlayerPossessionMode.ShipInterior ||
                sessionPlayer.ClaimedStarterShuttleId != requestedShipId ||
                !starterShuttle.TryBeginPiloting(sessionPlayer, player))
            {
                return false;
            }

            player.GetComponent<NetworkExplorerController>()
                ?.SetPossessionActive(false);
            if (IsHostConnection(connection))
            {
                originAuthority?.SetServerTrackingTarget(starterShuttle.transform);
            }

            if (starterShuttle.Rig != null &&
                starterShuttle.Rig.PilotSeatPoint != null)
            {
                PlaceExplorer(
                    player,
                    starterShuttle.Rig.PilotSeatPoint.position,
                    starterShuttle.Rig.PilotSeatPoint.rotation);
            }

            sessionPlayer.SetPossessionMode(PlayerPossessionMode.Spacecraft);
            return true;
        }

        bool AssignStarterShuttle(
            NetworkConnection connection,
            NetworkStarterShuttle starterShuttle)
        {
            if (connection == null ||
                starterShuttle == null ||
                !sessionPlayers.TryGetValue(
                    connection.ClientId,
                    out NetworkObject sessionObject) ||
                sessionObject == null)
            {
                return false;
            }

            NetworkSessionPlayer sessionPlayer =
                sessionObject.GetComponent<NetworkSessionPlayer>();
            if (sessionPlayer == null || !starterShuttle.EntityId.IsValid)
            {
                return false;
            }

            sessionPlayer.SetAssignedStarterShuttle(starterShuttle.EntityId);
            return true;
        }

        bool TryClaimStarterShuttle(
            NetworkSessionPlayer sessionPlayer,
            GeneratedEntityId requestedShipId,
            NetworkConnection connection,
            NetworkObject player,
            NetworkStarterShuttle starterShuttle)
        {
            if (sessionPlayer.ClaimedStarterShuttleId.IsValid ||
                !starterShuttle.TryGetInteriorPose(
                    out Vector3 interiorPosition,
                    out Quaternion interiorRotation) ||
                !starterShuttle.TryClaim(
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
            sessionPlayer.SetClaimedStarterShuttle(requestedShipId);
            sessionPlayer.SetPossessionMode(
                PlayerPossessionMode.ShipInterior);
            return true;
        }

        internal bool TryExitStarterShuttle(
            NetworkSessionPlayer sessionPlayer,
            GeneratedEntityId requestedShipId)
        {
            if (!TryResolveStarterShuttle(
                    sessionPlayer,
                    requestedShipId,
                    out _,
                    out NetworkObject player,
                    out NetworkStarterShuttle starterShuttle) ||
                sessionPlayer.PossessionMode != PlayerPossessionMode.Spacecraft ||
                sessionPlayer.ClaimedStarterShuttleId != requestedShipId ||
                !starterShuttle.IsPiloted ||
                !starterShuttle.IsClaimedBy(sessionPlayer.SessionPlayerId) ||
                !starterShuttle.TryGetExitPose(
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

            sessionPlayer.SetClaimedStarterShuttle(GeneratedEntityId.None);
            sessionPlayer.SetPossessionMode(PlayerPossessionMode.OnFoot);
            starterShuttle.ClearClaim();
            return true;
        }

        bool TryResolveStarterShuttle(
            NetworkSessionPlayer sessionPlayer,
            GeneratedEntityId requestedShipId,
            out NetworkConnection connection,
            out NetworkObject player,
            out NetworkStarterShuttle starterShuttle)
        {
            connection = sessionPlayer?.Owner;
            player = null;
            starterShuttle = null;
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
                !starterShuttles.TryGetValue(
                    player.gameObject.scene.handle,
                    out Dictionary<int, NetworkObject> ships) ||
                !ships.TryGetValue(slot, out NetworkObject ship) ||
                ship == null)
            {
                return false;
            }

            starterShuttle = ship.GetComponent<NetworkStarterShuttle>();
            return starterShuttle != null &&
                sessionPlayer.AssignedStarterShuttleId == requestedShipId &&
                starterShuttle.EntityId == requestedShipId;
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

        void ReleaseStarterShuttleClaims(int connectionId)
        {
            foreach (Dictionary<int, NetworkObject> ships in starterShuttles.Values)
            {
                foreach (NetworkObject ship in ships.Values)
                {
                    NetworkStarterShuttle starterShuttle =
                        ship != null
                            ? ship.GetComponent<NetworkStarterShuttle>()
                            : null;
                    if (starterShuttle != null)
                    {
                        ulong claimedSessionPlayerId =
                            starterShuttle.ClaimedBySessionPlayerId;
                        if (starterShuttle.ReleaseClaim(connectionId))
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
                    sessionPlayer.SetClaimedStarterShuttle(
                        GeneratedEntityId.None);
                    sessionPlayer.SetPossessionMode(
                        PlayerPossessionMode.OnFoot);
                    return;
                }
            }
        }

        void DespawnStarterShuttle(
            Dictionary<int, NetworkObject> ships,
            int slot)
        {
            if (!ships.Remove(slot, out NetworkObject ship) ||
                ship == null)
            {
                return;
            }

            NetworkStarterShuttle starterShuttle =
                ship.GetComponent<NetworkStarterShuttle>();
            PreserveStarterShuttleState(starterShuttle, slot);
            if (starterShuttle != null && starterShuttle.IsClaimed)
            {
                ulong claimedSessionPlayerId =
                    starterShuttle.ClaimedBySessionPlayerId;
                starterShuttle.ClearClaim();
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
