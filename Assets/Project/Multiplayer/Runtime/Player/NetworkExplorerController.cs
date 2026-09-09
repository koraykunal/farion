using System.Collections.Generic;
using Farion.Core.Physics;
using Farion.Core.Persistence;
using Farion.Gameplay.Actors;
using Farion.Gameplay.Character;
using Farion.Gameplay.Flight;
using Farion.Gameplay.Interaction;
using Farion.Gameplay.Presentation.Character;
using Farion.Multiplayer.Session;
using Farion.Multiplayer.Spacecraft;
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
using UnityEngine.Rendering;

namespace Farion.Multiplayer.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CelestialActorProbe))]
    [RequireComponent(typeof(ExplorerMotor))]
    [RequireComponent(typeof(ExplorerInput))]
    public sealed class NetworkExplorerController :
        TickNetworkBehaviour,
        ICelestialSurfaceCollisionObserver
    {
        [SerializeField] ExplorerMotor motor;
        [SerializeField] ExplorerInput input;
        [SerializeField] PlayerInteractionRaycaster interactionRaycaster;
        [SerializeField] PlayerExplorerSeatedPose seatedPose;
        [SerializeField] ExplorerTool tool;

        readonly PredictionRigidbody predictionRigidbody = new();
        readonly SyncVar<bool> possessionActive = new(true);
        readonly SyncVar<bool> insideShip = new(false);
        readonly SyncVar<ulong> sessionPlayerId = new();
        readonly SyncVar<byte> toolState = new();
        PredictionRigidbodyExplorerPhysicsBody physicsBody;
        Rigidbody body;
        CapsuleCollider capsule;
        CelestialActorProbe celestialProbe;
        ZoneOriginState originState;
        MultiplayerSceneContext sceneContext;
        ZonePhysicsTickDriver tickDriver;
        bool jumpQueued;
        bool previousJumpHeld;
        bool appliedPossessionActive = true;
        bool seatedBodyVisibleForOwner;
        RendererVisibility[] rendererVisibility = System.Array.Empty<RendererVisibility>();

        struct RendererVisibility
        {
            public Renderer Renderer;
            public bool Enabled;
            public ShadowCastingMode Shadows;
        }

        public ExplorerMotor Motor => motor;
        public ExplorerInput Input => input;
        public ExplorerTool Tool => tool;
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
            motor ??= GetComponent<ExplorerMotor>();
            input ??= GetComponent<ExplorerInput>();
            interactionRaycaster ??=
                GetComponent<PlayerInteractionRaycaster>();
            seatedPose ??= GetComponentInChildren<PlayerExplorerSeatedPose>(true);
            tool ??= GetComponent<ExplorerTool>();
            if (tool != null)
            {
                tool.SetControl(false, false);
                tool.LocalStateChanged += HandleLocalToolState;
            }

            toolState.OnChange += HandleToolStateChanged;
            CacheRendererVisibility();
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            celestialProbe = GetComponent<CelestialActorProbe>();
            gameObject.layer = FarionLayers.Explorer;
            if (!activeExplorers.Contains(this))
            {
                activeExplorers.Add(this);
            }

            predictionRigidbody.Initialize(body);
            physicsBody = new PredictionRigidbodyExplorerPhysicsBody(
                predictionRigidbody);
            possessionActive.OnChange += OnPossessionActiveChanged;
            insideShip.OnChange += OnInsideShipChanged;
        }

        public bool IsInsideShip => insideShip.Value;

        internal void SetInsideShip(bool inside)
        {
            if (!IsServerStarted)
            {
                return;
            }

            insideShip.Value = inside;
            ApplyInsideShipLayer(inside);
        }

        void OnInsideShipChanged(bool previous, bool next, bool asServer)
        {
            ApplyInsideShipLayer(next);
        }

        void ApplyInsideShipLayer(bool inside)
        {
            gameObject.layer = inside
                ? FarionLayers.ExplorerInterior
                : FarionLayers.Explorer;
        }

        void Update()
        {
            if (!IsOwner || !appliedPossessionActive || input == null)
            {
                return;
            }

            ExplorerInputState current = input.CurrentInput;
            if (current.Jump && !previousJumpHeld)
            {
                jumpQueued = true;
            }

            previousJumpHeld = current.Jump;
        }

        void LateUpdate()
        {
            if (appliedPossessionActive || seatedPose == null || seatedPose.IsSeated)
            {
                return;
            }

            seatedPose.SetSeat(ResolvePilotSeatRig());
        }

        SpacecraftRig ResolvePilotSeatRig()
        {
            ulong id = sessionPlayerId.Value;
            IReadOnlyList<NetworkSessionPlayer> players = NetworkSessionPlayer.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                NetworkSessionPlayer player = players[i];
                if (player == null || player.SessionPlayerId != id)
                {
                    continue;
                }

                NetworkStarterShuttle ship =
                    NetworkStarterShuttle.FindByEntityId(player.ClaimedStarterShuttleId);
                return ship != null && ship.IsPiloted ? ship.Rig : null;
            }

            return null;
        }

        internal void SetSeatedBodyVisibleForOwner(bool visible)
        {
            seatedBodyVisibleForOwner = visible;
            RefreshRendererVisibility();
        }

        void RefreshRendererVisibility()
        {
            bool cockpit = IsOwner && !appliedPossessionActive && !seatedBodyVisibleForOwner;
            for (int i = 0; i < rendererVisibility.Length; i++)
            {
                ApplyRendererVisibility(rendererVisibility[i], cockpit);
            }
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
                    sceneContext.ZoneOrigin);
            }

            ApplyPossessionState(possessionActive.Value);
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
            tool?.SetControl(false, false);
            if (sceneContext != null)
            {
                sceneContext.UnregisterFormationObserver(transform);
            }

            if (IsOwner && sceneContext != null)
            {
                sceneContext.UnbindOwnedPlayer(this);
            }

            for (int i = 0; i < rendererVisibility.Length; i++)
            {
                ApplyRendererVisibility(rendererVisibility[i], false);
            }
        }

        void OnDestroy()
        {
            if (tool != null) tool.LocalStateChanged -= HandleLocalToolState;
            toolState.OnChange -= HandleToolStateChanged;
            activeExplorers.Remove(this);
        }

        void HandleLocalToolState(byte value)
        {
            if (IsOwner && IsClientStarted) RequestToolState(value);
        }

        [ServerRpc]
        void RequestToolState(byte value)
        {
            if (value > ExplorerTool.Using) return;
            toolState.Value = appliedPossessionActive ? value : ExplorerTool.Stowed;
        }

        void HandleToolStateChanged(byte previous, byte next, bool asServer)
        {
            tool?.ApplyRemoteState(next);
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
            RefreshRendererVisibility();
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
            ZoneOriginState zoneOriginState)
        {
            originState = zoneOriginState;
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
                !appliedPossessionActive)
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
                jumpQueued = false;
                return default;
            }

            ExplorerInputState current = input.CurrentInput;
            ExplorerReplicateData data = new(
                motor.BuildInput(
                    current.Movement,
                    current.Aim || (tool != null && tool.Equipped),
                    jumpQueued,
                    current.Sprint,
                    current.Jump,
                    current.Dive),
                originState?.CurrentSequence ?? 0);
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

            bool staleOrigin = IsServerStarted &&
                originState != null &&
                !originState.SharesReferenceFrame(data.OriginSequence);
            ExplorerMotorInput motorInput = staleOrigin
                ? ExplorerMotorInput.None
                : data.ToMotorInput();
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
                originState?.CurrentSequence ?? 0));
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
            tool?.SetControl(IsOwner, active);
            tool?.ApplyRemoteState(toolState.Value);
            if (!active && IsServerStarted)
            {
                toolState.Value = ExplorerTool.Stowed;
            }

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
                jumpQueued = false;
                previousJumpHeld = false;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }

            body.isKinematic = !active;
            if (capsule != null)
            {
                capsule.enabled = active;
            }

            if (active)
            {
                seatedPose?.SetSeat(null);
            }

            RefreshRendererVisibility();
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

        void CacheRendererVisibility()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            rendererVisibility = new RendererVisibility[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                rendererVisibility[i] = new RendererVisibility
                {
                    Renderer = renderer,
                    Enabled = renderer.enabled,
                    Shadows = renderer.shadowCastingMode
                };
            }
        }

        static void ApplyRendererVisibility(in RendererVisibility state, bool hidden)
        {
            if (state.Renderer == null)
            {
                return;
            }

            state.Renderer.enabled = state.Enabled &&
                (!hidden || state.Shadows != ShadowCastingMode.Off);
            state.Renderer.shadowCastingMode = hidden && state.Shadows != ShadowCastingMode.Off
                ? ShadowCastingMode.ShadowsOnly
                : state.Shadows;
        }

        void ApplyPersistentIdentity()
        {
            string persistentId = BuildPersistentId(sessionPlayerId.Value);
            if (!string.IsNullOrEmpty(persistentId))
            {
                GetComponent<PersistentObjectId>()?.SetId(persistentId);
            }

            GetComponentInChildren<VisorEyes>(true)?.SetIdentity(sessionPlayerId.Value);
        }

    }
}
