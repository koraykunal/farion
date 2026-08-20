using Farion.Core.Identity;
using System;
using Farion.Core.Persistence;
using Farion.Gameplay.Actors;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Ships;
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

        const string ClaimPrompt = "Enter ship";
        const string PilotPrompt = "Pilot ship";

        readonly SyncVar<ulong> entityId = new();
        readonly SyncVar<byte> formationSlot = new();
        readonly SyncVar<ulong> claimedBySessionPlayerId = new();
        readonly SyncVar<bool> piloted = new();
        readonly SyncVar<bool> landingGearDeployed = new();
        readonly SyncVar<float> hullIntegrity = new(UnwrittenHullIntegrity);

        [Min(0.1f)]
        [SerializeField] float maximumClaimDistance = 6f;
        PersistentObjectId persistentObjectId;
        ShuttleCargoInventory cargo;
        VehicleBoardingPoint boardingPoint;
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
        SpacecraftLandingGearAnimator landingGear;
        Rigidbody body;
        Rigidbody pilotBody;
        readonly PredictionRigidbody predictionRigidbody = new();
        PredictionRigidbodySpacecraftPhysicsBody physicsBody;
        MultiplayerWorldOriginAuthority originAuthority;
        MultiplayerSceneContext sceneContext;
        int claimedConnectionId = -1;
        bool localPiloting;

        static readonly List<NetworkStarterShuttle> activeShips = new();

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
        public SpacecraftRig Rig => spacecraftRig;
        public ShuttleCargoInventory Cargo => cargo;
        public SpacecraftMotor Motor => motor;
        public SpacecraftHull Hull => hull;
        public KeyboardSpacecraftInput Input => input;
        public string InteractionPrompt => CanLocalPilot()
            ? PilotPrompt
            : ClaimPrompt;

        void Awake()
        {
            persistentObjectId = GetComponent<PersistentObjectId>();
            cargo = GetComponent<ShuttleCargoInventory>();
            boardingPoint = GetComponentInChildren<VehicleBoardingPoint>(true);
            boardingPoint?.Bind((IInteractable)this);
            spacecraftRig = GetComponent<SpacecraftRig>();
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
            landingGear = GetComponent<SpacecraftLandingGearAnimator>();
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

            if (input != null && input.CurrentInput.ToggleLandingGear)
            {
                RequestToggleLandingGear();
            }

            if (boarding.ExitVehicle)
            {
                NetworkSessionPlayer.Local?.RequestExitStarterShuttle(EntityId);
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

        void OnLandingGearDeployedChanged(bool previous, bool next, bool asServer)
        {
            landingGear?.SetCommandedDeployed(next);
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
                    sceneContext.OriginAuthority);
            }

            if (input != null)
            {
                input.enabled = false;
            }

            landingGear?.SetCommandedDeployed(landingGearDeployed.Value);
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
            motor.SetExternalSimulation(true);
            celestialProbe?.SetExternalSimulation(true);
            atmosphereInteractor?.SetExternalSimulation(true);
            oceanInteractor?.SetExternalSimulation(true);
            surfaceContactProbe?.SetExternalSimulation(true);
            surfaceContactStabilizer?.SetExternalSimulation(true);
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
            hull?.SetExternalSimulation(false);
            ApplyPilotedState(false);
        }

        public bool CanInteract(InteractionContext context)
        {
            NetworkSessionPlayer sessionPlayer = NetworkSessionPlayer.Local;
            return sessionPlayer != null &&
                   EntityId.IsValid &&
                   ((!sessionPlayer.ClaimedStarterShuttleId.IsValid &&
                     sessionPlayer.AssignedStarterShuttleId == EntityId &&
                     !IsClaimed &&
                     sessionPlayer.PossessionMode == PlayerPossessionMode.OnFoot) ||
                    CanLocalPilot());
        }

        public void Interact(InteractionContext context)
        {
            NetworkSessionPlayer.Local?.RequestUseStarterShuttle(EntityId);
        }

        protected override void TimeManager_OnTick()
        {
            hull?.BeginSimulationStep();
            SpacecraftInputState current = IsLocalPilot() && input != null
                ? input.CurrentInput
                : SpacecraftInputState.None;
            PerformReplicate(new SpacecraftReplicateData(
                current,
                originAuthority?.CurrentSequence ?? 0));
        }

        protected override void TimeManager_OnPostTick()
        {
            SyncPilotBodyToSeat();
            StepHull();
            CreateReconcile();
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
            if (!IsPiloted)
            {
                return;
            }

            bool invalidServerInput = IsServerStarted &&
                (!IsClaimed || !Owner.IsValid || OwnerId != claimedConnectionId);
            bool staleOrigin = IsServerStarted &&
                originAuthority != null &&
                data.OriginSequence != originAuthority.CurrentSequence;
            float deltaTime = (float)TimeManager.TickDelta;
            surfaceContactProbe?.BeginSimulationStep(deltaTime);
            celestialProbe?.RefreshSample(data.GetTick() * TimeManager.TickDelta);
            atmosphereInteractor?.Simulate(deltaTime, physicsBody);
            oceanInteractor?.Simulate(deltaTime, physicsBody);
            surfaceContactStabilizer?.Simulate(deltaTime, physicsBody);
            motor.Simulate(
                invalidServerInput || staleOrigin
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
                originAuthority?.CurrentSequence ?? 0));
        }

        [Reconcile]
        void PerformReconcile(
            SpacecraftReconcileData data,
            Channel channel = Channel.Unreliable)
        {
            if (originAuthority != null &&
                data.OriginSequence != originAuthority.CurrentSequence)
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

        internal bool TryClaim(
            NetworkSessionPlayer sessionPlayer,
            NetworkConnection connection,
            Vector3 explorerPosition)
        {
            if (!IsServerStarted ||
                sessionPlayer == null ||
                connection == null ||
                !connection.IsActive ||
                !IsWithinClaimDistance(explorerPosition) ||
                (IsClaimed &&
                 ClaimedBySessionPlayerId != sessionPlayer.SessionPlayerId))
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

        internal void BindScene(
            GravitySimulation gravitySimulation,
            CelestialFrameProvider frameProvider,
            MultiplayerWorldOriginAuthority authority)
        {
            originAuthority = authority;
            motor.SetSimulation(gravitySimulation);
            GetComponent<CelestialActorProbe>()
                ?.SetFrameProvider(frameProvider);
        }

        public bool TryGetSurfaceCollisionObserver(
            out CelestialSurfaceCollisionObserverState observer)
        {
            if (body == null || !body.gameObject.activeInHierarchy)
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

        internal bool TryBeginPiloting(
            NetworkSessionPlayer sessionPlayer,
            NetworkObject explorer)
        {
            if (!IsServerStarted ||
                sessionPlayer == null ||
                explorer == null ||
                !IsClaimedBy(sessionPlayer.SessionPlayerId) ||
                spacecraftRig == null ||
                spacecraftRig.PilotSeatPoint == null ||
                (explorer.transform.position -
                 spacecraftRig.PilotSeatPoint.position).sqrMagnitude >
                maximumClaimDistance * maximumClaimDistance)
            {
                return false;
            }

            pilotBody = explorer.GetComponent<Rigidbody>();
            piloted.Value = true;
            ApplyPilotedState(true);
            return true;
        }

        internal bool TryGetExitPose(
            out Vector3 position,
            out Quaternion rotation)
        {
            Transform target = spacecraftRig != null
                ? spacecraftRig.ExteriorExitPoint
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
            piloted.Value = false;
            pilotBody = null;
            ApplyPilotedState(false);
            claimedBySessionPlayerId.Value = 0UL;
            claimedConnectionId = -1;
            if (Owner.IsValid)
            {
                RemoveOwnership();
            }
        }

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

        bool CanLocalPilot()
        {
            NetworkSessionPlayer sessionPlayer = NetworkSessionPlayer.Local;
            return sessionPlayer != null &&
                sessionPlayer.PossessionMode == PlayerPossessionMode.ShipInterior &&
                sessionPlayer.ClaimedStarterShuttleId == EntityId &&
                IsClaimedBy(sessionPlayer.SessionPlayerId);
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

            bool simulate = active;
            if (!simulate && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.isKinematic = !simulate;
        }

    }
}
