using Farion.Gameplay.Actors;
using Farion.Gameplay.Character;
using Farion.Multiplayer.Session;
using Farion.Multiplayer.World;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;

namespace Farion.Multiplayer.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(FirstPersonMotor))]
    [RequireComponent(typeof(KeyboardFirstPersonInput))]
    public sealed class NetworkExplorerController :
        TickNetworkBehaviour,
        ICelestialSurfaceCollisionObserver
    {
        [SerializeField] FirstPersonMotor motor;
        [SerializeField] KeyboardFirstPersonInput input;
        [SerializeField] Renderer[] ownerHiddenRenderers;

        readonly PredictionRigidbody predictionRigidbody = new();
        PredictionRigidbodyFirstPersonPhysicsBody physicsBody;
        Rigidbody body;
        NetworkWorldOriginAuthority originAuthority;
        MultiplayerSceneContext sceneContext;
        float accumulatedYaw;
        bool jumpQueued;
        bool previousJumpHeld;

        public FirstPersonMotor Motor => motor;
        public KeyboardFirstPersonInput Input => input;

        void Awake()
        {
            motor ??= GetComponent<FirstPersonMotor>();
            input ??= GetComponent<KeyboardFirstPersonInput>();
            body = GetComponent<Rigidbody>();
            predictionRigidbody.Initialize(body);
            physicsBody = new PredictionRigidbodyFirstPersonPhysicsBody(
                predictionRigidbody);
        }

        void Update()
        {
            if (!IsOwner || input == null)
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
            motor.SetExternalSimulation(true);
            body.interpolation = RigidbodyInterpolation.None;
            SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
        }

        public override void OnStartClient()
        {
            sceneContext = MultiplayerSceneContext.FindIn(gameObject.scene);
            if (sceneContext != null)
            {
                sceneContext.AttachToShiftedWorld(transform);
                BindScene(
                    sceneContext.CelestialFrameProvider,
                    sceneContext.OriginAuthority);
            }

            if (input != null)
            {
                input.enabled = IsOwner;
            }

            SetOwnerRendererVisibility(!IsOwner);
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
            if (IsOwner)
            {
                sceneContext?.UnbindOwnedPlayer(this);
            }
        }

        public override void OnStopNetwork()
        {
            motor.SetExternalSimulation(false);
        }

        public void BindScene(
            CelestialFrameProvider frameProvider,
            NetworkWorldOriginAuthority worldOriginAuthority)
        {
            originAuthority = worldOriginAuthority;
            GetComponent<CelestialActorProbe>()?.SetFrameProvider(frameProvider);
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
            if (body == null || !body.gameObject.activeInHierarchy)
            {
                observer = default;
                return false;
            }

            observer = new CelestialSurfaceCollisionObserverState(body);
            return true;
        }

        ExplorerReplicateData BuildReplicateData()
        {
            if (!IsOwner || input == null)
            {
                return default;
            }

            FirstPersonInputState current = input.CurrentInput;
            ExplorerReplicateData data = new(
                current.Movement,
                accumulatedYaw,
                jumpQueued,
                current.Sprint,
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
            ExplorerReplicateData clamped = new(
                data.Movement,
                data.YawDegrees,
                data.Jump,
                data.Sprint,
                data.OriginSequence);
            bool staleOrigin = IsServerStarted &&
                originAuthority != null &&
                clamped.OriginSequence != originAuthority.CurrentSequence;
            FirstPersonMotorInput motorInput = staleOrigin
                ? FirstPersonMotorInput.None
                : new FirstPersonMotorInput(
                    clamped.Movement,
                    clamped.YawDegrees,
                    clamped.Jump,
                    clamped.Sprint);
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

        [Reconcile]
        void PerformReconcile(
            ExplorerReconcileData data,
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

    }
}
