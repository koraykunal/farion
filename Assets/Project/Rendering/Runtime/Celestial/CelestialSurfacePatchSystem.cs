using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farion.Core.Physics;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.Planetary;
using UnityEngine;
using UnityEngine.Rendering;

namespace Farion.Rendering.Celestial
{
    public enum CelestialSurfaceCollisionAuthority
    {
        GlobalFallback,
        LocalPreparing,
        LocalAuthoritative
    }

    [DefaultExecutionOrder(350)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CelestialBody))]
    [RequireComponent(typeof(CelestialBodyVisual))]
    public sealed partial class CelestialSurfacePatchSystem : MonoBehaviour
    {
        const string PatchContainerName = "Adaptive Surface Patches";
        const int PatchBudgetReserve = 32;
        const int CollisionPredictionSamples = 5;
        const int MaximumCollisionBakesPerFrame = 2;
        const float MorphStartRatio = 0.55f;
        const float RenderLeadSeconds = 0.5f;
        const float PatchBoundingRatio = 0.75f;
        static readonly int SurfaceObserverPropertyId =
            Shader.PropertyToID("_FarionSurfaceObserverWS");
        static readonly int PreviousSurfaceObserverPropertyId =
            Shader.PropertyToID("_FarionPreviousSurfaceObserverWS");
        const int PendingBuildDrainMilliseconds = 2000;

        [Header("Profile")]
        [SerializeField] CelestialSurfacePatchProfile profile;

        [Header("Sources")]
        [SerializeField] CelestialBodyVisual bodyVisual;
        [SerializeField] Camera targetCamera;
        [SerializeField] MonoBehaviour collisionObserverSource;

        [Header("Runtime State")]
        [SerializeField] bool surfaceModeActive;
        [SerializeField] bool surfaceRenderActive;
        [SerializeField] bool surfaceRenderRequested;
        [SerializeField] CelestialSurfaceCollisionAuthority collisionAuthority;
        [SerializeField] bool localCollisionCoverageReady;
        [SerializeField] float collisionObserverSpeed;
        [SerializeField] float predictedCollisionTravelDistance;
        [SerializeField] int activePatchCount;
        [SerializeField] int activeColliderCount;
        [SerializeField] int deepestActiveLevel;
        [SerializeField] bool patchTransitionPending;
        [SerializeField] int pendingPatchBuildCount;
        [SerializeField] int committedTransitionCount;
        [SerializeField] int skippedUnchangedRefreshCount;
        [SerializeField] int lastTransitionBuildCount;
        [SerializeField] int lastTransitionCommitFrame = -1;
        [SerializeField] int resolvedSubdivisionLevel;
        [SerializeField] int resolvedCollisionLevel;
        [SerializeField] float resolvedEnterAltitude;
        [SerializeField] float resolvedExitAltitude;
        [SerializeField] float resolvedCollisionSafetyMarginAngle;

        readonly Dictionary<PatchKey, SurfacePatch> activePatches = new();
        readonly Dictionary<PatchKey, SurfacePatch> stagedPatches = new();
        readonly Dictionary<PatchKey, PatchDescriptor> desiredPatchMap = new();
        readonly Stack<SurfacePatch> patchPool = new();
        readonly Stack<PatchGeometry> geometryPool = new();
        readonly Stack<PatchBuildOperation> operationPool = new();
        readonly List<PatchDescriptor> desiredPatches = new();
        readonly HashSet<PatchKey> desiredKeys = new();
        readonly HashSet<PatchKey> desiredCollisionKeys = new();
        readonly HashSet<PatchKey> activeCollisionKeys = new();
        readonly List<PatchBuildOperation> pendingPatchBuilds = new();
        readonly List<PatchBuildOperation> abandonedPatchBuilds = new();
        readonly List<PatchDescriptor> balanceQueue = new();
        readonly List<PatchKey> staleKeys = new();
        readonly HashSet<PatchKey> subdividedBranches = new();
        readonly List<CelestialSurfaceCollisionObserverState> gatheredObservers = new();
        readonly List<Rigidbody> lastGatheredObserverRigidbodies = new();
        readonly List<Vector3> lastGatheredObserverLocalPositions = new();
        readonly List<Vector3> splitObserverLocalPositions = new();
        readonly List<Vector3> splitObserverUpDirections = new();
        readonly List<float> splitObserverVisibleHalfAngles = new();
        readonly int[] faceTraversalOrder = { 0, 1, 2, 3, 4, 5 };

        Transform patchContainer;
        PlanetSurfaceModel surfaceModel;
        int publishedSubdivisionLevel = -1;
        bool forceRefresh = true;
        bool patchGeometryDirty;
        bool transitionRebuildsAllGeometry;
        bool transitionLocalCollisionCoverageReady;
        int transitionMaximumLevel;
        int nextPendingPatchBuild;
        static int collisionBakesThisFrame;
        static int collisionBakeFrame = -1;
        double patchBuildDeadline;
        float transitionBaseRadius;
        Vector3 transitionLeadLocalPosition;
        CelestialSurfaceSampler transitionSampler;
        CancellationTokenSource transitionCancellation;
        bool observerStateInitialized;
        bool surfaceObserverStateInitialized;
        Vector3 previousSurfaceObserverPosition;
        Vector3 lastObserverLocalPosition;
        Vector3 lastObserverLocalDirection;
        bool collisionObserverStateInitialized;
        Rigidbody lastCollisionObserverRigidbody;
        Vector3 lastCollisionObserverLocalPosition;
        Vector3 lastCollisionObserverLocalDirection;
        Vector3 lastCollisionObserverLocalVelocity;
        bool currentCollisionObserverAvailable;
        Vector3 currentCollisionObserverLocalPosition;
        Vector3 currentCollisionObserverLocalVelocity;
        float currentOuterRadius;
        ICelestialSurfaceCollisionObserver collisionObserver;
        ICelestialSurfaceCollisionObserverGroup collisionObserverGroup;

        public bool SurfaceModeActive => surfaceModeActive;
        public bool SurfaceRenderActive => surfaceRenderActive;
        public CelestialSurfaceCollisionAuthority CollisionAuthority => collisionAuthority;
        public bool LocalCollisionCoverageReady => localCollisionCoverageReady;
        public float CollisionObserverSpeed => collisionObserverSpeed;
        public float PredictedCollisionTravelDistance => predictedCollisionTravelDistance;
        public Rigidbody CollisionObserverRigidbody =>
            TryGetCollisionObserver(out CelestialSurfaceCollisionObserverState observer)
                ? observer.Rigidbody
                : null;
        public int ActivePatchCount => activePatchCount;
        public int ActiveColliderCount => activeColliderCount;
        public int DeepestActiveLevel => deepestActiveLevel;
        public bool PatchTransitionPending => patchTransitionPending;
        public int PendingPatchBuildCount => pendingPatchBuildCount;
        public int CommittedTransitionCount => committedTransitionCount;
        public int SkippedUnchangedRefreshCount => skippedUnchangedRefreshCount;
        public int LastTransitionCommitFrame => lastTransitionCommitFrame;
        public Camera TargetCamera => ResolveCamera();

        bool TryCreateSurfaceSampler(out CelestialSurfaceSampler sampler)
        {
            CelestialBody body = bodyVisual != null ? bodyVisual.Body : null;
            if (profile == null || bodyVisual == null || body == null)
            {
                sampler = default;
                return false;
            }

            sampler = CelestialSurfaceSampler.Create(
                Mathf.Max(0.01f, body.Radius),
                CalculatePatchAngularFootprint(
                    profile.ResolveSubdivisionLevel(body.Radius),
                    profile.PatchResolution),
                bodyVisual.ShapeProfile,
                bodyVisual.SurfaceProfile);
            return true;
        }

        internal bool TryResolveSurfaceAnchor(
            Vector3 unitDirection,
            out CelestialSurfaceAnchor anchor)
        {
            anchor = default;
            if (activePatches.Count == 0 ||
                !TryCreateSurfaceSampler(out CelestialSurfaceSampler sampler))
            {
                return false;
            }

            unitDirection = unitDirection.sqrMagnitude > 0.000001f
                ? unitDirection.normalized
                : Vector3.up;
            CelestialCubeProjection.Project(
                unitDirection,
                out CelestialCubeFace face,
                out float u,
                out float v);
            for (int level = resolvedSubdivisionLevel; level >= 0; level--)
            {
                if (!activePatches.TryGetValue(ResolvePatchKey(face, level, u, v), out SurfacePatch patch))
                {
                    continue;
                }

                float fineRadius = CelestialSurfaceGridSampler.EvaluateRenderedRadius(
                    sampler,
                    unitDirection,
                    level,
                    profile.PatchResolution);
                float coarseRadius = level > 0
                    ? CelestialSurfaceGridSampler.EvaluateRenderedRadius(
                        sampler,
                        unitDirection,
                        level - 1,
                        profile.PatchResolution)
                    : fineRadius;
                anchor = new CelestialSurfaceAnchor(level, fineRadius, coarseRadius, patch.MorphRange);
                return true;
            }

            return false;
        }

        internal bool IsSurfaceAnchorCurrent(Vector3 unitDirection, in CelestialSurfaceAnchor anchor)
        {
            if (!anchor.IsValid)
            {
                return false;
            }

            CelestialCubeProjection.Project(
                unitDirection.sqrMagnitude > 0.000001f ? unitDirection.normalized : Vector3.up,
                out CelestialCubeFace face,
                out float u,
                out float v);
            return activePatches.TryGetValue(ResolvePatchKey(face, anchor.Level, u, v), out SurfacePatch patch) &&
                patch.MorphRange == anchor.MorphRange;
        }

        void ApplyPatchMorph(SurfacePatch patch, Vector4 morphRange)
        {
            patch.MorphRange = morphRange;
            bodyVisual.ConfigureSurfaceRenderer(patch.Renderer, morphRange);
        }

        public void SetCamera(Camera camera)
        {
            if (targetCamera == camera)
            {
                return;
            }

            targetCamera = camera;
            forceRefresh = true;
        }

        public void SetCollisionObserverSource(MonoBehaviour source)
        {
            collisionObserverSource = source;
            collisionObserver = source as ICelestialSurfaceCollisionObserver;
            collisionObserverGroup = source as ICelestialSurfaceCollisionObserverGroup;
            collisionObserverStateInitialized = false;
            lastCollisionObserverRigidbody = null;
            lastGatheredObserverRigidbodies.Clear();
            lastGatheredObserverLocalPositions.Clear();
            forceRefresh = true;
        }

        void Awake()
        {
            ResolveSources();
        }

        void OnEnable()
        {
            PublishSampleFootprint();
            ResolveSources();
            Subscribe();
            forceRefresh = true;
        }

        void Start()
        {
            UpdateSurfaceMode(force: true);
        }

        void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            DrainAbandonedOperations();
            Camera observerCamera = ResolveCamera();
            if (observerCamera != null)
            {
                Vector3 observerPosition = observerCamera.transform.position;
                Shader.SetGlobalVector(
                    PreviousSurfaceObserverPropertyId,
                    surfaceObserverStateInitialized
                        ? previousSurfaceObserverPosition
                        : observerPosition);
                Shader.SetGlobalVector(
                    SurfaceObserverPropertyId,
                    observerPosition);
                previousSurfaceObserverPosition = observerPosition;
                surfaceObserverStateInitialized = true;
            }
            else
            {
                surfaceObserverStateInitialized = false;
            }

            UpdateSurfaceMode(force: false);
        }

        void OnDisable()
        {
            Unsubscribe();
            DeactivateSurfaceMode();
        }

        void OnDestroy()
        {
            DestroyAllPatches();
        }

        void WaitForPendingBuilds()
        {
            for (int i = 0; i < pendingPatchBuilds.Count; i++)
            {
                WaitForBuild(pendingPatchBuilds[i].Task);
            }

            for (int i = 0; i < abandonedPatchBuilds.Count; i++)
            {
                WaitForBuild(abandonedPatchBuilds[i].Task);
            }

            for (int i = abandonedPatchBuilds.Count - 1; i >= 0; i--)
            {
                PatchBuildOperation operation = abandonedPatchBuilds[i];
                if (operation.Patch != null)
                {
                    DestroyPatch(operation.Patch);
                    operation.Patch = null;
                }

                if (operation.TaskFinished)
                {
                    ReturnOperation(operation);
                }
            }

            abandonedPatchBuilds.Clear();
        }

        static void WaitForBuild(Task task)
        {
            if (task == null)
            {
                return;
            }

            try
            {
                task.Wait(PendingBuildDrainMilliseconds);
            }
            catch (AggregateException)
            {
            }
        }

        void PublishSampleFootprint()
        {
            if (profile == null)
            {
                return;
            }

            CelestialBody body = GetComponent<CelestialBody>();
            if (body == null)
            {
                return;
            }

            PublishSampleFootprint(profile.ResolveSubdivisionLevel(body.Radius));
        }

        void PublishSampleFootprint(int subdivisionLevel)
        {
            if (publishedSubdivisionLevel == subdivisionLevel)
            {
                return;
            }

            publishedSubdivisionLevel = subdivisionLevel;
            surfaceModel ??= GetComponent<PlanetSurfaceModel>();
            if (surfaceModel == null)
            {
                return;
            }

            surfaceModel.SetSampleFootprint(CalculatePatchAngularFootprint(
                subdivisionLevel,
                profile.PatchResolution));
        }

        void RefreshResolvedMetrics(float baseRadius, float outerRadius)
        {
            float reliefMeters = Mathf.Max(0f, outerRadius - baseRadius);
            resolvedSubdivisionLevel = profile.ResolveSubdivisionLevel(baseRadius);
            PublishSampleFootprint(resolvedSubdivisionLevel);
            resolvedCollisionLevel = profile.ResolveCollisionLevel(baseRadius);
            resolvedEnterAltitude = profile.ResolveEnterAltitude(reliefMeters, baseRadius);
            resolvedExitAltitude = profile.ResolveExitAltitude(reliefMeters, baseRadius);
            resolvedCollisionSafetyMarginAngle =
                profile.CollisionSafetyMarginMeters / Mathf.Max(0.01f, baseRadius);
        }

        void OnValidate()
        {
            collisionObserver = null;
            ResolveSources();
            forceRefresh = true;
            patchGeometryDirty = true;
        }

        void UpdateSurfaceMode(bool force)
        {
            ResolveSources();
            Camera camera = ResolveCamera();
            CelestialBody body = bodyVisual != null ? bodyVisual.Body : null;
            if (profile == null ||
                bodyVisual == null ||
                body == null ||
                !body.SupportsNonConvexSurfaceCollider)
            {
                DeactivateSurfaceMode();
                return;
            }

            float baseRadius = Mathf.Max(0.01f, body.Radius);
            float outerRadius = bodyVisual.HasTerrainRadiusRange
                ? bodyVisual.TerrainRadiusMinMax.y
                : baseRadius;
            currentOuterRadius = outerRadius;
            currentCollisionObserverAvailable = TryGetCollisionObserver(
                out CelestialSurfaceCollisionObserverState collisionState);
            if (camera == null && !currentCollisionObserverAvailable)
            {
                DeactivateSurfaceMode();
                return;
            }

            RefreshResolvedMetrics(baseRadius, outerRadius);

            float cameraAltitude = camera != null
                ? Vector3.Distance(camera.transform.position, transform.position) - outerRadius
                : float.PositiveInfinity;
            float collisionAltitude = float.PositiveInfinity;
            for (int i = 0; i < gatheredObservers.Count; i++)
            {
                float observerAltitude = Vector3.Distance(
                    gatheredObservers[i].Position,
                    transform.position) - outerRadius;
                collisionAltitude = Mathf.Min(collisionAltitude, observerAltitude);
            }

            bool shouldRenderSurface = camera != null &&
                (surfaceRenderRequested
                    ? cameraAltitude <= resolvedExitAltitude
                    : cameraAltitude <= resolvedEnterAltitude);
            bool shouldKeepCollisionSurface = surfaceModeActive || patchTransitionPending
                ? collisionAltitude <= resolvedExitAltitude
                : collisionAltitude <= resolvedEnterAltitude;
            bool shouldUseSurfaceMode = shouldRenderSurface || shouldKeepCollisionSurface;

            if (!shouldUseSurfaceMode)
            {
                DeactivateSurfaceMode();
                return;
            }

            surfaceRenderRequested = shouldRenderSurface;
            ApplyPrimaryTerrainState();

            Vector3 observerLocalPosition = camera != null
                ? transform.InverseTransformPoint(camera.transform.position)
                : transform.InverseTransformPoint(collisionState.Position);
            Vector3 observerLocalDirection = observerLocalPosition.sqrMagnitude > 0.000001f
                ? observerLocalPosition.normalized
                : Vector3.up;
            currentCollisionObserverLocalPosition = currentCollisionObserverAvailable
                ? transform.InverseTransformPoint(collisionState.Position)
                : observerLocalPosition;
            Vector3 collisionObserverLocalDirection =
                currentCollisionObserverLocalPosition.sqrMagnitude > 0.000001f
                    ? currentCollisionObserverLocalPosition.normalized
                    : observerLocalDirection;
            Vector3 relativeCollisionVelocity = currentCollisionObserverAvailable
                ? collisionState.Velocity -
                    body.GetVelocityAtPoint(collisionState.Position)
                : Vector3.zero;
            currentCollisionObserverLocalVelocity =
                transform.InverseTransformVector(relativeCollisionVelocity);
            collisionObserverSpeed = currentCollisionObserverAvailable
                ? relativeCollisionVelocity.magnitude
                : 0f;
            predictedCollisionTravelDistance = currentCollisionObserverAvailable
                ? collisionObserverSpeed * profile.CollisionPredictionSeconds
                : 0f;

            bool canPrepareLocalCollision = CanPrepareLocalCollision(
                currentCollisionObserverAvailable,
                currentCollisionObserverLocalPosition,
                baseRadius,
                outerRadius);
            if (collisionAuthority == CelestialSurfaceCollisionAuthority.LocalAuthoritative)
            {
                bool activeCoverageSafe = canPrepareLocalCollision &&
                    HasPredictedActiveCollisionCoverage(
                        currentCollisionObserverLocalPosition,
                        currentCollisionObserverLocalVelocity) &&
                    AllObserversHaveCollisionCoverage(
                        baseRadius,
                        outerRadius,
                        useDesiredPatches: false);
                if (!activeCoverageSafe &&
                    !ShouldRetainLocalCollisionAuthority(outerRadius))
                {
                    ActivateGlobalCollisionAuthority();
                }
            }

            bool needsRefresh = force ||
                forceRefresh ||
                !surfaceModeActive ||
                ObserverMovedEnough(
                    observerLocalPosition,
                    observerLocalDirection) ||
                CollisionObserverChangedEnough(
                    collisionState.Rigidbody,
                    currentCollisionObserverLocalPosition,
                    collisionObserverLocalDirection,
                    currentCollisionObserverLocalVelocity) ||
                GatheredObserversChangedEnough();
            if (!patchTransitionPending && needsRefresh)
            {
                BeginPatchTransition(
                    observerLocalPosition,
                    currentCollisionObserverLocalPosition,
                    collisionObserverLocalDirection,
                    currentCollisionObserverLocalVelocity,
                    canPrepareLocalCollision,
                    baseRadius);
                lastObserverLocalPosition = observerLocalPosition;
                lastObserverLocalDirection = observerLocalDirection;
                observerStateInitialized = true;
                lastCollisionObserverRigidbody = collisionState.Rigidbody;
                lastCollisionObserverLocalPosition = currentCollisionObserverLocalPosition;
                lastCollisionObserverLocalDirection = collisionObserverLocalDirection;
                lastCollisionObserverLocalVelocity = currentCollisionObserverLocalVelocity;
                collisionObserverStateInitialized = currentCollisionObserverAvailable;
                CaptureGatheredObserverBaseline();
                forceRefresh = false;
            }

            if (patchTransitionPending)
            {
                ProcessPatchTransition();
            }
        }

        bool ObserverMovedEnough(
            Vector3 observerLocalPosition,
            Vector3 observerLocalDirection)
        {
            if (!observerStateInitialized)
            {
                return true;
            }

            float moveThreshold = profile.ObserverMoveThresholdMeters;
            if ((observerLocalPosition - lastObserverLocalPosition).sqrMagnitude >
                moveThreshold * moveThreshold)
            {
                return true;
            }

            return Vector3.Angle(observerLocalDirection, lastObserverLocalDirection) >
                profile.ObserverAngleThresholdDegrees;
        }

        bool CollisionObserverChangedEnough(
            Rigidbody observerRigidbody,
            Vector3 observerLocalPosition,
            Vector3 observerLocalDirection,
            Vector3 observerLocalVelocity)
        {
            if (!currentCollisionObserverAvailable)
            {
                return collisionObserverStateInitialized;
            }

            if (!collisionObserverStateInitialized ||
                observerRigidbody != lastCollisionObserverRigidbody)
            {
                return true;
            }

            float moveThreshold = profile.ObserverMoveThresholdMeters;
            if ((observerLocalPosition - lastCollisionObserverLocalPosition).sqrMagnitude >
                moveThreshold * moveThreshold)
            {
                return true;
            }

            if (Vector3.Angle(
                    observerLocalDirection,
                    lastCollisionObserverLocalDirection) >
                profile.ObserverAngleThresholdDegrees)
            {
                return true;
            }

            Vector3 predictedVelocityDelta =
                (observerLocalVelocity - lastCollisionObserverLocalVelocity) *
                profile.CollisionPredictionSeconds;
            return predictedVelocityDelta.sqrMagnitude > moveThreshold * moveThreshold;
        }

        void BeginPatchTransition(
            Vector3 observerLocalPosition,
            Vector3 collisionObserverLocalPosition,
            Vector3 collisionObserverLocalDirection,
            Vector3 collisionObserverLocalVelocity,
            bool canPrepareLocalCollision,
            float baseRadius)
        {
            CancelPatchTransition();
            transitionCancellation = new CancellationTokenSource();
            RebuildSubdividedBranches();
            splitObserverLocalPositions.Clear();
            for (int i = 0; i < gatheredObservers.Count; i++)
            {
                splitObserverLocalPositions.Add(
                    transform.InverseTransformPoint(gatheredObservers[i].Position));
            }

            RebuildObserverVisibilityCones(observerLocalPosition, baseRadius);
            transitionLeadLocalPosition = observerLocalPosition +
                collisionObserverLocalVelocity * RenderLeadSeconds;

            desiredPatches.Clear();
            desiredPatchMap.Clear();
            desiredKeys.Clear();
            desiredCollisionKeys.Clear();
            pendingPatchBuilds.Clear();
            nextPendingPatchBuild = 0;
            transitionBaseRadius = baseRadius;
            transitionRebuildsAllGeometry = patchGeometryDirty;
            patchGeometryDirty = false;
            transitionMaximumLevel = ResolveTransitionMaximumLevel(
                canPrepareLocalCollision);

            BuildFaceTraversalOrder(observerLocalPosition);
            for (int face = 0; face < faceTraversalOrder.Length; face++)
            {
                CollectDesiredPatch(
                    (CelestialCubeFace)faceTraversalOrder[face],
                    level: 0,
                    x: 0,
                    y: 0,
                    uMin: -1f,
                    vMin: -1f,
                    size: 2f,
                    observerLocalPosition,
                    baseRadius);
            }

            BalanceDesiredPatches(baseRadius);
            ResolveDesiredPatchTopology();

            for (int i = 0; i < desiredPatches.Count; i++)
            {
                PatchDescriptor descriptor = desiredPatches[i];
                bool collisionRequired = ShouldEnableCollision(
                    descriptor,
                    collisionObserverLocalPosition,
                    collisionObserverLocalDirection,
                    baseRadius);
                if (collisionRequired)
                {
                    desiredCollisionKeys.Add(descriptor.Key);
                }
            }

            MarkObserverCollisionKeys(baseRadius);

            transitionLocalCollisionCoverageReady =
                canPrepareLocalCollision &&
                desiredCollisionKeys.Count > 0 &&
                HasPredictedDesiredCollisionCoverage(
                    collisionObserverLocalPosition,
                    collisionObserverLocalVelocity) &&
                AllObserversHaveCollisionCoverage(
                    baseRadius,
                    currentOuterRadius,
                    useDesiredPatches: true);
            if (!transitionLocalCollisionCoverageReady)
            {
                if (collisionAuthority ==
                    CelestialSurfaceCollisionAuthority.LocalAuthoritative)
                {
                    if (ShouldRetainLocalCollisionAuthority(currentOuterRadius))
                    {
                        CancelPatchTransition();
                        forceRefresh = true;
                        return;
                    }

                    ActivateGlobalCollisionAuthority();
                }
            }

            transitionSampler = CelestialSurfaceSampler.Create(
                baseRadius,
                CalculatePatchAngularFootprint(resolvedSubdivisionLevel, profile.PatchResolution),
                bodyVisual.ShapeProfile,
                bodyVisual.SurfaceProfile);

            for (int i = 0; i < desiredPatches.Count; i++)
            {
                PatchDescriptor descriptor = desiredPatches[i];
                bool collisionRequired = desiredCollisionKeys.Contains(descriptor.Key);
                SurfacePatch patch = null;
                bool rebuildGeometry = transitionRebuildsAllGeometry ||
                    !activePatches.TryGetValue(descriptor.Key, out patch) ||
                    !patch.Descriptor.HasSameEdgeTopology(descriptor);
                bool prepareExistingCollision = !rebuildGeometry &&
                    collisionRequired &&
                    !patch.CollisionBaked;
                if (rebuildGeometry || prepareExistingCollision)
                {
                    pendingPatchBuilds.Add(RentOperation(
                        descriptor,
                        rebuildGeometry,
                        collisionRequired));
                }
            }

            pendingPatchBuilds.Sort(ComparePendingPatchBuilds);

            if (!TransitionChangesCommittedSurface())
            {
                skippedUnchangedRefreshCount++;
                CancelPatchTransition();
                return;
            }

            patchTransitionPending = true;
            if (transitionLocalCollisionCoverageReady &&
                collisionAuthority ==
                CelestialSurfaceCollisionAuthority.GlobalFallback)
            {
                collisionAuthority =
                    CelestialSurfaceCollisionAuthority.LocalPreparing;
                localCollisionCoverageReady = false;
                ApplyPrimaryTerrainState();
            }

            pendingPatchBuildCount = pendingPatchBuilds.Count;
            lastTransitionBuildCount = pendingPatchBuildCount;
            if (pendingPatchBuildCount == 0)
            {
                CommitPatchTransition();
            }
        }

        bool TransitionChangesCommittedSurface()
        {
            if (transitionRebuildsAllGeometry ||
                activePatches.Count != desiredKeys.Count)
            {
                return true;
            }

            for (int i = 0; i < desiredPatches.Count; i++)
            {
                PatchKey key = desiredPatches[i].Key;
                if (!activePatches.TryGetValue(key, out SurfacePatch patch) ||
                    !patch.Descriptor.HasSameEdgeTopology(desiredPatches[i]) ||
                    patch.Collider.enabled != desiredCollisionKeys.Contains(key))
                {
                    return true;
                }
            }

            return false;
        }

        void ProcessPatchTransition()
        {
            if (collisionBakeFrame != Time.frameCount)
            {
                collisionBakeFrame = Time.frameCount;
                collisionBakesThisFrame = 0;
            }

            double deadline = Time.realtimeSinceStartupAsDouble +
                profile.PatchBuildBudgetMilliseconds / 1000d;
            patchBuildDeadline = deadline;
            DispatchQueuedPatchBuilds();

            int uploadedThisFrame = 0;
            int remaining = 0;

            for (int i = 0; i < pendingPatchBuilds.Count; i++)
            {
                PatchBuildOperation operation = pendingPatchBuilds[i];
                if (operation.Stage == PatchBuildStage.Complete)
                {
                    continue;
                }

                if (operation.Stage == PatchBuildStage.CollisionPending)
                {
                    TryPreparePatchCollision(operation);
                }

                if (operation.Stage == PatchBuildStage.Sampling && operation.TaskFinished)
                {
                    if (TryReportFailedBuild(operation))
                    {
                        CancelPatchTransition();
                        forceRefresh = true;
                        return;
                    }

                    bool canUpload = uploadedThisFrame < profile.MaximumPatchBuildsPerFrame &&
                        Time.realtimeSinceStartupAsDouble < deadline;
                    if (canUpload)
                    {
                        UploadPatchGeometry(operation);
                        uploadedThisFrame++;
                    }
                }
                if (operation.Stage != PatchBuildStage.Complete)
                {
                    remaining++;
                }
            }

            pendingPatchBuildCount = remaining;
            if (remaining == 0)
            {
                CommitPatchTransition();
            }
        }

        void DispatchQueuedPatchBuilds()
        {
            int inFlight = 0;
            for (int i = 0; i < pendingPatchBuilds.Count; i++)
            {
                if (pendingPatchBuilds[i].Stage == PatchBuildStage.Sampling &&
                    !pendingPatchBuilds[i].TaskFinished)
                {
                    inFlight++;
                }
            }

            for (int i = 0; i < abandonedPatchBuilds.Count; i++)
            {
                if (!abandonedPatchBuilds[i].TaskFinished)
                {
                    inFlight++;
                }
            }

            int concurrency = ResolveBuildConcurrency();
            while (nextPendingPatchBuild < pendingPatchBuilds.Count && inFlight < concurrency)
            {
                PatchBuildOperation operation = pendingPatchBuilds[nextPendingPatchBuild++];
                if (operation.RebuildGeometry)
                {
                    StartPatchSampling(operation);
                    inFlight++;
                    continue;
                }

                if (!activePatches.TryGetValue(operation.Descriptor.Key, out SurfacePatch patch))
                {
                    operation.Stage = PatchBuildStage.Complete;
                    continue;
                }

                operation.Patch = patch;
                TryPreparePatchCollision(operation);
            }
        }

        static int ResolveBuildConcurrency()
        {
            return Mathf.Clamp(SystemInfo.processorCount / 2, 1, 6);
        }

        void StartPatchSampling(PatchBuildOperation operation)
        {
            SurfacePatch patch = RentPatch();
            patch.Renderer.enabled = false;
            SetPatchCollision(patch, enabled: false);
            patch.Descriptor = operation.Descriptor;
            patch.CollisionBaked = false;
            operation.Patch = patch;
            operation.Geometry = RentGeometry();
            operation.Stage = PatchBuildStage.Sampling;

            PatchGeometry geometry = operation.Geometry;
            PatchDescriptor descriptor = operation.Descriptor;
            CelestialSurfaceSampler sampler = transitionSampler;
            int resolution = profile.PatchResolution;
            CancellationToken cancellationToken = transitionCancellation != null
                ? transitionCancellation.Token
                : CancellationToken.None;
            operation.Task = Task.Run(
                () => SamplePatchGeometry(
                    geometry,
                    descriptor,
                    sampler,
                    resolution,
                    cancellationToken),
                cancellationToken);
        }

        void UploadPatchGeometry(PatchBuildOperation operation)
        {
            PatchGeometry geometry = operation.Geometry;
            SurfacePatch patch = operation.Patch;
            Mesh previousMesh = patch.Mesh;
            Mesh mesh = new()
            {
                name = $"Surface Patch {operation.Descriptor.Key}",
                hideFlags = HideFlags.DontSave
            };
            patch.Mesh = mesh;
            mesh.indexFormat = geometry.VertexCount <= 65535
                ? IndexFormat.UInt16
                : IndexFormat.UInt32;
            mesh.SetVertices(geometry.Vertices, 0, geometry.VertexCount);
            mesh.SetNormals(geometry.Normals, 0, geometry.VertexCount);
            mesh.SetUVs(0, geometry.Shading, 0, geometry.VertexCount);
            mesh.SetUVs(1, geometry.MorphOffsets, 0, geometry.VertexCount);
            mesh.SetUVs(2, geometry.MorphNormals, 0, geometry.VertexCount);
            mesh.SetTriangles(geometry.Triangles, 0, geometry.TriangleIndexCount, 0, true);
            mesh.RecalculateBounds();

            patch.Filter.sharedMesh = mesh;
            patch.Collider.enabled = false;
            patch.Collider.sharedMesh = null;
            DestroyRuntimeObject(previousMesh);
            patch.GameObject.name = mesh.name;
            patch.GameObject.layer = FarionLayers.CelestialSurface;
            ApplyPatchMorph(
                patch,
                ResolveMorphRange(transitionBaseRadius, operation.Descriptor.Key.Level));
            stagedPatches[operation.Descriptor.Key] = patch;

            ReturnGeometry(operation.Geometry);
            operation.Geometry = null;
            operation.Task = null;
            TryPreparePatchCollision(operation);
        }

        bool TryReportFailedBuild(PatchBuildOperation operation)
        {
            if (operation.Task == null ||
                (!operation.Task.IsFaulted && !operation.Task.IsCanceled))
            {
                return false;
            }

            if (operation.Task.IsFaulted)
            {
                Debug.LogException(operation.Task.Exception, this);
            }

            operation.Task = null;
            operation.Stage = PatchBuildStage.Complete;
            if (operation.Patch != null)
            {
                ReleasePatch(operation.Patch);
                operation.Patch = null;
            }

            return true;
        }

        bool TryPreparePatchCollision(PatchBuildOperation operation)
        {
            SurfacePatch patch = operation.Patch;
            if (!operation.PrepareCollision || patch.CollisionBaked)
            {
                operation.Stage = PatchBuildStage.Complete;
                return true;
            }

            if (profile.BakeCollisionMeshes)
            {
                if (collisionBakesThisFrame >= MaximumCollisionBakesPerFrame ||
                    Time.realtimeSinceStartupAsDouble >= patchBuildDeadline)
                {
                    operation.Stage = PatchBuildStage.CollisionPending;
                    return false;
                }

                collisionBakesThisFrame++;
                Mesh patchMesh = patch.Mesh;
                if (patchMesh != null && patchMesh.vertexCount > 0)
                {
                    Physics.BakeMesh(patchMesh.GetEntityId(), false);
                }
            }

            patch.CollisionBaked = true;
            operation.Stage = PatchBuildStage.Complete;
            return true;
        }

        void CommitPatchTransition()
        {
            if (!patchTransitionPending)
            {
                return;
            }

            bool commitLocalCollision =
                transitionLocalCollisionCoverageReady &&
                currentCollisionObserverAvailable &&
                CanPrepareLocalCollision(
                    currentCollisionObserverAvailable,
                    currentCollisionObserverLocalPosition,
                    transitionBaseRadius,
                    currentOuterRadius) &&
                HasPredictedDesiredCollisionCoverage(
                    currentCollisionObserverLocalPosition,
                    currentCollisionObserverLocalVelocity) &&
                AllObserversHaveCollisionCoverage(
                    transitionBaseRadius,
                    currentOuterRadius,
                    useDesiredPatches: true);
            staleKeys.Clear();
            foreach (PatchKey key in activePatches.Keys)
            {
                if (transitionRebuildsAllGeometry ||
                    !desiredKeys.Contains(key) ||
                    stagedPatches.ContainsKey(key))
                {
                    staleKeys.Add(key);
                }
            }

            for (int i = 0; i < staleKeys.Count; i++)
            {
                PatchKey key = staleKeys[i];
                ReleasePatch(activePatches[key]);
                activePatches.Remove(key);
            }

            foreach (KeyValuePair<PatchKey, SurfacePatch> pair in stagedPatches)
            {
                activePatches[pair.Key] = pair.Value;
            }

            stagedPatches.Clear();
            deepestActiveLevel = 0;
            for (int i = 0; i < desiredPatches.Count; i++)
            {
                PatchDescriptor descriptor = desiredPatches[i];
                if (!activePatches.TryGetValue(descriptor.Key, out SurfacePatch patch))
                {
                    continue;
                }

                patch.GameObject.SetActive(true);
                ApplyPatchMorph(
                    patch,
                    desiredCollisionKeys.Contains(descriptor.Key)
                        ? Vector4.zero
                        : ResolveMorphRange(
                            transitionBaseRadius,
                            descriptor.Key.Level));
                patch.Renderer.enabled = surfaceRenderActive;
                SetPatchCollision(patch, desiredCollisionKeys.Contains(descriptor.Key));
                deepestActiveLevel = Mathf.Max(deepestActiveLevel, descriptor.Key.Level);
            }

            surfaceModeActive = true;
            if (commitLocalCollision && AreDesiredCollisionPatchesReady())
            {
                ActivateLocalCollisionAuthority();
            }
            else
            {
                ActivateGlobalCollisionAuthority();
            }

            patchTransitionPending = false;
            pendingPatchBuildCount = 0;
            committedTransitionCount++;
            lastTransitionCommitFrame = Time.frameCount;
            transitionRebuildsAllGeometry = false;
            transitionLocalCollisionCoverageReady = false;
            transitionCancellation?.Dispose();
            transitionCancellation = null;
            ReleasePendingOperations(releasePatches: false);
            desiredKeys.Clear();
            desiredCollisionKeys.Clear();
            forceRefresh = deepestActiveLevel < resolvedSubdivisionLevel;
            UpdateRuntimeCounts();
        }

        void CancelPatchTransition()
        {
            transitionCancellation?.Cancel();
            transitionCancellation?.Dispose();
            transitionCancellation = null;
            if (collisionAuthority ==
                CelestialSurfaceCollisionAuthority.LocalPreparing)
            {
                ActivateGlobalCollisionAuthority();
            }

            ReleasePendingOperations(releasePatches: true);
            stagedPatches.Clear();
            desiredPatches.Clear();
            desiredPatchMap.Clear();
            desiredKeys.Clear();
            desiredCollisionKeys.Clear();
            pendingPatchBuildCount = 0;
            patchTransitionPending = false;
            transitionRebuildsAllGeometry = false;
            transitionLocalCollisionCoverageReady = false;
        }

        void ReleasePendingOperations(bool releasePatches)
        {
            for (int i = 0; i < pendingPatchBuilds.Count; i++)
            {
                PatchBuildOperation operation = pendingPatchBuilds[i];
                bool ownsPatch = releasePatches && operation.RebuildGeometry;
                if (!ownsPatch)
                {
                    operation.Patch = null;
                }
                else
                {
                    stagedPatches.Remove(operation.Descriptor.Key);
                }

                if (!operation.TaskFinished)
                {
                    abandonedPatchBuilds.Add(operation);
                    continue;
                }

                if (operation.Patch != null)
                {
                    ReleasePatch(operation.Patch);
                }

                ReturnOperation(operation);
            }

            pendingPatchBuilds.Clear();
            nextPendingPatchBuild = 0;
        }

        void DrainAbandonedOperations()
        {
            for (int i = abandonedPatchBuilds.Count - 1; i >= 0; i--)
            {
                PatchBuildOperation operation = abandonedPatchBuilds[i];
                if (!operation.TaskFinished)
                {
                    continue;
                }

                abandonedPatchBuilds.RemoveAt(i);
                if (operation.Task != null && operation.Task.IsFaulted)
                {
                    Debug.LogException(operation.Task.Exception, this);
                }

                if (operation.Patch != null)
                {
                    ReleasePatch(operation.Patch);
                }

                ReturnOperation(operation);
            }
        }

        PatchBuildOperation RentOperation(
            PatchDescriptor descriptor,
            bool rebuildGeometry,
            bool prepareCollision)
        {
            PatchBuildOperation operation = operationPool.Count > 0
                ? operationPool.Pop()
                : new PatchBuildOperation();
            operation.Reset();
            operation.Descriptor = descriptor;
            operation.RebuildGeometry = rebuildGeometry;
            operation.PrepareCollision = prepareCollision;
            return operation;
        }

        void ReturnOperation(PatchBuildOperation operation)
        {
            if (operation.Geometry != null)
            {
                ReturnGeometry(operation.Geometry);
            }

            operation.Reset();
            operationPool.Push(operation);
        }

        PatchGeometry RentGeometry()
        {
            return geometryPool.Count > 0 ? geometryPool.Pop() : new PatchGeometry();
        }

        void ReturnGeometry(PatchGeometry geometry)
        {
            if (geometry != null)
            {
                geometryPool.Push(geometry);
            }
        }

        void UpdateRuntimeCounts()
        {
            RefreshActiveCollisionKeys();
            activePatchCount = activePatches.Count;
            activeColliderCount = activeCollisionKeys.Count;
        }

        void ActivateGlobalCollisionAuthority()
        {
            collisionAuthority =
                CelestialSurfaceCollisionAuthority.GlobalFallback;
            localCollisionCoverageReady = false;
            ApplyPrimaryTerrainState();
            DisablePatchCollision(activePatches);
            DisablePatchCollision(stagedPatches);
            UpdateRuntimeCounts();
        }

        void ActivateLocalCollisionAuthority()
        {
            collisionAuthority =
                CelestialSurfaceCollisionAuthority.LocalAuthoritative;
            localCollisionCoverageReady = true;
            ApplyPrimaryTerrainState();
        }

        void ApplyPrimaryTerrainState()
        {
            bool patchesAreSurface = surfaceModeActive &&
                activePatches.Count > 0 &&
                surfaceRenderRequested;
            bool patchesOwnCollision = collisionAuthority ==
                    CelestialSurfaceCollisionAuthority.LocalAuthoritative &&
                localCollisionCoverageReady;

            if (surfaceRenderActive != patchesAreSurface)
            {
                surfaceRenderActive = patchesAreSurface;
                foreach (SurfacePatch patch in activePatches.Values)
                {
                    patch.Renderer.enabled = patchesAreSurface;
                }
            }

            if (bodyVisual == null || !bodyVisual.isActiveAndEnabled)
            {
                return;
            }

            bodyVisual.SetPrimaryTerrainActive(
                renderEnabled: !patchesAreSurface,
                collisionEnabled: !patchesOwnCollision);
        }

        static void DisablePatchCollision(
            Dictionary<PatchKey, SurfacePatch> patches)
        {
            foreach (SurfacePatch patch in patches.Values)
            {
                patch.Collider.enabled = false;
            }
        }

        void RebuildObserverVisibilityCones(Vector3 observerLocalPosition, float baseRadius)
        {
            splitObserverUpDirections.Clear();
            splitObserverVisibleHalfAngles.Clear();

            float minimumRadius = baseRadius;
            float maximumRadius = baseRadius;
            if (bodyVisual != null && bodyVisual.HasTerrainRadiusRange)
            {
                minimumRadius = Mathf.Min(baseRadius, bodyVisual.TerrainRadiusMinMax.x);
                maximumRadius = Mathf.Max(baseRadius, bodyVisual.TerrainRadiusMinMax.y);
            }

            AddObserverVisibilityCone(observerLocalPosition, minimumRadius, maximumRadius);
        }

        void AddObserverVisibilityCone(
            Vector3 observerLocalPosition,
            float minimumRadius,
            float maximumRadius)
        {
            float observerRadius = observerLocalPosition.magnitude;
            if (observerRadius <= 0.0001f)
            {
                splitObserverUpDirections.Add(Vector3.up);
                splitObserverVisibleHalfAngles.Add(Mathf.PI);
                return;
            }

            splitObserverUpDirections.Add(observerLocalPosition / observerRadius);
            splitObserverVisibleHalfAngles.Add(
                ResolveVisibleHalfAngle(observerRadius, minimumRadius, maximumRadius));
        }

        static float ResolveVisibleHalfAngle(
            float observerRadius,
            float minimumRadius,
            float maximumRadius)
        {
            if (observerRadius <= minimumRadius)
            {
                return Mathf.PI;
            }

            float horizonAngle = Mathf.Acos(Mathf.Clamp01(minimumRadius / observerRadius));
            float reliefAngle = maximumRadius > minimumRadius
                ? Mathf.Acos(Mathf.Clamp01(minimumRadius / maximumRadius))
                : 0f;
            return horizonAngle + reliefAngle;
        }

        bool IsPatchPotentiallyVisible(Vector3 centerDirection, float size)
        {
            if (splitObserverUpDirections.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < splitObserverUpDirections.Count; i++)
            {
                float angle = Mathf.Acos(Mathf.Clamp(
                    Vector3.Dot(splitObserverUpDirections[i], centerDirection),
                    -1f,
                    1f));
                if (angle <= splitObserverVisibleHalfAngles[i] + size)
                {
                    return true;
                }
            }

            return false;
        }

        void BuildFaceTraversalOrder(Vector3 observerLocalPosition)
        {
            Vector3 observerDirection = observerLocalPosition.sqrMagnitude > 0.000001f
                ? observerLocalPosition.normalized
                : Vector3.forward;
            for (int i = 0; i < faceTraversalOrder.Length; i++)
            {
                faceTraversalOrder[i] = i;
            }

            for (int i = 1; i < faceTraversalOrder.Length; i++)
            {
                int face = faceTraversalOrder[i];
                float score = Vector3.Dot(
                    observerDirection,
                    CelestialCubeProjection.ToDirection(
                        (CelestialCubeFace)face,
                        0f,
                        0f));
                int insert = i;
                while (insert > 0)
                {
                    int previousFace = faceTraversalOrder[insert - 1];
                    float previousScore = Vector3.Dot(
                        observerDirection,
                        CelestialCubeProjection.ToDirection(
                            (CelestialCubeFace)previousFace,
                            0f,
                            0f));
                    if (previousScore >= score)
                    {
                        break;
                    }

                    faceTraversalOrder[insert] = previousFace;
                    insert--;
                }

                faceTraversalOrder[insert] = face;
            }
        }

        int ComparePendingPatchBuilds(PatchBuildOperation left, PatchBuildOperation right)
        {
            int collisionPriority = right.PrepareCollision.CompareTo(
                left.PrepareCollision);
            if (collisionPriority != 0)
            {
                return collisionPriority;
            }

            Vector3 observerPosition = currentCollisionObserverAvailable
                ? currentCollisionObserverLocalPosition
                : lastObserverLocalPosition;
            float leftDistance = (
                left.Descriptor.CenterDirection * transitionBaseRadius -
                observerPosition).sqrMagnitude;
            float rightDistance = (
                right.Descriptor.CenterDirection * transitionBaseRadius -
                observerPosition).sqrMagnitude;
            int distancePriority = leftDistance.CompareTo(rightDistance);
            if (distancePriority != 0)
            {
                return distancePriority;
            }

            int levelPriority = right.Descriptor.Key.Level.CompareTo(
                left.Descriptor.Key.Level);
            if (levelPriority != 0)
            {
                return levelPriority;
            }

            int facePriority = left.Descriptor.Key.Face.CompareTo(
                right.Descriptor.Key.Face);
            if (facePriority != 0)
            {
                return facePriority;
            }

            int yPriority = left.Descriptor.Key.Y.CompareTo(
                right.Descriptor.Key.Y);
            return yPriority != 0
                ? yPriority
                : left.Descriptor.Key.X.CompareTo(right.Descriptor.Key.X);
        }

        int ResolveTransitionMaximumLevel(bool canPrepareLocalCollision)
        {
            int maximumLevel = activePatches.Count == 0
                ? Mathf.Min(3, resolvedSubdivisionLevel)
                : Mathf.Min(resolvedSubdivisionLevel, deepestActiveLevel + 2);
            if (canPrepareLocalCollision)
            {
                maximumLevel = Mathf.Max(maximumLevel, resolvedCollisionLevel);
            }

            return maximumLevel;
        }

        void CollectDesiredPatch(
            CelestialCubeFace face,
            int level,
            int x,
            int y,
            float uMin,
            float vMin,
            float size,
            Vector3 observerLocalPosition,
            float baseRadius)
        {
            PatchKey key = new(face, level, x, y);
            Vector3 centerDirection = CelestialCubeProjection.ToDirection(face, uMin + size * 0.5f, vMin + size * 0.5f);
            float patchWorldSize = baseRadius * size;
            Vector3 patchCenter = centerDirection * baseRadius;
            float cameraDistance = Mathf.Min(
                Vector3.Distance(observerLocalPosition, patchCenter),
                Vector3.Distance(transitionLeadLocalPosition, patchCenter));
            float collisionObserverDistance = float.PositiveInfinity;
            for (int i = 0; i < splitObserverLocalPositions.Count; i++)
            {
                collisionObserverDistance = Mathf.Min(
                    collisionObserverDistance,
                    Vector3.Distance(splitObserverLocalPositions[i], patchCenter));
            }
            int balanceReserve = Mathf.Max(
                PatchBudgetReserve,
                profile.MaxActivePatches / 4);
            int selectionLimit = Mathf.Max(
                6,
                profile.MaxActivePatches - balanceReserve);
            bool hasPatchBudget = desiredPatches.Count + 3 <= selectionLimit;
            bool hasHardPatchBudget = desiredPatches.Count + 3 <= profile.MaxActivePatches;
            float splitDistance = patchWorldSize * profile.SplitDistanceMultiplier;
            if (WasBranchSubdivided(key))
            {
                splitDistance *= 1f + profile.SplitHysteresisRatio;
            }

            bool shouldSplitForRendering = surfaceRenderRequested &&
                level < transitionMaximumLevel &&
                cameraDistance < splitDistance &&
                IsPatchPotentiallyVisible(centerDirection, size);
            bool shouldSplitForCollision =
                level < transitionMaximumLevel &&
                collisionObserverDistance < splitDistance;
            bool shouldSplit = hasHardPatchBudget &&
                (shouldSplitForCollision || (hasPatchBudget && shouldSplitForRendering));

            if (!shouldSplit)
            {
                desiredPatches.Add(new PatchDescriptor(
                    key,
                    uMin,
                    vMin,
                    size,
                    centerDirection,
                    patchWorldSize));
                return;
            }

            float childSize = size * 0.5f;
            int childLevel = level + 1;
            int childX = x * 2;
            int childY = y * 2;
            CollectChildrenNearestFirst(
                face,
                childLevel,
                childX,
                childY,
                uMin,
                vMin,
                childSize,
                observerLocalPosition,
                baseRadius);
        }

        void CollectChildrenNearestFirst(
            CelestialCubeFace face,
            int childLevel,
            int childX,
            int childY,
            float uMin,
            float vMin,
            float childSize,
            Vector3 observerLocalPosition,
            float baseRadius)
        {
            Span<int> order = stackalloc int[4] { 0, 1, 2, 3 };
            Span<float> distances = stackalloc float[4];
            for (int i = 0; i < 4; i++)
            {
                float childUMin = uMin + (i & 1) * childSize;
                float childVMin = vMin + (i >> 1) * childSize;
                Vector3 childCenter = CelestialCubeProjection.ToDirection(
                    face,
                    childUMin + childSize * 0.5f,
                    childVMin + childSize * 0.5f) * baseRadius;
                distances[i] = ResolveNearestObserverDistance(
                    observerLocalPosition,
                    childCenter);
            }

            for (int i = 1; i < 4; i++)
            {
                int candidate = order[i];
                float candidateDistance = distances[candidate];
                int insert = i;
                while (insert > 0 && distances[order[insert - 1]] > candidateDistance)
                {
                    order[insert] = order[insert - 1];
                    insert--;
                }

                order[insert] = candidate;
            }

            for (int i = 0; i < 4; i++)
            {
                int child = order[i];
                CollectDesiredPatch(
                    face,
                    childLevel,
                    childX + (child & 1),
                    childY + (child >> 1),
                    uMin + (child & 1) * childSize,
                    vMin + (child >> 1) * childSize,
                    childSize,
                    observerLocalPosition,
                    baseRadius);
            }
        }

        float ResolveNearestObserverDistance(Vector3 observerLocalPosition, Vector3 patchCenter)
        {
            float nearest = Mathf.Min(
                Vector3.Distance(observerLocalPosition, patchCenter),
                Vector3.Distance(transitionLeadLocalPosition, patchCenter));
            for (int i = 0; i < splitObserverLocalPositions.Count; i++)
            {
                nearest = Mathf.Min(
                    nearest,
                    Vector3.Distance(splitObserverLocalPositions[i], patchCenter));
            }

            return nearest;
        }

        void BalanceDesiredPatches(float baseRadius)
        {
            desiredPatchMap.Clear();
            for (int i = 0; i < desiredPatches.Count; i++)
            {
                PatchDescriptor descriptor = desiredPatches[i];
                desiredPatchMap[descriptor.Key] = descriptor;
            }

            balanceQueue.Clear();
            balanceQueue.AddRange(desiredPatches);
            for (int cursor = 0; cursor < balanceQueue.Count; cursor++)
            {
                PatchDescriptor descriptor = balanceQueue[cursor];
                if (!desiredPatchMap.ContainsKey(descriptor.Key))
                {
                    continue;
                }

                for (int edge = 0; edge < 4; edge++)
                {
                    if (!TryFindDesiredPatchAcrossEdge(
                            descriptor,
                            (PatchEdge)edge,
                            out PatchDescriptor neighbour) ||
                        descriptor.Key.Level - neighbour.Key.Level <= 1)
                    {
                        continue;
                    }

                    SplitDesiredPatch(neighbour, baseRadius);
                    balanceQueue.Add(descriptor);
                    break;
                }
            }

            balanceQueue.Clear();
            desiredPatches.Clear();
            desiredPatches.AddRange(desiredPatchMap.Values);
        }

        void ResolveDesiredPatchTopology()
        {
            desiredKeys.Clear();
            for (int i = 0; i < desiredPatches.Count; i++)
            {
                desiredKeys.Add(desiredPatches[i].Key);
            }

            for (int i = 0; i < desiredPatches.Count; i++)
            {
                PatchDescriptor descriptor = desiredPatches[i];
                int coarserEdgeMask = 0;
                for (int edge = 0; edge < 4; edge++)
                {
                    if (!TryFindDesiredPatchAcrossEdge(
                            descriptor,
                            (PatchEdge)edge,
                            out PatchDescriptor neighbour))
                    {
                        continue;
                    }

                    if (neighbour.Key.Level < descriptor.Key.Level)
                    {
                        coarserEdgeMask |= 1 << edge;
                    }
                }

                PatchDescriptor resolved = descriptor.WithEdgeTopology(coarserEdgeMask);
                desiredPatches[i] = resolved;
                desiredPatchMap[resolved.Key] = resolved;
            }
        }

        void SplitDesiredPatch(PatchDescriptor descriptor, float baseRadius)
        {
            desiredPatchMap.Remove(descriptor.Key);
            int childLevel = descriptor.Key.Level + 1;
            int childX = descriptor.Key.X * 2;
            int childY = descriptor.Key.Y * 2;
            AddDesiredPatch(CreatePatchDescriptor(
                descriptor.Key.Face,
                childLevel,
                childX,
                childY,
                baseRadius));
            AddDesiredPatch(CreatePatchDescriptor(
                descriptor.Key.Face,
                childLevel,
                childX + 1,
                childY,
                baseRadius));
            AddDesiredPatch(CreatePatchDescriptor(
                descriptor.Key.Face,
                childLevel,
                childX,
                childY + 1,
                baseRadius));
            AddDesiredPatch(CreatePatchDescriptor(
                descriptor.Key.Face,
                childLevel,
                childX + 1,
                childY + 1,
                baseRadius));
        }

        void AddDesiredPatch(PatchDescriptor descriptor)
        {
            desiredPatchMap[descriptor.Key] = descriptor;
            balanceQueue.Add(descriptor);
        }

        static PatchDescriptor CreatePatchDescriptor(
            CelestialCubeFace face,
            int level,
            int x,
            int y,
            float baseRadius)
        {
            float size = 2f / (1 << level);
            float uMin = -1f + x * size;
            float vMin = -1f + y * size;
            Vector3 centerDirection = CelestialCubeProjection.ToDirection(
                face,
                uMin + size * 0.5f,
                vMin + size * 0.5f);
            return new PatchDescriptor(
                new PatchKey(face, level, x, y),
                uMin,
                vMin,
                size,
                centerDirection,
                baseRadius * size);
        }

        bool TryFindDesiredPatchAcrossEdge(
            PatchDescriptor descriptor,
            PatchEdge edge,
            out PatchDescriptor neighbour)
        {
            Vector3 direction = DirectionAcrossEdge(descriptor, edge);
            CelestialCubeProjection.Project(
                direction,
                out CelestialCubeFace face,
                out float u,
                out float v);
            for (int level = transitionMaximumLevel; level >= 0; level--)
            {
                int patchCount = 1 << level;
                int x = Mathf.Clamp(
                    Mathf.FloorToInt((u + 1f) * 0.5f * patchCount),
                    0,
                    patchCount - 1);
                int y = Mathf.Clamp(
                    Mathf.FloorToInt((v + 1f) * 0.5f * patchCount),
                    0,
                    patchCount - 1);
                if (desiredPatchMap.TryGetValue(
                        new PatchKey(face, level, x, y),
                        out neighbour))
                {
                    return true;
                }
            }

            neighbour = default;
            return false;
        }

        static Vector3 DirectionAcrossEdge(
            PatchDescriptor descriptor,
            PatchEdge edge)
        {
            float offset = Mathf.Max(0.000001f, descriptor.Size * 0.01f);
            float u = descriptor.UMin + descriptor.Size * 0.5f;
            float v = descriptor.VMin + descriptor.Size * 0.5f;
            switch (edge)
            {
                case PatchEdge.Bottom:
                    v = descriptor.VMin - offset;
                    break;
                case PatchEdge.Right:
                    u = descriptor.UMin + descriptor.Size + offset;
                    break;
                case PatchEdge.Top:
                    v = descriptor.VMin + descriptor.Size + offset;
                    break;
                default:
                    u = descriptor.UMin - offset;
                    break;
            }

            return CelestialCubeProjection.ToDirection(descriptor.Key.Face, u, v);
        }

        void RebuildSubdividedBranches()
        {
            subdividedBranches.Clear();
            foreach (PatchKey activeKey in activePatches.Keys)
            {
                int x = activeKey.X;
                int y = activeKey.Y;
                for (int level = activeKey.Level - 1; level >= 0; level--)
                {
                    x >>= 1;
                    y >>= 1;
                    subdividedBranches.Add(new PatchKey(activeKey.Face, level, x, y));
                }
            }
        }

        bool WasBranchSubdivided(PatchKey branch)
        {
            return subdividedBranches.Contains(branch);
        }

        bool ShouldEnableCollision(
            PatchDescriptor descriptor,
            Vector3 observerLocalPosition,
            Vector3 observerLocalDirection,
            float baseRadius)
        {
            CelestialBody body = bodyVisual.Body;
            if (body == null ||
                !body.SupportsNonConvexSurfaceCollider ||
                descriptor.Key.Level < resolvedCollisionLevel)
            {
                return false;
            }

            float surfaceDistance = Vector3.Angle(
                observerLocalDirection,
                descriptor.CenterDirection) * Mathf.Deg2Rad * baseRadius;
            float patchMargin = descriptor.PatchWorldSize * 0.8f;
            float observerAltitude = Mathf.Max(0f, observerLocalPosition.magnitude - baseRadius);
            return surfaceDistance <= profile.CollisionRadiusMeters + patchMargin &&
                observerAltitude <= resolvedExitAltitude;
        }

        bool CanPrepareLocalCollision(
            bool hasCollisionObserver,
            Vector3 observerLocalPosition,
            float baseRadius,
            float outerRadius)
        {
            if (!hasCollisionObserver || profile.CollisionRadiusMeters <= 0f)
            {
                return false;
            }

            float observerAltitude = Mathf.Max(
                0f,
                observerLocalPosition.magnitude - outerRadius);
            float preparationAltitude =
                CalculateLocalCollisionPreparationAltitude(baseRadius);
            if (observerAltitude > preparationAltitude)
            {
                return false;
            }

            return true;
        }

        bool ShouldRetainLocalCollisionAuthority(float outerRadius)
        {
            if (collisionAuthority !=
                CelestialSurfaceCollisionAuthority.LocalAuthoritative)
            {
                return false;
            }

            float retainAltitude = Mathf.Max(
                profile.CollisionSafetyMarginMeters * 2f,
                profile.CollisionTriangleEdgeMeters);
            bool retained = false;
            for (int i = 0; i < gatheredObservers.Count; i++)
            {
                Vector3 localPosition = transform.InverseTransformPoint(
                    gatheredObservers[i].Position);
                if (localPosition.magnitude - outerRadius > retainAltitude ||
                    localPosition.sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                if (!IsCollisionDirectionCovered(
                        localPosition.normalized,
                        useDesiredPatches: false))
                {
                    return false;
                }

                retained = true;
            }

            return retained;
        }

        void ResolveObserverLocalState(
            in CelestialSurfaceCollisionObserverState state,
            out Vector3 localPosition,
            out Vector3 localDirection,
            out Vector3 localVelocity)
        {
            CelestialBody body = bodyVisual != null ? bodyVisual.Body : null;
            localPosition = transform.InverseTransformPoint(state.Position);
            localDirection = localPosition.sqrMagnitude > 0.000001f
                ? localPosition.normalized
                : Vector3.up;
            Vector3 relativeVelocity = body != null
                ? state.Velocity - body.GetVelocityAtPoint(state.Position)
                : state.Velocity;
            localVelocity = transform.InverseTransformVector(relativeVelocity);
        }

        bool AllObserversHaveCollisionCoverage(
            float baseRadius,
            float outerRadius,
            bool useDesiredPatches)
        {
            for (int i = 0; i < gatheredObservers.Count; i++)
            {
                ResolveObserverLocalState(
                    gatheredObservers[i],
                    out Vector3 localPosition,
                    out _,
                    out Vector3 localVelocity);
                if (!CanPrepareLocalCollision(
                        true,
                        localPosition,
                        baseRadius,
                        outerRadius))
                {
                    continue;
                }

                if (!HasPredictedCollisionCoverage(
                        localPosition,
                        localVelocity,
                        useDesiredPatches))
                {
                    return false;
                }
            }

            return true;
        }

        void MarkObserverCollisionKeys(float baseRadius)
        {
            for (int observerIndex = 0; observerIndex < gatheredObservers.Count; observerIndex++)
            {
                ResolveObserverLocalState(
                    gatheredObservers[observerIndex],
                    out Vector3 localPosition,
                    out _,
                    out Vector3 localVelocity);
                for (int sample = 0; sample < CollisionPredictionSamples; sample++)
                {
                    float progress = sample / (CollisionPredictionSamples - 1f);
                    Vector3 predictedPosition = localPosition +
                        localVelocity * (profile.CollisionPredictionSeconds * progress);
                    if (predictedPosition.sqrMagnitude <= 0.000001f)
                    {
                        continue;
                    }

                    Vector3 predictedDirection = predictedPosition.normalized;
                    for (int i = 0; i < desiredPatches.Count; i++)
                    {
                        PatchDescriptor descriptor = desiredPatches[i];
                        if (!desiredCollisionKeys.Contains(descriptor.Key) &&
                            ShouldEnableCollision(
                                descriptor,
                                predictedPosition,
                                predictedDirection,
                                baseRadius))
                        {
                            desiredCollisionKeys.Add(descriptor.Key);
                        }
                    }
                }
            }
        }

        void CaptureGatheredObserverBaseline()
        {
            lastGatheredObserverRigidbodies.Clear();
            lastGatheredObserverLocalPositions.Clear();
            for (int i = 0; i < gatheredObservers.Count; i++)
            {
                lastGatheredObserverRigidbodies.Add(gatheredObservers[i].Rigidbody);
                lastGatheredObserverLocalPositions.Add(
                    transform.InverseTransformPoint(gatheredObservers[i].Position));
            }
        }

        bool GatheredObserversChangedEnough()
        {
            if (gatheredObservers.Count != lastGatheredObserverRigidbodies.Count)
            {
                return true;
            }

            float moveThreshold = profile.ObserverMoveThresholdMeters;
            float moveThresholdSqr = moveThreshold * moveThreshold;
            for (int i = 0; i < gatheredObservers.Count; i++)
            {
                if (gatheredObservers[i].Rigidbody != lastGatheredObserverRigidbodies[i])
                {
                    return true;
                }

                Vector3 localPosition =
                    transform.InverseTransformPoint(gatheredObservers[i].Position);
                if ((localPosition - lastGatheredObserverLocalPositions[i]).sqrMagnitude >
                    moveThresholdSqr)
                {
                    return true;
                }
            }

            return false;
        }

        float CalculateLocalCollisionPreparationAltitude(float baseRadius)
        {
            int minimumLevel = resolvedCollisionLevel;
            if (minimumLevel <= 0)
            {
                return resolvedExitAltitude;
            }

            float parentSize = 2f / (1 << (minimumLevel - 1));
            float hysteresis = collisionAuthority ==
                CelestialSurfaceCollisionAuthority.LocalAuthoritative
                    ? 1f + profile.SplitHysteresisRatio
                    : 1f;
            return baseRadius *
                parentSize *
                profile.SplitDistanceMultiplier *
                hysteresis;
        }

        bool HasPredictedDesiredCollisionCoverage(
            Vector3 observerLocalPosition,
            Vector3 observerLocalVelocity)
        {
            return HasPredictedCollisionCoverage(
                observerLocalPosition,
                observerLocalVelocity,
                useDesiredPatches: true);
        }

        bool HasPredictedActiveCollisionCoverage(
            Vector3 observerLocalPosition,
            Vector3 observerLocalVelocity)
        {
            if (activeColliderCount <= 0)
            {
                return false;
            }

            return HasPredictedCollisionCoverage(
                observerLocalPosition,
                observerLocalVelocity,
                useDesiredPatches: false);
        }

        bool HasPredictedCollisionCoverage(
            Vector3 observerLocalPosition,
            Vector3 observerLocalVelocity,
            bool useDesiredPatches)
        {
            float predictionSeconds = profile.CollisionPredictionSeconds;
            float safetyMarginAngle = resolvedCollisionSafetyMarginAngle;
            for (int sample = 0; sample < CollisionPredictionSamples; sample++)
            {
                float progress = sample / (CollisionPredictionSamples - 1f);
                Vector3 predictedPosition =
                    observerLocalPosition +
                    observerLocalVelocity * (predictionSeconds * progress);
                if (predictedPosition.sqrMagnitude <= 0.000001f)
                {
                    return false;
                }

                Vector3 direction = predictedPosition.normalized;
                if (!IsCollisionDirectionCovered(direction, useDesiredPatches))
                {
                    return false;
                }

                if (safetyMarginAngle <= 0f)
                {
                    continue;
                }

                Vector3 referenceAxis = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) < 0.9f
                    ? Vector3.up
                    : Vector3.right;
                Vector3 tangent = Vector3.Cross(referenceAxis, direction).normalized;
                Vector3 bitangent = Vector3.Cross(direction, tangent).normalized;
                if (!IsCollisionDirectionCovered(
                        (direction + tangent * safetyMarginAngle).normalized,
                        useDesiredPatches) ||
                    !IsCollisionDirectionCovered(
                        (direction - tangent * safetyMarginAngle).normalized,
                        useDesiredPatches) ||
                    !IsCollisionDirectionCovered(
                        (direction + bitangent * safetyMarginAngle).normalized,
                        useDesiredPatches) ||
                    !IsCollisionDirectionCovered(
                        (direction - bitangent * safetyMarginAngle).normalized,
                        useDesiredPatches))
                {
                    return false;
                }
            }

            return true;
        }

        bool IsCollisionDirectionCovered(Vector3 direction, bool useDesiredPatches)
        {
            CelestialCubeProjection.Project(
                direction,
                out CelestialCubeFace face,
                out float u,
                out float v);
            HashSet<PatchKey> collisionKeys = useDesiredPatches
                ? desiredCollisionKeys
                : activeCollisionKeys;
            int maximumLevel = Mathf.Max(transitionMaximumLevel, resolvedSubdivisionLevel);
            for (int level = 0; level <= maximumLevel; level++)
            {
                if (collisionKeys.Contains(ResolvePatchKey(face, level, u, v)))
                {
                    return true;
                }
            }

            return false;
        }

        static PatchKey ResolvePatchKey(CelestialCubeFace face, int level, float u, float v)
        {
            int patchCount = 1 << level;
            int x = Mathf.Clamp(
                Mathf.FloorToInt((u + 1f) * 0.5f * patchCount),
                0,
                patchCount - 1);
            int y = Mathf.Clamp(
                Mathf.FloorToInt((v + 1f) * 0.5f * patchCount),
                0,
                patchCount - 1);
            return new PatchKey(face, level, x, y);
        }

        void RefreshActiveCollisionKeys()
        {
            activeCollisionKeys.Clear();
            foreach (KeyValuePair<PatchKey, SurfacePatch> pair in activePatches)
            {
                if (pair.Value.Collider.enabled)
                {
                    activeCollisionKeys.Add(pair.Key);
                }
            }

            foreach (KeyValuePair<PatchKey, SurfacePatch> pair in stagedPatches)
            {
                if (pair.Value.Collider.enabled)
                {
                    activeCollisionKeys.Add(pair.Key);
                }
            }
        }

        bool AreDesiredCollisionPatchesReady()
        {
            if (desiredCollisionKeys.Count == 0)
            {
                return false;
            }

            foreach (PatchKey key in desiredCollisionKeys)
            {
                if (!activePatches.TryGetValue(key, out SurfacePatch patch) ||
                    !patch.Collider.enabled ||
                    patch.Collider.sharedMesh != patch.Mesh)
                {
                    return false;
                }
            }

            return true;
        }

        static void SamplePatchGeometry(
            PatchGeometry geometry,
            PatchDescriptor descriptor,
            CelestialSurfaceSampler sampler,
            int resolution,
            CancellationToken cancellationToken)
        {
            int rowSize = resolution + 1;
            int extendedRowSize = resolution + 3;
            int vertexCount = rowSize * rowSize;
            int triangleIndexCount = resolution * resolution * 6;
            geometry.EnsureCapacity(
                vertexCount,
                triangleIndexCount,
                extendedRowSize * extendedRowSize);

            Vector3[] extendedPositions = geometry.ExtendedPositions;
            Vector3[] vertices = geometry.Vertices;
            Vector3[] normals = geometry.Normals;
            Vector4[] shading = geometry.Shading;
            Vector4[] morphOffsets = geometry.MorphOffsets;
            Vector4[] morphNormals = geometry.MorphNormals;
            int[] triangles = geometry.Triangles;

            for (int extendedY = 0; extendedY < extendedRowSize; extendedY++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int y = extendedY - 1;
                float v = descriptor.VMin + descriptor.Size * (y / (float)resolution);
                for (int extendedX = 0; extendedX < extendedRowSize; extendedX++)
                {
                    int x = extendedX - 1;
                    float u = descriptor.UMin + descriptor.Size * (x / (float)resolution);
                    Vector3 direction = CelestialCubeProjection.ToDirection(
                        descriptor.Key.Face,
                        u,
                        v);
                    bool isSurfaceVertex = x >= 0 && x <= resolution && y >= 0 && y <= resolution;
                    float vertexRadius;
                    if (isSurfaceVertex)
                    {
                        CelestialShapeSample sample = sampler.EvaluateSample(direction);
                        shading[y * rowSize + x] = sample.ShadingData;
                        vertexRadius = sample.Radius;
                    }
                    else
                    {
                        vertexRadius = sampler.EvaluateRadius(direction);
                    }

                    extendedPositions[extendedY * extendedRowSize + extendedX] =
                        direction * vertexRadius;
                }
            }

            SnapEdgesToCoarserNeighbours(
                descriptor.CoarserEdgeMask,
                resolution,
                extendedRowSize,
                extendedPositions);

            for (int y = 0; y <= resolution; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int extendedY = y + 1;
                for (int x = 0; x <= resolution; x++)
                {
                    int extendedX = x + 1;
                    int extendedIndex = extendedY * extendedRowSize + extendedX;
                    Vector3 position = extendedPositions[extendedIndex];
                    Vector3 tangentX =
                        extendedPositions[extendedIndex + 1] -
                        extendedPositions[extendedIndex - 1];
                    Vector3 tangentY =
                        extendedPositions[extendedIndex + extendedRowSize] -
                        extendedPositions[extendedIndex - extendedRowSize];
                    Vector3 normal = Vector3.Cross(tangentY, tangentX);
                    if (normal.sqrMagnitude <= 0.000001f)
                    {
                        normal = position.normalized;
                    }
                    else
                    {
                        normal.Normalize();
                        if (Vector3.Dot(normal, position) < 0f)
                        {
                            normal = -normal;
                        }
                    }

                    int index = y * rowSize + x;
                    vertices[index] = position;
                    normals[index] = normal;
                }
            }

            BuildMorphData(
                vertices,
                normals,
                morphOffsets,
                morphNormals,
                resolution,
                rowSize);

            int triangleIndex = 0;
            for (int y = 0; y < resolution; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x < resolution; x++)
                {
                    int a = y * rowSize + x;
                    int b = a + 1;
                    int c = a + rowSize;
                    int d = c + 1;
                    triangleIndex = AddOutwardTriangle(triangles, vertices, triangleIndex, a, c, b);
                    triangleIndex = AddOutwardTriangle(triangles, vertices, triangleIndex, b, c, d);
                }
            }

            geometry.VertexCount = vertexCount;
            geometry.TriangleIndexCount = triangleIndex;
        }

        static void BuildMorphData(
            Vector3[] vertices,
            Vector3[] normals,
            Vector4[] morphOffsets,
            Vector4[] morphNormals,
            int resolution,
            int rowSize)
        {
            for (int y = 0; y <= resolution; y++)
            {
                int yLow = y & ~1;
                int yHigh = (y & 1) != 0 ? yLow + 2 : yLow;
                for (int x = 0; x <= resolution; x++)
                {
                    int xLow = x & ~1;
                    int xHigh = (x & 1) != 0 ? xLow + 2 : xLow;
                    Vector3 coarse = (x & 1) != 0 && (y & 1) != 0
                        ? (vertices[yLow * rowSize + xHigh] +
                            vertices[yHigh * rowSize + xLow]) * 0.5f
                        : (vertices[yLow * rowSize + xLow] +
                            vertices[yLow * rowSize + xHigh] +
                            vertices[yHigh * rowSize + xLow] +
                            vertices[yHigh * rowSize + xHigh]) * 0.25f;
                    int index = y * rowSize + x;
                    Vector3 offset = coarse - vertices[index];
                    morphOffsets[index] = new Vector4(offset.x, offset.y, offset.z, 0f);
                    Vector3 coarseNormal = (x & 1) != 0 && (y & 1) != 0
                        ? (normals[yLow * rowSize + xHigh] +
                            normals[yHigh * rowSize + xLow]).normalized
                        : (normals[yLow * rowSize + xLow] +
                            normals[yLow * rowSize + xHigh] +
                            normals[yHigh * rowSize + xLow] +
                            normals[yHigh * rowSize + xHigh]).normalized;
                    morphNormals[index] = new Vector4(
                        coarseNormal.x,
                        coarseNormal.y,
                        coarseNormal.z,
                        0f);
                }
            }
        }

        static int AddOutwardTriangle(
            int[] triangles,
            Vector3[] vertices,
            int triangleIndex,
            int a,
            int b,
            int c)
        {
            Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            Vector3 center = vertices[a] + vertices[b] + vertices[c];
            if (Vector3.Dot(normal, center) < 0f)
            {
                (b, c) = (c, b);
            }

            triangles[triangleIndex] = a;
            triangles[triangleIndex + 1] = b;
            triangles[triangleIndex + 2] = c;
            return triangleIndex + 3;
        }

        static void SnapEdgesToCoarserNeighbours(
            int coarserEdgeMask,
            int resolution,
            int rowSize,
            Vector3[] positions)
        {
            for (int edge = 0; edge < 4; edge++)
            {
                if ((coarserEdgeMask & 1 << edge) == 0)
                {
                    continue;
                }

                PatchEdge patchEdge = (PatchEdge)edge;
                for (int i = 1; i < resolution; i += 2)
                {
                    int previous = GetExtendedEdgeIndex(
                        patchEdge,
                        i - 1,
                        resolution,
                        rowSize);
                    int current = GetExtendedEdgeIndex(
                        patchEdge,
                        i,
                        resolution,
                        rowSize);
                    int next = GetExtendedEdgeIndex(
                        patchEdge,
                        i + 1,
                        resolution,
                        rowSize);
                    positions[current] = (positions[previous] + positions[next]) * 0.5f;
                }
            }
        }

        static int GetExtendedEdgeIndex(
            PatchEdge edge,
            int index,
            int resolution,
            int rowSize)
        {
            return edge switch
            {
                PatchEdge.Bottom => rowSize + index + 1,
                PatchEdge.Right => (index + 1) * rowSize + resolution + 1,
                PatchEdge.Top => (resolution + 1) * rowSize + resolution - index + 1,
                _ => (resolution - index + 1) * rowSize + 1
            };
        }

        void SetPatchCollision(SurfacePatch patch, bool enabled)
        {
            if (!enabled)
            {
                patch.Collider.enabled = false;
                return;
            }

            if (patch.Collider.sharedMesh != patch.Mesh)
            {
                patch.Collider.sharedMesh = patch.Mesh;
            }

            patch.Collider.enabled = patch.Collider.sharedMesh != null;
        }

        SurfacePatch RentPatch()
        {
            SurfacePatch patch;
            if (patchPool.Count > 0)
            {
                patch = patchPool.Pop();
            }
            else
            {
                EnsurePatchContainer();
                GameObject patchObject = new("Surface Patch")
                {
                    hideFlags = HideFlags.DontSave
                };
                patchObject.transform.SetParent(patchContainer, false);
                MeshFilter filter = patchObject.AddComponent<MeshFilter>();
                MeshRenderer renderer = patchObject.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                MeshCollider collider = patchObject.AddComponent<MeshCollider>();
                collider.convex = false;
                collider.enabled = false;
                patch = new SurfacePatch(patchObject, filter, renderer, collider, null);
            }

            patch.GameObject.SetActive(true);
            return patch;
        }

        void ReleasePatch(SurfacePatch patch)
        {
            if (patch == null)
            {
                return;
            }

            patch.Collider.enabled = false;
            patch.Renderer.enabled = false;
            patch.GameObject.SetActive(false);
            patchPool.Push(patch);
        }

        void DeactivateSurfaceMode()
        {
            if (!surfaceModeActive &&
                !patchTransitionPending &&
                activePatches.Count == 0)
            {
                surfaceRenderRequested = false;
                if (collisionAuthority !=
                    CelestialSurfaceCollisionAuthority.GlobalFallback)
                {
                    collisionAuthority =
                        CelestialSurfaceCollisionAuthority.GlobalFallback;
                    localCollisionCoverageReady = false;
                }

                ApplyPrimaryTerrainState();
                return;
            }

            CancelPatchTransition();
            if (bodyVisual != null && bodyVisual.isActiveAndEnabled)
            {
                bodyVisual.SetPrimaryTerrainActive(renderEnabled: true, collisionEnabled: true);
            }

            ReleaseAllActivePatches();
            surfaceModeActive = false;
            surfaceRenderActive = false;
            surfaceRenderRequested = false;
            collisionAuthority =
                CelestialSurfaceCollisionAuthority.GlobalFallback;
            localCollisionCoverageReady = false;
            collisionObserverSpeed = 0f;
            predictedCollisionTravelDistance = 0f;
            activePatchCount = 0;
            activeColliderCount = 0;
            deepestActiveLevel = 0;
            observerStateInitialized = false;
            collisionObserverStateInitialized = false;
            lastCollisionObserverRigidbody = null;
            currentCollisionObserverAvailable = false;
            forceRefresh = true;
        }

        void ReleaseAllActivePatches()
        {
            staleKeys.Clear();
            foreach (PatchKey key in activePatches.Keys)
            {
                staleKeys.Add(key);
            }

            for (int i = 0; i < staleKeys.Count; i++)
            {
                PatchKey key = staleKeys[i];
                ReleasePatch(activePatches[key]);
                activePatches.Remove(key);
            }
        }

        void DestroyAllPatches()
        {
            CancelPatchTransition();
            WaitForPendingBuilds();
            foreach (SurfacePatch patch in activePatches.Values)
            {
                DestroyPatch(patch);
            }

            activePatches.Clear();
            while (patchPool.Count > 0)
            {
                DestroyPatch(patchPool.Pop());
            }

            if (patchContainer != null)
            {
                DestroyRuntimeObject(patchContainer.gameObject);
                patchContainer = null;
            }
        }

        static void DestroyPatch(SurfacePatch patch)
        {
            if (patch == null)
            {
                return;
            }

            patch.Collider.sharedMesh = null;
            patch.Filter.sharedMesh = null;
            DestroyRuntimeObject(patch.Mesh);
            DestroyRuntimeObject(patch.GameObject);
        }

        static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        void EnsurePatchContainer()
        {
            if (patchContainer != null)
            {
                return;
            }

            Transform existing = transform.Find(PatchContainerName);
            if (existing != null)
            {
                patchContainer = existing;
                return;
            }

            GameObject containerObject = new(PatchContainerName)
            {
                hideFlags = HideFlags.DontSave
            };
            patchContainer = containerObject.transform;
            patchContainer.SetParent(transform, false);
        }

        void ResolveSources()
        {
            if (bodyVisual == null)
            {
                bodyVisual = GetComponent<CelestialBodyVisual>();
            }

            if (collisionObserverSource == null)
            {
                collisionObserver = null;
                collisionObserverGroup = null;
            }
            else if ((collisionObserver == null && collisionObserverGroup == null) ||
                (!ReferenceEquals(collisionObserver, collisionObserverSource) &&
                 !ReferenceEquals(collisionObserverGroup, collisionObserverSource)))
            {
                collisionObserver =
                    collisionObserverSource as ICelestialSurfaceCollisionObserver;
                collisionObserverGroup =
                    collisionObserverSource as ICelestialSurfaceCollisionObserverGroup;
            }
        }

        void GatherCollisionObservers()
        {
            ResolveSources();
            gatheredObservers.Clear();
            if (collisionObserverGroup != null)
            {
                collisionObserverGroup.GetSurfaceCollisionObservers(gatheredObservers);
                for (int i = gatheredObservers.Count - 1; i >= 0; i--)
                {
                    if (!gatheredObservers[i].IsValid)
                    {
                        gatheredObservers.RemoveAt(i);
                    }
                }

                return;
            }

            if (collisionObserver != null &&
                collisionObserver.TryGetSurfaceCollisionObserver(
                    out CelestialSurfaceCollisionObserverState state) &&
                state.IsValid)
            {
                gatheredObservers.Add(state);
            }
        }

        bool TryGetCollisionObserver(
            out CelestialSurfaceCollisionObserverState observer)
        {
            GatherCollisionObservers();
            if (gatheredObservers.Count == 0)
            {
                observer = default;
                return false;
            }

            int bestIndex = 0;
            float bestSqrDistance = float.MaxValue;
            Vector3 center = transform.position;
            for (int i = 0; i < gatheredObservers.Count; i++)
            {
                float sqrDistance = (gatheredObservers[i].Position - center).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestIndex = i;
                }
            }

            observer = gatheredObservers[bestIndex];
            return true;
        }

        Camera ResolveCamera()
        {
            return targetCamera != null ? targetCamera : Camera.main;
        }

        void Subscribe()
        {
            if (bodyVisual != null)
            {
                bodyVisual.Rebuilt -= HandleBodyVisualRebuilt;
                bodyVisual.Rebuilt += HandleBodyVisualRebuilt;
                bodyVisual.MaterialPropertiesChanged -= HandleMaterialPropertiesChanged;
                bodyVisual.MaterialPropertiesChanged += HandleMaterialPropertiesChanged;
            }
        }

        void Unsubscribe()
        {
            if (bodyVisual != null)
            {
                bodyVisual.Rebuilt -= HandleBodyVisualRebuilt;
                bodyVisual.MaterialPropertiesChanged -= HandleMaterialPropertiesChanged;
            }
        }

        void HandleBodyVisualRebuilt()
        {
            CancelPatchTransition();
            forceRefresh = true;
            patchGeometryDirty = true;
            if (surfaceModeActive ||
                collisionAuthority !=
                CelestialSurfaceCollisionAuthority.GlobalFallback)
            {
                ActivateGlobalCollisionAuthority();
            }
        }

        void HandleMaterialPropertiesChanged()
        {
            float baseRadius = bodyVisual != null && bodyVisual.Body != null
                ? Mathf.Max(0.01f, bodyVisual.Body.Radius)
                : 0f;
            foreach (SurfacePatch patch in activePatches.Values)
            {
                ApplyPatchMorph(
                    patch,
                    patch.Collider.enabled
                        ? Vector4.zero
                        : ResolveMorphRange(baseRadius, patch.Descriptor.Key.Level));
            }

            foreach (SurfacePatch patch in stagedPatches.Values)
            {
                ApplyPatchMorph(
                    patch,
                    desiredCollisionKeys.Contains(patch.Descriptor.Key)
                        ? Vector4.zero
                        : ResolveMorphRange(baseRadius, patch.Descriptor.Key.Level));
            }
        }

        Vector4 ResolveMorphRange(float baseRadius, int level)
        {
            if (level <= 0 || baseRadius <= 0f)
            {
                return Vector4.zero;
            }

            float parentWorldSize = baseRadius * (2f / (1 << (level - 1)));
            float completeDistance = parentWorldSize * Mathf.Max(
                0.1f,
                profile.SplitDistanceMultiplier - PatchBoundingRatio);
            return new Vector4(completeDistance * MorphStartRatio, completeDistance, 0f, 0f);
        }

        static float CalculatePatchAngularFootprint(int level, int resolution)
        {
            float faceAngularSize = Mathf.PI * 0.5f / (1 << Mathf.Max(0, level));
            return faceAngularSize / Mathf.Max(1, resolution);
        }

        enum PatchEdge
        {
            Bottom,
            Right,
            Top,
            Left
        }
    }
}
