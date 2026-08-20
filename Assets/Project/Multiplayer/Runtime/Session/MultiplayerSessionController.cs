using Farion.Core.Identity;
using Farion.Core.Persistence;
using System;
using System.Collections;
using System.Collections.Generic;
using Farion.Gameplay.Persistence;
using Farion.Gameplay.Session;
using Farion.Multiplayer.Spawning;
using Farion.Multiplayer.World;
using Farion.Simulation.World;
using Farion.UI.Gameplay;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
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
        [SerializeField] NetworkStatusReporter statusReporter;
        [SerializeField] MultiplayerSaveBridge saveBridge;
        [SerializeField] string presentationSceneName = DefaultPresentationScene;
        [SerializeField] string startingZoneSceneName = DefaultStartingZoneScene;

        static SimulationMode authoredSimulationMode = SimulationMode.FixedUpdate;
        static SimulationMode2D authoredSimulation2DMode =
            SimulationMode2D.FixedUpdate;

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
        public MultiplayerFailureReason FailureReason { get; private set; } =
            MultiplayerFailureReason.None;
        public bool IsHost => host;
        public bool CanStartSession =>
            State == MultiplayerSessionState.Idle ||
            State == MultiplayerSessionState.Failed;

        public event Action<MultiplayerSessionState> StateChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetActive()
        {
            Active = null;
            authoredSimulationMode = Physics.simulationMode;
            authoredSimulation2DMode = Physics2D.simulationMode;
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
            statusReporter ??= GetComponent<NetworkStatusReporter>();
            saveBridge ??= GetComponent<MultiplayerSaveBridge>();
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
            Physics.simulationMode = authoredSimulationMode;
            Physics2D.simulationMode = authoredSimulation2DMode;
        }

        public void StartHost() => StartHost(MultiplayerEndpoint.DefaultPort);

        public void StartHost(ushort port)
        {
            if (!CanStart())
            {
                return;
            }

            ConfigureTransportTimeouts();
            SelectClientTransport(MultiplayerTransportKind.Direct);
            host = true;
            ResetReadiness();
            FailureReason = MultiplayerFailureReason.None;
            GameplaySessionModeRequest.Request(GameplaySessionMode.Multiplayer);
            SetState(MultiplayerSessionState.Starting);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!MultiplayerLobbyGateway.IsAvailable)
            {
                Debug.Log(
                    "[Farion Multiplayer] Steam is unavailable; hosting over direct connections only.");
            }
#endif
            networkManager.ServerManager.StartConnection(port);
            if (!IsAnyServerTransportActive())
            {
                FailAndStop();
                return;
            }

            if (!networkManager.ClientManager.StartConnection(
                    MultiplayerEndpoint.LoopbackAddress,
                    port))
            {
                FailAndStop();
            }
        }

        bool IsAnyServerTransportActive()
        {
            Transport transport = networkManager.TransportManager.Transport;
            if (transport is not Multipass multipass)
            {
                return transport != null &&
                    transport.GetConnectionState(server: true) !=
                    LocalConnectionState.Stopped;
            }

            for (int i = 0; i < multipass.Transports.Count; i++)
            {
                if (multipass.GetConnectionState(true, i) !=
                    LocalConnectionState.Stopped)
                {
                    return true;
                }
            }

            return false;
        }

        public void StartClient(string address)
        {
            if (!CanStart())
            {
                return;
            }

            if (!MultiplayerEndpoint.TryParse(
                    address,
                    out MultiplayerEndpoint endpoint))
            {
                FailureReason = MultiplayerFailureReason.InvalidAddress;
                SetState(MultiplayerSessionState.Failed);
                return;
            }

            StartClient(endpoint);
        }

        public void StartClient(MultiplayerEndpoint endpoint)
        {
            if (!CanStart())
            {
                return;
            }

            ConfigureTransportTimeouts();
            SelectClientTransport(MultiplayerTransportKind.Direct);
            host = false;
            ResetReadiness();
            FailureReason = MultiplayerFailureReason.None;
            GameplaySessionModeRequest.Request(GameplaySessionMode.Multiplayer);
            SetState(MultiplayerSessionState.Starting);
            if (!networkManager.ClientManager.StartConnection(
                    endpoint.Address,
                    endpoint.Port))
            {
                FailAndStop();
            }
        }

        public void StartSteamClient(string hostAddress)
        {
            if (!CanStart())
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(hostAddress) ||
                !SelectClientTransport(MultiplayerTransportKind.Steam))
            {
                FailureReason = MultiplayerFailureReason.InvalidAddress;
                SetState(MultiplayerSessionState.Failed);
                return;
            }

            ConfigureTransportTimeouts();
            host = false;
            ResetReadiness();
            FailureReason = MultiplayerFailureReason.None;
            GameplaySessionModeRequest.Request(GameplaySessionMode.Multiplayer);
            SetState(MultiplayerSessionState.Starting);
            if (!networkManager.ClientManager.StartConnection(hostAddress.Trim()))
            {
                FailAndStop();
            }
        }

        bool SelectClientTransport(MultiplayerTransportKind kind)
        {
            if (networkManager.TransportManager.Transport is not Multipass multipass)
            {
                return kind == MultiplayerTransportKind.Direct;
            }

            int index = (int)kind;
            if (index < 0 || index >= multipass.Transports.Count)
            {
                return false;
            }

            multipass.SetClientTransport(multipass.Transports[index]);
            return true;
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
            Transport transport = networkManager.TransportManager.Transport;
            if (transport is Multipass multipass)
            {
                for (int i = 0; i < multipass.Transports.Count; i++)
                {
                    ApplyTimeout(multipass.Transports[i]);
                }

                return;
            }

            ApplyTimeout(transport);
        }

        static void ApplyTimeout(Transport transport)
        {
            if (transport == null)
            {
                return;
            }

            transport.SetTimeout(ConnectionTimeoutSeconds, asServer: false);
            transport.SetTimeout(ConnectionTimeoutSeconds, asServer: true);
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
                FailAndReturnToMainMenu(MultiplayerFailureReason.ConnectionLost);
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

            FailAndReturnToMainMenu(MultiplayerFailureReason.ConnectionLost);
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
                        FailAndReturnToMainMenu(
                            MultiplayerFailureReason.SessionSetup);
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
                    FailAndReturnToMainMenu(
                        MultiplayerFailureReason.SessionSetup);
                    return;
                }

                context.BindPresentation(presentation);
                context.BindSession(playerSpawner, worldOriginAuthority);
                BindSaveBridge(context);
            }
        }

        void BindSaveBridge(MultiplayerSceneContext context)
        {
            if (saveBridge == null || context.RuntimeRoot == null)
            {
                return;
            }

            saveBridge.BindZone(
                context.RuntimeRoot.GetComponent<GameplaySaveCoordinator>());
            if (!host || !saveBridge.CanSave)
            {
                return;
            }

            SaveGameStartupRequest.Consume(
                out SaveGameStartupMode startupMode,
                out string requestedSlotName);
            saveBridge.SetSlot(requestedSlotName);
            presentation?.GameplayUi?.SetSaveAction(
                saveBridge.Save,
                saveBridge.SlotName);
            if (startupMode == SaveGameStartupMode.LoadGame)
            {
                saveBridge.Load(saveBridge.SlotName);
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
                FailAndReturnToMainMenu(MultiplayerFailureReason.SessionSetup);
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
            statusReporter?.BindPresentation(presentation.GameplayUi);
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

        internal void ReportFailure(MultiplayerFailureReason reason)
        {
            if (reason == MultiplayerFailureReason.None ||
                FailureReason != MultiplayerFailureReason.None)
            {
                return;
            }

            FailureReason = reason;
            if (gameplaySceneEntered)
            {
                MultiplayerSessionOutcome.Report(reason);
            }
        }

        void FailAndStop()
        {
            ReportFailure(MultiplayerFailureReason.ConnectionFailed);
            SetState(MultiplayerSessionState.Failed);
            Stop();
        }

        void FailAndReturnToMainMenu(MultiplayerFailureReason reason)
        {
            ReportFailure(reason);
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
            MultiplayerLobbyGateway.Service?.Leave();
            saveBridge?.UnbindZone();
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
