using Farion.Multiplayer.Player;
using Farion.Multiplayer.World;
using Farion.Simulation.Physics;
using Farion.UI.Gameplay;
using Farion.UI.Localization;
using FishNet.Managing;
using FishNet.Managing.Predicting;
using Unity.Profiling;
using UnityEngine;

namespace Farion.Multiplayer.Session
{
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class MultiplayerStatusReporter : MonoBehaviour
    {
        const float WindowSeconds = 5f;

        [SerializeField] NetworkManager networkManager;
        [SerializeField, Min(0.1f)] float refreshInterval = 0.5f;
        [Tooltip("Developer overlay: frame drops, origin shifts and reconcile error under the connection headline.")]
        [SerializeField] bool showDiagnostics;
        [SerializeField, Range(1, 25)] int maximumFrameTicks = 3;

        UiGameplayController gameplayUi;
        PredictionManager predictionManager;
        float nextRefreshTime;
        string lastStatus;
        int recentDroppedFrames;
        int droppedWindow;
        float droppedWindowEnd;
        float recentPeakMilliseconds;
        float peakWindowEnd;
        int reconcilesPerSecond;
        int reconcileWindow;
        float reconcileWindowEnd;
        int originShifts;
        uint lastOriginSequence;
        ProfilerRecorder mainThreadRecorder;
        ProfilerRecorder presentWaitRecorder;
        ProfilerRecorder drawCallRecorder;
        ProfilerRecorder setPassRecorder;
        bool jitterInitialized;
        Vector3 lastCameraPosition;
        Quaternion lastCameraRotation;
        Vector3 lastBodyPosition;
        Vector3 lastRigidbodyPosition;
        Vector3 lastPlanetPosition;
        Quaternion lastPlanetRotation;
        float peakCameraShiftMillimeters;
        float peakCameraTurnDegrees;
        float peakBodyShiftMillimeters;
        float peakRigidbodyShiftMillimeters;
        float peakPlanetShiftMillimeters;
        float peakPlanetTurnMicroRadians;

        void Awake()
        {
            networkManager ??= GetComponent<NetworkManager>();
            if (networkManager != null)
            {
                predictionManager = networkManager.GetComponent<PredictionManager>();
            }
        }

        void OnEnable()
        {
            if (predictionManager != null)
            {
                predictionManager.OnPostReconcile += OnPostReconcile;
            }

            if (showDiagnostics)
            {
                mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
                presentWaitRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread", 15);
                drawCallRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                setPassRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            }
        }

        void OnDisable()
        {
            if (predictionManager != null)
            {
                predictionManager.OnPostReconcile -= OnPostReconcile;
            }

            mainThreadRecorder.Dispose();
            presentWaitRecorder.Dispose();
            drawCallRecorder.Dispose();
            setPassRecorder.Dispose();
        }

        public void BindPresentation(UiGameplayController controller)
        {
            if (gameplayUi != null && gameplayUi != controller)
            {
                gameplayUi.SetNetworkStatus(null);
            }

            gameplayUi = controller;
            lastStatus = null;
            nextRefreshTime = 0f;
            recentDroppedFrames = 0;
            droppedWindow = 0;
            droppedWindowEnd = 0f;
            recentPeakMilliseconds = 0f;
            peakWindowEnd = 0f;
            reconcilesPerSecond = 0;
            reconcileWindow = 0;
            reconcileWindowEnd = 0f;
            originShifts = 0;
            lastOriginSequence = ResolveLocalOriginSequence();
            PredictionDiagnostics.Reset();
        }

        void OnDestroy()
        {
            if (gameplayUi != null)
            {
                gameplayUi.SetNetworkStatus(null);
            }
        }

        void Update()
        {
            if (gameplayUi == null)
            {
                return;
            }

            if (showDiagnostics)
            {
                SampleDiagnostics();
            }

            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + refreshInterval;
            string status = BuildStatus();
            if (status == lastStatus)
            {
                return;
            }

            lastStatus = status;
            gameplayUi.SetNetworkStatus(status);
        }

        void LateUpdate()
        {
            if (!showDiagnostics || gameplayUi == null)
            {
                jitterInitialized = false;
                return;
            }

            SampleJitter();
        }

        void SampleJitter()
        {
            NetworkExplorerController explorer = ResolveOwnedExplorer();
            Camera camera = Camera.main;
            if (explorer == null || camera == null || explorer.Motor == null)
            {
                jitterInitialized = false;
                return;
            }

            Transform cameraTransform = camera.transform;
            Rigidbody body = explorer.Motor.Rigidbody;
            CelestialBody planet = explorer.Motor.ActorProbe.CurrentSample.Body;
            Vector3 cameraPosition = cameraTransform.position;
            Quaternion cameraRotation = cameraTransform.rotation;
            Vector3 bodyPosition = explorer.transform.position;
            Vector3 rigidbodyPosition = body != null ? body.position : bodyPosition;
            Vector3 planetPosition = planet != null ? planet.transform.position : Vector3.zero;
            Quaternion planetRotation = planet != null ? planet.transform.rotation : Quaternion.identity;

            if (jitterInitialized)
            {
                peakCameraShiftMillimeters = Mathf.Max(
                    peakCameraShiftMillimeters,
                    Vector3.Distance(cameraPosition, lastCameraPosition) * 1000f);
                peakCameraTurnDegrees = Mathf.Max(
                    peakCameraTurnDegrees,
                    Quaternion.Angle(cameraRotation, lastCameraRotation));
                peakBodyShiftMillimeters = Mathf.Max(
                    peakBodyShiftMillimeters,
                    Vector3.Distance(bodyPosition, lastBodyPosition) * 1000f);
                peakRigidbodyShiftMillimeters = Mathf.Max(
                    peakRigidbodyShiftMillimeters,
                    Vector3.Distance(rigidbodyPosition, lastRigidbodyPosition) * 1000f);
                peakPlanetShiftMillimeters = Mathf.Max(
                    peakPlanetShiftMillimeters,
                    Vector3.Distance(planetPosition, lastPlanetPosition) * 1000f);
                peakPlanetTurnMicroRadians = Mathf.Max(
                    peakPlanetTurnMicroRadians,
                    Quaternion.Angle(planetRotation, lastPlanetRotation) * Mathf.Deg2Rad * 1e6f);
            }

            lastCameraPosition = cameraPosition;
            lastCameraRotation = cameraRotation;
            lastBodyPosition = bodyPosition;
            lastRigidbodyPosition = rigidbodyPosition;
            lastPlanetPosition = planetPosition;
            lastPlanetRotation = planetRotation;
            jitterInitialized = true;
        }

        static NetworkExplorerController ResolveOwnedExplorer()
        {
            for (int i = 0; i < NetworkExplorerController.ActiveExplorers.Count; i++)
            {
                NetworkExplorerController explorer = NetworkExplorerController.ActiveExplorers[i];
                if (explorer != null && explorer.IsOwner)
                {
                    return explorer;
                }
            }

            return null;
        }

        void SampleDiagnostics()
        {
            if (networkManager == null || !networkManager.IsClientStarted)
            {
                return;
            }

            float frameMilliseconds = Time.unscaledDeltaTime * 1000f;
            if (frameMilliseconds > recentPeakMilliseconds)
            {
                recentPeakMilliseconds = frameMilliseconds;
            }

            if (Time.unscaledDeltaTime >
                networkManager.TimeManager.TickDelta * maximumFrameTicks)
            {
                droppedWindow++;
            }

            if (Time.unscaledTime >= droppedWindowEnd)
            {
                recentDroppedFrames = droppedWindow;
                droppedWindow = 0;
                droppedWindowEnd = Time.unscaledTime + WindowSeconds;
            }

            if (Time.unscaledTime >= peakWindowEnd)
            {
                recentPeakMilliseconds = frameMilliseconds;
                peakWindowEnd = Time.unscaledTime + WindowSeconds;
            }

            uint sequence = ResolveLocalOriginSequence();
            if (sequence != lastOriginSequence)
            {
                lastOriginSequence = sequence;
                originShifts++;
            }
        }

        static uint ResolveLocalOriginSequence()
        {
            ZoneOriginState zone = MultiplayerSceneContext.Active != null
                ? MultiplayerSceneContext.Active.ZoneOrigin
                : null;
            return zone?.CurrentSequence ?? 0u;
        }

        void OnPostReconcile(uint clientTick, uint serverTick)
        {
            reconcileWindow++;
            if (Time.unscaledTime < reconcileWindowEnd)
            {
                return;
            }

            reconcilesPerSecond = reconcileWindow;
            reconcileWindow = 0;
            reconcileWindowEnd = Time.unscaledTime + 1f;
        }

        string BuildStatus()
        {
            MultiplayerSessionController session = MultiplayerSessionController.Active;
            if (networkManager == null ||
                !networkManager.IsClientStarted ||
                session == null ||
                (session.IsPrivate && !showDiagnostics))
            {
                return null;
            }

            string headline = networkManager.IsServerStarted
                ? UiLocalization.Get(UiTextKeys.CoopStatusHost)
                : string.Format(
                    UiLocalization.Get(UiTextKeys.CoopStatusPing),
                    networkManager.TimeManager.RoundTripTime);
            if (!showDiagnostics)
            {
                return headline;
            }

            string status = $"{headline}\n" +
                $"DROP/5S {recentDroppedFrames}  PEAK/5S {recentPeakMilliseconds:0} MS  " +
                $"SHIFT {originShifts}  RECON/S {reconcilesPerSecond}\n" +
                $"FIX {PredictionDiagnostics.LastPositionError:0.000} M  " +
                $"MAXFIX {PredictionDiagnostics.WorstPositionError:0.00} M  " +
                $"MAXROT {PredictionDiagnostics.WorstRotationError:0.0} DEG\n" +
                $"JIT CAM {peakCameraShiftMillimeters:0.00} MM {peakCameraTurnDegrees:0.0000} DEG  " +
                $"BODY {peakBodyShiftMillimeters:0.00} MM  RB {peakRigidbodyShiftMillimeters:0.00} MM  " +
                $"PLANET {peakPlanetShiftMillimeters:0.00} MM {peakPlanetTurnMicroRadians:0.00} URAD\n" +
                $"CPU {AverageMilliseconds(mainThreadRecorder):0.0} MS  " +
                $"GPUWAIT {AverageMilliseconds(presentWaitRecorder):0.0} MS  " +
                $"DRAW {drawCallRecorder.LastValue}  SETPASS {setPassRecorder.LastValue}";
            peakCameraShiftMillimeters = 0f;
            peakCameraTurnDegrees = 0f;
            peakBodyShiftMillimeters = 0f;
            peakRigidbodyShiftMillimeters = 0f;
            peakPlanetShiftMillimeters = 0f;
            peakPlanetTurnMicroRadians = 0f;
            return status;
        }

        static float AverageMilliseconds(ProfilerRecorder recorder)
        {
            if (!recorder.Valid || recorder.Count == 0)
            {
                return 0f;
            }

            double total = 0d;
            for (int i = 0; i < recorder.Count; i++)
            {
                total += recorder.GetSample(i).Value;
            }

            return (float)(total / recorder.Count * 1e-6);
        }
    }
}
