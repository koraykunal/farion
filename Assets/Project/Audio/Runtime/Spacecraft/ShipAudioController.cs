using System.Collections.Generic;
using Farion.Gameplay.Flight;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace Farion.Audio.Spacecraft
{
    [DefaultExecutionOrder(320)]
    [DisallowMultipleComponent]
    public sealed class ShipAudioController : MonoBehaviour
    {
        const string DefaultRpmParameter = "Rpm";
        const string DefaultLoadParameter = "Load";
        const string DefaultBoostParameter = "Boost";
        const string DefaultRollParameter = "Roll";
        const string DefaultEngineStateParameter = "EngineState";
        const string DefaultPerspectiveParameter = "Perspective";
        const float StartupState = 1f;
        const float RunningState = 2f;
        const float ShutdownState = 3f;

        [Header("FMOD")]
        [SerializeField] EventReference engineEvent;
        [SerializeField] bool playOnEnable = true;
        [SerializeField] bool attachToRigidbody = true;
        [SerializeField] bool allowFadeoutOnStop = true;

        [Header("Sources")]
        [SerializeField] ShipAudioTelemetryProvider telemetryProvider;
        [SerializeField] SpacecraftMotor motor;

        [Header("FMOD Parameters")]
        [SerializeField] string rpmParameter = DefaultRpmParameter;
        [SerializeField] string loadParameter = DefaultLoadParameter;
        [SerializeField] string boostParameter = DefaultBoostParameter;
        [SerializeField] string rollParameter = DefaultRollParameter;
        [SerializeField] string engineStateParameter = DefaultEngineStateParameter;
        [SerializeField] string perspectiveParameter = DefaultPerspectiveParameter;
        [Tooltip("Optional. Leave empty until this parameter exists on the FMOD event.")]
        [SerializeField] string hullStressParameter;
        [Tooltip("Optional. Leave empty until this parameter exists on the FMOD event.")]
        [SerializeField] string impactParameter;
        [Tooltip("Optional. Leave empty until this parameter exists on the FMOD event.")]
        [SerializeField] string atmosphereParameter;
        [Tooltip("Optional. Leave empty until this parameter exists on the FMOD event.")]
        [SerializeField] string waterSubmersionParameter;

        [Header("Engine State")]
        [Min(0f)]
        [SerializeField] float startupSeconds = 0.75f;

        [Header("RPM Model")]
        [Range(0f, 100f)]
        [SerializeField] float idleRpm = 18f;
        [Range(0f, 100f)]
        [SerializeField] float maximumRpm = 100f;
        [Range(0f, 1f)]
        [SerializeField] float speedRpmWeight = 0.42f;
        [Range(0f, 1f)]
        [SerializeField] float thrustRpmWeight = 0.24f;
        [Range(0f, 1f)]
        [SerializeField] float loadRpmWeight = 0.14f;
        [Range(0f, 1f)]
        [SerializeField] float boostRpmWeight = 0.2f;

        [Header("Response")]
        [Min(0f)]
        [SerializeField] float rpmSpoolUpSeconds = 0.9f;
        [Min(0f)]
        [SerializeField] float rpmSpoolDownSeconds = 1.8f;
        [Min(0f)]
        [SerializeField] float loadAttackSeconds = 0.18f;
        [Min(0f)]
        [SerializeField] float loadReleaseSeconds = 0.5f;
        [Min(0f)]
        [SerializeField] float boostAttackSeconds = 0.12f;
        [Min(0f)]
        [SerializeField] float boostReleaseSeconds = 0.4f;
        [Min(0f)]
        [SerializeField] float rollResponse = 7f;

        [Header("Runtime Debug")]
        [SerializeField, Range(0f, 100f)] float debugRpm;
        [SerializeField, Range(0f, 1f)] float debugLoad;
        [SerializeField, Range(0f, 1f)] float debugBoost;
        [SerializeField, Range(-1f, 1f)] float debugRoll;
        [SerializeField] float debugEngineState;
        [SerializeField, Range(0f, 1f)] float debugPerspective;
        [SerializeField, Range(0f, 1f)] float debugHullStress;
        [SerializeField, Range(0f, 1f)] float debugImpact;
        [SerializeField, Range(0f, 1f)] float debugAtmosphere;
        [SerializeField, Range(0f, 1f)] float debugWaterSubmersion;

        EventInstance engineInstance;
        bool engineStarted;
        float engineAge;
        float smoothedRpm01;
        float rpmVelocity;
        float smoothedLoad;
        float loadVelocity;
        float smoothedBoost;
        float boostVelocity;
        float smoothedRoll;
        Rigidbody cachedRigidbody;
        readonly HashSet<string> warnedParameterNames = new();
        bool warnedUnexpectedStop;

        void Awake()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            ResolveReferences();
            if (playOnEnable)
            {
                StartEngine();
            }
        }

        void OnValidate()
        {
            startupSeconds = Mathf.Max(0f, startupSeconds);
            idleRpm = Mathf.Clamp(idleRpm, 0f, 100f);
            maximumRpm = Mathf.Clamp(maximumRpm, idleRpm, 100f);
            speedRpmWeight = Mathf.Clamp01(speedRpmWeight);
            thrustRpmWeight = Mathf.Clamp01(thrustRpmWeight);
            loadRpmWeight = Mathf.Clamp01(loadRpmWeight);
            boostRpmWeight = Mathf.Clamp01(boostRpmWeight);
            rpmSpoolUpSeconds = Mathf.Max(0f, rpmSpoolUpSeconds);
            rpmSpoolDownSeconds = Mathf.Max(0f, rpmSpoolDownSeconds);
            loadAttackSeconds = Mathf.Max(0f, loadAttackSeconds);
            loadReleaseSeconds = Mathf.Max(0f, loadReleaseSeconds);
            boostAttackSeconds = Mathf.Max(0f, boostAttackSeconds);
            boostReleaseSeconds = Mathf.Max(0f, boostReleaseSeconds);
            rollResponse = Mathf.Max(0f, rollResponse);
            ResolveReferences();
        }

        void Update()
        {
            ResolveReferences();
            if (!engineStarted || telemetryProvider == null)
            {
                return;
            }

            if (HasStoppedUnexpectedly())
            {
                StopReleasedInstance();
                return;
            }

            telemetryProvider.RefreshTelemetry();
            ShipAudioTelemetry telemetry = telemetryProvider.Telemetry;
            engineAge += Time.deltaTime;

            float thrustActivity = CalculateMainThrusterActivity(telemetry);
            float targetRpm01 = Mathf.Clamp01(
                telemetry.NormalizedSpeed * speedRpmWeight +
                thrustActivity * thrustRpmWeight +
                telemetry.EngineLoad * loadRpmWeight +
                telemetry.Boost * boostRpmWeight);

            smoothedRpm01 = SmoothTowards(
                smoothedRpm01,
                targetRpm01,
                ref rpmVelocity,
                targetRpm01 > smoothedRpm01 ? rpmSpoolUpSeconds : rpmSpoolDownSeconds,
                Time.deltaTime);

            smoothedLoad = SmoothTowards(
                smoothedLoad,
                telemetry.EngineLoad,
                ref loadVelocity,
                telemetry.EngineLoad > smoothedLoad ? loadAttackSeconds : loadReleaseSeconds,
                Time.deltaTime);

            smoothedBoost = SmoothTowards(
                smoothedBoost,
                telemetry.Boost,
                ref boostVelocity,
                telemetry.Boost > smoothedBoost ? boostAttackSeconds : boostReleaseSeconds,
                Time.deltaTime);

            float targetRoll = motor != null ? Mathf.Clamp(motor.LastLocalRotationInput.z, -1f, 1f) : 0f;
            smoothedRoll = Mathf.MoveTowards(smoothedRoll, targetRoll, rollResponse * Time.deltaTime);

            debugRpm = Mathf.Lerp(idleRpm, maximumRpm, smoothedRpm01);
            debugLoad = smoothedLoad;
            debugBoost = smoothedBoost;
            debugRoll = smoothedRoll;
            debugEngineState = engineAge < startupSeconds ? StartupState : RunningState;
            debugPerspective = IsInteriorPerspective(telemetry.Perspective) ? 1f : 0f;
            debugHullStress = telemetry.HullStress;
            debugImpact = telemetry.Impact;
            debugAtmosphere = telemetry.Atmosphere;
            debugWaterSubmersion = telemetry.WaterSubmersion;

            SetParameter(rpmParameter, debugRpm);
            SetParameter(loadParameter, debugLoad);
            SetParameter(boostParameter, debugBoost);
            SetParameter(rollParameter, debugRoll);
            SetParameter(engineStateParameter, debugEngineState);
            SetParameter(perspectiveParameter, debugPerspective);
            SetParameter(hullStressParameter, debugHullStress);
            SetParameter(impactParameter, debugImpact);
            SetParameter(atmosphereParameter, debugAtmosphere);
            SetParameter(waterSubmersionParameter, debugWaterSubmersion);
        }

        void OnDisable()
        {
            StopEngine();
        }

        void OnDestroy()
        {
            StopEngine();
        }

        public void StartEngine()
        {
            if (engineStarted || engineEvent.IsNull)
            {
                return;
            }

            try
            {
                engineInstance = RuntimeManager.CreateInstance(engineEvent);
            }
            catch (EventNotFoundException)
            {
                Debug.LogWarning($"FMOD ship engine event was not found: {engineEvent}.", this);
                return;
            }

            if (!engineInstance.isValid())
            {
                Debug.LogWarning($"FMOD ship engine event is invalid: {engineEvent}.", this);
                return;
            }

            if (attachToRigidbody)
            {
                cachedRigidbody ??= GetComponent<Rigidbody>();
            }

            if (attachToRigidbody && cachedRigidbody != null)
            {
                RuntimeManager.AttachInstanceToGameObject(engineInstance, gameObject, cachedRigidbody);
            }
            else
            {
                RuntimeManager.AttachInstanceToGameObject(engineInstance, gameObject);
            }

            engineAge = 0f;
            smoothedRpm01 = 0f;
            rpmVelocity = 0f;
            smoothedLoad = 0f;
            loadVelocity = 0f;
            smoothedBoost = 0f;
            boostVelocity = 0f;
            smoothedRoll = 0f;
            warnedUnexpectedStop = false;
            warnedParameterNames.Clear();
            engineStarted = true;
            SetParameter(engineStateParameter, StartupState);
            SetParameter(rpmParameter, idleRpm);
            engineInstance.start();
        }

        public void StopEngine()
        {
            if (!engineStarted)
            {
                return;
            }

            SetParameter(engineStateParameter, ShutdownState);
            engineInstance.stop(allowFadeoutOnStop ? STOP_MODE.ALLOWFADEOUT : STOP_MODE.IMMEDIATE);
            StopReleasedInstance();
        }

        void ResolveReferences()
        {
            telemetryProvider ??= GetComponent<ShipAudioTelemetryProvider>();
            telemetryProvider ??= GetComponentInParent<ShipAudioTelemetryProvider>();
            motor ??= GetComponent<SpacecraftMotor>();
            motor ??= GetComponentInParent<SpacecraftMotor>();
        }

        void SetParameter(string parameterName, float value)
        {
            if (!engineStarted || string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            FMOD.RESULT result = engineInstance.setParameterByName(parameterName, value);
            if (result == FMOD.RESULT.OK || warnedParameterNames.Contains(parameterName))
            {
                return;
            }

            warnedParameterNames.Add(parameterName);
            Debug.LogWarning(
                $"FMOD ship engine parameter '{parameterName}' could not be set on '{engineEvent.Path}': {result}.",
                this);
        }

        bool HasStoppedUnexpectedly()
        {
            FMOD.RESULT result = engineInstance.getPlaybackState(out PLAYBACK_STATE playbackState);
            if (result != FMOD.RESULT.OK || playbackState != PLAYBACK_STATE.STOPPED)
            {
                return false;
            }

            if (!warnedUnexpectedStop)
            {
                warnedUnexpectedStop = true;
                Debug.LogWarning(
                    $"FMOD ship engine event stopped while ShipAudioController was active: {engineEvent.Path}. " +
                    "For engine audio, author the FMOD event as a looping/sustained event instead of a one-shot.",
                    this);
            }

            return true;
        }

        void StopReleasedInstance()
        {
            if (engineInstance.isValid())
            {
                engineInstance.release();
                engineInstance.clearHandle();
            }

            engineStarted = false;
        }

        static float CalculateMainThrusterActivity(ShipAudioTelemetry telemetry)
        {
            return Mathf.Clamp01(Mathf.Max(
                telemetry.ForwardThrust,
                telemetry.ReverseThrust * 0.6f,
                telemetry.LateralThrust * 0.45f,
                telemetry.VerticalThrust * 0.4f));
        }

        static bool IsInteriorPerspective(SpacecraftAudioPerspective perspective)
        {
            return perspective is SpacecraftAudioPerspective.Cockpit or
                SpacecraftAudioPerspective.ShipInterior;
        }

        static float SmoothTowards(float current, float target, ref float velocity, float smoothTime, float deltaTime)
        {
            if (smoothTime <= 0f || deltaTime <= 0f)
            {
                return target;
            }

            return Mathf.Clamp01(Mathf.SmoothDamp(current, target, ref velocity, smoothTime, Mathf.Infinity, deltaTime));
        }
    }
}
