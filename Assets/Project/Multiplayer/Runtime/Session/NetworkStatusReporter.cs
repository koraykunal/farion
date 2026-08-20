using Farion.Multiplayer.World;
using Farion.UI.Gameplay;
using FishNet.Managing;
using FishNet.Managing.Predicting;
using UnityEngine;

namespace Farion.Multiplayer.Session
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class NetworkStatusReporter : MonoBehaviour
    {
        const string HostLabel = "HOST";
        const string ConnectingLabel = "CONNECTING";
        const float WindowSeconds = 5f;

        [SerializeField] NetworkManager networkManager;
        [SerializeField] NetworkWorldOriginAuthority originAuthority;
        [SerializeField, Min(0.1f)] float refreshInterval = 0.5f;
        [SerializeField] bool showDiagnostics;
        [SerializeField, Range(1, 25)] int maximumFrameTicks = 3;

        GameplayUiController gameplayUi;
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

        void Awake()
        {
            networkManager ??= GetComponent<NetworkManager>();
            originAuthority ??= GetComponent<NetworkWorldOriginAuthority>();
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
        }

        void OnDisable()
        {
            if (predictionManager != null)
            {
                predictionManager.OnPostReconcile -= OnPostReconcile;
            }
        }

        public void BindPresentation(GameplayUiController controller)
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
            lastOriginSequence = originAuthority != null
                ? originAuthority.CurrentSequence
                : 0u;
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
            SampleDiagnostics();
            if (gameplayUi == null || Time.unscaledTime < nextRefreshTime)
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

        void SampleDiagnostics()
        {
            if (networkManager == null ||
                !networkManager.IsClientStarted ||
                networkManager.TimeManager == null)
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

            uint sequence = originAuthority != null
                ? originAuthority.CurrentSequence
                : 0u;
            if (sequence != lastOriginSequence)
            {
                lastOriginSequence = sequence;
                originShifts++;
            }
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
            if (networkManager == null || !networkManager.IsClientStarted)
            {
                return null;
            }

            if (networkManager.TimeManager == null)
            {
                return ConnectingLabel;
            }

            string headline = networkManager.IsServerStarted
                ? HostLabel
                : $"{networkManager.TimeManager.RoundTripTime} MS";
            if (!showDiagnostics)
            {
                return headline;
            }

            return $"{headline}\n" +
                $"DROP/5S {recentDroppedFrames}  PEAK/5S {recentPeakMilliseconds:0} MS  " +
                $"SHIFT {originShifts}  RECON/S {reconcilesPerSecond}\n" +
                $"FIX {PredictionDiagnostics.LastPositionError:0.000} M  " +
                $"MAXFIX {PredictionDiagnostics.WorstPositionError:0.00} M  " +
                $"MAXROT {PredictionDiagnostics.WorstRotationError:0.0} DEG";
        }
    }
}
