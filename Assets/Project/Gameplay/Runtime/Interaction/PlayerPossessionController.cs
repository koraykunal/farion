using System.Collections.Generic;
using Farion.Core.Persistence;
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
        [SerializeField] SpacecraftPilotCameraView initialPilotCameraView = SpacecraftPilotCameraView.Exterior;
        [SerializeField] SpacecraftPilotCameraView currentPilotCameraView = SpacecraftPilotCameraView.Exterior;
        [SerializeField] VehicleBoardingPoint boardingPoint;
        [SerializeField] bool spawnInsideShipOnPilotExit = true;
        [SerializeField] bool openRampOnPilotExit;
        [SerializeField] bool closeRampOnEnter = true;
        [Min(0.1f)]
        [SerializeField] float exteriorTransitionDistance = 1.5f;
        [Min(0f)]
        [SerializeField] float exteriorTransitionCooldownSeconds = 0.35f;
        [Min(0f)]
        [SerializeField] float exteriorTransitionProgressDistance = 0.75f;

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
        readonly SpacecraftExteriorCollisionGate exteriorCollisionGate = new();
        readonly ShipInteriorExitGate shipInteriorExitGate = new();

        public PlayerPossessionMode CurrentMode => currentMode;
        public SpacecraftPilotCameraView CurrentPilotCameraView => currentPilotCameraView;
        public bool IsPilotingSpacecraft => currentMode == PlayerPossessionMode.Spacecraft;
        public SpacecraftMotor SpacecraftMotor
        {
            get
            {
                ResolveReferences();
                return spacecraftMotor;
            }
        }
        public bool IsOnFoot => currentMode == PlayerPossessionMode.OnFoot;
        public bool IsInShipInterior => currentMode == PlayerPossessionMode.ShipInterior;
        public bool CanEnterSpacecraft => PlayerPossessionTransitionPolicy.CanTransition(
            currentMode,
            PlayerPossessionMode.Spacecraft,
            PlayerPossessionTransitionRequest.EnterPilotSeat);
        public bool CanEnterShipInterior => PlayerPossessionTransitionPolicy.CanTransition(
            currentMode,
            PlayerPossessionMode.ShipInterior,
            PlayerPossessionTransitionRequest.EnterShipInterior);
        public bool CanExitPilotSeat
        {
            get
            {
                PlayerPossessionMode nextMode = spawnInsideShipOnPilotExit
                    ? PlayerPossessionMode.ShipInterior
                    : PlayerPossessionMode.OnFoot;
                return PlayerPossessionTransitionPolicy.CanTransition(
                    currentMode,
                    nextMode,
                    PlayerPossessionTransitionRequest.ExitPilotSeat);
            }
        }
        public bool HasPersistentExplorerTarget
        {
            get
            {
                ResolveReferences();
                return !string.IsNullOrEmpty(ResolveExplorerPersistentId());
            }
        }

        public bool HasPersistentSpacecraftTarget
        {
            get
            {
                ResolveReferences();
                return !string.IsNullOrEmpty(ResolveSpacecraftPersistentId());
            }
        }

        public bool HasPersistentTargets => HasPersistentExplorerTarget && HasPersistentSpacecraftTarget;

        public PlayerPossessionSnapshot CaptureSnapshot()
        {
            ResolveReferences();
            if (!HasPersistentTargets)
            {
                return null;
            }

            return new PlayerPossessionSnapshot(
                currentMode,
                ResolveExplorerPersistentId(),
                TransformPoseSnapshot.Capture(explorerRigidbody, explorerRoot != null ? explorerRoot.transform : null),
                ResolveSpacecraftPersistentId(),
                TransformPoseSnapshot.Capture(spacecraftRigidbody, spacecraftRoot),
                currentPilotCameraView);
        }

        public bool ApplySnapshot(PlayerPossessionSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsSupported || !PlayerPossessionTransitionPolicy.IsSupportedMode(snapshot.Mode))
            {
                return false;
            }

            ResolveReferences();
            if (!CanApplySnapshot(snapshot))
            {
                return false;
            }

            PlayerPossessionPose.Apply(spacecraftRigidbody, spacecraftRoot, snapshot.SpacecraftPose);

            if (explorerRoot != null)
            {
                explorerRoot.SetActive(true);
            }

            PlayerPossessionPose.Apply(
                explorerRigidbody,
                explorerRoot != null ? explorerRoot.transform : null,
                snapshot.ExplorerPose);
            explorerMotor?.ResetMotorState();
            currentPilotCameraView = snapshot.PilotCameraView;
            ApplyMode(snapshot.Mode, PlayerPossessionTransitionRequest.RestoreSnapshot);
            return true;
        }

        public bool CanApplySnapshot(PlayerPossessionSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsSupported || !PlayerPossessionTransitionPolicy.IsSupportedMode(snapshot.Mode))
            {
                return false;
            }

            ResolveReferences();
            return PlayerPossessionIdentity.SnapshotIdMatches(snapshot.ExplorerId, ResolveExplorerPersistentId()) &&
                   PlayerPossessionIdentity.SnapshotIdMatches(snapshot.SpacecraftId, ResolveSpacecraftPersistentId());
        }

        void Awake()
        {
            ResolveReferences();
            BindPossessionInteractables();
            BindPossessionContextReceivers();
            ResolveInputSource();
            currentPilotCameraView = initialPilotCameraView;

            if (applyInitialModeOnAwake)
            {
                ApplyMode(initialMode, PlayerPossessionTransitionRequest.Bootstrap);
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
            exteriorTransitionCooldownSeconds = Mathf.Max(0f, exteriorTransitionCooldownSeconds);
            exteriorTransitionProgressDistance = Mathf.Max(0f, exteriorTransitionProgressDistance);
            exteriorGroundClearance = Mathf.Max(0f, exteriorGroundClearance);
            exteriorSurfaceClearance = Mathf.Max(0f, exteriorSurfaceClearance);
        }

        void Update()
        {
            ResolveInputSource();

            BoardingInputState input = resolvedBoardingInput?.CurrentInput ?? BoardingInputState.None;
            if (currentMode == PlayerPossessionMode.Spacecraft)
            {
                if (input.TogglePilotCamera)
                {
                    TogglePilotCameraView();
                }

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

        [ContextMenu("Enter Pilot Seat")]
        public bool EnterPilotSeat()
        {
            ResolveReferences();
            if (!PlayerPossessionTransitionPolicy.CanTransition(
                    currentMode,
                    PlayerPossessionMode.Spacecraft,
                    PlayerPossessionTransitionRequest.EnterPilotSeat))
            {
                return false;
            }

            if (closeRampOnEnter)
            {
                spacecraftRig?.CloseRamp();
            }

            ApplyMode(PlayerPossessionMode.Spacecraft, PlayerPossessionTransitionRequest.EnterPilotSeat);
            return true;
        }

        public void TogglePilotCameraView()
        {
            SetPilotCameraView(currentPilotCameraView == SpacecraftPilotCameraView.Exterior
                ? SpacecraftPilotCameraView.Cockpit
                : SpacecraftPilotCameraView.Exterior);
        }

        public void SetPilotCameraView(SpacecraftPilotCameraView view)
        {
            currentPilotCameraView = view;
            if (spacecraftCameraRig == null)
            {
                return;
            }

            spacecraftCameraRig.SetExteriorTarget(ResolveSpacecraftExteriorCameraTarget());
            spacecraftCameraRig.SetCockpitTarget(ResolveSpacecraftCockpitCameraTarget());
            spacecraftCameraRig.SetView(currentPilotCameraView);
            if (currentMode == PlayerPossessionMode.Spacecraft)
            {
                spacecraftCameraRig.SnapToTarget();
            }
        }

        [ContextMenu("Exit Spacecraft")]
        public bool ExitSpacecraft()
        {
            return ExitPilotSeat();
        }

        [ContextMenu("Exit Pilot Seat")]
        public bool ExitPilotSeat()
        {
            ResolveReferences();
            PlayerPossessionMode nextMode = spawnInsideShipOnPilotExit
                ? PlayerPossessionMode.ShipInterior
                : PlayerPossessionMode.OnFoot;
            if (!PlayerPossessionTransitionPolicy.CanTransition(
                    currentMode,
                    nextMode,
                    PlayerPossessionTransitionRequest.ExitPilotSeat))
            {
                return false;
            }

            if (openRampOnPilotExit)
            {
                spacecraftRig?.OpenRamp();
            }

            SetSpacecraftExteriorCollisionIgnored(spawnInsideShipOnPilotExit);
            PlaceExplorerAtTransform(ResolvePilotExitTransform(), !spawnInsideShipOnPilotExit);
            ApplyMode(nextMode, PlayerPossessionTransitionRequest.ExitPilotSeat);
            return true;
        }

        [ContextMenu("Enter Ship Interior")]
        public bool EnterShipInterior()
        {
            ResolveReferences();
            if (!PlayerPossessionTransitionPolicy.CanTransition(
                    currentMode,
                    PlayerPossessionMode.ShipInterior,
                    PlayerPossessionTransitionRequest.EnterShipInterior))
            {
                return false;
            }

            SetSpacecraftExteriorCollisionIgnored(true);
            PlaceExplorerAtTransform(ResolveExteriorEntryTransform(), false);
            ApplyMode(PlayerPossessionMode.ShipInterior, PlayerPossessionTransitionRequest.EnterShipInterior);
            return true;
        }

        void ApplyMode(PlayerPossessionMode nextMode, PlayerPossessionTransitionRequest request)
        {
            ResolveReferences();
            if (!PlayerPossessionTransitionPolicy.CanTransition(currentMode, nextMode, request))
            {
                return;
            }

            currentMode = nextMode;

            bool piloting = currentMode == PlayerPossessionMode.Spacecraft;
            bool firstPerson = !piloting;
            SetBehaviourEnabled(firstPersonCameraRig, firstPerson);
            SetBehaviourEnabled(spacecraftMotor, true);
            SetBehaviourEnabled(spacecraftInput, piloting);
            SetBehaviourEnabled(spacecraftCameraRig, piloting);

            if (spacecraftCameraRig != null)
            {
                spacecraftCameraRig.SetExteriorTarget(ResolveSpacecraftExteriorCameraTarget());
                spacecraftCameraRig.SetCockpitTarget(ResolveSpacecraftCockpitCameraTarget());
                spacecraftCameraRig.SetView(currentPilotCameraView);
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

            Transform trackingTarget = piloting
                ? GetSpacecraftTrackingTarget()
                : GetExplorerTrackingTarget();
            PlayerPossessionTracking.ApplyTargets(
                updateOriginTrackingTarget,
                originRebaser,
                updateResourceStreamingTarget,
                resourceStreamers,
                trackingTarget);

            if (currentMode == PlayerPossessionMode.ShipInterior)
            {
                shipInteriorExitGate.Reset(Time.time, spacecraftRig, explorerRoot, explorerRigidbody);
            }
            else
            {
                shipInteriorExitGate.Clear();
            }

            SetSpacecraftExteriorCollisionIgnored(currentMode == PlayerPossessionMode.ShipInterior);
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

        void BindPossessionInteractables()
        {
            if (spacecraftRoot == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerPossessionController)} on {name} cannot bind spacecraft interactions without a spacecraft root.",
                    this);
                return;
            }

            PilotSeatInteractable[] pilotSeats =
                spacecraftRoot.GetComponentsInChildren<PilotSeatInteractable>(true);
            foreach (PilotSeatInteractable pilotSeat in pilotSeats)
            {
                pilotSeat.Bind(this);
            }

            VehicleBoardingPoint[] boardingPoints =
                spacecraftRoot.GetComponentsInChildren<VehicleBoardingPoint>(true);
            foreach (VehicleBoardingPoint point in boardingPoints)
            {
                point.Bind(this);
            }

            if (pilotSeats.Length == 0 || boardingPoints.Length == 0)
            {
                Debug.LogError(
                    $"{nameof(PlayerPossessionController)} on {name} requires at least one pilot seat and boarding point under {spacecraftRoot.name}.",
                    this);
            }
        }

        void BindPossessionContextReceivers()
        {
            if (spacecraftRoot == null)
            {
                return;
            }

            MonoBehaviour[] behaviours =
                spacecraftRoot.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerPossessionContextReceiver receiver)
                {
                    receiver.SetPossessionController(this);
                }
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
            return PlayerPossessionTracking.ResolveSpacecraftTarget(spacecraftRigidbody, spacecraftRoot);
        }

        Transform ResolveSpacecraftExteriorCameraTarget()
        {
            return PlayerPossessionCameraTargets.ResolveExterior(spacecraftCameraTarget, spacecraftRig, spacecraftRoot);
        }

        Transform ResolveSpacecraftCockpitCameraTarget()
        {
            return PlayerPossessionCameraTargets.ResolveCockpit(spacecraftRig);
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
            if (shipInteriorExitGate.CanTransitionOutside(
                    Time.time,
                    spacecraftRig,
                    explorerRoot,
                    explorerRigidbody,
                    exteriorTransitionDistance,
                    exteriorTransitionCooldownSeconds,
                    exteriorTransitionProgressDistance))
            {
                SnapExplorerToExteriorSurface();
                ApplyMode(PlayerPossessionMode.OnFoot, PlayerPossessionTransitionRequest.ExitShipInterior);
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

        void SetSpacecraftExteriorCollisionIgnored(bool ignore)
        {
            exteriorCollisionGate.SetIgnored(ResolveExplorerCapsule(), spacecraftRoot, ignore);
        }

        CapsuleCollider ResolveExplorerCapsule()
        {
            if (explorerMotor != null && explorerMotor.Capsule != null)
            {
                return explorerMotor.Capsule;
            }

            return explorerRoot != null ? explorerRoot.GetComponent<CapsuleCollider>() : null;
        }

        Transform GetExplorerTrackingTarget()
        {
            return PlayerPossessionTracking.ResolveExplorerTarget(explorerRigidbody, explorerRoot);
        }

        static void SetBehaviourEnabled(Behaviour behaviour, bool enabled)
        {
            if (behaviour != null)
            {
                behaviour.enabled = enabled;
            }
        }

        string ResolveSpacecraftPersistentId()
        {
            return PlayerPossessionIdentity.ResolveSpacecraftPersistentId(spacecraftRoot, spacecraftRigidbody);
        }

        string ResolveExplorerPersistentId()
        {
            return PlayerPossessionIdentity.ResolveExplorerPersistentId(explorerRoot, explorerRigidbody);
        }
    }
}
