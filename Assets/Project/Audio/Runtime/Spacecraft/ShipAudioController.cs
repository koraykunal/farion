using System.Collections.Generic;
using Farion.Gameplay.Flight;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.Serialization;

namespace Farion.Audio.Spacecraft
{
    [DefaultExecutionOrder(320)]
    [DisallowMultipleComponent]
    public sealed class ShipAudioController : MonoBehaviour
    {
        const string DefaultSpeedParameter = "Speed";
        const string DefaultBoostParameter = "Boost";
        const string DefaultRollParameter = "Roll";

        [Header("FMOD")]
        [SerializeField] EventReference engineEvent;
        [SerializeField] EventReference boostIgnitionEvent;
        [SerializeField] EventReference boostShutdownEvent;
        [SerializeField] bool playOnEnable = true;
        [SerializeField] bool attachToRigidbody = true;
        [SerializeField] bool allowFadeoutOnStop = true;

        [Header("Sources")]
        [SerializeField] ShipAudioTelemetryProvider telemetryProvider;
        [SerializeField] SpacecraftMotor motor;

        [Header("FMOD Parameters")]
        [FormerlySerializedAs("rpmParameter")]
        [SerializeField] string speedParameter = DefaultSpeedParameter;
        [SerializeField] string boostParameter = DefaultBoostParameter;
        [SerializeField] string rollParameter = DefaultRollParameter;
        [Tooltip("Optional. Use for thrust/load texture that must remain independent from ship speed.")]
        [SerializeField] string loadParameter;
        [Tooltip("Optional. Use 0 for exterior and 1 for cockpit/interior.")]
        [SerializeField] string perspectiveParameter;
        [Tooltip("Optional. Leave empty until this parameter exists on the FMOD event.")]
        [SerializeField] string hullStressParameter;
        [Tooltip("Optional. Leave empty until this parameter exists on the FMOD event.")]
        [SerializeField] string impactParameter;
        [Tooltip("Optional. Leave empty until this parameter exists on the FMOD event.")]
        [SerializeField] string atmosphereParameter;
        [Tooltip("Optional. Leave empty until this parameter exists on the FMOD event.")]
        [SerializeField] string waterSubmersionParameter;

        [Header("Response")]
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
        [SerializeField, Range(0f, 100f)] float debugSpeed;
        [SerializeField, Range(0f, 1f)] float debugLoad;
        [SerializeField, Range(0f, 1f)] float debugBoost;
        [SerializeField, Range(-1f, 1f)] float debugRoll;
        [SerializeField, Range(0f, 1f)] float debugPerspective;
        [SerializeField, Range(0f, 1f)] float debugHullStress;
        [SerializeField, Range(0f, 1f)] float debugImpact;
        [SerializeField, Range(0f, 1f)] float debugAtmosphere;
        [SerializeField, Range(0f, 1f)] float debugWaterSubmersion;

        EventInstance engineInstance;
        bool engineStarted;
        bool boostWasActive;
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

            BoostTransition boostTransition =
                EvaluateBoostTransition(boostWasActive, telemetry.BoostActive);
            boostWasActive = telemetry.BoostActive;
            if (boostTransition == BoostTransition.Ignition)
            {
                PlayAttachedOneShot(boostIgnitionEvent);
            }
            else if (boostTransition == BoostTransition.Shutdown)
            {
                PlayAttachedOneShot(boostShutdownEvent);
            }

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

            debugSpeed = telemetry.NormalizedSpeed * 100f;
            debugLoad = smoothedLoad;
            debugBoost = smoothedBoost;
            debugRoll = smoothedRoll;
            debugPerspective = IsInteriorPerspective(telemetry.Perspective) ? 1f : 0f;
            debugHullStress = telemetry.HullStress;
            debugImpact = telemetry.Impact;
            debugAtmosphere = telemetry.Atmosphere;
            debugWaterSubmersion = telemetry.WaterSubmersion;

            SetParameter(speedParameter, debugSpeed);
            SetParameter(boostParameter, debugBoost);
            SetParameter(rollParameter, debugRoll);
            SetParameter(loadParameter, debugLoad);
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

            boostWasActive = false;
            smoothedLoad = 0f;
            loadVelocity = 0f;
            smoothedBoost = 0f;
            boostVelocity = 0f;
            smoothedRoll = 0f;
            warnedUnexpectedStop = false;
            warnedParameterNames.Clear();
            engineStarted = true;
            SetParameter(speedParameter, 0f);
            SetParameter(boostParameter, 0f);
            SetParameter(rollParameter, 0f);
            engineInstance.start();
        }

        public void StopEngine()
        {
            if (!engineStarted)
            {
                return;
            }

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
                $"FMOD ship engine parameter '{parameterName}' could not be set on '{engineEvent}': {result}.",
                this);
        }

        void PlayAttachedOneShot(EventReference eventReference)
        {
            if (!eventReference.IsNull)
            {
                RuntimeManager.PlayOneShotAttached(eventReference, gameObject);
            }
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
                    $"FMOD ship engine event stopped while ShipAudioController was active: {engineEvent}. " +
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
            boostWasActive = false;
        }

        internal static BoostTransition EvaluateBoostTransition(
            bool wasActive,
            bool isActive)
        {
            if (wasActive == isActive)
            {
                return BoostTransition.None;
            }

            return isActive
                ? BoostTransition.Ignition
                : BoostTransition.Shutdown;
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

        internal enum BoostTransition
        {
            None,
            Ignition,
            Shutdown
        }
    }
}
