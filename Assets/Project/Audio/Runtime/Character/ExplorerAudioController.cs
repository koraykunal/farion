using Farion.Core.Physics;
using Farion.Gameplay.Actors;
using Farion.Gameplay.Character;
using Farion.Simulation.Physics;
using Farion.Simulation.Planetary;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace Farion.Audio.Character
{
    [DefaultExecutionOrder(320)]
    [DisallowMultipleComponent]
    public sealed class ExplorerAudioController : MonoBehaviour
    {
        const string SurfaceParameter = "Surface";
        const string IntensityParameter = "Intensity";
        const string WetnessParameter = "Wetness";
        const float MetalSurfaceIndex = 4f;

        [Header("FMOD")]
        [SerializeField] EventReference footstepEvent;
        [SerializeField] EventReference jumpEvent;
        [SerializeField] EventReference landEvent;

        [Header("Sources")]
        [SerializeField] ExplorerLocomotionSignals signals;
        [SerializeField] FirstPersonMotor motor;
        [SerializeField] CelestialActorProbe probe;

        [Header("Response")]
        [Tooltip("Surface speed mapped to footstep Intensity 1.")]
        [Min(0.1f)]
        [SerializeField] float footstepIntensityReferenceSpeed = 6f;
        [Tooltip("Impact speed mapped to landing Intensity 1.")]
        [Min(0.1f)]
        [SerializeField] float landIntensityReferenceSpeed = 10f;
        [Tooltip("Footsteps are muted for this long after a landing so the land event reads clearly.")]
        [Min(0f)]
        [SerializeField] float stepSuppressAfterLandSeconds = 0.2f;

        float lastLandTime = float.NegativeInfinity;
        CelestialBody cachedBody;
        PlanetSurfaceModel cachedSurfaceModel;
        bool footstepEventDisabled;
        bool jumpEventDisabled;
        bool landEventDisabled;

        void Awake()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            ResolveReferences();
            if (signals != null)
            {
                signals.Stepped += HandleStepped;
                signals.Landed += HandleLanded;
                signals.Jumped += HandleJumped;
            }
        }

        void OnDisable()
        {
            if (signals != null)
            {
                signals.Stepped -= HandleStepped;
                signals.Landed -= HandleLanded;
                signals.Jumped -= HandleJumped;
            }
        }

        void ResolveReferences()
        {
            if (signals == null)
            {
                signals = GetComponent<ExplorerLocomotionSignals>();
            }

            if (motor == null)
            {
                motor = GetComponent<FirstPersonMotor>();
            }

            if (probe == null)
            {
                probe = GetComponent<CelestialActorProbe>();
            }
        }

        void HandleStepped(float speed)
        {
            if (Time.time - lastLandTime < stepSuppressAfterLandSeconds)
            {
                return;
            }

            FirstPersonMotorState state = signals.LastState;
            float wetness = state.TouchingWater
                ? Mathf.Clamp01(state.WaterSubmergedFraction)
                : 0f;
            PlayOneShot(
                footstepEvent,
                ref footstepEventDisabled,
                ResolveSurfaceIndex(),
                Mathf.Clamp01(speed / footstepIntensityReferenceSpeed),
                wetness);
        }

        void HandleLanded(float impactSpeed)
        {
            lastLandTime = Time.time;
            PlayOneShot(
                landEvent,
                ref landEventDisabled,
                ResolveSurfaceIndex(),
                Mathf.Clamp01(impactSpeed / landIntensityReferenceSpeed),
                null);
        }

        void HandleJumped()
        {
            PlayOneShot(jumpEvent, ref jumpEventDisabled, null, null, null);
        }

        float ResolveSurfaceIndex()
        {
            if (motor != null && motor.GroundLayer == FarionLayers.SpacecraftInterior)
            {
                return MetalSurfaceIndex;
            }

            CelestialBody body = probe != null ? probe.CurrentSample.Body : null;
            if (body == null)
            {
                return 0f;
            }

            if (!ReferenceEquals(body, cachedBody))
            {
                cachedBody = body;
                cachedSurfaceModel = body.GetComponent<PlanetSurfaceModel>();
            }

            if (cachedSurfaceModel == null ||
                !cachedSurfaceModel.TrySamplePlanetSurface(
                    body,
                    transform.position,
                    out PlanetSurfaceSample sample) ||
                sample.SurfaceMaterial.Material == null)
            {
                return 0f;
            }

            return ResolveSurfaceIndex(sample.SurfaceMaterial.Material.Category);
        }

        internal static float ResolveSurfaceIndex(SurfaceMaterialCategory category)
        {
            return category switch
            {
                SurfaceMaterialCategory.Regolith => 1f,
                SurfaceMaterialCategory.Soil => 2f,
                SurfaceMaterialCategory.Ice => 3f,
                _ => 0f
            };
        }

        void PlayOneShot(
            EventReference eventReference,
            ref bool eventDisabled,
            float? surface,
            float? intensity,
            float? wetness)
        {
            if (eventDisabled || eventReference.IsNull)
            {
                return;
            }

            EventInstance instance;
            try
            {
                instance = RuntimeManager.CreateInstance(eventReference);
            }
            catch (EventNotFoundException)
            {
                eventDisabled = true;
                Debug.LogWarning($"FMOD explorer event was not found: {eventReference}.", this);
                return;
            }

            if (!instance.isValid())
            {
                eventDisabled = true;
                Debug.LogWarning($"FMOD explorer event is invalid: {eventReference}.", this);
                return;
            }

            RuntimeManager.AttachInstanceToGameObject(instance, gameObject);
            if (surface.HasValue)
            {
                instance.setParameterByName(SurfaceParameter, surface.Value);
            }

            if (intensity.HasValue)
            {
                instance.setParameterByName(IntensityParameter, intensity.Value);
            }

            if (wetness.HasValue)
            {
                instance.setParameterByName(WetnessParameter, wetness.Value);
            }

            instance.start();
            instance.release();
        }
    }
}
