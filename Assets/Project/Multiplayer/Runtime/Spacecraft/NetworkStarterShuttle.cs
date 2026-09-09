using Farion.Core.Numerics;
using Farion.Core.Identity;
using System;
using Farion.Core.Persistence;
using Farion.Gameplay.Actors;
using Farion.Gameplay.Character;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Input;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Presentation.Flight;
using Farion.Gameplay.Ships;
using Farion.Multiplayer.Player;
using Farion.Multiplayer.Session;
using Farion.Multiplayer.World;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.World;
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using FishNet.Utility.Template;
using System.Collections.Generic;
using UnityEngine;

namespace Farion.Multiplayer.Spacecraft
{
    public sealed class NetworkStarterShuttle :
        TickNetworkBehaviour,
        IInteractable,
        ICelestialSurfaceCollisionObserver
    {
        const float UnwrittenHullIntegrity = -1f;
        const float StrandedFuelFraction = 0.02f;
        const float ParkSpeedThreshold = 0.5f;

        readonly SyncVar<ulong> entityId = new();
        readonly SyncVar<byte> formationSlot = new();
        readonly SyncVar<ulong> claimedBySessionPlayerId = new();
        readonly SyncVar<bool> piloted = new();
        readonly SyncVar<bool> landingGearDeployed = new();
        readonly SyncVar<bool> floodlightsOn = new();
        readonly SyncVar<bool> rampOpen = new();
        readonly SyncVar<float> hullIntegrity = new(UnwrittenHullIntegrity);

        [Header("Boarding")]
        [Tooltip("Explorers farther than this from the boarding point cannot enter the ship.")]
        [Min(0.1f)]
        [SerializeField] float maximumClaimDistance = 6f;
        [Tooltip("Leaving the pilot seat puts the explorer inside the ship instead of outside it.")]
        [SerializeField] bool spawnInsideShipOnPilotExit = true;
        [Tooltip("Opens the ramp automatically when the pilot leaves the seat.")]
        [SerializeField] bool openRampOnPilotExit;
        [Tooltip("Closes the ramp automatically when the pilot takes the seat.")]
        [SerializeField] bool closeRampOnEnter = true;

        [Header("Interior Exit")]
        [Tooltip("Metres the explorer must walk past the exterior exit point before the ship releases them.")]
        [Min(0.1f)]
        [SerializeField] float exteriorTransitionDistance = 1.5f;
        [Tooltip("Seconds after entering the ship before walking out can release the explorer.")]
        [Min(0f)]
        [SerializeField] float exteriorTransitionCooldownSeconds = 0.35f;
        [Tooltip("Extra distance beyond the entry distance required to count as walking out.")]
        [Min(0f)]
        [SerializeField] float exteriorTransitionProgressDistance = 0.75f;
        [Tooltip("Vertical clearance applied to explorer placement points.")]
        [Min(0f)]
        [SerializeField] float exitPoseClearance = 0.25f;
        [Tooltip("Snaps the explorer onto the terrain surface when they leave the ship.")]
        [SerializeField] bool snapExplorerToExteriorSurface = true;
        [Min(0f)]
        [SerializeField] float exteriorGroundClearance = 0.08f;
        [Min(0f)]
        [SerializeField] float exteriorSurfaceClearance = 0.15f;

        PersistentObjectId persistentObjectId;
        ShuttleCargoInventory cargo;
        VehicleBoardingPoint boardingPoint;
        PilotSeatInteractable pilotSeat;
        SpacecraftRig spacecraftRig;
        SpacecraftMotor motor;
        SpacecraftHull hull;
        KeyboardSpacecraftInput input;
        KeyboardBoardingInput boardingInput;
        CelestialActorProbe celestialProbe;
        SpacecraftAtmosphereInteractor atmosphereInteractor;
        SpacecraftOceanInteractor oceanInteractor;
        SpacecraftSurfaceContactProbe surfaceContactProbe;
        SpacecraftSurfaceContactStabilizer surfaceContactStabilizer;
        SpacecraftSurfaceGuard surfaceGuard;
        SpacecraftLandingGearAnimator landingGear;
        SpacecraftFloodlights floodlights;
        Rigidbody body;
        Rigidbody pilotBody;
        readonly PredictionRigidbody predictionRigidbody = new();
        readonly ShipInteriorExitGate interiorExitGate = new();
        PredictionRigidbodySpacecraftPhysicsBody physicsBody;
        ZoneOriginState originState;
        MultiplayerSceneContext sceneContext;
        ZonePhysicsTickDriver tickDriver;
        CelestialBody parkedBody;
        Vector3 parkedLocalPosition;
        Quaternion parkedLocalRotation;
        int claimedConnectionId = -1;
        NetworkSessionPlayer occupantPlayer;
        NetworkObject occupant;
        bool localPiloting;

        static readonly List<NetworkStarterShuttle> activeShips = new();

        internal event Action<NetworkStarterShuttle, NetworkSessionPlayer, NetworkObject, PlayerPossessionMode>
            OccupancyChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetActiveShips()
        {
            activeShips.Clear();
        }

        internal static IReadOnlyList<NetworkStarterShuttle> ActiveShips => activeShips;

        internal static NetworkStarterShuttle FindByEntityId(GeneratedEntityId id)
        {
            for (int i = 0; i < activeShips.Count; i++)
            {
                if (activeShips[i] != null && activeShips[i].EntityId == id)
                {
                    return activeShips[i];
                }
            }

            return null;
        }

        public GeneratedEntityId EntityId => entityId.Value == 0UL
            ? GeneratedEntityId.None
            : new GeneratedEntityId(entityId.Value);
        public int FormationSlot => formationSlot.Value;
        public ulong ClaimedBySessionPlayerId =>
            claimedBySessionPlayerId.Value;
        public bool IsClaimed => ClaimedBySessionPlayerId != 0UL;
        public bool IsPiloted => piloted.Value;
        public bool IsRampOpen => rampOpen.Value;
        public SpacecraftRig Rig => spacecraftRig;
        public ShuttleCargoInventory Cargo => cargo;
        public SpacecraftMotor Motor => motor;
        public SpacecraftHull Hull => hull;
        public bool IsStranded =>
            motor != null &&
            (motor.FuelNormalized <= StrandedFuelFraction ||
             (hull != null && hull.IsBreached));
        public KeyboardSpacecraftInput Input => input;
        public KeyboardBoardingInput BoardingInput => boardingInput;
        public string InteractionPrompt => InteractionPromptKeys.EnterShip;

        void Awake()
        {
            persistentObjectId = GetComponent<PersistentObjectId>();
            cargo = GetComponent<ShuttleCargoInventory>();
            boardingPoint = GetComponentInChildren<VehicleBoardingPoint>(true);
            boardingPoint?.Bind((IInteractable)this);
            pilotSeat = GetComponentInChildren<PilotSeatInteractable>(true);
            pilotSeat?.Bind(new PilotSeatHost(this));
            spacecraftRig = GetComponent<SpacecraftRig>();
            spacecraftRig?.RampController?.Bind(new RampHost(this));
            motor = GetComponent<SpacecraftMotor>();
            hull = GetComponent<SpacecraftHull>();
            input = GetComponent<KeyboardSpacecraftInput>();
            boardingInput = GetComponent<KeyboardBoardingInput>();
            celestialProbe = GetComponent<CelestialActorProbe>();
            atmosphereInteractor = GetComponent<SpacecraftAtmosphereInteractor>();
            oceanInteractor = GetComponent<SpacecraftOceanInteractor>();
            surfaceContactProbe = GetComponent<SpacecraftSurfaceContactProbe>();
            surfaceContactStabilizer =
                GetComponent<SpacecraftSurfaceContactStabilizer>();
            surfaceGuard = GetComponent<SpacecraftSurfaceGuard>();
            landingGear = GetComponent<SpacecraftLandingGearAnimator>();
            floodlights = GetComponentInChildren<SpacecraftFloodlights>(true);
            body = GetComponent<Rigidbody>();
            predictionRigidbody.Initialize(body);
            physicsBody = new PredictionRigidbodySpacecraftPhysicsBody(
                predictionRigidbody);
            if (!activeShips.Contains(this))
            {
                activeShips.Add(this);
            }

            piloted.OnChange += OnPilotedChanged;
            landingGearDeployed.OnChange += OnLandingGearDeployedChanged;
            floodlightsOn.OnChange += OnFloodlightsOnChanged;
            rampOpen.OnChange += OnRampOpenChanged;
            hullIntegrity.OnChange += OnHullIntegrityChanged;
        }

        void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            bool shouldPilot = IsLocalPilot();
            if (shouldPilot != localPiloting)
            {
                bool applied = shouldPilot
                    ? sceneContext != null && sceneContext.BindOwnedSpacecraft(this)
                    : true;
                if (applied)
                {
                    if (boardingInput != null)
                    {
                        boardingInput.enabled = shouldPilot;
                    }

                    if (!shouldPilot)
                    {
                        RestoreOwnedExplorer();
                    }

                    localPiloting = shouldPilot;
                }
            }

            if (!localPiloting || boardingInput == null)
            {
                return;
            }

            BoardingInputState boarding = boardingInput.CurrentInput;
            if (boarding.TogglePilotCamera)
            {
                sceneContext?.TogglePilotCameraView();
            }

            if (boarding.ToggleHud)
            {
                PlayerViewPreferences.FlightHudVisible = !PlayerViewPreferences.FlightHudVisible;
            }

            if (input != null && input.CurrentInput.ToggleLandingGear)
            {
                RequestToggleLandingGear();
            }

            if (input != null && input.CurrentInput.ToggleFloodlights)
            {
                RequestToggleFloodlights();
            }

            if (boarding.ExitVehicle)
            {
                NetworkSessionPlayer.Local?.RequestLeavePilotSeat(EntityId);
            }
        }

        [ServerRpc]
        void RequestToggleLandingGear()
        {
            if (IsPiloted)
            {
                landingGearDeployed.Value = !landingGearDeployed.Value;
            }
        }

        [ServerRpc]
        void RequestToggleFloodlights()
        {
            if (IsPiloted)
            {
                floodlightsOn.Value = !floodlightsOn.Value;
            }
        }

        [ServerRpc]
        void RequestToggleRamp()
        {
            if (IsClaimed && !IsPiloted)
            {
                rampOpen.Value = !rampOpen.Value;
            }
        }

        void OnLandingGearDeployedChanged(bool previous, bool next, bool asServer)
        {
            landingGear?.SetCommandedDeployed(next);
        }

        void OnFloodlightsOnChanged(bool previous, bool next, bool asServer)
        {
            floodlights?.SetOn(next);
        }

        void OnRampOpenChanged(bool previous, bool next, bool asServer)
        {
            spacecraftRig?.RampController?.SetOpen(next);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            maximumClaimDistance = Mathf.Max(0.1f, maximumClaimDistance);
        }

        internal void Initialize(GeneratedEntityId id, int slot)
        {
            if (!id.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            if (slot < 0 || slot > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            entityId.Value = id.Value;
            formationSlot.Value = (byte)slot;
            landingGearDeployed.Value =
                landingGear != null && landingGear.IsCommandedDeployed;
            floodlightsOn.Value = floodlights != null && floodlights.IsOn;
            rampOpen.Value = spacecraftRig?.RampController != null &&
                spacecraftRig.RampController.IsCommandedOpen;
            ApplyPersistentId();
        }

        public override void OnStartServer()
        {
            PublishHullIntegrity();
        }

        public override void OnStartClient()
        {
            ApplyPersistentId();
            sceneContext = MultiplayerSceneContext.FindIn(gameObject.scene);
            if (sceneContext != null)
            {
                sceneContext.AttachToShiftedWorld(transform);
                BindScene(
                    sceneContext.GravitySimulation,
                    sceneContext.CelestialFrameProvider,
                    sceneContext.ZoneOrigin);
            }

            if (input != null)
            {
                input.enabled = false;
            }

            landingGear?.SetCommandedDeployed(landingGearDeployed.Value);
            floodlights?.SetOn(floodlightsOn.Value);
            spacecraftRig?.RampController?.SetOpen(rampOpen.Value);
            if (!IsServerStarted &&
                hull != null &&
                hullIntegrity.Value >= 0f)
            {
                hull.SetIntegrityAmount(hullIntegrity.Value, notifyDamage: false);
            }

            ApplyPilotedState(IsPiloted);
        }

        public override void OnStartNetwork()
        {
            tickDriver = NetworkManager != null
                ? NetworkManager.GetComponent<ZonePhysicsTickDriver>()
                : null;
            motor.SetExternalSimulation(true);
            celestialProbe?.SetExternalSimulation(true);
            atmosphereInteractor?.SetExternalSimulation(true);
            oceanInteractor?.SetExternalSimulation(true);
            surfaceContactProbe?.SetExternalSimulation(true);
            surfaceContactStabilizer?.SetExternalSimulation(true);
            surfaceGuard?.SetExternalSimulation(true);
            hull?.SetExternalSimulation(true);
            body.interpolation = RigidbodyInterpolation.None;
            SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
            ApplyPilotedState(IsPiloted);
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            if (!IsOwner && localPiloting)
            {
                RestoreOwnedExplorer();
            }

            localPiloting = false;
            ApplyPilotedState(IsPiloted);
        }

        public override void OnStopClient()
        {
            if (localPiloting)
            {
                RestoreOwnedExplorer();
                localPiloting = false;
            }
        }

        void OnDestroy()
        {
            activeShips.Remove(this);
        }

        public override void OnStopNetwork()
        {
            motor.SetExternalSimulation(false);
            celestialProbe?.SetExternalSimulation(false);
            atmosphereInteractor?.SetExternalSimulation(false);
            oceanInteractor?.SetExternalSimulation(false);
            surfaceContactProbe?.SetExternalSimulation(false);
            surfaceContactStabilizer?.SetExternalSimulation(false);
            surfaceGuard?.SetExternalSimulation(false);
            hull?.SetExternalSimulation(false);
            ApplyPilotedState(false);
        }

        public bool CanInteract(InteractionContext context)
        {
            NetworkSessionPlayer sessionPlayer = NetworkSessionPlayer.Local;
            return sessionPlayer != null &&
                EntityId.IsValid &&
                sessionPlayer.PossessionMode == PlayerPossessionMode.OnFoot &&
                sessionPlayer.AssignedStarterShuttleId == EntityId &&
                (!IsClaimed || IsClaimedBy(sessionPlayer.SessionPlayerId)) &&
                context.Actor != null &&
                IsWithinClaimDistance(context.Actor.transform.position);
        }

        public void Interact(InteractionContext context)
        {
            NetworkSessionPlayer.Local?.RequestBoardStarterShuttle(EntityId);
        }

        bool CanLocalTakeSeat()
        {
            NetworkSessionPlayer sessionPlayer = NetworkSessionPlayer.Local;
            return sessionPlayer != null &&
                sessionPlayer.PossessionMode == PlayerPossessionMode.ShipInterior &&
                sessionPlayer.ClaimedStarterShuttleId == EntityId &&
                IsClaimedBy(sessionPlayer.SessionPlayerId);
        }

        bool CanLocalToggleRamp()
        {
            return IsOwner && CanLocalTakeSeat();
        }

        protected override void TimeManager_OnTick()
        {
            hull?.BeginSimulationStep();
            SpacecraftInputState current = IsLocalPilot() && input != null
                ? input.CurrentInput
                : SpacecraftInputState.None;
            PerformReplicate(new SpacecraftReplicateData(
                current,
                originState?.CurrentSequence ?? 0));
        }

        protected override void TimeManager_OnPostTick()
        {
            SyncPilotBodyToSeat();
            if (IsServerStarted)
            {
                MaintainParkAnchor();
                ParkWhenSettled();
                UpdateInteriorExit();
            }

            StepHull();
            CreateReconcile();
        }

        double ResolveSimulationSeconds(uint tick)
        {
            return tickDriver != null
                ? tickDriver.ResolveSimulationSeconds(tick)
                : tick * TimeManager.TickDelta;
        }

        void StepHull()
        {
            if (hull == null)
            {
                return;
            }

            if (!IsServerStarted)
            {
                hull.SyncCapacity();
                hull.DiscardPendingImpact();
                return;
            }

            hull.Step();
            PublishHullIntegrity();
        }

        void PublishHullIntegrity()
        {
            if (hull == null || hullIntegrity.Value == hull.Integrity.Current)
            {
                return;
            }

            hullIntegrity.Value = hull.Integrity.Current;
        }

        void OnHullIntegrityChanged(float previous, float next, bool asServer)
        {
            if (asServer || hull == null || next < 0f)
            {
                return;
            }

            hull.SetIntegrityAmount(next, notifyDamage: previous >= 0f);
        }

        [Replicate]
        void PerformReplicate(
            SpacecraftReplicateData data,
            ReplicateState state = ReplicateState.Invalid,
            Channel channel = Channel.Unreliable)
        {
            if (body.isKinematic)
            {
                return;
            }

            bool invalidServerInput = IsServerStarted &&
                (!IsPiloted || !IsClaimed || !Owner.IsValid || OwnerId != claimedConnectionId);
            bool staleOrigin = IsServerStarted &&
                originState != null &&
                !originState.SharesReferenceFrame(data.OriginSequence);
            float deltaTime = (float)TimeManager.TickDelta;
            surfaceContactProbe?.BeginSimulationStep(deltaTime);
            celestialProbe?.RefreshSample(ResolveSimulationSeconds(data.GetTick()));
            atmosphereInteractor?.Simulate(deltaTime, physicsBody);
            oceanInteractor?.Simulate(deltaTime, physicsBody);
            surfaceContactStabilizer?.Simulate(deltaTime, physicsBody);
            surfaceGuard?.Simulate(deltaTime, physicsBody);
            motor.Simulate(
                !IsPiloted || invalidServerInput || staleOrigin
                    ? SpacecraftInputState.None
                    : data.Input,
                deltaTime,
                physicsBody);
        }

        public override void CreateReconcile()
        {
            PerformReconcile(new SpacecraftReconcileData(
                predictionRigidbody,
                motor.CaptureState(),
                originState?.CurrentSequence ?? 0));
        }

        [Reconcile]
        void PerformReconcile(
            SpacecraftReconcileData data,
            Channel channel = Channel.Unreliable)
        {
            if (originState != null &&
                !originState.SharesReferenceFrame(data.OriginSequence))
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Vector3 previousPosition = body.position;
            Quaternion previousRotation = body.rotation;
#endif
            predictionRigidbody.Reconcile(data.PredictionRigidbody);
            motor.RestoreState(data.MotorState);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PredictionDiagnostics.ReportReconcile(
                previousPosition,
                previousRotation,
                body.position,
                body.rotation);
#endif
        }

        internal bool TryBoard(
            NetworkSessionPlayer sessionPlayer,
            NetworkConnection connection,
            NetworkObject explorer)
        {
            if (!IsServerStarted ||
                sessionPlayer == null ||
                explorer == null ||
                !PlayerPossessionTransitionPolicy.CanTransition(
                    sessionPlayer.PossessionMode,
                    PlayerPossessionMode.ShipInterior,
                    PlayerPossessionTransitionRequest.EnterShipInterior) ||
                !IsWithinClaimDistance(explorer.transform.position) ||
                !TryGetInteriorPose(out Vector3 position, out Quaternion rotation) ||
                !TryClaim(sessionPlayer, connection))
            {
                return false;
            }

            EnterInterior(sessionPlayer, explorer, position, rotation);
            return true;
        }

        internal bool TryTakePilotSeat(
            NetworkSessionPlayer sessionPlayer,
            NetworkObject explorer)
        {
            if (!IsServerStarted ||
                sessionPlayer == null ||
                explorer == null ||
                !IsClaimedBy(sessionPlayer.SessionPlayerId) ||
                !PlayerPossessionTransitionPolicy.CanTransition(
                    sessionPlayer.PossessionMode,
                    PlayerPossessionMode.Spacecraft,
                    PlayerPossessionTransitionRequest.EnterPilotSeat) ||
                spacecraftRig == null ||
                spacecraftRig.PilotSeatPoint == null ||
                (explorer.transform.position -
                 spacecraftRig.PilotSeatPoint.position).sqrMagnitude >
                maximumClaimDistance * maximumClaimDistance)
            {
                return false;
            }

            BeginPiloting(sessionPlayer, explorer);
            return true;
        }

        internal bool TryLeavePilotSeat(
            NetworkSessionPlayer sessionPlayer,
            NetworkObject explorer)
        {
            PlayerPossessionMode nextMode = spawnInsideShipOnPilotExit
                ? PlayerPossessionMode.ShipInterior
                : PlayerPossessionMode.OnFoot;
            if (!IsServerStarted ||
                sessionPlayer == null ||
                explorer == null ||
                !IsPiloted ||
                !IsClaimedBy(sessionPlayer.SessionPlayerId) ||
                !PlayerPossessionTransitionPolicy.CanTransition(
                    sessionPlayer.PossessionMode,
                    nextMode,
                    PlayerPossessionTransitionRequest.ExitPilotSeat))
            {
                return false;
            }

            StopPiloting();
            NetworkExplorerController controller =
                explorer.GetComponent<NetworkExplorerController>();
            controller?.SetPossessionActive(true);
            if (nextMode == PlayerPossessionMode.ShipInterior)
            {
                Transform target = spacecraftRig != null
                    ? spacecraftRig.InteriorSpawnPoint ?? spacecraftRig.PilotSeatPoint
                    : null;
                if (target == null)
                {
                    nextMode = PlayerPossessionMode.OnFoot;
                }
                else
                {
                    EnterInterior(sessionPlayer, explorer, target.position, target.rotation);
                    if (openRampOnPilotExit)
                    {
                        rampOpen.Value = true;
                    }

                    return true;
                }
            }

            PlayerExplorerPlacement.PlaceAtTransform(
                CreatePlacementContext(explorer),
                ResolveExteriorExitTransform(),
                snapExplorerToExteriorSurface);
            ReleaseOccupant(sessionPlayer, explorer);
            return true;
        }

        internal void RestoreOccupancy(
            NetworkSessionPlayer sessionPlayer,
            NetworkConnection connection,
            NetworkObject explorer,
            PlayerPossessionMode mode)
        {
            if (!IsServerStarted ||
                sessionPlayer == null ||
                explorer == null ||
                mode == PlayerPossessionMode.OnFoot ||
                !TryGetInteriorPose(out Vector3 position, out Quaternion rotation) ||
                !TryClaim(sessionPlayer, connection))
            {
                return;
            }

            EnterInterior(sessionPlayer, explorer, position, rotation);
            if (mode == PlayerPossessionMode.Spacecraft)
            {
                BeginPiloting(sessionPlayer, explorer);
            }
        }

        internal void ServiceAtDock()
        {
            if (!IsServerStarted)
            {
                return;
            }

            motor?.RefillFuel();
            hull?.RefillIntegrity();
            PublishHullIntegrity();
        }

        internal bool ReleaseClaim(int connectionId)
        {
            if (claimedConnectionId != connectionId)
            {
                return false;
            }

            ClearClaim();
            return true;
        }

        internal void ClearClaim()
        {
            NetworkSessionPlayer player = occupantPlayer;
            NetworkObject explorer = occupant;
            StopPiloting();
            claimedBySessionPlayerId.Value = 0UL;
            claimedConnectionId = -1;
            occupantPlayer = null;
            occupant = null;
            interiorExitGate.Clear();
            if (Owner.IsValid)
            {
                RemoveOwnership();
            }

            if (player == null)
            {
                return;
            }

            player.SetClaimedStarterShuttle(GeneratedEntityId.None);
            player.SetPossessionMode(PlayerPossessionMode.OnFoot);
            if (explorer != null)
            {
                NetworkExplorerController controller =
                    explorer.GetComponent<NetworkExplorerController>();
                controller?.SetInsideShip(false);
                controller?.SetPossessionActive(true);
            }

            OccupancyChanged?.Invoke(this, player, explorer, PlayerPossessionMode.OnFoot);
        }

        bool TryClaim(NetworkSessionPlayer sessionPlayer, NetworkConnection connection)
        {
            if (connection == null ||
                !connection.IsActive ||
                (IsClaimed && ClaimedBySessionPlayerId != sessionPlayer.SessionPlayerId))
            {
                return false;
            }

            claimedBySessionPlayerId.Value = sessionPlayer.SessionPlayerId;
            claimedConnectionId = connection.ClientId;
            if (OwnerId != connection.ClientId)
            {
                GiveOwnership(connection);
            }

            return true;
        }

        void EnterInterior(
            NetworkSessionPlayer sessionPlayer,
            NetworkObject explorer,
            Vector3 position,
            Quaternion rotation)
        {
            occupantPlayer = sessionPlayer;
            occupant = explorer;
            NetworkExplorerController controller =
                explorer.GetComponent<NetworkExplorerController>();
            controller?.SetPossessionActive(true);
            controller?.SetInsideShip(true);
            PlaceExplorer(explorer, position, rotation);
            sessionPlayer.SetClaimedStarterShuttle(EntityId);
            sessionPlayer.SetPossessionMode(PlayerPossessionMode.ShipInterior);
            interiorExitGate.Reset(
                Time.time,
                spacecraftRig,
                explorer.gameObject,
                explorer.GetComponent<Rigidbody>());
            OccupancyChanged?.Invoke(
                this,
                sessionPlayer,
                explorer,
                PlayerPossessionMode.ShipInterior);
        }

        void BeginPiloting(NetworkSessionPlayer sessionPlayer, NetworkObject explorer)
        {
            if (closeRampOnEnter)
            {
                rampOpen.Value = false;
            }

            if (spacecraftRig != null && spacecraftRig.PilotSeatPoint != null)
            {
                PlaceExplorer(
                    explorer,
                    spacecraftRig.PilotSeatPoint.position,
                    spacecraftRig.PilotSeatPoint.rotation);
            }

            explorer.GetComponent<NetworkExplorerController>()?.SetPossessionActive(false);
            pilotBody = explorer.GetComponent<Rigidbody>();
            piloted.Value = true;
            ApplyPilotedState(true);
            interiorExitGate.Clear();
            sessionPlayer.SetPossessionMode(PlayerPossessionMode.Spacecraft);
            OccupancyChanged?.Invoke(
                this,
                sessionPlayer,
                explorer,
                PlayerPossessionMode.Spacecraft);
        }

        void StopPiloting()
        {
            pilotBody = null;
            if (piloted.Value)
            {
                piloted.Value = false;
            }

            ApplyPilotedState(false);
        }

        void ReleaseOccupant(NetworkSessionPlayer sessionPlayer, NetworkObject explorer)
        {
            NetworkExplorerController controller =
                explorer.GetComponent<NetworkExplorerController>();
            controller?.SetInsideShip(false);
            occupantPlayer = null;
            occupant = null;
            interiorExitGate.Clear();
            claimedBySessionPlayerId.Value = 0UL;
            claimedConnectionId = -1;
            if (Owner.IsValid)
            {
                RemoveOwnership();
            }

            sessionPlayer.SetClaimedStarterShuttle(GeneratedEntityId.None);
            sessionPlayer.SetPossessionMode(PlayerPossessionMode.OnFoot);
            OccupancyChanged?.Invoke(this, sessionPlayer, explorer, PlayerPossessionMode.OnFoot);
        }

        void UpdateInteriorExit()
        {
            if (IsPiloted ||
                occupant == null ||
                occupantPlayer == null ||
                occupantPlayer.PossessionMode != PlayerPossessionMode.ShipInterior ||
                !interiorExitGate.CanTransitionOutside(
                    Time.time,
                    spacecraftRig,
                    occupant.gameObject,
                    occupant.GetComponent<Rigidbody>(),
                    exteriorTransitionDistance,
                    exteriorTransitionCooldownSeconds,
                    exteriorTransitionProgressDistance))
            {
                return;
            }

            NetworkSessionPlayer player = occupantPlayer;
            NetworkObject explorer = occupant;
            PlayerExplorerPlacement.SnapToExteriorSurface(CreatePlacementContext(explorer));
            ReleaseOccupant(player, explorer);
        }

        PlayerExplorerPlacementContext CreatePlacementContext(NetworkObject explorer)
        {
            return new PlayerExplorerPlacementContext(
                explorer.gameObject,
                explorer.GetComponent<Rigidbody>(),
                explorer.GetComponent<ExplorerMotor>(),
                explorer.GetComponent<CelestialActorProbe>(),
                transform,
                body,
                celestialProbe,
                exitPoseClearance,
                snapExplorerToExteriorSurface,
                exteriorGroundClearance,
                exteriorSurfaceClearance);
        }

        Transform ResolveExteriorExitTransform()
        {
            if (boardingPoint != null && boardingPoint.HasExplicitExitPoint)
            {
                return boardingPoint.ExitPoint;
            }

            if (spacecraftRig != null && spacecraftRig.ExteriorExitPoint != null)
            {
                return spacecraftRig.ExteriorExitPoint;
            }

            return boardingPoint != null ? boardingPoint.ExitPoint : transform;
        }

        static void PlaceExplorer(NetworkObject explorer, Vector3 position, Quaternion rotation)
        {
            if (explorer.TryGetComponent(out Rigidbody explorerBody))
            {
                explorerBody.position = position;
                explorerBody.rotation = rotation;
                if (!explorerBody.isKinematic)
                {
                    explorerBody.linearVelocity = Vector3.zero;
                    explorerBody.angularVelocity = Vector3.zero;
                }
            }

            explorer.transform.SetPositionAndRotation(position, rotation);
            explorer.GetComponent<ExplorerMotor>()?.ResetMotorState();
        }

        internal void BindScene(
            GravitySimulation gravitySimulation,
            CelestialFrameProvider frameProvider,
            ZoneOriginState zoneOriginState)
        {
            originState = zoneOriginState;
            motor.SetSimulation(gravitySimulation);
            GetComponent<CelestialActorProbe>()
                ?.SetFrameProvider(frameProvider);
        }

        public bool TryGetSurfaceCollisionObserver(
            out CelestialSurfaceCollisionObserverState observer)
        {
            if (body == null ||
                !body.gameObject.activeInHierarchy ||
                body.isKinematic)
            {
                observer = default;
                return false;
            }

            observer = new CelestialSurfaceCollisionObserverState(body);
            return true;
        }

        internal bool TryGetInteriorPose(
            out Vector3 position,
            out Quaternion rotation)
        {
            Transform target = spacecraftRig != null
                ? spacecraftRig.InteriorSpawnPoint ??
                  spacecraftRig.PilotSeatPoint
                : null;
            if (target == null)
            {
                position = default;
                rotation = default;
                return false;
            }

            position = target.position;
            rotation = target.rotation;
            return true;
        }

        internal bool IsClaimedBy(ulong sessionPlayerId) =>
            sessionPlayerId != 0UL &&
            ClaimedBySessionPlayerId == sessionPlayerId;

        void ApplyPersistentId()
        {
            if (persistentObjectId == null || entityId.Value == 0UL)
            {
                return;
            }

            persistentObjectId.SetId($"ship.generated.{entityId.Value:X16}");
            cargo?.RefreshIdentity();
        }

        bool IsWithinClaimDistance(Vector3 explorerPosition)
        {
            Vector3 target = boardingPoint != null
                ? boardingPoint.transform.position
                : transform.position;
            float maximumDistance = Mathf.Max(0.1f, maximumClaimDistance);
            return (explorerPosition - target).sqrMagnitude <=
                   maximumDistance * maximumDistance;
        }

        bool IsLocalPilot()
        {
            NetworkSessionPlayer sessionPlayer = NetworkSessionPlayer.Local;
            return IsOwner &&
                IsPiloted &&
                sessionPlayer != null &&
                sessionPlayer.PossessionMode == PlayerPossessionMode.Spacecraft &&
                sessionPlayer.ClaimedStarterShuttleId == EntityId &&
                IsClaimedBy(sessionPlayer.SessionPlayerId);
        }

        void OnPilotedChanged(bool previous, bool next, bool asServer)
        {
            ApplyPilotedState(next);
            if (!next && localPiloting)
            {
                if (boardingInput != null)
                {
                    boardingInput.enabled = false;
                }

                RestoreOwnedExplorer();
                localPiloting = false;
            }
        }

        void RestoreOwnedExplorer()
        {
            if (sceneContext != null)
            {
                sceneContext.RestoreOwnedExplorer(this);
            }
        }

        void SyncPilotBodyToSeat()
        {
            if (!IsServerStarted || !IsPiloted || pilotBody == null)
            {
                return;
            }

            Transform seat = spacecraftRig != null
                ? spacecraftRig.PilotSeatPoint
                : null;
            if (seat != null)
            {
                pilotBody.position = seat.position;
                pilotBody.rotation = seat.rotation;
            }
        }

        void ApplyPilotedState(bool active)
        {
            if (body == null)
            {
                return;
            }

            if (active)
            {
                body.isKinematic = false;
                parkedBody = null;
                return;
            }

            if (!IsServerStarted)
            {
                return;
            }

            if (HasSettledOnSurface())
            {
                Park();
            }
        }

        bool HasSettledOnSurface()
        {
            if (surfaceContactProbe == null || !surfaceContactProbe.HasContact)
            {
                return body.isKinematic;
            }

            Vector3 referenceVelocity =
                celestialProbe != null && celestialProbe.HasSample
                    ? celestialProbe.CurrentSample.BodyPointVelocity
                    : Vector3.zero;
            Vector3 relativeVelocity = body.isKinematic
                ? Vector3.zero
                : body.linearVelocity - referenceVelocity;
            return relativeVelocity.sqrMagnitude <= ParkSpeedThreshold * ParkSpeedThreshold;
        }

        void Park()
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }

            CaptureParkAnchor();
        }

        void ParkWhenSettled()
        {
            if (IsPiloted || body.isKinematic || !HasSettledOnSurface())
            {
                return;
            }

            Park();
        }

        void CaptureParkAnchor()
        {
            parkedBody = ResolveAnchorBody();
            if (parkedBody == null)
            {
                return;
            }

            Transform anchorTransform = parkedBody.transform;
            parkedLocalPosition = anchorTransform.InverseTransformPoint(body.position);
            parkedLocalRotation =
                Quaternion.Inverse(anchorTransform.rotation) * body.rotation;
        }

        CelestialBody ResolveAnchorBody()
        {
            if (celestialProbe != null &&
                celestialProbe.HasSample &&
                celestialProbe.CurrentSample.Body != null)
            {
                return celestialProbe.CurrentSample.Body;
            }

            sceneContext ??= MultiplayerSceneContext.FindIn(gameObject.scene);
            GravitySimulation simulation = sceneContext != null
                ? sceneContext.GravitySimulation
                : null;
            if (simulation == null)
            {
                return null;
            }

            GravitySample sample = simulation.FindDominantBody(body.position);
            return sample.HasBody ? sample.Body : null;
        }

        void MaintainParkAnchor()
        {
            if (IsPiloted || body == null || !body.isKinematic)
            {
                return;
            }

            if (parkedBody == null)
            {
                CaptureParkAnchor();
                if (parkedBody == null)
                {
                    return;
                }
            }

            Transform anchorTransform = parkedBody.transform;
            Vector3 targetPosition = anchorTransform.TransformPoint(parkedLocalPosition);
            Quaternion targetRotation = anchorTransform.rotation * parkedLocalRotation;
            if ((body.position - targetPosition).sqrMagnitude > 1e-10f)
            {
                body.MovePosition(targetPosition);
            }

            if (!FarionMath.IsSameRotation(body.rotation, targetRotation))
            {
                body.MoveRotation(targetRotation);
            }
        }

        sealed class PilotSeatHost : IInteractable
        {
            readonly NetworkStarterShuttle ship;

            public PilotSeatHost(NetworkStarterShuttle ship) => this.ship = ship;

            public string InteractionPrompt => InteractionPromptKeys.PilotSeat;

            public bool CanInteract(InteractionContext context) => ship.CanLocalTakeSeat();

            public void Interact(InteractionContext context)
            {
                NetworkSessionPlayer.Local?.RequestPilotStarterShuttle(ship.EntityId);
            }
        }

        sealed class RampHost : IInteractable
        {
            readonly NetworkStarterShuttle ship;

            public RampHost(NetworkStarterShuttle ship) => this.ship = ship;

            public string InteractionPrompt => InteractionPromptKeys.ToggleRamp;

            public bool CanInteract(InteractionContext context) => ship.CanLocalToggleRamp();

            public void Interact(InteractionContext context)
            {
                if (ship.CanLocalToggleRamp())
                {
                    ship.RequestToggleRamp();
                }
            }
        }
    }
}
