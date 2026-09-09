using Farion.Core.Identity;
using System;
using Farion.Gameplay.Actors;
using System.Collections.Generic;
using Farion.Gameplay.Commands;
using Farion.Gameplay.Definitions;
using Farion.Gameplay.Fleet;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Inventory;
using Farion.Core.Persistence;
using Farion.Gameplay.Persistence;
using Farion.Gameplay.Session;
using Farion.Gameplay.Ships;
using Farion.Multiplayer.Player;
using Farion.Multiplayer.Session;
using Farion.Multiplayer.World;
using Farion.Simulation.Physics;
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
        readonly Dictionary<int, PendingZoneHandoff> pendingHandoffs = new();
        readonly HashSet<string> failedPlayerRestores = new();
        readonly HashSet<int> failedShipRestores = new();
        readonly HashSet<int> spawnedAtRestoredPose = new();
        readonly Dictionary<int, ShuttleDockingBoundary> dockingBoundaries = new();
        ulong nextSessionPlayerId;
        MultiplayerWorldOriginAuthority originAuthority;
        ZonePhysicsTickDriver physicsTickDriver;
        GameplayDefinitionRegistry definitions;

        public int SpawnedPlayerCount => players.Count;

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

            if (TryGetStartingZoneBindings(out GameplayRuntimeBindings bindings))
            {
                registry = bindings.Definitions;
            }

            return registry != null;
        }

        internal bool TryGetRuntimeBindings(
            Scene scene,
            out GameplayRuntimeBindings bindings)
        {
            bindings = contexts.TryGetValue(
                    scene.handle,
                    out MultiplayerSceneContext context) &&
                context != null &&
                context.RuntimeRoot != null
                    ? context.RuntimeRoot.Bindings
                    : null;
            return bindings != null;
        }

        internal bool TryGetStartingZoneBindings(
            out GameplayRuntimeBindings bindings)
        {
            bindings = null;
            foreach (MultiplayerSceneContext context in contexts.Values)
            {
                if (context == null || context.RuntimeRoot == null)
                {
                    continue;
                }

                if (context.ZoneId == MultiplayerZoneCatalog.StartingZoneId)
                {
                    bindings = context.RuntimeRoot.Bindings;
                    return true;
                }

                bindings ??= context.RuntimeRoot.Bindings;
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
            BindOriginAuthority(worldOriginAuthority);
            BindDockingService(sceneContext);
            RefreshStarterShuttles(sceneContext);
            foreach (NetworkConnection connection in
                     networkManager.ServerManager.Clients.Values)
            {
                if (connection.Scenes.Contains(sceneContext.gameObject.scene))
                {
                    SpawnFor(connection, sceneContext);
                }
            }
        }

        public static bool CanRestore(
            IReadOnlyList<MultiplayerPlayerSaveEntry> players,
            IReadOnlyList<MultiplayerShipSaveEntry> shipCargo,
            GameplayDefinitionRegistry definitionRegistry)
        {
            if (definitionRegistry == null)
            {
                return players == null || players.Count == 0;
            }

            for (int i = 0; players != null && i < players.Count; i++)
            {
                MultiplayerPlayerSaveEntry entry = players[i];
                if (entry != null &&
                    entry.IsValid &&
                    !IsResolvable(entry.CarriedInventory, definitionRegistry))
                {
                    return false;
                }
            }

            for (int i = 0; shipCargo != null && i < shipCargo.Count; i++)
            {
                MultiplayerShipSaveEntry entry = shipCargo[i];
                if (entry != null &&
                    entry.IsValid &&
                    !IsResolvable(entry.Cargo, definitionRegistry))
                {
                    return false;
                }
            }

            return true;
        }

        static bool IsResolvable(
            InventoryContainerSnapshot snapshot,
            GameplayDefinitionRegistry definitionRegistry)
        {
            if (snapshot == null)
            {
                return true;
            }

            IReadOnlyList<InventoryStackSnapshot> stacks = snapshot.Stacks;
            for (int i = 0; i < stacks.Count; i++)
            {
                if (!stacks[i].IsValid ||
                    !definitionRegistry.TryGetInventoryItem(stacks[i].ItemId, out _))
                {
                    return false;
                }
            }

            return true;
        }

        internal string ResolveOwnedIdentity(string persistentPlayerId, string ownerProof)
        {
            if (!restoredPlayers.TryGetValue(
                    persistentPlayerId,
                    out MultiplayerPlayerSaveEntry entry) ||
                entry.OwnerProof.Length == 0 ||
                string.Equals(entry.OwnerProof, ownerProof, StringComparison.Ordinal))
            {
                return persistentPlayerId;
            }

            string derived =
                $"{persistentPlayerId}.{ownerProof[..Math.Min(12, ownerProof.Length)]}";
            Debug.LogWarning(
                $"Persistent id '{persistentPlayerId}' is owned by another proof; the connection continues as '{derived}'.",
                this);
            return derived;
        }

        public void LoadRestoredState(
            IReadOnlyList<MultiplayerPlayerSaveEntry> players,
            GameplayDefinitionRegistry definitionRegistry)
        {
            restoredPlayers.Clear();
            failedPlayerRestores.Clear();
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
                if (!inventory.ApplyContainerSnapshot(
                        entry.CarriedInventory.WithContainerId(inventory.ContainerId.Value),
                        registry))
                {
                    failedPlayerRestores.Add(sessionPlayer.PersistentPlayerId);
                    Debug.LogError(
                        $"Saved inventory for '{sessionPlayer.PersistentPlayerId}' could not be applied; the saved entry is kept and this session will not overwrite it.",
                        this);
                    return;
                }

                sessionPlayer.GetComponent<NetworkGameplayCommands>()
                    ?.PushOwnerInventory(inventory);
            }

            restoredPlayers.Remove(sessionPlayer.PersistentPlayerId);
            failedPlayerRestores.Remove(sessionPlayer.PersistentPlayerId);
            NetworkConnection owner = sessionPlayer.Owner;
            bool poseApplied = owner != null && spawnedAtRestoredPose.Remove(owner.ClientId);
            if (!poseApplied &&
                entry.HasExplorerPose &&
                entry.PossessionMode == PlayerPossessionMode.OnFoot &&
                IsStartingZoneEntry(entry.ZoneId))
            {
                PlayerExplorerPlacement.PlaceAtPose(
                    explorer.gameObject,
                    entry.ExplorerPose.Position,
                    entry.ExplorerPose.Rotation);
            }

            RestoreOccupancy(sessionPlayer, explorer.NetworkObject, entry.PossessionMode);
        }

        void RestoreOccupancy(
            NetworkSessionPlayer sessionPlayer,
            NetworkObject explorer,
            PlayerPossessionMode mode)
        {
            NetworkConnection connection = sessionPlayer.Owner;
            if (mode == PlayerPossessionMode.OnFoot ||
                connection == null ||
                !slots.TryGetReserved(connection.ClientId, out int slot) ||
                !TryGetStarterShuttleInSlot(slot, out NetworkStarterShuttle ship) ||
                ship.gameObject.scene != explorer.gameObject.scene)
            {
                return;
            }

            ship.RestoreOccupancy(sessionPlayer, connection, explorer, mode);
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
            int slot = slots.TryGetReserved(connectionId, out int reserved) ? reserved : -1;
            if (TryGetSpawnedExplorer(
                    sessionPlayer,
                    out NetworkExplorerController explorer))
            {
                InventoryContainerComponent inventory =
                    explorer.GetComponentInChildren<InventoryContainerComponent>(true);
                entry = new MultiplayerPlayerSaveEntry(
                    sessionPlayer.PersistentPlayerId,
                    sessionPlayer.DisplayName,
                    slot,
                    inventory != null ? inventory.CaptureContainerSnapshot() : null);
                entry.SetOwnerProof(sessionPlayer.OwnerProof);
                entry.SetExplorerPose(
                    TransformPoseSnapshot.Capture(
                        explorer.GetComponent<Rigidbody>(),
                        explorer.transform));
                entry.SetPossessionMode(sessionPlayer.PossessionMode);
                entry.SetZone(ResolveZoneIdValue(explorer.gameObject.scene));
                return true;
            }

            if (!pendingHandoffs.TryGetValue(connectionId, out PendingZoneHandoff handoff))
            {
                return false;
            }

            entry = new MultiplayerPlayerSaveEntry(
                sessionPlayer.PersistentPlayerId,
                sessionPlayer.DisplayName,
                slot,
                handoff.CarriedInventory);
            entry.SetOwnerProof(sessionPlayer.OwnerProof);
            entry.SetPossessionMode(handoff.Snapshot.PossessionMode);
            entry.SetZone(handoff.ZoneId.Value);
            return true;
        }

        ulong ResolveZoneIdValue(Scene scene)
        {
            return contexts.TryGetValue(
                    scene.handle,
                    out MultiplayerSceneContext context) &&
                context != null
                    ? context.ZoneId.Value
                    : 0UL;
        }

        static bool IsStartingZoneEntry(ulong zoneIdValue)
        {
            return zoneIdValue == 0UL ||
                zoneIdValue == MultiplayerZoneCatalog.StartingZoneId.Value;
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
                    !failedPlayerRestores.Contains(sessionPlayer.PersistentPlayerId) &&
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
                    if (ship == null ||
                        ship.Cargo == null ||
                        ship.Motor == null ||
                        failedShipRestores.Contains(pair.Key))
                    {
                        continue;
                    }

                    MultiplayerShipSaveEntry shipEntry = CreateShipEntry(ship, pair.Key);
                    if (shipEntry.HasOwner)
                    {
                        capturedOwners.Add(shipEntry.PersistentPlayerId);
                    }

                    capturedSlots.Add(pair.Key);
                    shipCargo.Add(shipEntry);
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

        MultiplayerShipSaveEntry CreateShipEntry(NetworkStarterShuttle ship, int slot)
        {
            MultiplayerShipSaveEntry entry = new(
                ResolveSlotOwner(slot),
                slot,
                ship.Cargo.CaptureContainerSnapshot(),
                ship.Motor.Fuel,
                ship.Hull != null ? ship.Hull.Integrity : default);
            entry.SetShipPose(
                TransformPoseSnapshot.Capture(
                    ship.GetComponent<Rigidbody>(),
                    ship.transform));
            entry.SetZone(ResolveZoneIdValue(ship.gameObject.scene));
            return entry;
        }

        public void LoadRestoredShipCargo(
            IReadOnlyList<MultiplayerShipSaveEntry> shipCargo)
        {
            restoredShipCargo.Clear();
            failedShipRestores.Clear();
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

                if (entry.Cargo != null &&
                    !starterShuttle.Cargo.ApplyContainerSnapshot(
                        entry.Cargo.WithContainerId(starterShuttle.Cargo.ContainerId.Value),
                        registry))
                {
                    failedShipRestores.Add(slot);
                    Debug.LogError(
                        $"Saved cargo for formation slot {slot + 1} could not be applied; the saved entry is kept and this session will not overwrite it.",
                        this);
                    continue;
                }

                failedShipRestores.Remove(slot);
                if (entry.HasFuel && starterShuttle.Motor != null)
                {
                    starterShuttle.Motor.RestoreFuel(entry.Fuel);
                }

                if (entry.HasHull && starterShuttle.Hull != null)
                {
                    starterShuttle.Hull.RestoreIntegrity(entry.Hull);
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

            MultiplayerShipSaveEntry entry = CreateShipEntry(starterShuttle, slot);
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

        void BindOriginAuthority(MultiplayerWorldOriginAuthority authority)
        {
            if (originAuthority == authority)
            {
                return;
            }

            if (originAuthority != null)
            {
                originAuthority.OriginShifted -= HandleOriginShifted;
            }

            originAuthority = authority;
            if (originAuthority != null)
            {
                originAuthority.OriginShifted += HandleOriginShifted;
            }
        }

        void HandleOriginShifted(
            GeneratedEntityId zoneId,
            Vector3 originOffset)
        {
            if (zoneId != MultiplayerZoneCatalog.StartingZoneId)
            {
                return;
            }

            foreach (MultiplayerPlayerSaveEntry entry in restoredPlayers.Values)
            {
                entry?.ShiftPose(originOffset);
            }

            for (int i = 0; i < restoredShipCargo.Count; i++)
            {
                restoredShipCargo[i]?.ShiftPose(originOffset);
            }
        }

        bool TryResolveRestoredSpawnPose(
            NetworkSessionPlayer sessionPlayer,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (sessionPlayer == null ||
                string.IsNullOrEmpty(sessionPlayer.PersistentPlayerId) ||
                !restoredPlayers.TryGetValue(
                    sessionPlayer.PersistentPlayerId,
                    out MultiplayerPlayerSaveEntry entry) ||
                entry == null)
            {
                return false;
            }

            if (!IsStartingZoneEntry(entry.ZoneId))
            {
                return false;
            }

            if (entry.PossessionMode != PlayerPossessionMode.OnFoot &&
                TryResolveRestoredShipPose(
                    entry.PersistentPlayerId,
                    out TransformPoseSnapshot shipPose))
            {
                position = shipPose.Position;
                rotation = shipPose.Rotation;
                return true;
            }

            if (!entry.HasExplorerPose)
            {
                return false;
            }

            position = entry.ExplorerPose.Position;
            rotation = entry.ExplorerPose.Rotation;
            return true;
        }

        bool TryResolveRestoredSlotShipPose(
            int slot,
            out TransformPoseSnapshot pose)
        {
            string owner = ResolveSlotOwner(slot);
            for (int i = 0; i < restoredShipCargo.Count; i++)
            {
                MultiplayerShipSaveEntry entry = restoredShipCargo[i];
                if (entry == null ||
                    !entry.HasShipPose ||
                    !IsStartingZoneEntry(entry.ZoneId))
                {
                    continue;
                }

                bool matches = entry.HasOwner && !string.IsNullOrEmpty(owner)
                    ? entry.PersistentPlayerId == owner
                    : entry.FormationSlot == slot;
                if (matches)
                {
                    pose = entry.ShipPose;
                    return true;
                }
            }

            pose = default;
            return false;
        }

        bool TryResolveRestoredShipPose(
            string persistentPlayerId,
            out TransformPoseSnapshot pose)
        {
            for (int i = 0; i < restoredShipCargo.Count; i++)
            {
                MultiplayerShipSaveEntry entry = restoredShipCargo[i];
                if (entry != null &&
                    entry.HasShipPose &&
                    IsStartingZoneEntry(entry.ZoneId) &&
                    entry.PersistentPlayerId == persistentPlayerId)
                {
                    pose = entry.ShipPose;
                    return true;
                }
            }

            pose = default;
            return false;
        }

        ZonePhysicsTickDriver ResolvePhysicsTickDriver()
        {
            return physicsTickDriver != null
                ? physicsTickDriver
                : physicsTickDriver = GetComponent<ZonePhysicsTickDriver>();
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
            pendingHandoffs.Clear();
            failedPlayerRestores.Clear();
            failedShipRestores.Clear();
            spawnedAtRestoredPose.Clear();
            foreach (ShuttleDockingBoundary boundary in dockingBoundaries.Values)
            {
                if (boundary != null)
                {
                    boundary.ShuttleDocked -= HandleShuttleDocked;
                }
            }

            dockingBoundaries.Clear();
            definitions = null;
            BindOriginAuthority(null);
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
                RefreshStarterShuttles(sceneContext);
                SpawnFor(args.Connection, sceneContext);
                return;
            }

            DespawnAvatar(args.Connection, args.Scene);
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

            NetworkSessionPlayer sessionPlayer =
                sessionPlayers[connection.ClientId]
                    .GetComponent<NetworkSessionPlayer>();
            bool hasHandoff = pendingHandoffs.TryGetValue(
                    connection.ClientId,
                    out PendingZoneHandoff handoff) &&
                handoff.ZoneId == sceneContext.ZoneId &&
                sceneContext.GravitySimulation != null;
            Vector3 handoffVelocity = Vector3.zero;
            if (hasHandoff && handoff.ReturnToFormation)
            {
            }
            else if (hasHandoff)
            {
                sceneContext.GravitySimulation.TrySystemToWorld(
                    handoff.Snapshot.ExplorerSystemPosition,
                    handoff.Snapshot.ExplorerSystemVelocity,
                    out position,
                    out handoffVelocity);
                rotation = sceneContext.GravitySimulation.SystemToWorldRotation(
                    handoff.Snapshot.ExplorerSystemRotation);
            }
            else if (TryResolveRestoredSpawnPose(
                    sessionPlayer,
                    out Vector3 restoredPosition,
                    out Quaternion restoredRotation))
            {
                position = restoredPosition;
                rotation = restoredRotation;
                spawnedAtRestoredPose.Add(connection.ClientId);
            }

            originAuthority?.SendCurrentOrigin(connection, sceneContext.ZoneId);
            ResolvePhysicsTickDriver()?.SendSimulationEpoch(connection);
            NetworkObject player = Instantiate(playerPrefab, position, rotation);
            NetworkExplorerController explorer =
                player.GetComponent<NetworkExplorerController>();
            explorer?.InitializeIdentity(sessionPlayer.SessionPlayerId);
            explorer?.BindScene(
                sceneContext.CelestialFrameProvider,
                sceneContext.ZoneOrigin);
            networkManager.ServerManager.Spawn(
                player,
                connection,
                sceneContext.gameObject.scene);
            sceneContext.AttachToShiftedWorld(player.transform);
            players.Add(connection.ClientId, player);
            connection.SetFirstObject(player);
            if (IsHostConnection(connection))
            {
                originAuthority?.SetServerTrackingTarget(
                    sceneContext.ZoneId,
                    player.transform);
            }

            if (hasHandoff)
            {
                pendingHandoffs.Remove(connection.ClientId);
                ApplyHandoffInventory(connection, player, handoff.CarriedInventory);
                CompleteHandoff(
                    connection,
                    sceneContext,
                    handoff.Snapshot,
                    player,
                    handoffVelocity);
                return;
            }

            RestorePlayerState(sessionPlayer);
        }

        void ApplyHandoffInventory(
            NetworkConnection connection,
            NetworkObject player,
            InventoryContainerSnapshot snapshot)
        {
            if (snapshot == null ||
                !TryResolveDefinitions(out GameplayDefinitionRegistry registry))
            {
                return;
            }

            InventoryContainerComponent inventory =
                player.GetComponentInChildren<InventoryContainerComponent>(true);
            if (inventory == null)
            {
                return;
            }

            if (!inventory.ApplyContainerSnapshot(
                    snapshot.WithContainerId(inventory.ContainerId.Value),
                    registry))
            {
                Debug.LogError(
                    $"Handoff inventory for connection {connection.ClientId} could not be applied.",
                    this);
            }

            if (sessionPlayers.TryGetValue(
                    connection.ClientId,
                    out NetworkObject sessionObject) &&
                sessionObject != null)
            {
                sessionObject.GetComponent<NetworkGameplayCommands>()
                    ?.PushOwnerInventory(inventory);
            }
        }

        internal bool IsHandoffPending(int connectionId)
        {
            return pendingHandoffs.ContainsKey(connectionId);
        }

        internal bool PrepareHandoff(
            NetworkConnection connection,
            MultiplayerSceneContext sourceContext,
            GeneratedEntityId targetZoneId,
            bool returnToFormation = false)
        {
            if (!networkManager.IsServerStarted ||
                connection == null ||
                sourceContext == null ||
                sourceContext.GravitySimulation == null ||
                pendingHandoffs.ContainsKey(connection.ClientId) ||
                !players.TryGetValue(
                    connection.ClientId,
                    out NetworkObject playerObject) ||
                playerObject == null ||
                !sessionPlayers.TryGetValue(
                    connection.ClientId,
                    out NetworkObject sessionObject) ||
                sessionObject == null)
            {
                return false;
            }

            GravitySimulation sourceSimulation = sourceContext.GravitySimulation;
            NetworkSessionPlayer sessionPlayer =
                sessionObject.GetComponent<NetworkSessionPlayer>();
            Rigidbody playerBody = playerObject.GetComponent<Rigidbody>();
            if (!sourceSimulation.TryWorldToSystem(
                    playerObject.transform.position,
                    playerBody != null ? playerBody.linearVelocity : Vector3.zero,
                    out Vector3 explorerSystemPosition,
                    out Vector3 explorerSystemVelocity))
            {
                return false;
            }

            Quaternion explorerSystemRotation =
                sourceSimulation.WorldToSystemRotation(
                    playerObject.transform.rotation);
            InventoryContainerComponent carriedInventory = playerObject
                .GetComponentInChildren<InventoryContainerComponent>(true);
            InventoryContainerSnapshot carriedSnapshot =
                carriedInventory != null
                    ? carriedInventory.CaptureContainerSnapshot()
                    : null;

            GeneratedEntityId shipId = GeneratedEntityId.None;
            int shipSlot = -1;
            Vector3 shipSystemPosition = default;
            Vector3 shipSystemVelocity = default;
            Quaternion shipSystemRotation = Quaternion.identity;
            if (sessionPlayer != null &&
                sessionPlayer.ClaimedStarterShuttleId.IsValid &&
                TryResolveClaimedShuttle(
                    sessionPlayer,
                    connection.ClientId,
                    out NetworkStarterShuttle shuttle,
                    out shipSlot) &&
                shuttle.gameObject.scene == sourceContext.gameObject.scene)
            {
                Rigidbody shipBody = shuttle.GetComponent<Rigidbody>();
                if (!sourceSimulation.TryWorldToSystem(
                        shuttle.transform.position,
                        shipBody != null ? shipBody.linearVelocity : Vector3.zero,
                        out shipSystemPosition,
                        out shipSystemVelocity))
                {
                    return false;
                }

                shipSystemRotation = sourceSimulation.WorldToSystemRotation(
                    shuttle.transform.rotation);
                shipId = shuttle.EntityId;
                PreserveStarterShuttleState(shuttle, shipSlot);
                if (starterShuttles.TryGetValue(
                        sourceContext.gameObject.scene.handle,
                        out Dictionary<int, NetworkObject> sourceShips))
                {
                    sourceShips.Remove(shipSlot);
                }

                NetworkObject shuttleObject = shuttle.NetworkObject;
                if (shuttleObject != null && shuttleObject.IsSpawned)
                {
                    networkManager.ServerManager.Despawn(shuttleObject);
                }
            }

            pendingHandoffs[connection.ClientId] = new PendingZoneHandoff(
                targetZoneId,
                new ZoneHandoffSnapshot(
                    explorerSystemPosition,
                    explorerSystemVelocity,
                    explorerSystemRotation,
                    sessionPlayer != null
                        ? sessionPlayer.PossessionMode
                        : PlayerPossessionMode.OnFoot,
                    shipId,
                    shipSlot,
                    shipSystemPosition,
                    shipSystemVelocity,
                    shipSystemRotation),
                carriedSnapshot,
                returnToFormation);
            return true;
        }

        bool TryResolveClaimedShuttle(
            NetworkSessionPlayer sessionPlayer,
            int connectionId,
            out NetworkStarterShuttle shuttle,
            out int slot)
        {
            shuttle = null;
            if (!slots.TryGetReserved(connectionId, out slot))
            {
                return false;
            }

            return TryGetStarterShuttleInSlot(slot, out shuttle) &&
                shuttle.EntityId == sessionPlayer.ClaimedStarterShuttleId;
        }

        void CompleteHandoff(
            NetworkConnection connection,
            MultiplayerSceneContext sceneContext,
            ZoneHandoffSnapshot snapshot,
            NetworkObject playerObject,
            Vector3 explorerVelocity)
        {
            Rigidbody playerBody = playerObject.GetComponent<Rigidbody>();
            if (playerBody != null && !playerBody.isKinematic)
            {
                playerBody.linearVelocity = explorerVelocity;
            }

            if (!snapshot.HasShip ||
                starterShuttlePrefab == null ||
                sceneContext.GravitySimulation == null)
            {
                return;
            }

            sceneContext.GravitySimulation.TrySystemToWorld(
                snapshot.ShipSystemPosition,
                snapshot.ShipSystemVelocity,
                out Vector3 shipPosition,
                out Vector3 shipVelocity);
            Quaternion shipRotation =
                sceneContext.GravitySimulation.SystemToWorldRotation(
                    snapshot.ShipSystemRotation);
            NetworkStarterShuttle starterShuttle = SpawnStarterShuttle(
                sceneContext,
                snapshot.ShipId,
                snapshot.ShipSlot,
                shipPosition,
                shipRotation);
            if (starterShuttle == null)
            {
                return;
            }

            ApplyRestoredShipCargo(restoredShipCargo);
            NetworkSessionPlayer sessionPlayer =
                sessionPlayers.TryGetValue(
                    connection.ClientId,
                    out NetworkObject sessionObject) &&
                sessionObject != null
                    ? sessionObject.GetComponent<NetworkSessionPlayer>()
                    : null;
            if (sessionPlayer != null)
            {
                AssignStarterShuttle(connection, starterShuttle);
                starterShuttle.RestoreOccupancy(
                    sessionPlayer,
                    connection,
                    playerObject,
                    snapshot.PossessionMode);
            }

            Rigidbody shipBody = starterShuttle.GetComponent<Rigidbody>();
            if (shipBody != null && !shipBody.isKinematic)
            {
                shipBody.linearVelocity = shipVelocity;
            }
        }

        NetworkStarterShuttle SpawnStarterShuttle(
            MultiplayerSceneContext sceneContext,
            GeneratedEntityId shipId,
            int slot,
            Vector3 position,
            Quaternion rotation)
        {
            NetworkObject ship = Instantiate(starterShuttlePrefab, position, rotation);
            NetworkStarterShuttle starterShuttle =
                ship.GetComponent<NetworkStarterShuttle>();
            if (starterShuttle == null)
            {
                Destroy(ship.gameObject);
                return null;
            }

            starterShuttle.Initialize(shipId, slot);
            starterShuttle.BindScene(
                sceneContext.GravitySimulation,
                sceneContext.CelestialFrameProvider,
                sceneContext.ZoneOrigin);
            starterShuttle.OccupancyChanged += HandleOccupancyChanged;
            Scene scene = sceneContext.gameObject.scene;
            networkManager.ServerManager.Spawn(ship, null, scene);
            sceneContext.AttachToShiftedWorld(ship.transform);
            if (!starterShuttles.TryGetValue(
                    scene.handle,
                    out Dictionary<int, NetworkObject> ships))
            {
                ships = new Dictionary<int, NetworkObject>(MaximumPlayers);
                starterShuttles.Add(scene.handle, ships);
            }

            ships[slot] = ship;
            return starterShuttle;
        }

        void HandleOccupancyChanged(
            NetworkStarterShuttle ship,
            NetworkSessionPlayer sessionPlayer,
            NetworkObject explorer,
            PlayerPossessionMode mode)
        {
            NetworkConnection connection = sessionPlayer != null ? sessionPlayer.Owner : null;
            if (!IsHostConnection(connection))
            {
                return;
            }

            Component target = mode == PlayerPossessionMode.Spacecraft
                ? ship
                : explorer;
            SetHostTrackingTarget(target);
        }

        readonly struct PendingZoneHandoff
        {
            public PendingZoneHandoff(
                GeneratedEntityId zoneId,
                ZoneHandoffSnapshot snapshot,
                InventoryContainerSnapshot carriedInventory,
                bool returnToFormation)
            {
                ZoneId = zoneId;
                Snapshot = snapshot;
                CarriedInventory = carriedInventory;
                ReturnToFormation = returnToFormation;
            }

            public GeneratedEntityId ZoneId { get; }
            public ZoneHandoffSnapshot Snapshot { get; }
            public InventoryContainerSnapshot CarriedInventory { get; }
            public bool ReturnToFormation { get; }
        }

        void SetHostTrackingTarget(Component target)
        {
            if (originAuthority == null ||
                target == null ||
                !contexts.TryGetValue(
                    target.gameObject.scene.handle,
                    out MultiplayerSceneContext context) ||
                context == null)
            {
                return;
            }

            originAuthority.SetServerTrackingTarget(
                context.ZoneId,
                target.transform);
        }

        void OnRemoteConnectionState(
            NetworkConnection connection,
            RemoteConnectionStateArgs args)
        {
            if (connection == null ||
                args.ConnectionState != RemoteConnectionState.Stopped)
            {
                return;
            }

            PreservePlayerState(args.ConnectionId);
            ReleaseStarterShuttleClaims(args.ConnectionId);
            RemoveStarterShuttle(args.ConnectionId);
            sessionPlayers.Remove(args.ConnectionId);
            players.Remove(args.ConnectionId);
            pendingHandoffs.Remove(args.ConnectionId);
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
            if (dockingBoundaries.Remove(scene.handle, out ShuttleDockingBoundary boundary) &&
                boundary != null)
            {
                boundary.ShuttleDocked -= HandleShuttleDocked;
            }
        }

        void BindDockingService(MultiplayerSceneContext sceneContext)
        {
            int handle = sceneContext.gameObject.scene.handle;
            if (dockingBoundaries.ContainsKey(handle))
            {
                return;
            }

            FleetRuntime fleet = sceneContext.RuntimeRoot != null &&
                sceneContext.RuntimeRoot.Bindings != null
                    ? sceneContext.RuntimeRoot.Bindings.Fleet
                    : null;
            ShuttleDockingBoundary boundary = fleet != null
                ? fleet.GetComponentInChildren<ShuttleDockingBoundary>(true)
                : null;
            if (boundary == null)
            {
                return;
            }

            boundary.ShuttleDocked += HandleShuttleDocked;
            dockingBoundaries.Add(handle, boundary);
        }

        void HandleShuttleDocked(ShuttleRuntimeBinding binding)
        {
            if (networkManager.IsServerStarted &&
                binding != null &&
                binding.TryGetComponent(out NetworkStarterShuttle ship))
            {
                ship.ServiceAtDock();
            }
        }

        internal bool TryRecallStarterShuttle(
            NetworkSessionPlayer sessionPlayer,
            MultiplayerZoneCoordinator zoneCoordinator,
            out ShuttleRecallResult result)
        {
            result = ShuttleRecallResult.Failed;
            NetworkConnection connection = sessionPlayer != null ? sessionPlayer.Owner : null;
            if (!networkManager.IsServerStarted ||
                connection == null ||
                !connection.IsActive ||
                !slots.TryGetReserved(connection.ClientId, out int slot) ||
                !TryResolveDefinitions(out GameplayDefinitionRegistry registry) ||
                !TryGetSpawnedExplorer(sessionPlayer, out NetworkExplorerController explorer) ||
                !contexts.TryGetValue(
                    explorer.gameObject.scene.handle,
                    out MultiplayerSceneContext context))
            {
                return false;
            }

            if (!TryGetStarterShuttleInSlot(slot, out NetworkStarterShuttle ship) ||
                ship.EntityId != sessionPlayer.AssignedStarterShuttleId ||
                ship.Motor == null)
            {
                result = ShuttleRecallResult.MissingShuttle;
                return false;
            }

            if (!ship.IsStranded)
            {
                result = ShuttleRecallResult.NotStranded;
                return false;
            }

            ship.Cargo.ApplyContainerSnapshot(
                new InventoryContainerSnapshot(
                    ship.Cargo.ContainerId.Value,
                    ship.Cargo.SlotCapacity,
                    Array.Empty<InventoryStackSnapshot>()),
                registry);
            ship.ServiceAtDock();
            if (starterShuttles.TryGetValue(
                    ship.gameObject.scene.handle,
                    out Dictionary<int, NetworkObject> ships))
            {
                DespawnStarterShuttle(ships, slot);
            }

            ClearRestoredShipPose(slot, sessionPlayer.PersistentPlayerId);
            if (context.ZoneId == MultiplayerZoneCatalog.StartingZoneId)
            {
                if (context.TryGetSpawnPose(slot, out Vector3 position, out Quaternion rotation))
                {
                    PlayerExplorerPlacement.PlaceAtPose(explorer.gameObject, position, rotation);
                }

                RefreshStarterShuttles(context);
            }
            else if (zoneCoordinator == null ||
                     !zoneCoordinator.BeginReturnToStartingZone(connection))
            {
                return false;
            }

            result = ShuttleRecallResult.Succeeded;
            return true;
        }

        void ClearRestoredShipPose(int slot, string owner)
        {
            for (int i = 0; i < restoredShipCargo.Count; i++)
            {
                MultiplayerShipSaveEntry entry = restoredShipCargo[i];
                bool matches = entry.HasOwner && !string.IsNullOrEmpty(owner)
                    ? entry.PersistentPlayerId == owner
                    : entry.FormationSlot == slot;
                if (matches)
                {
                    entry.ClearShipPose();
                }
            }
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
                    !EnsureSessionPlayer(connection) ||
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

                if (sceneContext.ZoneId != MultiplayerZoneCatalog.StartingZoneId)
                {
                    continue;
                }

                if (!sceneContext.TryGetStarterShuttlePose(
                        slot,
                        out Vector3 position,
                        out Quaternion rotation))
                {
                    Debug.LogError(
                        $"Starter ship formation slot {slot + 1} is invalid.",
                        sceneContext);
                    failedConnections.Add(connection);
                    continue;
                }

                bool restoredPose = TryResolveRestoredSlotShipPose(
                    slot,
                    out TransformPoseSnapshot restoredShipPose);
                if (restoredPose)
                {
                    position = restoredShipPose.Position;
                    rotation = restoredShipPose.Rotation;
                }

                GeneratedEntityId shipId = GeneratedEntityId.FromHash(
                    StableHashUtility.Combine(
                        sceneContext.ZoneId.Value,
                        "starter_ship",
                        slot));
                NetworkStarterShuttle starterShuttle = SpawnStarterShuttle(
                    sceneContext,
                    shipId,
                    slot,
                    position,
                    rotation);
                if (starterShuttle == null)
                {
                    failedConnections.Add(connection);
                    continue;
                }

                if (!restoredPose)
                {
                    CelestialSurfaceSettling.TrySettle(
                        sceneContext.CelestialFrameProvider,
                        starterShuttle.transform);
                }

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
                if (activeSlots.Contains(pair.Key))
                {
                    continue;
                }

                NetworkStarterShuttle staleShuttle = pair.Value != null
                    ? pair.Value.GetComponent<NetworkStarterShuttle>()
                    : null;
                if (staleShuttle == null || !staleShuttle.IsClaimed)
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

        internal bool TryBoardStarterShuttle(
            NetworkSessionPlayer sessionPlayer,
            GeneratedEntityId requestedShipId)
        {
            return TryResolveStarterShuttle(
                    sessionPlayer,
                    requestedShipId,
                    out NetworkConnection connection,
                    out NetworkObject player,
                    out NetworkStarterShuttle starterShuttle) &&
                starterShuttle.TryBoard(sessionPlayer, connection, player);
        }

        internal bool TryPilotStarterShuttle(
            NetworkSessionPlayer sessionPlayer,
            GeneratedEntityId requestedShipId)
        {
            return TryResolveStarterShuttle(
                    sessionPlayer,
                    requestedShipId,
                    out _,
                    out NetworkObject player,
                    out NetworkStarterShuttle starterShuttle) &&
                starterShuttle.TryTakePilotSeat(sessionPlayer, player);
        }

        internal bool TryLeavePilotSeat(
            NetworkSessionPlayer sessionPlayer,
            GeneratedEntityId requestedShipId)
        {
            return TryResolveStarterShuttle(
                    sessionPlayer,
                    requestedShipId,
                    out _,
                    out NetworkObject player,
                    out NetworkStarterShuttle starterShuttle) &&
                starterShuttle.TryLeavePilotSeat(sessionPlayer, player);
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
                    starterShuttle?.ReleaseClaim(connectionId);
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
            if (starterShuttle != null)
            {
                starterShuttle.OccupancyChanged -= HandleOccupancyChanged;
                if (starterShuttle.IsClaimed)
                {
                    starterShuttle.ClearClaim();
                }
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
