using Farion.Core.Identity;
using System;
using Farion.Core.Persistence;
using Farion.Gameplay.Actors;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Interaction;
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
using UnityEngine;

namespace Farion.Multiplayer.Spawning
{
    public struct SpacecraftReplicateData : IReplicateData
    {
        public Vector3 Translation;
        public Vector2 Look;
        public float Roll;
        public bool Boost;
        public bool Brake;
        public bool ToggleFlightAssist;
        public uint OriginSequence;
        uint tick;

        public SpacecraftReplicateData(
            SpacecraftInputState input,
            uint originSequence)
        {
            SpacecraftInputState clamped = new(
                input.Translation,
                input.Look,
                input.Roll,
                input.Boost,
                input.Brake,
                input.ToggleFlightAssist);
            Translation = clamped.Translation;
            Look = clamped.Look;
            Roll = clamped.Roll;
            Boost = clamped.Boost;
            Brake = clamped.Brake;
            ToggleFlightAssist = clamped.ToggleFlightAssist;
            OriginSequence = originSequence;
            tick = 0;
        }

        public SpacecraftInputState Input => new(
            Translation,
            Look,
            Roll,
            Boost,
            Brake,
            ToggleFlightAssist);
        public void Dispose()
        {
        }
        public uint GetTick() => tick;
        public void SetTick(uint value) => tick = value;
    }

    public struct SpacecraftReconcileData : IReconcileData
    {
        public PredictionRigidbody PredictionRigidbody;
        public SpacecraftMotorState MotorState;
        public uint OriginSequence;
        uint tick;

        public SpacecraftReconcileData(
            PredictionRigidbody predictionRigidbody,
            SpacecraftMotorState motorState,
            uint originSequence)
        {
            PredictionRigidbody = predictionRigidbody;
            MotorState = motorState;
            OriginSequence = originSequence;
            tick = 0;
        }

        public void Dispose()
        {
        }
        public uint GetTick() => tick;
        public void SetTick(uint value) => tick = value;
    }

    sealed class PredictionRigidbodySpacecraftPhysicsBody :
        ISpacecraftPhysicsBody
    {
        readonly PredictionRigidbody predictionRigidbody;

        public PredictionRigidbodySpacecraftPhysicsBody(
            PredictionRigidbody predictionRigidbody) =>
            this.predictionRigidbody = predictionRigidbody;

        Rigidbody Rigidbody => predictionRigidbody.Rigidbody;
        public Vector3 Position => Rigidbody.position;
        public Vector3 WorldCenterOfMass => Rigidbody.worldCenterOfMass;
        public Vector3 LinearVelocity => Rigidbody.linearVelocity;
        public Vector3 AngularVelocity => Rigidbody.angularVelocity;
        public void SetLinearVelocity(Vector3 velocity) =>
            predictionRigidbody.Velocity(velocity);
        public void SetAngularVelocity(Vector3 velocity) =>
            predictionRigidbody.AngularVelocity(velocity);
        public void MovePosition(Vector3 position) =>
            predictionRigidbody.MovePosition(position);
        public void AddForce(Vector3 force, ForceMode mode) =>
            predictionRigidbody.AddForce(force, mode);
        public void AddRelativeTorque(Vector3 torque, ForceMode mode) =>
            predictionRigidbody.AddRelativeTorque(torque, mode);
        public void Commit() => predictionRigidbody.Simulate();
    }

    public sealed class NetworkStarterShip :
        TickNetworkBehaviour,
        IInteractable,
        ICelestialSurfaceCollisionObserver
    {
        const string ClaimPrompt = "Enter ship";
        const string PilotPrompt = "Pilot ship";

        readonly SyncVar<ulong> entityId = new();
        readonly SyncVar<byte> formationSlot = new();
        readonly SyncVar<ulong> claimedBySessionPlayerId = new();
        readonly SyncVar<bool> piloted = new();

        [Min(0.1f)]
        [SerializeField] float maximumClaimDistance = 6f;
        PersistentObjectId persistentObjectId;
        VehicleBoardingPoint boardingPoint;
        SpacecraftRig spacecraftRig;
        SpacecraftMotor motor;
        KeyboardSpacecraftInput input;
        KeyboardBoardingInput boardingInput;
        CelestialActorProbe celestialProbe;
        SpacecraftAtmosphereInteractor atmosphereInteractor;
        SpacecraftOceanInteractor oceanInteractor;
        SpacecraftSurfaceContactProbe surfaceContactProbe;
        SpacecraftSurfaceContactStabilizer surfaceContactStabilizer;
        Rigidbody body;
        Rigidbody pilotBody;
        readonly PredictionRigidbody predictionRigidbody = new();
        PredictionRigidbodySpacecraftPhysicsBody physicsBody;
        NetworkWorldOriginAuthority originAuthority;
        MultiplayerSceneContext sceneContext;
        int claimedConnectionId = -1;
        bool localPiloting;

        public GeneratedEntityId EntityId => entityId.Value == 0UL
            ? GeneratedEntityId.None
            : new GeneratedEntityId(entityId.Value);
        public int FormationSlot => formationSlot.Value;
        public ulong ClaimedBySessionPlayerId =>
            claimedBySessionPlayerId.Value;
        public bool IsClaimed => ClaimedBySessionPlayerId != 0UL;
        public bool IsPiloted => piloted.Value;
        public SpacecraftRig Rig => spacecraftRig;
        public SpacecraftMotor Motor => motor;
        public KeyboardSpacecraftInput Input => input;
        public string InteractionPrompt => CanLocalPilot()
            ? PilotPrompt
            : ClaimPrompt;

        void Awake()
        {
            persistentObjectId = GetComponent<PersistentObjectId>();
            boardingPoint = GetComponentInChildren<VehicleBoardingPoint>(true);
            boardingPoint?.Bind((IInteractable)this);
            spacecraftRig = GetComponent<SpacecraftRig>();
            motor = GetComponent<SpacecraftMotor>();
            input = GetComponent<KeyboardSpacecraftInput>();
            boardingInput = GetComponent<KeyboardBoardingInput>();
            celestialProbe = GetComponent<CelestialActorProbe>();
            atmosphereInteractor = GetComponent<SpacecraftAtmosphereInteractor>();
            oceanInteractor = GetComponent<SpacecraftOceanInteractor>();
            surfaceContactProbe = GetComponent<SpacecraftSurfaceContactProbe>();
            surfaceContactStabilizer =
                GetComponent<SpacecraftSurfaceContactStabilizer>();
            body = GetComponent<Rigidbody>();
            predictionRigidbody.Initialize(body);
            physicsBody = new PredictionRigidbodySpacecraftPhysicsBody(
                predictionRigidbody);
            piloted.OnChange += OnPilotedChanged;
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

            if (localPiloting &&
                boardingInput != null &&
                boardingInput.CurrentInput.ExitVehicle)
            {
                NetworkSessionPlayer.Local?.RequestExitStarterShip(EntityId);
            }
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
            ApplyPersistentId();
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

        public override void OnStopNetwork()
        {
            motor.SetExternalSimulation(false);
            celestialProbe?.SetExternalSimulation(false);
            atmosphereInteractor?.SetExternalSimulation(false);
            oceanInteractor?.SetExternalSimulation(false);
            surfaceContactProbe?.SetExternalSimulation(false);
            surfaceContactStabilizer?.SetExternalSimulation(false);
            ApplyPilotedState(false);
        }

        public bool CanInteract(InteractionContext context)
        {
            NetworkSessionPlayer sessionPlayer = NetworkSessionPlayer.Local;
            return sessionPlayer != null &&
                   EntityId.IsValid &&
                   ((!sessionPlayer.ClaimedStarterShipId.IsValid &&
                     sessionPlayer.AssignedStarterShipId == EntityId &&
                     !IsClaimed &&
                     sessionPlayer.PossessionMode == PlayerPossessionMode.OnFoot) ||
                    CanLocalPilot());
        }

        public void Interact(InteractionContext context)
        {
            NetworkSessionPlayer.Local?.RequestUseStarterShip(EntityId);
        }

        protected override void TimeManager_OnTick()
        {
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
            CreateReconcile();
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
            celestialProbe?.RefreshSample();
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

            predictionRigidbody.Reconcile(data.PredictionRigidbody);
            motor.RestoreState(data.MotorState);
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
            NetworkWorldOriginAuthority authority)
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
            if (persistentObjectId != null && entityId.Value != 0UL)
            {
                persistentObjectId.SetId($"ship.generated.{entityId.Value:X16}");
            }
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
                sessionPlayer.ClaimedStarterShipId == EntityId &&
                IsClaimedBy(sessionPlayer.SessionPlayerId);
        }

        bool IsLocalPilot()
        {
            NetworkSessionPlayer sessionPlayer = NetworkSessionPlayer.Local;
            return IsOwner &&
                IsPiloted &&
                sessionPlayer != null &&
                sessionPlayer.PossessionMode == PlayerPossessionMode.Spacecraft &&
                sessionPlayer.ClaimedStarterShipId == EntityId &&
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

            bool simulate = active && (IsServerStarted || IsOwner);
            if (!simulate && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.isKinematic = !simulate;
        }

    }
}
