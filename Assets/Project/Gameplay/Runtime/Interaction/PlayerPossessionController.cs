using System.Collections.Generic;
using Farion.Gameplay.Actors;
using Farion.Gameplay.Character;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Input;
using Farion.Gameplay.Resources;
using Farion.Simulation.Celestial;
using Farion.Simulation.World;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class PlayerPossessionController : MonoBehaviour
    {
        [Header("Mode")]
        [SerializeField] PlayerPossessionMode initialMode = PlayerPossessionMode.Spacecraft;
        [SerializeField] PlayerPossessionMode currentMode = PlayerPossessionMode.Spacecraft;
        [SerializeField] bool applyInitialModeOnAwake = true;

        [Header("Input")]
        [SerializeField] MonoBehaviour boardingInputSource;
        [SerializeField] PlayerControlLock controlLock;

        [Header("Spacecraft")]
        [SerializeField] Transform spacecraftRoot;
        [SerializeField] Rigidbody spacecraftRigidbody;
        [SerializeField] SpacecraftMotor spacecraftMotor;
        [SerializeField] KeyboardSpacecraftInput spacecraftInput;
        [SerializeField] SpacecraftCameraRig spacecraftCameraRig;
        [SerializeField] SpacecraftRig spacecraftRig;
        [SerializeField] Transform spacecraftCameraTarget;
        [SerializeField] VehicleBoardingPoint boardingPoint;
        [SerializeField] bool spawnInsideShipOnPilotExit = true;
        [SerializeField] bool openRampOnPilotExit;
        [SerializeField] bool closeRampOnEnter = true;
        [Min(0.1f)]
        [SerializeField] float exteriorTransitionDistance = 1.5f;

        [Header("Explorer")]
        [SerializeField] GameObject explorerRoot;
        [SerializeField] Rigidbody explorerRigidbody;
        [SerializeField] FirstPersonMotor explorerMotor;
        [SerializeField] KeyboardFirstPersonInput explorerInput;
        [SerializeField] FirstPersonCameraRig firstPersonCameraRig;
        [SerializeField] PlayerInteractionRaycaster explorerInteractionRaycaster;
        [Min(0f)]
        [SerializeField] float exitPoseClearance = 0.25f;

        [Header("Exterior Surface Exit")]
        [SerializeField] bool snapExplorerToExteriorSurface = true;
        [Min(0f)]
        [SerializeField] float exteriorGroundClearance = 0.08f;
        [Min(0f)]
        [SerializeField] float exteriorSurfaceClearance = 0.15f;

        [Header("World Origin")]
        [SerializeField] WorldOriginRebaser originRebaser;
        [SerializeField] bool updateOriginTrackingTarget = true;

        [Header("Resource Streaming")]
        [SerializeField] List<ResourceDepositRuntimeSpawner> resourceStreamers = new();
        [SerializeField] bool updateResourceStreamingTarget = true;

        IBoardingInputSource resolvedBoardingInput;
        CelestialActorProbe spacecraftCelestialProbe;
        CelestialActorProbe explorerCelestialProbe;
        bool explorerCanTransitionOutside;

        public PlayerPossessionMode CurrentMode => currentMode;
        public bool IsPilotingSpacecraft => currentMode == PlayerPossessionMode.Spacecraft;
        public bool IsOnFoot => currentMode == PlayerPossessionMode.OnFoot;
        public bool IsInShipInterior => currentMode == PlayerPossessionMode.ShipInterior;

        void Awake()
        {
            ResolveReferences();
            ResolveInputSource();

            if (applyInitialModeOnAwake)
            {
                ApplyMode(initialMode);
            }
        }

        void OnValidate()
        {
            exitPoseClearance = Mathf.Max(0f, exitPoseClearance);
            if (boardingInputSource != null && boardingInputSource is not IBoardingInputSource)
            {
                boardingInputSource = null;
            }
            exteriorTransitionDistance = Mathf.Max(0.1f, exteriorTransitionDistance);
            exteriorGroundClearance = Mathf.Max(0f, exteriorGroundClearance);
            exteriorSurfaceClearance = Mathf.Max(0f, exteriorSurfaceClearance);
        }

        void Update()
        {
            ResolveInputSource();

            BoardingInputState input = resolvedBoardingInput?.CurrentInput ?? BoardingInputState.None;
            if (currentMode == PlayerPossessionMode.Spacecraft)
            {
                if (input.ExitVehicle)
                {
                    ExitPilotSeat();
                }

                return;
            }

            if (currentMode == PlayerPossessionMode.ShipInterior)
            {
                RefreshExteriorTransition();
            }
        }

        [ContextMenu("Enter Spacecraft")]
        public void EnterSpacecraft()
        {
            ResolveReferences();
            if (closeRampOnEnter)
            {
                spacecraftRig?.CloseRamp();
            }

            ApplyMode(PlayerPossessionMode.Spacecraft);
        }

        [ContextMenu("Exit Spacecraft")]
        public void ExitSpacecraft()
        {
            ExitPilotSeat();
        }

        [ContextMenu("Exit Pilot Seat")]
        public void ExitPilotSeat()
        {
            ResolveReferences();
            if (openRampOnPilotExit)
            {
                spacecraftRig?.OpenRamp();
            }

            PlaceExplorerAtTransform(ResolvePilotExitTransform(), !spawnInsideShipOnPilotExit);
            ApplyMode(spawnInsideShipOnPilotExit ? PlayerPossessionMode.ShipInterior : PlayerPossessionMode.OnFoot);
        }

        [ContextMenu("Enter Ship Interior")]
        public void EnterShipInterior()
        {
            ResolveReferences();
            PlaceExplorerAtTransform(ResolveExteriorEntryTransform(), false);
            ApplyMode(PlayerPossessionMode.ShipInterior);
        }

        public void ApplyMode(PlayerPossessionMode nextMode)
        {
            ResolveReferences();
            currentMode = nextMode;

            bool piloting = currentMode == PlayerPossessionMode.Spacecraft;
            bool firstPerson = !piloting;
            SetBehaviourEnabled(firstPersonCameraRig, firstPerson);
            SetBehaviourEnabled(spacecraftMotor, true);
            SetBehaviourEnabled(spacecraftInput, piloting);
            SetBehaviourEnabled(spacecraftCameraRig, piloting);

            if (spacecraftCameraRig != null)
            {
                spacecraftCameraRig.SetTarget(ResolveSpacecraftCameraTarget());
                if (piloting)
                {
                    spacecraftCameraRig.SnapToTarget();
                }
            }

            if (explorerRoot != null)
            {
                explorerRoot.SetActive(firstPerson);
            }

            SetBehaviourEnabled(explorerMotor, firstPerson);
            SetBehaviourEnabled(explorerInput, firstPerson);
            explorerInput?.SetControlLock(controlLock);
            spacecraftInput?.SetControlLock(controlLock);
            explorerInteractionRaycaster?.SetControlLock(controlLock);

            if (firstPersonCameraRig != null && explorerMotor != null)
            {
                firstPersonCameraRig.SetTarget(explorerMotor);
                firstPersonCameraRig.SetInputSource(explorerInput);
            }

            if (updateOriginTrackingTarget && originRebaser != null)
            {
                Transform target = piloting
                    ? GetSpacecraftTrackingTarget()
                    : GetExplorerTrackingTarget();
                originRebaser.SetTrackingTarget(target);
            }

            if (updateResourceStreamingTarget)
            {
                Transform target = piloting
                    ? GetSpacecraftTrackingTarget()
                    : GetExplorerTrackingTarget();
                UpdateResourceStreamingTargets(target);
            }

        }

        void UpdateResourceStreamingTargets(Transform target)
        {
            if (resourceStreamers == null)
            {
                return;
            }

            for (int i = 0; i < resourceStreamers.Count; i++)
            {
                ResourceDepositRuntimeSpawner streamer = resourceStreamers[i];
                if (streamer != null)
                {
                    streamer.SetTrackingTarget(target);
                }
            }
        }

        void PlaceExplorerAtTransform(Transform targetTransform, bool snapToExteriorSurface)
        {
            if (explorerRoot == null)
            {
                return;
            }

            if (targetTransform == null)
            {
                return;
            }

            explorerRoot.SetActive(true);

            Vector3 up = targetTransform.up.sqrMagnitude > 0.0001f ? targetTransform.up.normalized : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(targetTransform.forward, up);
            if (forward.sqrMagnitude <= 0.0001f && spacecraftRoot != null)
            {
                forward = Vector3.ProjectOnPlane(spacecraftRoot.forward, up);
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.right, up);
            }

            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Quaternion rotation = Quaternion.LookRotation(forward, up);
            Vector3 position = targetTransform.position + up * exitPoseClearance;

            if (snapToExteriorSurface)
            {
                TryResolveExteriorSurfacePose(ref position, ref rotation);
            }

            ApplyExplorerPose(position, rotation, true);

            if (explorerMotor != null)
            {
                explorerMotor.ResetMotorState();
            }
        }

        void ApplyExplorerPose(Vector3 position, Quaternion rotation, bool inheritSpacecraftVelocity)
        {
            if (explorerRigidbody != null)
            {
                explorerRigidbody.position = position;
                explorerRigidbody.rotation = rotation;
                explorerRigidbody.linearVelocity = inheritSpacecraftVelocity && spacecraftRigidbody != null
                    ? spacecraftRigidbody.linearVelocity
                    : Vector3.zero;
                explorerRigidbody.angularVelocity = Vector3.zero;
            }
            else
            {
                explorerRoot.transform.SetPositionAndRotation(position, rotation);
            }

        }

        bool TryResolveExteriorSurfacePose(ref Vector3 position, ref Quaternion rotation)
        {
            if (!snapExplorerToExteriorSurface)
            {
                return false;
            }

            Vector3 velocity = explorerRigidbody != null
                ? explorerRigidbody.linearVelocity
                : spacecraftRigidbody != null
                    ? spacecraftRigidbody.linearVelocity
                    : Vector3.zero;

            if (!TrySampleExitFrame(position, velocity, out CelestialFrameSample frame))
            {
                return false;
            }

            CelestialSurfacePlacementOptions options = new(
                ResolveExplorerGroundCenterOffset(),
                exteriorSurfaceClearance,
                CelestialSurfacePlacementMode.TerrainSurface);
            if (!CelestialSurfacePlacement.TryResolvePose(
                    frame,
                    position,
                    rotation * Vector3.forward,
                    options,
                    out CelestialSurfacePlacementResult placement))
            {
                return false;
            }

            position = placement.Position;
            rotation = placement.Rotation;
            return true;
        }

        bool TrySampleExitFrame(Vector3 position, Vector3 velocity, out CelestialFrameSample frame)
        {
            CelestialFrameProvider frameProvider = ResolveExitFrameProvider();
            if (frameProvider != null)
            {
                return frameProvider.TrySample(position, velocity, out frame);
            }

            if (spacecraftCelestialProbe != null && spacecraftCelestialProbe.HasSample)
            {
                frame = spacecraftCelestialProbe.CurrentSample;
                return frame.HasBody;
            }

            if (explorerCelestialProbe != null && explorerCelestialProbe.HasSample)
            {
                frame = explorerCelestialProbe.CurrentSample;
                return frame.HasBody;
            }

            frame = CelestialFrameSample.Empty(position, velocity);
            return false;
        }

        CelestialFrameProvider ResolveExitFrameProvider()
        {
            ResolveReferences();

            if (explorerCelestialProbe != null && explorerCelestialProbe.FrameProvider != null)
            {
                return explorerCelestialProbe.FrameProvider;
            }

            if (spacecraftCelestialProbe != null && spacecraftCelestialProbe.FrameProvider != null)
            {
                return spacecraftCelestialProbe.FrameProvider;
            }

            return null;
        }

        float ResolveExplorerGroundCenterOffset()
        {
            if (explorerMotor != null)
            {
                CapsuleCollider capsule = explorerMotor.Capsule;
                if (capsule != null)
                {
                    return Mathf.Max(capsule.height * 0.5f, capsule.radius) + exteriorGroundClearance;
                }
            }

            if (explorerRoot != null && explorerRoot.TryGetComponent(out CapsuleCollider rootCapsule))
            {
                return Mathf.Max(rootCapsule.height * 0.5f, rootCapsule.radius) + exteriorGroundClearance;
            }

            return exitPoseClearance + exteriorGroundClearance;
        }

        void ResolveReferences()
        {
            if (controlLock == null)
            {
                controlLock = PlayerControlLock.Active;
            }

            if (spacecraftRoot != null)
            {
                spacecraftRigidbody ??= spacecraftRoot.GetComponent<Rigidbody>();
                spacecraftMotor ??= spacecraftRoot.GetComponent<SpacecraftMotor>();
                spacecraftInput ??= spacecraftRoot.GetComponent<KeyboardSpacecraftInput>();
                spacecraftRig ??= spacecraftRoot.GetComponent<SpacecraftRig>();
                spacecraftRig ??= spacecraftRoot.GetComponentInChildren<SpacecraftRig>(true);
                boardingPoint ??= spacecraftRoot.GetComponentInChildren<VehicleBoardingPoint>(true);
                spacecraftCelestialProbe ??= spacecraftRoot.GetComponent<CelestialActorProbe>();
            }

            if (explorerRoot != null)
            {
                explorerRigidbody ??= explorerRoot.GetComponent<Rigidbody>();
                explorerMotor ??= explorerRoot.GetComponent<FirstPersonMotor>();
                explorerInput ??= explorerRoot.GetComponent<KeyboardFirstPersonInput>();
                explorerInteractionRaycaster ??= explorerRoot.GetComponent<PlayerInteractionRaycaster>();
                explorerCelestialProbe ??= explorerRoot.GetComponent<CelestialActorProbe>();
            }
        }

        void ResolveInputSource()
        {
            if (boardingInputSource is IBoardingInputSource explicitSource)
            {
                resolvedBoardingInput = explicitSource;
                return;
            }

            resolvedBoardingInput ??= GetComponent<IBoardingInputSource>();
        }

        Transform GetSpacecraftTrackingTarget()
        {
            if (spacecraftRigidbody != null)
            {
                return spacecraftRigidbody.transform;
            }

            return spacecraftRoot;
        }

        Transform ResolveSpacecraftCameraTarget()
        {
            if (spacecraftCameraTarget != null)
            {
                return spacecraftCameraTarget;
            }

            if (spacecraftRig != null && spacecraftRig.ChaseCameraTarget != null)
            {
                return spacecraftRig.ChaseCameraTarget;
            }

            return spacecraftRoot;
        }

        Transform ResolveExplorerExitTransform()
        {
            if (boardingPoint != null && boardingPoint.HasExplicitExitPoint)
            {
                return boardingPoint.ExitPoint;
            }

            if (spacecraftRig != null && spacecraftRig.ExteriorExitPoint != null)
            {
                return spacecraftRig.ExteriorExitPoint;
            }

            if (boardingPoint != null)
            {
                return boardingPoint.ExitPoint;
            }

            return spacecraftRoot;
        }

        Transform ResolvePilotExitTransform()
        {
            if (spawnInsideShipOnPilotExit && spacecraftRig != null)
            {
                if (spacecraftRig.InteriorSpawnPoint != null)
                {
                    return spacecraftRig.InteriorSpawnPoint;
                }

                if (spacecraftRig.PilotSeatPoint != null)
                {
                    return spacecraftRig.PilotSeatPoint;
                }
            }

            return ResolveExplorerExitTransform();
        }

        Transform ResolveExteriorEntryTransform()
        {
            if (spacecraftRig != null)
            {
                if (spacecraftRig.InteriorSpawnPoint != null)
                {
                    return spacecraftRig.InteriorSpawnPoint;
                }

                if (spacecraftRig.ExteriorExitPoint != null)
                {
                    return spacecraftRig.ExteriorExitPoint;
                }
            }

            return ResolveExplorerExitTransform();
        }

        void RefreshExteriorTransition()
        {
            explorerCanTransitionOutside = false;
            if (spacecraftRig == null ||
                spacecraftRig.ExteriorExitPoint == null ||
                spacecraftRig.RampController == null ||
                !spacecraftRig.RampController.IsOpen ||
                explorerRoot == null)
            {
                return;
            }

            Vector3 explorerPosition = explorerRigidbody != null
                ? explorerRigidbody.position
                : explorerRoot.transform.position;
            Transform exteriorExit = spacecraftRig.ExteriorExitPoint;
            Vector3 exitForward = exteriorExit.forward.sqrMagnitude > 0.0001f
                ? exteriorExit.forward.normalized
                : exteriorExit.forward;
            float outsideDistance = Vector3.Dot(explorerPosition - exteriorExit.position, exitForward);
            explorerCanTransitionOutside = outsideDistance >= exteriorTransitionDistance;
            if (explorerCanTransitionOutside)
            {
                SnapExplorerToExteriorSurface();
                ApplyMode(PlayerPossessionMode.OnFoot);
            }
        }

        void SnapExplorerToExteriorSurface()
        {
            if (explorerRoot == null)
            {
                return;
            }

            Vector3 position = explorerRigidbody != null
                ? explorerRigidbody.position
                : explorerRoot.transform.position;
            Quaternion rotation = explorerRigidbody != null
                ? explorerRigidbody.rotation
                : explorerRoot.transform.rotation;

            if (!TryResolveExteriorSurfacePose(ref position, ref rotation))
            {
                return;
            }

            ApplyExplorerPose(position, rotation, true);
            if (explorerMotor != null)
            {
                explorerMotor.ResetMotorState();
            }
        }

        Transform GetExplorerTrackingTarget()
        {
            if (explorerRigidbody != null)
            {
                return explorerRigidbody.transform;
            }

            return explorerRoot != null ? explorerRoot.transform : null;
        }

        static void SetBehaviourEnabled(Behaviour behaviour, bool enabled)
        {
            if (behaviour != null)
            {
                behaviour.enabled = enabled;
            }
        }

    }
}

