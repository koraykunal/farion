using Farion.Core.Identity;
using System.Collections.Generic;
using Farion.Multiplayer.Session;
using Farion.Multiplayer.Spawning;
using Farion.Simulation.Physics;
using Farion.Simulation.World;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Multiplayer.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    [RequireComponent(typeof(ZonePhysicsTickDriver))]
    public sealed class MultiplayerZoneCoordinator : MonoBehaviour
    {
        [SerializeField] NetworkManager networkManager;
        [SerializeField] ZonePhysicsTickDriver physicsTickDriver;
        [SerializeField] MultiplayerWorldOriginAuthority originAuthority;
        [SerializeField] MultiplayerPlayerSpawner playerSpawner;

        readonly Dictionary<GeneratedEntityId, SimulationZoneContext> zones = new();
        readonly Dictionary<GeneratedEntityId, int> pendingPinBodies = new();
        readonly Dictionary<GeneratedEntityId, List<NetworkConnection>> pendingZoneJoins = new();
        readonly HashSet<GeneratedEntityId> pendingZoneLoads = new();
        string zoneSceneName;
        int startingBodyStableId;
        bool subscribed;

        public int LoadedZoneCount => zones.Count;

        void Awake()
        {
            networkManager ??= GetComponent<NetworkManager>();
            physicsTickDriver ??= GetComponent<ZonePhysicsTickDriver>();
            originAuthority ??= GetComponent<MultiplayerWorldOriginAuthority>();
            playerSpawner ??= GetComponent<MultiplayerPlayerSpawner>();
        }

        void Start()
        {
            InstallSceneProcessor();
            Subscribe();
        }

        void InstallSceneProcessor()
        {
            if (networkManager?.SceneManager == null ||
                networkManager.SceneManager.GetSceneProcessor()
                    is FarionZoneSceneProcessor)
            {
                return;
            }

            SceneProcessorBase previous =
                networkManager.SceneManager.GetSceneProcessor();
            FarionZoneSceneProcessor processor =
                gameObject.AddComponent<FarionZoneSceneProcessor>();
            processor.Initialize(networkManager.SceneManager);
            networkManager.SceneManager.SetSceneProcessor(processor);
            if (previous != null && previous.GetType() == typeof(DefaultSceneProcessor))
            {
                Destroy(previous);
            }
        }

        void OnDestroy()
        {
            Unsubscribe();
        }

        public bool LoadZoneForConnection(
            NetworkConnection connection,
            string sceneName,
            GeneratedEntityId zoneId)
        {
            if (!networkManager.IsServerStarted ||
                connection == null ||
                !connection.IsActive)
            {
                return false;
            }

            zoneSceneName = sceneName;
            QueueZoneLoad(connection, zoneId);
            return true;
        }

        void QueueZoneLoad(NetworkConnection connection, GeneratedEntityId zoneId)
        {
            if (zones.TryGetValue(zoneId, out SimulationZoneContext existingZone) &&
                existingZone != null &&
                existingZone.Scene.IsValid() &&
                existingZone.Scene.isLoaded)
            {
                networkManager.SceneManager.LoadConnectionScenes(
                    connection,
                    MultiplayerZoneSceneLoad.Create(existingZone.Scene, zoneId));
                return;
            }

            if (!pendingZoneLoads.Add(zoneId))
            {
                if (!pendingZoneJoins.TryGetValue(
                        zoneId,
                        out List<NetworkConnection> joins))
                {
                    joins = new List<NetworkConnection>();
                    pendingZoneJoins[zoneId] = joins;
                }

                joins.Add(connection);
                return;
            }

            networkManager.SceneManager.LoadConnectionScenes(
                connection,
                MultiplayerZoneSceneLoad.Create(zoneSceneName, zoneId));
        }

        void FlushPendingZoneJoins(GeneratedEntityId zoneId, Scene scene)
        {
            pendingZoneLoads.Remove(zoneId);
            if (!pendingZoneJoins.Remove(zoneId, out List<NetworkConnection> joins))
            {
                return;
            }

            for (int i = 0; i < joins.Count; i++)
            {
                NetworkConnection connection = joins[i];
                if (connection != null &&
                    connection.IsActive &&
                    !connection.Scenes.Contains(scene))
                {
                    networkManager.SceneManager.LoadConnectionScenes(
                        connection,
                        MultiplayerZoneSceneLoad.Create(scene, zoneId));
                }
            }
        }

        void Subscribe()
        {
            if (subscribed || networkManager?.SceneManager == null)
            {
                return;
            }

            subscribed = true;
            networkManager.SceneManager.OnLoadEnd += SceneManager_OnLoadEnd;
            networkManager.SceneManager.OnClientPresenceChangeEnd +=
                SceneManager_OnClientPresenceChangeEnd;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded +=
                UnitySceneManager_OnSceneUnloaded;
        }

        void Unsubscribe()
        {
            if (!subscribed || networkManager?.SceneManager == null)
            {
                return;
            }

            subscribed = false;
            networkManager.SceneManager.OnLoadEnd -= SceneManager_OnLoadEnd;
            networkManager.SceneManager.OnClientPresenceChangeEnd -=
                SceneManager_OnClientPresenceChangeEnd;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -=
                UnitySceneManager_OnSceneUnloaded;
        }

        void SceneManager_OnLoadEnd(SceneLoadEndEventArgs args)
        {
            if (!MultiplayerZoneSceneLoad.TryReadZoneId(args, out GeneratedEntityId zoneId))
            {
                return;
            }

            for (int i = 0; i < args.LoadedScenes.Length; i++)
            {
                Scene scene = args.LoadedScenes[i];
                SimulationZoneContext context = FindZoneContext(scene);
                if (context == null)
                {
                    Debug.LogError(
                        $"Zone scene '{scene.name}' has no {nameof(SimulationZoneContext)}.",
                        this);
                    continue;
                }

                context.Configure(zoneId);
                if (!context.IsDrivenBy(physicsTickDriver) &&
                    !physicsTickDriver.RegisterZone(context))
                {
                    Debug.LogError(
                        $"Zone '{zoneId}' did not load with an isolated 3D PhysicsScene.",
                        context);
                    continue;
                }

                if (!args.QueueData.AsServer)
                {
                    continue;
                }

                if (zones.TryGetValue(zoneId, out SimulationZoneContext existing) &&
                    existing != null &&
                    existing != context)
                {
                    Debug.LogError(
                        $"Zone id '{zoneId}' is already assigned to scene '{existing.Scene.name}'.",
                        context);
                    continue;
                }

                zones[zoneId] = context;
                ApplyZonePin(zoneId, scene);
                FlushPendingZoneJoins(zoneId, scene);
                foreach (NetworkConnection connection in
                         networkManager.ServerManager.Clients.Values)
                {
                    if (connection.Scenes.Contains(scene))
                    {
                        SetConnectionZone(connection, zoneId);
                    }
                }
            }
        }

        void ApplyZonePin(GeneratedEntityId zoneId, Scene scene)
        {
            if (!pendingPinBodies.Remove(zoneId, out int bodyStableId))
            {
                return;
            }

            MultiplayerSceneContext sceneContext =
                MultiplayerSceneContext.FindIn(scene);
            if (sceneContext == null ||
                sceneContext.GravitySimulation == null ||
                !sceneContext.GravitySimulation
                    .ApplyNetworkPhysicsReferenceBody(bodyStableId))
            {
                Debug.LogError(
                    $"Zone '{zoneId}' could not pin celestial body '{bodyStableId}'.",
                    this);
                return;
            }

            originAuthority?.PinReferenceBody(zoneId, bodyStableId);
        }

        public bool BeginHandoff(
            NetworkConnection connection,
            CelestialBody targetBody)
        {
            if (!networkManager.IsServerStarted ||
                connection == null ||
                !connection.IsActive ||
                targetBody == null ||
                playerSpawner == null ||
                string.IsNullOrEmpty(zoneSceneName))
            {
                return false;
            }

            MultiplayerSceneContext sourceContext =
                ResolveConnectionContext(connection);
            if (sourceContext == null || !sourceContext.ZoneId.IsValid)
            {
                return false;
            }

            GeneratedEntityId targetZoneId = MultiplayerZoneCatalog.ZoneIdForBody(
                targetBody.StableId,
                ResolveStartingBodyStableId());
            if (!targetZoneId.IsValid || targetZoneId == sourceContext.ZoneId)
            {
                return false;
            }

            if (!playerSpawner.PrepareHandoff(
                    connection,
                    sourceContext,
                    targetZoneId))
            {
                return false;
            }

            networkManager.SceneManager.UnloadConnectionScenes(
                connection,
                new SceneUnloadData(sourceContext.gameObject.scene));

            bool zoneReady = zones.TryGetValue(
                    targetZoneId,
                    out SimulationZoneContext existing) &&
                existing != null &&
                existing.Scene.IsValid() &&
                existing.Scene.isLoaded;
            if (!zoneReady)
            {
                pendingPinBodies[targetZoneId] = targetBody.StableId;
            }

            QueueZoneLoad(connection, targetZoneId);
            return true;
        }

        public bool IsHandoffPending(NetworkConnection connection)
        {
            return playerSpawner != null &&
                connection != null &&
                playerSpawner.IsHandoffPending(connection.ClientId);
        }

        MultiplayerSceneContext ResolveConnectionContext(
            NetworkConnection connection)
        {
            NetworkObject firstObject = connection.FirstObject;
            return firstObject != null
                ? MultiplayerSceneContext.FindIn(firstObject.gameObject.scene)
                : null;
        }

        int ResolveStartingBodyStableId()
        {
            if (startingBodyStableId == 0 &&
                originAuthority != null &&
                originAuthority.TryGetZone(
                    MultiplayerZoneCatalog.StartingZoneId,
                    out ZoneOriginState startingZone))
            {
                startingBodyStableId = startingZone.ReferenceBodyId;
            }

            return startingBodyStableId;
        }

        void SceneManager_OnClientPresenceChangeEnd(
            ClientPresenceChangeEventArgs args)
        {
            if (!networkManager.IsServerStarted || args.Connection == null)
            {
                return;
            }

            SimulationZoneContext context = FindZoneContext(args.Scene);
            if (context == null || !context.IsConfigured)
            {
                return;
            }

            if (args.Added)
            {
                SetConnectionZone(args.Connection, context.ZoneId);
            }
            else
            {
                SetConnectionZone(
                    args.Connection,
                    GeneratedEntityId.None,
                    context.ZoneId);
            }
        }

        void UnitySceneManager_OnSceneUnloaded(Scene scene)
        {
            GeneratedEntityId remove = GeneratedEntityId.None;
            foreach (KeyValuePair<GeneratedEntityId, SimulationZoneContext> item
                     in zones)
            {
                if (item.Value == null || item.Value.Scene == scene)
                {
                    remove = item.Key;
                    break;
                }
            }

            if (remove.IsValid)
            {
                zones.Remove(remove);
            }
        }

        public bool TryGetZone(
            GeneratedEntityId zoneId,
            out SimulationZoneContext context)
        {
            return zones.TryGetValue(zoneId, out context) && context != null;
        }

        static void SetConnectionZone(
            NetworkConnection connection,
            GeneratedEntityId zoneId,
            GeneratedEntityId expectedCurrentZone = default)
        {
            foreach (NetworkObject ownedObject in connection.Objects)
            {
                NetworkSessionPlayer sessionPlayer =
                    ownedObject.GetComponent<NetworkSessionPlayer>();
                if (sessionPlayer == null ||
                    (expectedCurrentZone.IsValid &&
                     sessionPlayer.CurrentZoneId != expectedCurrentZone))
                {
                    continue;
                }

                sessionPlayer.SetCurrentZone(zoneId);
                return;
            }
        }

        static SimulationZoneContext FindZoneContext(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                SimulationZoneContext context =
                    roots[i].GetComponentInChildren<SimulationZoneContext>(true);
                if (context != null)
                {
                    return context;
                }
            }

            return null;
        }
    }
}
