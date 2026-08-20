using Farion.Core.Identity;
using System.Collections.Generic;
using Farion.Multiplayer.Session;
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
    public sealed class NetworkZoneCoordinator : MonoBehaviour
    {
        [SerializeField] NetworkManager networkManager;
        [SerializeField] ZonePhysicsTickDriver physicsTickDriver;

        readonly Dictionary<GeneratedEntityId, SimulationZoneContext> zones = new();
        GeneratedEntityId activeZoneId;
        bool subscribed;

        public int LoadedZoneCount => zones.Count;

        void Awake()
        {
            networkManager ??= GetComponent<NetworkManager>();
            physicsTickDriver ??= GetComponent<ZonePhysicsTickDriver>();
        }

        void Start()
        {
            Subscribe();
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
                !connection.IsActive ||
                (activeZoneId.IsValid && activeZoneId != zoneId))
            {
                return false;
            }

            activeZoneId = zoneId;

            SceneLoadData load = zones.TryGetValue(
                zoneId,
                out SimulationZoneContext existingZone) &&
                existingZone != null &&
                existingZone.Scene.IsValid() &&
                existingZone.Scene.isLoaded
                    ? NetworkZoneSceneLoad.Create(existingZone.Scene, zoneId)
                    : NetworkZoneSceneLoad.Create(sceneName, zoneId);
            networkManager.SceneManager.LoadConnectionScenes(connection, load);
            return true;
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
            if (!NetworkZoneSceneLoad.TryReadZoneId(args, out GeneratedEntityId zoneId))
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
                if (zones.Count == 0)
                {
                    activeZoneId = GeneratedEntityId.None;
                }
            }
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
