using System;
using System.Collections;
using System.Collections.Generic;
using Farion.Gameplay.Character;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Input;
using Farion.Gameplay.Session;
using Farion.Multiplayer.Spawning;
using Farion.Multiplayer.World;
using Farion.Multiplayer.World.Zones;
using Farion.Simulation.World.Identity;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farion.Multiplayer.Session
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class MultiplayerSessionController : MonoBehaviour
    {
        const string DefaultPresentationScene = "SC_MultiplayerShell";
        const string DefaultStartingZoneScene = "SC_WorldZone";
        const string MainMenuScene = "SC_MainMenu";
        const float ConnectionTimeoutSeconds = 30f;
        static readonly GeneratedEntityId StartingZoneId =
            GeneratedEntityId.FromHash(
                StableHashUtility.Combine("zone.starting_system"));

        [SerializeField] NetworkManager networkManager;
        [SerializeField] NetworkPlayerSpawner playerSpawner;
        [SerializeField] NetworkWorldOriginAuthority worldOriginAuthority;
        [SerializeField] NetworkZoneCoordinator zoneCoordinator;
        [SerializeField] string presentationSceneName = DefaultPresentationScene;
        [SerializeField] string startingZoneSceneName = DefaultStartingZoneScene;

        readonly HashSet<int> zoneLoadRequests = new();
        bool host;
        bool sceneLoadRequested;
        bool gameplaySceneEntered;
        bool returningToMainMenu;
        bool subscribed;
        Coroutine stopRoutine;
        SpacecraftCameraRig spacecraftCameraRig;
        FirstPersonCameraRig firstPersonCameraRig;
        Transform viewReference;
        PlayerControlLock controlLock;

        public static MultiplayerSessionController Active { get; private set; }

        public MultiplayerSessionState State { get; private set; } =
            MultiplayerSessionState.Idle;
        public bool CanStartSession =>
            State == MultiplayerSessionState.Idle ||
            State == MultiplayerSessionState.Failed;

        public event Action<MultiplayerSessionState> StateChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetActive()
        {
            Active = null;
        }

        void Awake()
        {
            if (Active != null && Active != this)
            {
                Destroy(gameObject);
                return;
            }

            Active = this;
            networkManager ??= GetComponent<NetworkManager>();
            zoneCoordinator ??= GetComponent<NetworkZoneCoordinator>();
            DontDestroyOnLoad(gameObject);
            Subscribe();
        }

        void OnDestroy()
        {
            if (Active != this)
            {
                return;
            }

            Unsubscribe();
            GameplaySessionModeRequest.Cancel();
            Active = null;
            Physics.simulationMode = SimulationMode.FixedUpdate;
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
        }

        public void StartHost()
        {
            if (!CanStart())
            {
                return;
            }

            ConfigureTransportTimeouts();
            host = true;
            GameplaySessionModeRequest.Request(GameplaySessionMode.Multiplayer);
            SetState(MultiplayerSessionState.Starting);
            if (!networkManager.ServerManager.StartConnection() ||
                !networkManager.ClientManager.StartConnection("127.0.0.1"))
            {
                FailAndStop();
            }
        }

        public void StartClient(string address)
        {
            if (!CanStart())
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                SetState(MultiplayerSessionState.Failed);
                return;
            }

            ConfigureTransportTimeouts();
            host = false;
            GameplaySessionModeRequest.Request(GameplaySessionMode.Multiplayer);
            SetState(MultiplayerSessionState.Starting);
            if (!networkManager.ClientManager.StartConnection(address.Trim()))
            {
                FailAndStop();
            }
        }

        public void Stop()
        {
            GameplaySessionModeRequest.Cancel();
            if (State == MultiplayerSessionState.Stopping)
            {
                return;
            }

            if (State == MultiplayerSessionState.Idle)
            {
                ResetSession();
                Destroy(gameObject);
                return;
            }

            SetState(MultiplayerSessionState.Stopping);
            networkManager.ClientManager.StopConnection();
            if (networkManager.ServerManager.IsAnyServerStarted())
            {
                networkManager.ServerManager.StopConnection(true);
            }

            stopRoutine ??= StartCoroutine(FinishStop());
        }

        bool CanStart()
        {
            return CanStartSession;
        }

        void ConfigureTransportTimeouts()
        {
            networkManager.TransportManager.Transport.SetTimeout(
                ConnectionTimeoutSeconds,
                asServer: false);
            networkManager.TransportManager.Transport.SetTimeout(
                ConnectionTimeoutSeconds,
                asServer: true);
        }

        void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            subscribed = true;
            networkManager.ClientManager.OnClientConnectionState +=
                OnClientConnectionState;
            networkManager.ServerManager.OnServerConnectionState +=
                OnServerConnectionState;
            networkManager.ServerManager.OnRemoteConnectionState +=
                OnRemoteConnectionState;
            networkManager.SceneManager.OnClientLoadedStartScenes +=
                OnClientLoadedStartScenes;
            networkManager.SceneManager.OnLoadEnd += OnSceneLoadEnd;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded +=
                OnUnitySceneLoaded;
        }

        void Unsubscribe()
        {
            if (!subscribed || networkManager == null)
            {
                return;
            }

            subscribed = false;
            networkManager.ClientManager.OnClientConnectionState -=
                OnClientConnectionState;
            networkManager.ServerManager.OnServerConnectionState -=
                OnServerConnectionState;
            networkManager.ServerManager.OnRemoteConnectionState -=
                OnRemoteConnectionState;
            networkManager.SceneManager.OnClientLoadedStartScenes -=
                OnClientLoadedStartScenes;
            networkManager.SceneManager.OnLoadEnd -= OnSceneLoadEnd;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -=
                OnUnitySceneLoaded;
        }

        void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (host &&
                args.ConnectionState == LocalConnectionState.Stopped &&
                State != MultiplayerSessionState.Idle &&
                State != MultiplayerSessionState.Stopping)
            {
                FailAndReturnToMainMenu();
                return;
            }

            if (!host ||
                args.ConnectionState != LocalConnectionState.Started ||
                sceneLoadRequested)
            {
                return;
            }

            sceneLoadRequested = true;
            SceneLoadData data = new(presentationSceneName)
            {
                ReplaceScenes = ReplaceOption.All,
                PreferredActiveScene = new PreferredScene(
                    new SceneLookupData(presentationSceneName))
            };
            networkManager.SceneManager.LoadGlobalScenes(data);
        }

        void OnRemoteConnectionState(
            NetworkConnection connection,
            RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped)
            {
                zoneLoadRequests.Remove(connection.ClientId);
                return;
            }

        }

        void OnClientLoadedStartScenes(
            NetworkConnection connection,
            bool asServer)
        {
            if (asServer && gameplaySceneEntered)
            {
                RequestStartingZone(connection);
            }
        }

        void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                return;
            }

            if (args.ConnectionState != LocalConnectionState.Stopped ||
                State == MultiplayerSessionState.Idle ||
                State == MultiplayerSessionState.Stopping)
            {
                return;
            }

            FailAndReturnToMainMenu();
        }

        void OnUnitySceneLoaded(Scene scene, LoadSceneMode _)
        {
            if (scene.name == MainMenuScene &&
                gameplaySceneEntered &&
                State != MultiplayerSessionState.Idle &&
                State != MultiplayerSessionState.Stopping)
            {
                Stop();
            }
        }

        void OnSceneLoadEnd(SceneLoadEndEventArgs args)
        {
            for (int i = 0; i < args.LoadedScenes.Length; i++)
            {
                Scene scene = args.LoadedScenes[i];
                if (!scene.IsValid())
                {
                    continue;
                }

                if (scene.name == presentationSceneName)
                {
                    gameplaySceneEntered = true;
                    if (!ResolvePresentation(scene))
                    {
                        Debug.LogError(
                            $"Multiplayer presentation scene '{scene.name}' is incomplete.",
                            this);
                        FailAndReturnToMainMenu();
                        return;
                    }

                    if (args.QueueData.AsServer)
                    {
                        foreach (NetworkConnection connection in
                                 networkManager.ServerManager.Clients.Values)
                        {
                            RequestStartingZone(connection);
                        }
                    }

                    continue;
                }

                if (scene.name != startingZoneSceneName)
                {
                    continue;
                }

                MultiplayerSceneContext context =
                    MultiplayerSceneContext.FindIn(scene);
                if (context == null)
                {
                    Debug.LogError(
                        $"Starting zone scene '{scene.name}' has no multiplayer context.",
                        this);
                    FailAndReturnToMainMenu();
                    return;
                }

                context.BindPresentation(
                    spacecraftCameraRig,
                    firstPersonCameraRig,
                    viewReference,
                    controlLock);
                context.BindSession(playerSpawner, worldOriginAuthority);
            }
        }

        internal void NotifyOwnedPlayerReady()
        {
            if (State == MultiplayerSessionState.Starting)
            {
                SetState(MultiplayerSessionState.Connected);
            }
        }

        internal void FailOwnedPlayerSetup()
        {
            if (State != MultiplayerSessionState.Idle &&
                State != MultiplayerSessionState.Stopping)
            {
                FailAndReturnToMainMenu();
            }
        }

        bool ResolvePresentation(Scene scene)
        {
            spacecraftCameraRig = FindInScene<SpacecraftCameraRig>(scene);
            firstPersonCameraRig = FindInScene<FirstPersonCameraRig>(scene);
            Camera camera = FindInScene<Camera>(scene);
            viewReference = camera != null ? camera.transform : null;
            controlLock = FindInScene<PlayerControlLock>(scene);
            return spacecraftCameraRig != null &&
                firstPersonCameraRig != null &&
                viewReference != null &&
                controlLock != null;
        }

        void RequestStartingZone(NetworkConnection connection)
        {
            if (connection == null ||
                !connection.IsActive ||
                !zoneLoadRequests.Add(connection.ClientId))
            {
                return;
            }

            if (zoneCoordinator == null ||
                !zoneCoordinator.LoadZoneForConnection(
                    connection,
                    startingZoneSceneName,
                    StartingZoneId))
            {
                zoneLoadRequests.Remove(connection.ClientId);
                connection.Disconnect(immediately: true);
            }
        }

        static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T component = roots[i].GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        void FailAndStop()
        {
            SetState(MultiplayerSessionState.Failed);
            Stop();
        }

        void FailAndReturnToMainMenu()
        {
            if (returningToMainMenu)
            {
                return;
            }

            returningToMainMenu = true;
            SetState(MultiplayerSessionState.Failed);
            Stop();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name !=
                MainMenuScene)
            {
                UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                    MainMenuScene,
                    LoadSceneMode.Single);
            }
        }

        IEnumerator FinishStop()
        {
            int frames = 0;
            while (frames++ < 120 &&
                   (networkManager.ClientManager.Started ||
                    networkManager.ServerManager.Started))
            {
                yield return null;
            }

            ResetSession();
            SetState(MultiplayerSessionState.Idle);
            stopRoutine = null;
            Destroy(gameObject);
        }

        void ResetSession()
        {
            sceneLoadRequested = false;
            gameplaySceneEntered = false;
            returningToMainMenu = false;
            host = false;
            zoneLoadRequests.Clear();
            spacecraftCameraRig = null;
            firstPersonCameraRig = null;
            viewReference = null;
            controlLock = null;
            worldOriginAuthority?.ResetSession();
            playerSpawner?.ResetSession();
        }

        void SetState(MultiplayerSessionState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
