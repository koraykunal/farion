using Farion.Core.Identity;
using System;
using System.Collections;
using System.Collections.Generic;
using Farion.Gameplay.Session;
using Farion.Multiplayer.Spawning;
using Farion.Multiplayer.World;
using Farion.Simulation.World;
using Farion.UI.Gameplay;
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
        const string DefaultPresentationScene = "SC_GameplayShell";
        const string DefaultStartingZoneScene = "SC_WorldZone";
        const string MainMenuScene = "SC_MainMenu";
        const float ConnectionTimeoutSeconds = 30f;
        const float StopTimeoutSeconds = 5f;
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
        bool ownedPlayerReady;
        bool assignedStarterShipReady;
        Coroutine stopRoutine;
        GameplaySceneShellController presentation;

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
            ResetReadiness();
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
            ResetReadiness();
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

        public void ReturnToMainMenu()
        {
            if (returningToMainMenu)
            {
                return;
            }

            returningToMainMenu = true;
            Stop();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name !=
                MainMenuScene)
            {
                UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                    MainMenuScene,
                    LoadSceneMode.Single);
            }
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

                context.BindPresentation(presentation);
                context.BindSession(playerSpawner, worldOriginAuthority);
            }
        }

        internal void NotifyOwnedPlayerReady()
        {
            ownedPlayerReady = true;
            TryCompleteStartup();
        }

        internal void NotifyAssignedStarterShipReady()
        {
            assignedStarterShipReady = true;
            TryCompleteStartup();
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
            presentation = FindInScene<GameplaySceneShellController>(scene);
            if (presentation == null)
            {
                return false;
            }

            presentation.GameplayUi?.SetSessionActions(
                ReturnToMainMenu,
                UnityEngine.Application.Quit);
            return presentation.IsValid;
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
            SetState(MultiplayerSessionState.Failed);
            ReturnToMainMenu();
        }

        IEnumerator FinishStop()
        {
            float deadline = Time.realtimeSinceStartup + StopTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline &&
                   (networkManager.ClientManager.Started ||
                    networkManager.ServerManager.Started))
            {
                yield return null;
            }

            if (networkManager.ClientManager.Started ||
                networkManager.ServerManager.Started)
            {
                Debug.LogWarning(
                    "Network shutdown timed out; destroying the session root to force transport cleanup.",
                    this);
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
            ResetReadiness();
            zoneLoadRequests.Clear();
            presentation = null;
            worldOriginAuthority?.ResetSession();
            playerSpawner?.ResetSession();
        }

        void TryCompleteStartup()
        {
            if (State == MultiplayerSessionState.Starting &&
                ownedPlayerReady &&
                assignedStarterShipReady)
            {
                SetState(MultiplayerSessionState.Connected);
            }
        }

        void ResetReadiness()
        {
            ownedPlayerReady = false;
            assignedStarterShipReady = false;
        }

        void SetState(MultiplayerSessionState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Farion Multiplayer] Session state: {state}.");
#endif
            StateChanged?.Invoke(state);
        }
    }
}
