using System;
using System.Collections.Generic;
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

        [Header("Profile")]
        [SerializeField] CelestialSurfacePatchProfile profile;

        [Header("Sources")]
        [SerializeField] CelestialBodyVisual bodyVisual;
        [SerializeField] Camera targetCamera;
        [SerializeField] MonoBehaviour collisionObserverSource;

        [Header("Runtime State")]
        [SerializeField] bool surfaceModeActive;
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

        readonly Dictionary<PatchKey, SurfacePatch> activePatches = new();
        readonly Dictionary<PatchKey, SurfacePatch> stagedPatches = new();
        readonly Stack<SurfacePatch> patchPool = new();
        readonly List<PatchDescriptor> desiredPatches = new();
        readonly HashSet<PatchKey> desiredKeys = new();
        readonly HashSet<PatchKey> desiredCollisionKeys = new();
        readonly List<PatchBuildWork> pendingPatchBuilds = new();
        readonly List<PatchKey> staleKeys = new();
        readonly List<Vector3> patchBuildVertices = new();
        readonly List<Vector3> patchBuildNormals = new();
        readonly List<Vector4> patchBuildShadingData = new();
        readonly List<int> patchBuildTriangles = new();
        readonly List<int> patchBuildCollisionTriangles = new();
        readonly HashSet<PatchKey> subdividedBranches = new();
        readonly List<CelestialSurfaceCollisionObserverState> gatheredObservers = new();
        readonly List<Rigidbody> lastGatheredObserverRigidbodies = new();
        readonly List<Vector3> lastGatheredObserverLocalPositions = new();
        readonly List<Vector3> splitObserverLocalPositions = new();

        Transform patchContainer;
        Vector3[] extendedPositionBuffer = Array.Empty<Vector3>();
        CelestialShapeSample[] surfaceSampleBuffer = Array.Empty<CelestialShapeSample>();
        bool forceRefresh = true;
        bool patchGeometryDirty;
        bool transitionRebuildsAllGeometry;
        bool transitionLocalCollisionCoverageReady;
        int nextPendingPatchBuild;
        float transitionBaseRadius;
        bool observerStateInitialized;
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
        int lastActiveCoverageCheckFrame = -1;
        bool lastActiveCoverageSafe;

        public bool SurfaceModeActive => surfaceModeActive;
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
            lastActiveCoverageCheckFrame = -1;
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
            if (Application.isPlaying)
            {
                UpdateSurfaceMode(force: false);
            }
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

        void PublishSampleFootprint()
        {
            if (profile == null)
            {
                return;
            }

            PlanetSurfaceModel surfaceModel = GetComponent<PlanetSurfaceModel>();
            if (surfaceModel != null)
            {
                surfaceModel.SetSampleFootprint(CalculatePatchAngularFootprint(
                    profile.MaxSubdivisionLevel,
                    profile.PatchResolution));
            }
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
                !body.SupportsNonConvexSurfaceCollider ||
                camera == null)
            {
                DeactivateSurfaceMode();
                return;
            }

            float baseRadius = Mathf.Max(0.01f, body.Radius);
            float outerRadius = bodyVisual.HasRenderRadiusRange
                ? bodyVisual.RenderRadiusMinMax.y
                : baseRadius;
            currentOuterRadius = outerRadius;
            currentCollisionObserverAvailable = TryGetCollisionObserver(
                out CelestialSurfaceCollisionObserverState collisionState);

            float altitude = Vector3.Distance(camera.transform.position, transform.position) - outerRadius;
            float effectiveAltitudeRatio = altitude / baseRadius;
            for (int i = 0; i < gatheredObservers.Count; i++)
            {
                float observerAltitude = Vector3.Distance(
                    gatheredObservers[i].Position,
                    transform.position) - outerRadius;
                effectiveAltitudeRatio = Mathf.Min(
                    effectiveAltitudeRatio,
                    observerAltitude / baseRadius);
            }

            bool shouldUseSurfaceMode = surfaceModeActive || patchTransitionPending
                ? effectiveAltitudeRatio <= profile.ExitAltitudeRatio
                : effectiveAltitudeRatio <= profile.EnterAltitudeRatio;

            if (!shouldUseSurfaceMode)
            {
                DeactivateSurfaceMode();
                return;
            }

            Vector3 observerLocalPosition = transform.InverseTransformPoint(camera.transform.position);
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
            const int CoverageCheckFrameInterval = 5;
            if (Time.frameCount - lastActiveCoverageCheckFrame >= CoverageCheckFrameInterval)
            {
                lastActiveCoverageCheckFrame = Time.frameCount;
                lastActiveCoverageSafe = canPrepareLocalCollision &&
                    HasPredictedActiveCollisionCoverage(
                        currentCollisionObserverLocalPosition,
                        currentCollisionObserverLocalVelocity) &&
                    AllObserversHaveCollisionCoverage(
                        baseRadius,
                        outerRadius,
                        useDesiredPatches: false);
            }

            bool activeLocalCoverageSafe = lastActiveCoverageSafe;
            if (collisionAuthority == CelestialSurfaceCollisionAuthority.LocalAuthoritative &&
                !activeLocalCoverageSafe)
            {
                ActivateGlobalCollisionAuthority();
            }

            if (surfaceModeActive)
            {
                ApplyPrimaryTerrainState();
            }

            bool needsRefresh = force ||
                forceRefresh ||
                !surfaceModeActive ||
                ObserverMovedEnough(
                    observerLocalPosition,
                    observerLocalDirection,
                    baseRadius) ||
                CollisionObserverChangedEnough(
                    collisionState.Rigidbody,
                    currentCollisionObserverLocalPosition,
                    collisionObserverLocalDirection,
                    currentCollisionObserverLocalVelocity,
                    baseRadius) ||
                GatheredObserversChangedEnough(baseRadius);
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
            Vector3 observerLocalDirection,
            float baseRadius)
        {
            if (!observerStateInitialized)
            {
                return true;
            }

            float moveThreshold = baseRadius * profile.ObserverMoveThresholdRatio;
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
            Vector3 observerLocalVelocity,
            float baseRadius)
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

            float moveThreshold = baseRadius * profile.ObserverMoveThresholdRatio;
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
            RebuildSubdividedBranches();
            splitObserverLocalPositions.Clear();
            for (int i = 0; i < gatheredObservers.Count; i++)
            {
                splitObserverLocalPositions.Add(
                    transform.InverseTransformPoint(gatheredObservers[i].Position));
            }

            desiredPatches.Clear();
            desiredKeys.Clear();
            desiredCollisionKeys.Clear();
            pendingPatchBuilds.Clear();
            nextPendingPatchBuild = 0;
            transitionBaseRadius = baseRadius;
            transitionRebuildsAllGeometry = patchGeometryDirty;
            patchGeometryDirty = false;

            for (int face = 0; face < 6; face++)
            {
                CollectDesiredPatch(
                    (CelestialCubeFace)face,
                    level: 0,
                    x: 0,
                    y: 0,
                    uMin: -1f,
                    vMin: -1f,
                    size: 2f,
                    observerLocalPosition,
                    baseRadius);
            }

            for (int i = 0; i < desiredPatches.Count; i++)
            {
                PatchDescriptor descriptor = desiredPatches[i];
                desiredKeys.Add(descriptor.Key);
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
                desiredCollisionKeys.Clear();
                if (collisionAuthority ==
                    CelestialSurfaceCollisionAuthority.LocalAuthoritative)
                {
                    ActivateGlobalCollisionAuthority();
                }
            }

            for (int i = 0; i < desiredPatches.Count; i++)
            {
                PatchDescriptor descriptor = desiredPatches[i];
                bool collisionRequired = desiredCollisionKeys.Contains(descriptor.Key);
                SurfacePatch patch = null;
                bool rebuildGeometry = transitionRebuildsAllGeometry ||
                    !activePatches.TryGetValue(descriptor.Key, out patch);
                bool prepareExistingCollision = !rebuildGeometry &&
                    collisionRequired &&
                    !patch.CollisionBaked;
                if (rebuildGeometry || prepareExistingCollision)
                {
                    pendingPatchBuilds.Add(new PatchBuildWork(
                        descriptor,
                        rebuildGeometry,
                        collisionRequired));
                }
            }

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
                    patch.Collider.enabled != desiredCollisionKeys.Contains(key))
                {
                    return true;
                }
            }

            return false;
        }

        void ProcessPatchTransition()
        {
            double deadline = Time.realtimeSinceStartupAsDouble +
                profile.PatchBuildBudgetMilliseconds / 1000d;
            int processedThisFrame = 0;

            while (nextPendingPatchBuild < pendingPatchBuilds.Count &&
                processedThisFrame < profile.MaximumPatchBuildsPerFrame &&
                (processedThisFrame == 0 || Time.realtimeSinceStartupAsDouble < deadline))
            {
                PatchBuildWork work = pendingPatchBuilds[nextPendingPatchBuild++];
                SurfacePatch patch;
                if (work.RebuildGeometry)
                {
                    patch = RentPatch();
                    BuildPatch(patch, work.Descriptor, transitionBaseRadius);
                    patch.Renderer.enabled = false;
                    patch.Collider.enabled = false;
                    stagedPatches.Add(work.Descriptor.Key, patch);
                }
                else if (!activePatches.TryGetValue(work.Descriptor.Key, out patch))
                {
                    continue;
                }

                if (work.PrepareCollision)
                {
                    PreparePatchCollision(patch);
                }

                processedThisFrame++;
            }

            pendingPatchBuildCount = pendingPatchBuilds.Count - nextPendingPatchBuild;
            if (pendingPatchBuildCount == 0)
            {
                CommitPatchTransition();
            }
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
            if (!commitLocalCollision &&
                collisionAuthority ==
                CelestialSurfaceCollisionAuthority.LocalAuthoritative)
            {
                ActivateGlobalCollisionAuthority();
            }

            staleKeys.Clear();
            foreach (PatchKey key in activePatches.Keys)
            {
                if (transitionRebuildsAllGeometry || !desiredKeys.Contains(key))
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
                activePatches.Add(pair.Key, pair.Value);
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
                patch.Renderer.enabled = true;
                SetPatchCollision(
                    patch,
                    commitLocalCollision &&
                    desiredCollisionKeys.Contains(descriptor.Key));
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
            pendingPatchBuilds.Clear();
            desiredKeys.Clear();
            desiredCollisionKeys.Clear();
            UpdateRuntimeCounts();
        }

        void CancelPatchTransition()
        {
            foreach (SurfacePatch patch in stagedPatches.Values)
            {
                ReleasePatch(patch);
            }

            stagedPatches.Clear();
            pendingPatchBuilds.Clear();
            desiredPatches.Clear();
            desiredKeys.Clear();
            desiredCollisionKeys.Clear();
            nextPendingPatchBuild = 0;
            pendingPatchBuildCount = 0;
            patchTransitionPending = false;
            transitionRebuildsAllGeometry = false;
            transitionLocalCollisionCoverageReady = false;
        }

        void UpdateRuntimeCounts()
        {
            activePatchCount = activePatches.Count;
            activeColliderCount = 0;
            foreach (SurfacePatch patch in activePatches.Values)
            {
                if (patch.Collider.enabled)
                {
                    activeColliderCount++;
                }
            }
        }

        void ActivateGlobalCollisionAuthority()
        {
            if (bodyVisual != null && bodyVisual.isActiveAndEnabled)
            {
                bodyVisual.SetPrimaryTerrainEnabled(
                    renderEnabled: !surfaceModeActive,
                    collisionEnabled: true);
            }

            foreach (SurfacePatch patch in activePatches.Values)
            {
                SetPatchCollision(patch, enabled: false);
            }

            collisionAuthority =
                CelestialSurfaceCollisionAuthority.GlobalFallback;
            localCollisionCoverageReady = false;
            lastActiveCoverageCheckFrame = -1;
            UpdateRuntimeCounts();
        }

        void ActivateLocalCollisionAuthority()
        {
            collisionAuthority =
                CelestialSurfaceCollisionAuthority.LocalAuthoritative;
            localCollisionCoverageReady = true;
            lastActiveCoverageCheckFrame = -1;
            ApplyPrimaryTerrainState();
        }

        void ApplyPrimaryTerrainState()
        {
            if (bodyVisual == null || !bodyVisual.isActiveAndEnabled)
            {
                return;
            }

            bodyVisual.SetPrimaryTerrainEnabled(
                renderEnabled: !surfaceModeActive,
                collisionEnabled: collisionAuthority !=
                    CelestialSurfaceCollisionAuthority.LocalAuthoritative);
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
            float observerDistance = Vector3.Distance(observerLocalPosition, patchCenter);
            for (int i = 0; i < splitObserverLocalPositions.Count; i++)
            {
                observerDistance = Mathf.Min(
                    observerDistance,
                    Vector3.Distance(splitObserverLocalPositions[i], patchCenter));
            }
            bool hasPatchBudget = desiredPatches.Count <
                Mathf.Max(1, profile.MaxActivePatches - PatchBudgetReserve);
            float splitDistance = patchWorldSize * profile.SplitDistanceMultiplier;
            if (WasBranchSubdivided(key))
            {
                splitDistance *= 1f + profile.SplitHysteresisRatio;
            }

            bool shouldSplit = level < profile.MaxSubdivisionLevel &&
                hasPatchBudget &&
                observerDistance < splitDistance;

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
            CollectDesiredPatch(
                face,
                childLevel,
                childX,
                childY,
                uMin,
                vMin,
                childSize,
                observerLocalPosition,
                baseRadius);
            CollectDesiredPatch(
                face,
                childLevel,
                childX + 1,
                childY,
                uMin + childSize,
                vMin,
                childSize,
                observerLocalPosition,
                baseRadius);
            CollectDesiredPatch(
                face,
                childLevel,
                childX,
                childY + 1,
                uMin,
                vMin + childSize,
                childSize,
                observerLocalPosition,
                baseRadius);
            CollectDesiredPatch(
                face,
                childLevel,
                childX + 1,
                childY + 1,
                uMin + childSize,
                vMin + childSize,
                childSize,
                observerLocalPosition,
                baseRadius);
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
                descriptor.Key.Level < profile.MinimumCollisionLevel)
            {
                return false;
            }

            float surfaceDistance = Vector3.Angle(
                observerLocalDirection,
                descriptor.CenterDirection) * Mathf.Deg2Rad * baseRadius;
            float patchMargin = descriptor.PatchWorldSize * 0.8f;
            float collisionRadius = baseRadius * profile.CollisionRadiusRatio;
            float observerAltitude = Mathf.Max(0f, observerLocalPosition.magnitude - baseRadius);
            return surfaceDistance <= collisionRadius + patchMargin &&
                observerAltitude <= baseRadius * profile.ExitAltitudeRatio;
        }

        bool CanPrepareLocalCollision(
            bool hasCollisionObserver,
            Vector3 observerLocalPosition,
            float baseRadius,
            float outerRadius)
        {
            return CanPrepareLocalCollision(
                hasCollisionObserver,
                observerLocalPosition,
                currentCollisionObserverLocalVelocity,
                baseRadius,
                outerRadius);
        }

        bool CanPrepareLocalCollision(
            bool hasCollisionObserver,
            Vector3 observerLocalPosition,
            Vector3 observerLocalVelocity,
            float baseRadius,
            float outerRadius)
        {
            if (!hasCollisionObserver || profile.CollisionRadiusRatio <= 0f)
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

            float safetyMargin = baseRadius * profile.CollisionSafetyMarginRatio;
            float availableTravel = baseRadius *
                profile.CollisionRadiusRatio *
                profile.CollisionCoverageSafetyRatio;
            float predictedTravel =
                observerLocalVelocity.magnitude *
                profile.CollisionPredictionSeconds;
            return predictedTravel + safetyMargin <= availableTravel;
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
                        localVelocity,
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
                    out Vector3 localDirection,
                    out _);
                for (int i = 0; i < desiredPatches.Count; i++)
                {
                    PatchDescriptor descriptor = desiredPatches[i];
                    if (!desiredCollisionKeys.Contains(descriptor.Key) &&
                        ShouldEnableCollision(
                            descriptor,
                            localPosition,
                            localDirection,
                            baseRadius))
                    {
                        desiredCollisionKeys.Add(descriptor.Key);
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

        bool GatheredObserversChangedEnough(float baseRadius)
        {
            if (gatheredObservers.Count != lastGatheredObserverRigidbodies.Count)
            {
                return true;
            }

            float moveThreshold = baseRadius * profile.ObserverMoveThresholdRatio;
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
            int minimumLevel = profile.MinimumCollisionLevel;
            if (minimumLevel <= 0)
            {
                return baseRadius * profile.ExitAltitudeRatio;
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
            float safetyMarginRatio = profile.CollisionSafetyMarginRatio;
            const int PredictionSamples = 5;
            for (int sample = 0; sample < PredictionSamples; sample++)
            {
                float progress = sample / (PredictionSamples - 1f);
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

                if (safetyMarginRatio <= 0f)
                {
                    continue;
                }

                Vector3 referenceAxis = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) < 0.9f
                    ? Vector3.up
                    : Vector3.right;
                Vector3 tangent = Vector3.Cross(referenceAxis, direction).normalized;
                Vector3 bitangent = Vector3.Cross(direction, tangent).normalized;
                if (!IsCollisionDirectionCovered(
                        (direction + tangent * safetyMarginRatio).normalized,
                        useDesiredPatches) ||
                    !IsCollisionDirectionCovered(
                        (direction - tangent * safetyMarginRatio).normalized,
                        useDesiredPatches) ||
                    !IsCollisionDirectionCovered(
                        (direction + bitangent * safetyMarginRatio).normalized,
                        useDesiredPatches) ||
                    !IsCollisionDirectionCovered(
                        (direction - bitangent * safetyMarginRatio).normalized,
                        useDesiredPatches))
                {
                    return false;
                }
            }

            return true;
        }

        bool IsCollisionDirectionCovered(Vector3 direction, bool useDesiredPatches)
        {
            if (useDesiredPatches)
            {
                for (int i = 0; i < desiredPatches.Count; i++)
                {
                    PatchDescriptor descriptor = desiredPatches[i];
                    if (desiredCollisionKeys.Contains(descriptor.Key) &&
                        DescriptorContainsDirection(descriptor, direction))
                    {
                        return true;
                    }
                }

                return false;
            }

            foreach (SurfacePatch patch in activePatches.Values)
            {
                if (patch.Collider.enabled &&
                    DescriptorContainsDirection(patch.Descriptor, direction))
                {
                    return true;
                }
            }

            return false;
        }

        static bool DescriptorContainsDirection(
            PatchDescriptor descriptor,
            Vector3 direction)
        {
            CelestialCubeProjection.Project(direction, out CelestialCubeFace face, out float u, out float v);
            if (face != descriptor.Key.Face)
            {
                return false;
            }

            const float Epsilon = 0.0001f;
            return u >= descriptor.UMin - Epsilon &&
                u <= descriptor.UMin + descriptor.Size + Epsilon &&
                v >= descriptor.VMin - Epsilon &&
                v <= descriptor.VMin + descriptor.Size + Epsilon;
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
                    patch.Collider.sharedMesh != patch.CollisionMesh)
                {
                    return false;
                }
            }

            return true;
        }

        void BuildPatch(SurfacePatch patch, PatchDescriptor descriptor, float baseRadius)
        {
            patch.Descriptor = descriptor;
            int resolution = profile.PatchResolution;
            int rowSize = resolution + 1;
            int surfaceVertexCount = rowSize * rowSize;
            int extendedRowSize = resolution + 3;
            int estimatedVertexCount = surfaceVertexCount + rowSize * 4;
            int estimatedIndexCount = resolution * resolution * 6 + resolution * 4 * 12;
            List<Vector3> vertices = patchBuildVertices;
            List<Vector3> normals = patchBuildNormals;
            List<Vector4> shadingData = patchBuildShadingData;
            List<int> triangles = patchBuildTriangles;
            vertices.Clear();
            normals.Clear();
            shadingData.Clear();
            triangles.Clear();
            if (vertices.Capacity < estimatedVertexCount)
            {
                vertices.Capacity = estimatedVertexCount;
                normals.Capacity = estimatedVertexCount;
                shadingData.Capacity = estimatedVertexCount;
            }

            if (triangles.Capacity < estimatedIndexCount)
            {
                triangles.Capacity = estimatedIndexCount;
            }

            int extendedPositionCount = extendedRowSize * extendedRowSize;
            if (extendedPositionBuffer.Length < extendedPositionCount)
            {
                Array.Resize(ref extendedPositionBuffer, extendedPositionCount);
            }

            if (surfaceSampleBuffer.Length < surfaceVertexCount)
            {
                Array.Resize(ref surfaceSampleBuffer, surfaceVertexCount);
            }

            Vector3[] extendedPositions = extendedPositionBuffer;
            CelestialShapeSample[] surfaceSamples = surfaceSampleBuffer;
            // Shared vertices must resolve to the same radius across adjacent LODs.
            // Level-dependent filtering made split/merge transitions pulse visibly.
            float angularFootprint = CalculatePatchAngularFootprint(
                profile.MaxSubdivisionLevel,
                resolution);

            for (int extendedY = 0; extendedY < extendedRowSize; extendedY++)
            {
                int y = extendedY - 1;
                float v = descriptor.VMin + descriptor.Size * (y / (float)resolution);
                for (int extendedX = 0; extendedX < extendedRowSize; extendedX++)
                {
                    int x = extendedX - 1;
                    float u = descriptor.UMin + descriptor.Size * (x / (float)resolution);
                    Vector3 direction = CelestialCubeProjection.ToDirection(descriptor.Key.Face, u, v);
                    bool isSurfaceVertex = x >= 0 && x <= resolution && y >= 0 && y <= resolution;
                    float vertexRadius;
                    if (isSurfaceVertex)
                    {
                        CelestialShapeSample sample = CelestialSurfaceSampling.EvaluateSample(
                            baseRadius,
                            direction,
                            angularFootprint,
                            bodyVisual.ShapeProfile,
                            bodyVisual.SurfaceProfile);
                        surfaceSamples[y * rowSize + x] = sample;
                        vertexRadius = sample.Radius;
                    }
                    else
                    {
                        vertexRadius = CelestialSurfaceSampling.EvaluateRadius(
                            baseRadius,
                            direction,
                            angularFootprint,
                            bodyVisual.ShapeProfile,
                            bodyVisual.SurfaceProfile);
                    }

                    extendedPositions[extendedY * extendedRowSize + extendedX] =
                        direction * vertexRadius;
                }
            }

            for (int y = 0; y <= resolution; y++)
            {
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

                    CelestialShapeSample sample = surfaceSamples[y * rowSize + x];
                    vertices.Add(position);
                    normals.Add(normal);
                    shadingData.Add(sample.ShadingData);
                }
            }

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int a = y * rowSize + x;
                    int b = a + 1;
                    int c = a + rowSize;
                    int d = c + 1;
                    AddOutwardTriangle(triangles, vertices, a, c, b);
                    AddOutwardTriangle(triangles, vertices, b, c, d);
                }
            }

            patchBuildCollisionTriangles.Clear();
            patchBuildCollisionTriangles.AddRange(triangles);
            int surfaceVertexCountForCollision = vertices.Count;

            float skirtDepth = Mathf.Max(0.01f, baseRadius * profile.SkirtDepthRatio);
            AddSkirt(
                PatchEdge.Bottom,
                resolution,
                rowSize,
                skirtDepth,
                vertices,
                normals,
                shadingData,
                triangles);
            AddSkirt(
                PatchEdge.Right,
                resolution,
                rowSize,
                skirtDepth,
                vertices,
                normals,
                shadingData,
                triangles);
            AddSkirt(
                PatchEdge.Top,
                resolution,
                rowSize,
                skirtDepth,
                vertices,
                normals,
                shadingData,
                triangles);
            AddSkirt(
                PatchEdge.Left,
                resolution,
                rowSize,
                skirtDepth,
                vertices,
                normals,
                shadingData,
                triangles);

            Mesh mesh = patch.Mesh;
            mesh.Clear();
            mesh.name = $"Surface Patch {descriptor.Key}";
            mesh.indexFormat = vertices.Count <= 65535
                ? IndexFormat.UInt16
                : IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.SetUVs(0, shadingData);
            mesh.SetNormals(normals);
            mesh.RecalculateBounds();

            Mesh collisionMesh = patch.CollisionMesh;
            collisionMesh.Clear();
            collisionMesh.name = $"Surface Patch Collision {descriptor.Key}";
            collisionMesh.indexFormat = surfaceVertexCountForCollision <= 65535
                ? IndexFormat.UInt16
                : IndexFormat.UInt32;
            collisionMesh.SetVertices(vertices, 0, surfaceVertexCountForCollision);
            collisionMesh.SetTriangles(patchBuildCollisionTriangles, 0, true);
            collisionMesh.RecalculateBounds();

            patch.CollisionBaked = false;
            patch.Filter.sharedMesh = mesh;
            patch.Collider.sharedMesh = null;
            patch.Collider.enabled = false;
            patch.GameObject.name = $"Surface Patch {descriptor.Key}";
            patch.GameObject.layer = FarionLayers.CelestialSurface;
            bodyVisual.ConfigureSurfaceRenderer(patch.Renderer);
        }

        static void AddOutwardTriangle(
            List<int> triangles,
            List<Vector3> vertices,
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

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        static void AddSkirt(
            PatchEdge edge,
            int resolution,
            int rowSize,
            float skirtDepth,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector4> shadingData,
            List<int> triangles)
        {
            int skirtStart = vertices.Count;
            for (int i = 0; i <= resolution; i++)
            {
                int sourceIndex = GetEdgeIndex(edge, i, resolution, rowSize);
                Vector3 radialDirection = vertices[sourceIndex].normalized;
                vertices.Add(vertices[sourceIndex] - radialDirection * skirtDepth);
                normals.Add(normals[sourceIndex]);
                shadingData.Add(shadingData[sourceIndex]);
            }

            for (int i = 0; i < resolution; i++)
            {
                int a = GetEdgeIndex(edge, i, resolution, rowSize);
                int b = GetEdgeIndex(edge, i + 1, resolution, rowSize);
                int skirtA = skirtStart + i;
                int skirtB = skirtA + 1;
                AddDoubleSidedTriangle(triangles, a, b, skirtB);
                AddDoubleSidedTriangle(triangles, a, skirtB, skirtA);
            }
        }

        static int GetEdgeIndex(PatchEdge edge, int index, int resolution, int rowSize)
        {
            return edge switch
            {
                PatchEdge.Bottom => index,
                PatchEdge.Right => index * rowSize + resolution,
                PatchEdge.Top => resolution * rowSize + (resolution - index),
                _ => (resolution - index) * rowSize
            };
        }

        static void AddDoubleSidedTriangle(List<int> triangles, int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(b);
        }

        void SetPatchCollision(SurfacePatch patch, bool enabled)
        {
            if (!enabled)
            {
                if (!patch.Collider.enabled && patch.Collider.sharedMesh == null)
                {
                    return;
                }

                patch.Collider.enabled = false;
                patch.Collider.sharedMesh = null;
                return;
            }

            if (patch.Collider.enabled && patch.Collider.sharedMesh == patch.CollisionMesh)
            {
                return;
            }

            PreparePatchCollision(patch);
            patch.Collider.enabled = true;
        }

        void PreparePatchCollision(SurfacePatch patch)
        {
            if (!patch.CollisionBaked && profile.BakeCollisionMeshes)
            {
                CelestialMeshColliderBaker.BakeImmediate(patch.CollisionMesh);
                patch.CollisionBaked = true;
            }

            if (patch.Collider.sharedMesh != patch.CollisionMesh)
            {
                patch.Collider.sharedMesh = patch.CollisionMesh;
            }
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
                Mesh mesh = new()
                {
                    name = "Celestial Surface Patch",
                    hideFlags = HideFlags.DontSave
                };
                Mesh collisionMesh = new()
                {
                    name = "Celestial Surface Patch Collision",
                    hideFlags = HideFlags.DontSave
                };
                patch = new SurfacePatch(patchObject, filter, renderer, collider, mesh, collisionMesh);
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
            patch.Collider.sharedMesh = null;
            patch.Renderer.enabled = false;
            patch.Filter.sharedMesh = null;
            patch.GameObject.SetActive(false);
            patchPool.Push(patch);
        }

        void DeactivateSurfaceMode()
        {
            if (!surfaceModeActive &&
                !patchTransitionPending &&
                activePatches.Count == 0)
            {
                if (collisionAuthority !=
                    CelestialSurfaceCollisionAuthority.GlobalFallback)
                {
                    collisionAuthority =
                        CelestialSurfaceCollisionAuthority.GlobalFallback;
                    localCollisionCoverageReady = false;
                    ApplyPrimaryTerrainState();
                }

                return;
            }

            CancelPatchTransition();
            if (bodyVisual != null && bodyVisual.isActiveAndEnabled)
            {
                bodyVisual.SetPrimaryTerrainEnabled(renderEnabled: true, collisionEnabled: true);
            }

            ReleaseAllActivePatches();
            surfaceModeActive = false;
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
            DestroyRuntimeObject(patch.CollisionMesh);
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
            foreach (SurfacePatch patch in activePatches.Values)
            {
                bodyVisual.ConfigureSurfaceRenderer(patch.Renderer);
            }

            foreach (SurfacePatch patch in stagedPatches.Values)
            {
                bodyVisual.ConfigureSurfaceRenderer(patch.Renderer);
            }
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
