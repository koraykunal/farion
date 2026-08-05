using System;
using System.Collections.Generic;
using Farion.Core.Persistence;
using Farion.Simulation.Physics;
using Farion.Gameplay.Actors;
using Farion.Gameplay.Character;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Input;
using Farion.Gameplay.Resources;
using Farion.Simulation.World;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class PlayerPossessionController :
        MonoBehaviour,
        ICelestialSurfaceCollisionObserver
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

        public event Action<PlayerPossessionMode> ModeChanged;

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
        public PlayerInteractionRaycaster ExplorerInteractionRaycaster
        {
            get
            {
                ResolveReferences();
                return explorerInteractionRaycaster;
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

        public bool TryGetSurfaceCollisionObserver(
            out CelestialSurfaceCollisionObserverState observer)
        {
            ResolveReferences();
            Rigidbody target = currentMode == PlayerPossessionMode.OnFoot
                ? explorerRigidbody
                : spacecraftRigidbody;
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                observer = default;
                return false;
            }

            observer = new CelestialSurfaceCollisionObserverState(target);
            return true;
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
        public string ExplorerPersistentId
        {
            get
            {
                ResolveReferences();
                return ResolveExplorerPersistentId();
            }
        }

        public string SpacecraftPersistentId
        {
            get
            {
                ResolveReferences();
                return ResolveSpacecraftPersistentId();
            }
        }

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
            BindControlLock();
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
            PlayerExplorerPlacement.PlaceAtTransform(
                CreateExplorerPlacementContext(),
                ResolvePilotExitTransform(),
                !spawnInsideShipOnPilotExit);
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
            PlayerExplorerPlacement.PlaceAtTransform(
                CreateExplorerPlacementContext(),
                ResolveExteriorEntryTransform(),
                snapToExteriorSurface: false);
            ApplyMode(PlayerPossessionMode.ShipInterior, PlayerPossessionTransitionRequest.EnterShipInterior);
            return true;
        }

        void ApplyMode(PlayerPossessionMode nextMode, PlayerPossessionTransitionRequest request)
        {
            ResolveReferences();
            BindControlLock();
            if (!PlayerPossessionTransitionPolicy.CanTransition(currentMode, nextMode, request))
            {
                return;
            }

            PlayerPossessionMode previousMode = currentMode;
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
            if (previousMode != currentMode)
            {
                ModeChanged?.Invoke(currentMode);
            }
        }

        void ResolveReferences()
        {
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

        void BindControlLock()
        {
            explorerInput?.SetControlLock(controlLock);
            spacecraftInput?.SetControlLock(controlLock);
            explorerInteractionRaycaster?.SetControlLock(controlLock);
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
                PlayerExplorerPlacement.SnapToExteriorSurface(
                    CreateExplorerPlacementContext());
                ApplyMode(PlayerPossessionMode.OnFoot, PlayerPossessionTransitionRequest.ExitShipInterior);
            }
        }

        PlayerExplorerPlacementContext CreateExplorerPlacementContext()
        {
            ResolveReferences();
            return new PlayerExplorerPlacementContext(
                explorerRoot,
                explorerRigidbody,
                explorerMotor,
                explorerCelestialProbe,
                spacecraftRoot,
                spacecraftRigidbody,
                spacecraftCelestialProbe,
                exitPoseClearance,
                snapExplorerToExteriorSurface,
                exteriorGroundClearance,
                exteriorSurfaceClearance);
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
