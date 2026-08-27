using System.Collections.Generic;
using Farion.Core.Physics;
using Farion.Core.Persistence;
using Farion.Gameplay.Actors;
using Farion.Gameplay.Character;
using Farion.Gameplay.Interaction;
using Farion.Multiplayer.Session;
using Farion.Multiplayer.World;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;

namespace Farion.Multiplayer.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CelestialActorProbe))]
    [RequireComponent(typeof(FirstPersonMotor))]
    [RequireComponent(typeof(KeyboardFirstPersonInput))]
    public sealed class NetworkExplorerController :
        TickNetworkBehaviour,
        ICelestialSurfaceCollisionObserver
    {
        [SerializeField] FirstPersonMotor motor;
        [SerializeField] KeyboardFirstPersonInput input;
        [SerializeField] PlayerInteractionRaycaster interactionRaycaster;
        [SerializeField] Renderer[] ownerHiddenRenderers;

        readonly PredictionRigidbody predictionRigidbody = new();
        readonly SyncVar<bool> possessionActive = new(true);
        readonly SyncVar<ulong> sessionPlayerId = new();
        PredictionRigidbodyFirstPersonPhysicsBody physicsBody;
        Rigidbody body;
        CapsuleCollider capsule;
        CelestialActorProbe celestialProbe;
        MultiplayerWorldOriginAuthority originAuthority;
        MultiplayerSceneContext sceneContext;
        ZonePhysicsTickDriver tickDriver;
        float accumulatedYaw;
        bool jumpQueued;
        bool previousJumpHeld;
        bool appliedPossessionActive = true;

        public FirstPersonMotor Motor => motor;
        public KeyboardFirstPersonInput Input => input;
        public PlayerInteractionRaycaster InteractionRaycaster =>
            interactionRaycaster;
        public ulong SessionPlayerId => sessionPlayerId.Value;

        static readonly List<NetworkExplorerController> activeExplorers = new();

        public static IReadOnlyList<NetworkExplorerController> ActiveExplorers =>
            activeExplorers;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetActiveExplorers()
        {
            activeExplorers.Clear();
        }

        internal void InitializeIdentity(ulong value)
        {
            if (value == 0UL)
            {
                return;
            }

            sessionPlayerId.Value = value;
            ApplyPersistentIdentity();
        }

        internal static string BuildPersistentId(ulong value) =>
            value == 0UL ? string.Empty : $"explorer.net.{value}";

        void Awake()
        {
            motor ??= GetComponent<FirstPersonMotor>();
            input ??= GetComponent<KeyboardFirstPersonInput>();
            interactionRaycaster ??=
                GetComponent<PlayerInteractionRaycaster>();
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            celestialProbe = GetComponent<CelestialActorProbe>();
            gameObject.layer = FarionLayers.Explorer;
            if (!activeExplorers.Contains(this))
            {
                activeExplorers.Add(this);
            }

            predictionRigidbody.Initialize(body);
            physicsBody = new PredictionRigidbodyFirstPersonPhysicsBody(
                predictionRigidbody);
            possessionActive.OnChange += OnPossessionActiveChanged;
        }

        void Update()
        {
            if (!IsOwner || !appliedPossessionActive || input == null)
            {
                return;
            }

            FirstPersonInputState current = input.CurrentInput;
            accumulatedYaw +=
                current.Look.x * motor.YawDegreesPerMouseUnit;
            if (current.Jump && !previousJumpHeld)
            {
                jumpQueued = true;
            }

            previousJumpHeld = current.Jump;
        }

        public override void OnStartNetwork()
        {
            tickDriver = NetworkManager != null
                ? NetworkManager.GetComponent<ZonePhysicsTickDriver>()
                : null;
            motor.SetExternalSimulation(true);
            celestialProbe.SetExternalSimulation(true);
            ApplyOwnerInterpolation(Owner.IsLocalClient);
            SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
            ApplyPossessionState(possessionActive.Value);
        }

        public override void OnStartClient()
        {
            ApplyPersistentIdentity();
            sceneContext = MultiplayerSceneContext.FindIn(gameObject.scene);
            if (sceneContext != null)
            {
                sceneContext.AttachToShiftedWorld(transform);
                BindScene(
                    sceneContext.CelestialFrameProvider,
                    sceneContext.OriginAuthority);
            }

            ApplyPossessionState(possessionActive.Value);
            SetOwnerRendererVisibility(
                appliedPossessionActive && !IsOwner);
            if (sceneContext != null && (IsServerStarted || IsOwner))
            {
                sceneContext.RegisterFormationObserver(transform);
            }

            if (!IsOwner)
            {
                return;
            }

            if (sceneContext != null && sceneContext.BindOwnedPlayer(this))
            {
                MultiplayerSessionController.Active?.NotifyOwnedPlayerReady();
            }
            else
            {
                MultiplayerSessionController.Active?.FailOwnedPlayerSetup();
            }
        }

        public override void OnStopClient()
        {
            if (sceneContext != null)
            {
                sceneContext.UnregisterFormationObserver(transform);
            }

            if (IsOwner && sceneContext != null)
            {
                sceneContext.UnbindOwnedPlayer(this);
            }
        }

        void OnDestroy()
        {
            activeExplorers.Remove(this);
        }

        public override void OnStopNetwork()
        {
            motor.SetExternalSimulation(false);
            celestialProbe.SetExternalSimulation(false);
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            ApplyPossessionState(possessionActive.Value);
            ApplyOwnerInterpolation(IsOwner);
            SetOwnerRendererVisibility(
                appliedPossessionActive && !IsOwner);
        }

        void ApplyOwnerInterpolation(bool owned)
        {
            if (body == null)
            {
                return;
            }

            body.interpolation = owned
                ? RigidbodyInterpolation.Interpolate
                : RigidbodyInterpolation.None;
        }

        public void BindScene(
            CelestialFrameProvider frameProvider,
            MultiplayerWorldOriginAuthority worldOriginAuthority)
        {
            originAuthority = worldOriginAuthority;
            celestialProbe.SetFrameProvider(frameProvider);
        }

        protected override void TimeManager_OnTick()
        {
            ExplorerReplicateData data = BuildReplicateData();
            PerformReplicate(data);
        }

        protected override void TimeManager_OnPostTick()
        {
            CreateReconcile();
        }

        public bool TryGetSurfaceCollisionObserver(
            out CelestialSurfaceCollisionObserverState observer)
        {
            if (body == null ||
                !body.gameObject.activeInHierarchy ||
                !appliedPossessionActive ||
                (!IsServerStarted && !IsOwner))
            {
                observer = default;
                return false;
            }

            observer = new CelestialSurfaceCollisionObserverState(body);
            return true;
        }

        ExplorerReplicateData BuildReplicateData()
        {
            if (!IsOwner || !appliedPossessionActive || input == null)
            {
                accumulatedYaw = 0f;
                jumpQueued = false;
                return default;
            }

            FirstPersonInputState current = input.CurrentInput;
            ExplorerReplicateData data = new(
                current.Movement,
                accumulatedYaw,
                jumpQueued,
                current.Sprint,
                current.Jump,
                originAuthority?.CurrentSequence ?? 0);
            accumulatedYaw = 0f;
            jumpQueued = false;
            return data;
        }

        [Replicate]
        void PerformReplicate(
            ExplorerReplicateData data,
            ReplicateState state = ReplicateState.Invalid,
            Channel channel = Channel.Unreliable)
        {
            if (!appliedPossessionActive)
            {
                return;
            }

            ExplorerReplicateData clamped = new(
                data.Movement,
                data.YawDegrees,
                data.Jump,
                data.Sprint,
                data.SwimAscend,
                data.OriginSequence);
            bool staleOrigin = IsServerStarted &&
                originAuthority != null &&
                !originAuthority.SharesReferenceFrame(clamped.OriginSequence);
            FirstPersonMotorInput motorInput = staleOrigin
                ? FirstPersonMotorInput.None
                : new FirstPersonMotorInput(
                    clamped.Movement,
                    clamped.YawDegrees,
                    clamped.Jump,
                    clamped.Sprint,
                    clamped.SwimAscend);
            celestialProbe.RefreshSample(tickDriver != null
                ? tickDriver.ResolveSimulationSeconds(data.GetTick())
                : data.GetTick() * TimeManager.TickDelta);
            motor.Simulate(
                motorInput,
                (float)TimeManager.TickDelta,
                physicsBody);
        }

        public override void CreateReconcile()
        {
            PerformReconcile(new ExplorerReconcileData(
                predictionRigidbody,
                motor.CaptureState(),
                originAuthority?.CurrentSequence ?? 0));
        }

        internal void SetPossessionActive(bool active)
        {
            if (!IsServerStarted)
            {
                return;
            }

            possessionActive.Value = active;
            ApplyPossessionState(active);
        }

        internal void ApplyPossessionState(bool active)
        {
            appliedPossessionActive = active;
            if (input != null)
            {
                input.enabled = active && IsOwner;
            }

            if (interactionRaycaster != null)
            {
                interactionRaycaster.enabled = active && IsOwner;
            }

            if (!active)
            {
                accumulatedYaw = 0f;
                jumpQueued = false;
                previousJumpHeld = false;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }

            bool simulate = active;
            if (!simulate && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.isKinematic = !simulate;
            if (capsule != null)
            {
                capsule.enabled = active;
            }

            SetOwnerRendererVisibility(active && !IsOwner);
        }

        void OnPossessionActiveChanged(bool previous, bool next, bool asServer)
        {
            ApplyPossessionState(next);
        }

        [Reconcile]
        void PerformReconcile(
            ExplorerReconcileData data,
            Channel channel = Channel.Unreliable)
        {
            if (originAuthority != null &&
                !originAuthority.SharesReferenceFrame(data.OriginSequence))
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

        void SetOwnerRendererVisibility(bool visible)
        {
            if (ownerHiddenRenderers == null)
            {
                return;
            }

            for (int i = 0; i < ownerHiddenRenderers.Length; i++)
            {
                if (ownerHiddenRenderers[i] != null)
                {
                    ownerHiddenRenderers[i].enabled = visible;
                }
            }
        }

        void ApplyPersistentIdentity()
        {
            string persistentId = BuildPersistentId(sessionPlayerId.Value);
            if (!string.IsNullOrEmpty(persistentId))
            {
                GetComponent<PersistentObjectId>()?.SetId(persistentId);
            }
        }

    }
}
