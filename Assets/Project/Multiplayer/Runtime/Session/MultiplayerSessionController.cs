using Farion.Core.Identity;
using Farion.Core.Persistence;
using System;
using System.Collections;
using System.Collections.Generic;
using Farion.Gameplay.Persistence;
using Farion.Multiplayer.Spawning;
using Farion.Multiplayer.World;
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

        [SerializeField] NetworkManager networkManager;
        [SerializeField] MultiplayerPlayerSpawner playerSpawner;
        [SerializeField] MultiplayerWorldOriginAuthority worldOriginAuthority;
        [SerializeField] MultiplayerZoneCoordinator zoneCoordinator;
        [SerializeField] MultiplayerStatusReporter statusReporter;
        [SerializeField] MultiplayerSaveBridge saveBridge;
        [SerializeField] ZonePhysicsTickDriver physicsTickDriver;
        [SerializeField] string presentationSceneName = DefaultPresentationScene;
        [SerializeField] string startingZoneSceneName = DefaultStartingZoneScene;

        static SimulationMode authoredSimulationMode = SimulationMode.FixedUpdate;
        static SimulationMode2D authoredSimulation2DMode =
            SimulationMode2D.FixedUpdate;

        readonly HashSet<int> zoneLoadRequests = new();
        bool host;
        bool privateSession;
        ushort hostPort;
        bool sceneLoadRequested;
        bool gameplaySceneEntered;
        bool returningToMainMenu;
        bool subscribed;
        bool ownedPlayerReady;
        bool assignedStarterShuttleReady;
        Coroutine stopRoutine;
        UiGameplaySceneShellController presentation;
        Scene localZoneScene;
        MultiplayerSceneContext localZoneContext;

        public static MultiplayerSessionController Active { get; private set; }

        public MultiplayerSessionState State { get; private set; } =
            MultiplayerSessionState.Idle;
        public MultiplayerFailureReason FailureReason { get; private set; } =
            MultiplayerFailureReason.None;
        public bool IsHost => host;
        public bool IsPrivate => privateSession;
        public ushort HostPort => hostPort;
        public int PlayerCapacity => privateSession
            ? 1
            : MultiplayerPlayerSpawner.MaximumPlayers;
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
            zoneCoordinator ??= GetComponent<MultiplayerZoneCoordinator>();
            statusReporter ??= GetComponent<MultiplayerStatusReporter>();
            saveBridge ??= GetComponent<MultiplayerSaveBridge>();
            physicsTickDriver ??= GetComponent<ZonePhysicsTickDriver>();
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
            Active = null;
            Physics.simulationMode = authoredSimulationMode == SimulationMode.Script
                ? SimulationMode.FixedUpdate
                : authoredSimulationMode;
            Physics2D.simulationMode =
                authoredSimulation2DMode == SimulationMode2D.Script
                    ? SimulationMode2D.FixedUpdate
                    : authoredSimulation2DMode;
        }

        public void StartSolo()
        {
            StartListenServer(MultiplayerEndpoint.DefaultPort, privateSession: true);
        }

        public void StartHost() => StartHost(MultiplayerEndpoint.DefaultPort);

        public void StartHost(ushort port)
        {
            StartListenServer(port, privateSession: false);
        }

        void StartListenServer(ushort port, bool privateSession)
        {
            if (!CanStartSession)
            {
                return;
            }

            ConfigureTransportTimeouts();
            SelectClientTransport(MultiplayerTransportKind.Direct);
            host = true;
            this.privateSession = privateSession;
            hostPort = port;
            ResetReadiness();
            FailureReason = MultiplayerFailureReason.None;
            SetState(MultiplayerSessionState.Starting);
            ApplyPlayerCapacity();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!privateSession && !MultiplayerLobbyGateway.IsAvailable)
            {
                Debug.Log(
                    "[Farion Multiplayer] Steam is unavailable; hosting over direct connections only.");
            }
#endif
            if (!StartServerTransports(port))
            {
                FailAndStop(MultiplayerFailureReason.ConnectionFailed);
                return;
            }

            if (!networkManager.ClientManager.StartConnection(
                    MultiplayerEndpoint.LoopbackAddress,
                    port))
            {
                FailAndStop(MultiplayerFailureReason.ConnectionFailed);
            }
        }

        bool StartServerTransports(ushort port)
        {
            Transport transport = networkManager.TransportManager.Transport;
            if (transport is not Multipass multipass)
            {
                networkManager.ServerManager.StartConnection(port);
                return transport != null &&
                    transport.GetConnectionState(server: true) !=
                    LocalConnectionState.Stopped;
            }

            if (privateSession)
            {
                int direct = (int)MultiplayerTransportKind.Direct;
                multipass.Transports[direct].SetPort(port);
                return multipass.StartConnection(true, direct) &&
                    multipass.GetConnectionState(true, direct) !=
                    LocalConnectionState.Stopped;
            }

            networkManager.ServerManager.StartConnection(port);
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

        void ApplyPlayerCapacity()
        {
            Transport transport = networkManager.TransportManager.Transport;
            if (transport is Multipass multipass)
            {
                for (int i = 0; i < multipass.Transports.Count; i++)
                {
                    multipass.Transports[i].SetMaximumClients(PlayerCapacity);
                }

                return;
            }

            transport?.SetMaximumClients(PlayerCapacity);
        }

        public void StartClient(string address)
        {
            if (!CanStartSession)
            {
                return;
            }

            if (!MultiplayerEndpoint.TryParse(
                    address,
                    out MultiplayerEndpoint endpoint))
            {
                FailAndStop(MultiplayerFailureReason.InvalidAddress);
                return;
            }

            StartClient(endpoint);
        }

        public void StartClient(MultiplayerEndpoint endpoint)
        {
            if (!CanStartSession)
            {
                return;
            }

            ConfigureTransportTimeouts();
            SelectClientTransport(MultiplayerTransportKind.Direct);
            BeginClientSession();
            if (!networkManager.ClientManager.StartConnection(
                    endpoint.Address,
                    endpoint.Port))
            {
                FailAndStop(MultiplayerFailureReason.ConnectionFailed);
            }
        }

        public void StartSteamClient(string hostAddress)
        {
            if (!CanStartSession)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(hostAddress) ||
                !SelectClientTransport(MultiplayerTransportKind.Steam))
            {
                FailAndStop(MultiplayerFailureReason.InvalidAddress);
                return;
            }

            ConfigureTransportTimeouts();
            BeginClientSession();
            if (!networkManager.ClientManager.StartConnection(hostAddress.Trim()))
            {
                FailAndStop(MultiplayerFailureReason.ConnectionFailed);
            }
        }

        void BeginClientSession()
        {
            host = false;
            privateSession = false;
            ResetReadiness();
            FailureReason = MultiplayerFailureReason.None;
            SetState(MultiplayerSessionState.Starting);
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

        public AsyncOperation ReturnToMainMenu()
        {
            if (returningToMainMenu)
            {
                return null;
            }

            returningToMainMenu = true;
            Stop();
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ==
                MainMenuScene
                ? null
                : UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                    MainMenuScene,
                    LoadSceneMode.Single);
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
            networkManager.SceneManager.OnUnloadEnd += OnSceneUnloadEnd;
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
            networkManager.SceneManager.OnUnloadEnd -= OnSceneUnloadEnd;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -=
                OnUnitySceneLoaded;
        }

        void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (host &&
                args.ConnectionState == LocalConnectionState.Stopped &&
                State != MultiplayerSessionState.Idle &&
                State != MultiplayerSessionState.Stopping &&
                !networkManager.ServerManager.IsAnyServerStarted())
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
            if (args.ConnectionState != LocalConnectionState.Stopped ||
                State == MultiplayerSessionState.Idle ||
                State == MultiplayerSessionState.Stopping)
            {
                return;
            }

            FailAndReturnToMainMenu(
                State == MultiplayerSessionState.Starting
                    ? MultiplayerFailureReason.ConnectionFailed
                    : MultiplayerFailureReason.ConnectionLost);
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
            bool isZoneLoad = MultiplayerZoneSceneLoad.TryReadZoneId(
                args,
                out GeneratedEntityId zoneId);
            if (!gameplaySceneEntered && WasPresentationSceneSkipped(args))
            {
                Scene loadedPresentation =
                    UnityEngine.SceneManagement.SceneManager.GetSceneByName(
                        presentationSceneName);
                if (loadedPresentation.isLoaded &&
                    !EnterPresentationScene(loadedPresentation, args.QueueData.AsServer))
                {
                    return;
                }
            }

            for (int i = 0; i < args.LoadedScenes.Length; i++)
            {
                Scene scene = args.LoadedScenes[i];
                if (!scene.IsValid())
                {
                    continue;
                }

                if (scene.name == presentationSceneName)
                {
                    if (!EnterPresentationScene(scene, args.QueueData.AsServer))
                    {
                        return;
                    }

                    continue;
                }

                if (!isZoneLoad)
                {
                    continue;
                }

                MultiplayerSceneContext context =
                    MultiplayerSceneContext.FindIn(scene);
                if (context == null)
                {
                    Debug.LogError(
                        $"Zone scene '{scene.name}' has no multiplayer context.",
                        this);
                    if (zoneId == MultiplayerZoneCatalog.StartingZoneId)
                    {
                        FailAndReturnToMainMenu(
                            MultiplayerFailureReason.SessionSetup);
                        return;
                    }

                    continue;
                }

                context.BindSession(playerSpawner, worldOriginAuthority, zoneId);
                if (IsLocalZoneLoad(args.QueueData))
                {
                    ApplyLocalZone(context, scene);
                }
                else
                {
                    context.SetPresentationActive(false);
                    if (localZoneScene.IsValid() && localZoneScene.isLoaded)
                    {
                        UnityEngine.SceneManagement.SceneManager.SetActiveScene(
                            localZoneScene);
                    }
                }

                if (zoneId == MultiplayerZoneCatalog.StartingZoneId)
                {
                    BindSaveBridge(context);
                }
            }
        }

        bool WasPresentationSceneSkipped(SceneLoadEndEventArgs args)
        {
            string[] skipped = args.SkippedSceneNames;
            for (int i = 0; skipped != null && i < skipped.Length; i++)
            {
                if (skipped[i] == presentationSceneName)
                {
                    return true;
                }
            }

            return false;
        }

        bool EnterPresentationScene(Scene scene, bool asServer)
        {
            gameplaySceneEntered = true;
            if (!ResolvePresentation(scene))
            {
                Debug.LogError(
                    $"Multiplayer presentation scene '{scene.name}' is incomplete.",
                    this);
                FailAndReturnToMainMenu(MultiplayerFailureReason.SessionSetup);
                return false;
            }

            if (asServer)
            {
                foreach (NetworkConnection connection in
                         networkManager.ServerManager.Clients.Values)
                {
                    RequestStartingZone(connection);
                }
            }

            return true;
        }

        void OnSceneUnloadEnd(SceneUnloadEndEventArgs args)
        {
            if (localZoneScene.IsValid() &&
                localZoneScene.isLoaded &&
                UnityEngine.SceneManagement.SceneManager.GetActiveScene() !=
                    localZoneScene)
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(
                    localZoneScene);
            }
        }

        internal void NotifyLocalZone(MultiplayerSceneContext context)
        {
            if (context != null)
            {
                ApplyLocalZone(context, context.gameObject.scene);
            }
        }

        void ApplyLocalZone(MultiplayerSceneContext context, Scene scene)
        {
            if (context == localZoneContext)
            {
                return;
            }

            if (localZoneContext != null)
            {
                localZoneContext.UnbindPresentation();
                localZoneContext.SetPresentationActive(false);
            }

            localZoneContext = context;
            localZoneScene = scene;
            context.SetPresentationActive(true);
            context.BindPresentation(presentation);
            if (scene.IsValid() && scene.isLoaded)
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            }
        }

        bool IsLocalZoneLoad(LoadQueueData queueData)
        {
            if (!queueData.AsServer)
            {
                return true;
            }

            NetworkConnection localConnection =
                networkManager.ClientManager.Connection;
            if (localConnection == null || !localConnection.IsValid)
            {
                return false;
            }

            NetworkConnection[] connections = queueData.Connections;
            for (int i = 0; i < connections.Length; i++)
            {
                if (connections[i] == localConnection)
                {
                    return true;
                }
            }

            return false;
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
            if (startupMode == SaveGameStartupMode.LoadGame)
            {
                SaveGameOperationResult loadResult =
                    saveBridge.Load(saveBridge.SlotName);
                if (!loadResult.Succeeded)
                {
                    Debug.LogError(
                        $"Session load failed for slot '{saveBridge.SlotName}': {loadResult.Status}.",
                        this);
                    FailAndReturnToMainMenu(
                        MultiplayerFailureReason.SaveLoadFailed);
                    return;
                }
            }

            presentation?.GameplayUi?.SetSaveAction(
                saveBridge.Save,
                saveBridge.SlotName);
        }

        internal void NotifyOwnedPlayerReady()
        {
            ownedPlayerReady = true;
            TryCompleteStartup();
        }

        internal void NotifyAssignedStarterShuttleReady()
        {
            assignedStarterShuttleReady = true;
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
            presentation = FindInScene<UiGameplaySceneShellController>(scene);
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
                    MultiplayerZoneCatalog.StartingZoneId))
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
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name !=
                MainMenuScene)
            {
                MultiplayerSessionOutcome.Report(reason);
            }
        }

        void FailAndStop(MultiplayerFailureReason reason)
        {
            ReportFailure(reason);
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
            privateSession = false;
            ResetReadiness();
            zoneLoadRequests.Clear();
            localZoneScene = default;
            localZoneContext = null;
            MultiplayerLobbyGateway.Service?.Leave();
            saveBridge?.UnbindZone();
            presentation = null;
            worldOriginAuthority?.ResetSession();
            playerSpawner?.ResetSession();
            physicsTickDriver?.ResetSession();
        }

        void TryCompleteStartup()
        {
            if (State == MultiplayerSessionState.Starting &&
                ownedPlayerReady &&
                assignedStarterShuttleReady)
            {
                SetState(MultiplayerSessionState.Connected);
            }
        }

        void ResetReadiness()
        {
            ownedPlayerReady = false;
            assignedStarterShuttleReady = false;
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
