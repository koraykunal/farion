using System.Collections.Generic;
using Farion.Core.Physics;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [DefaultExecutionOrder(375)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CelestialBody), typeof(PlanetSurfaceModel))]
    public sealed class SurfaceFormationSpawner : MonoBehaviour
    {
        [SerializeField] SurfaceFormationProfile profile;
        [SerializeField] CelestialBody body;
        [SerializeField] PlanetSurfaceModel surfaceModel;
        [SerializeField] CelestialSurfacePatchSystem patchSystem;
        [SerializeField] Transform formationRoot;

        const string FormationContainerName = "Surface Formations";
        const float TerrainAnchorEpsilon = 0.01f;
        const int TerrainAnchorRefreshBudget = 32;

        [Header("Runtime State")]
        [SerializeField] bool placementSuspended;
        [SerializeField] int activeFormationCount;
        [SerializeField] int activePieceCount;
        [SerializeField] int pendingCellCount;
        [SerializeField] int activeColliderCount;

        readonly List<RuleRuntime> ruleRuntimes = new();
        readonly List<BiomeWeight> biomeWeights = new();
        readonly List<SurfaceFormationPlacementItem> compositionBuffer = new();
        readonly Dictionary<GameObject, Stack<GameObject>> pool = new();
        readonly Dictionary<GameObject, Bounds> prefabBounds = new();
        readonly List<Renderer> shadingRenderers = new();
        readonly List<Collider> pieceColliders = new();
        MaterialPropertyBlock shadingBlock;
        int terrainAnchorRefreshBudget;
        CelestialBodyVisual bodyVisual;
        readonly List<SurfaceScatterCell> staleCells = new();
        readonly List<Transform> observers = new();
        readonly List<Vector3> observerLocalPositions = new();
        bool runtimeDirty = true;
        int nextRefreshRuleIndex;
        bool hasPreviousCameraPosition;
        Vector3 previousCameraLocalPosition;
        Vector3 observerLocalVelocity;
        Vector3 prefetchCenter;
        MonoBehaviour environmentSource;

        public SurfaceFormationProfile Profile => profile;
        public int ActiveFormationCount => activeFormationCount;
        public int ActivePieceCount => activePieceCount;
        public int ActiveColliderCount => activeColliderCount;
        public bool PlacementSuspended => placementSuspended;

        void Awake()
        {
            ResolveComponents();
        }

        void OnEnable()
        {
            ResolveComponents();
            if (surfaceModel != null)
            {
                surfaceModel.Changed += HandleSurfaceChanged;
            }

            runtimeDirty = true;
        }

        void OnDisable()
        {
            if (surfaceModel != null)
            {
                surfaceModel.Changed -= HandleSurfaceChanged;
            }

            ClearRuntime();
        }

        void OnValidate()
        {
            ResolveComponents(createRuntimeRoot: false);
            runtimeDirty = true;
        }

        void LateUpdate()
        {
            ResolveComponents();
            if (!CanPlace())
            {
                return;
            }

            if (runtimeDirty || ruleRuntimes.Count != profile.Rules.Count)
            {
                RebuildRuntimes();
            }

            RefreshObserverPositions();
            if (observerLocalPositions.Count == 0)
            {
                return;
            }

            if (!TryGetPrimaryPlacementObserver(out Vector3 primaryObserver))
            {
                placementSuspended = true;
                RefreshFormationCollision();
                UpdateRuntimeCounts();
                return;
            }

            float observerSpeed = ResolveObserverSpeed(primaryObserver);
            prefetchCenter = primaryObserver + observerLocalVelocity * profile.PrefetchSeconds;
            placementSuspended =
                !SurfaceGeometryReady() ||
                (observerLocalPositions.Count == 1 &&
                    profile.PlacementPauseSpeed > 0f &&
                    observerSpeed > profile.PlacementPauseSpeed);

            if (!placementSuspended)
            {
                ReleaseDistantFormations();
                RefreshDesiredCells(primaryObserver);
                ProcessPendingCells();
            }

            RefreshFormationCollision();
            RefreshTerrainAnchors();
            UpdateRuntimeCounts();
        }

        public void AddObserver(Transform observer)
        {
            if (observer != null && !observers.Contains(observer))
            {
                observers.Add(observer);
            }
        }

        public void RemoveObserver(Transform observer)
        {
            observers.Remove(observer);
        }

        void RefreshObserverPositions()
        {
            observerLocalPositions.Clear();
            for (int i = observers.Count - 1; i >= 0; i--)
            {
                if (observers[i] == null)
                {
                    observers.RemoveAt(i);
                    continue;
                }

                Vector3 local = transform.InverseTransformPoint(observers[i].position);
                if (local.sqrMagnitude > 0.0001f)
                {
                    observerLocalPositions.Add(local);
                }
            }

            if (observerLocalPositions.Count > 0)
            {
                return;
            }

            Camera camera = patchSystem != null ? patchSystem.TargetCamera : Camera.main;
            if (camera == null)
            {
                return;
            }

            Vector3 cameraLocal = transform.InverseTransformPoint(camera.transform.position);
            if (cameraLocal.sqrMagnitude > 0.0001f)
            {
                observerLocalPositions.Add(cameraLocal);
            }
        }

        float NearestObserverDistance(Vector3 localPosition)
        {
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < observerLocalPositions.Count; i++)
            {
                nearest = Mathf.Min(
                    nearest,
                    Vector3.Distance(observerLocalPositions[i], localPosition));
            }

            return nearest;
        }

        bool TryGetPrimaryPlacementObserver(out Vector3 observer)
        {
            for (int i = 0; i < observerLocalPositions.Count; i++)
            {
                if (CanPlaceAroundObserver(observerLocalPositions[i]))
                {
                    observer = observerLocalPositions[i];
                    return true;
                }
            }

            observer = default;
            return false;
        }

        bool CanPlaceAroundObserver(Vector3 observerLocalPosition)
        {
            float maximumAltitude = profile != null
                ? profile.PlacementPauseAltitude
                : 0f;
            return maximumAltitude <= 0f ||
                observerLocalPosition.magnitude - body.Radius <= maximumAltitude;
        }

        public float SampleFormationInfluence(Vector3 localPosition)
        {
            float influence = 0f;
            for (int i = 0; i < ruleRuntimes.Count; i++)
            {
                RuleRuntime runtime = ruleRuntimes[i];
                float radius = runtime.Rule.InfluenceRadius;
                if (radius <= 0f)
                {
                    continue;
                }

                foreach (FormationInstance instance in runtime.Instances.Values)
                {
                    float distance = Vector3.Distance(instance.LocalCenter, localPosition);
                    if (distance >= radius)
                    {
                        continue;
                    }

                    influence = Mathf.Max(influence, 1f - distance / radius);
                    if (influence >= 1f)
                    {
                        return 1f;
                    }
                }
            }

            return influence;
        }

        void ResolveComponents(bool createRuntimeRoot = true)
        {
            body ??= GetComponent<CelestialBody>();
            surfaceModel ??= GetComponent<PlanetSurfaceModel>();
            patchSystem ??= GetComponent<CelestialSurfacePatchSystem>();
            bodyVisual ??= GetComponent<CelestialBodyVisual>();
            if (formationRoot == null || formationRoot == transform)
            {
                Transform existing = transform.Find(FormationContainerName);
                if (existing != null)
                {
                    formationRoot = existing;
                }
                else if (createRuntimeRoot && Application.isPlaying)
                {
                    formationRoot = CreateFormationRoot();
                }
            }
        }

        Transform CreateFormationRoot()
        {
            GameObject container = new(FormationContainerName);
            container.transform.SetParent(transform, false);
            container.layer = FarionLayers.CelestialSurface;
            return container.transform;
        }

        bool CanPlace()
        {
            return Application.isPlaying &&
                profile != null &&
                profile.Rules.Count > 0 &&
                body != null &&
                surfaceModel != null;
        }

        PieceAnchor BuildPieceAnchor(Vector3 surfaceLocalPosition, Vector3 baseLocalPosition)
        {
            return new PieceAnchor
            {
                Direction = surfaceLocalPosition.sqrMagnitude > 0.000001f
                    ? surfaceLocalPosition.normalized
                    : Vector3.up,
                BaseLocalPosition = baseLocalPosition,
                AnalyticRadius = surfaceLocalPosition.magnitude,
                AppliedOffset = 0f,
                AppliedTransition = -1
            };
        }

        void RefreshTerrainAnchors()
        {
            if (patchSystem == null || observerLocalPositions.Count == 0)
            {
                return;
            }

            int committedTransition = patchSystem.CommittedTransitionCount;
            if (committedTransition <= 0)
            {
                return;
            }

            Vector3 observerLocalPosition = observerLocalPositions[0];
            terrainAnchorRefreshBudget = TerrainAnchorRefreshBudget;
            for (int i = 0; i < ruleRuntimes.Count; i++)
            {
                foreach (FormationInstance instance in ruleRuntimes[i].Instances.Values)
                {
                    for (int piece = 0; piece < instance.Anchors.Count; piece++)
                    {
                        GameObject spawned = instance.Pieces[piece];
                        if (spawned == null)
                        {
                            continue;
                        }

                        ApplyTerrainAnchor(
                            spawned.transform,
                            instance.Anchors[piece],
                            observerLocalPosition,
                            committedTransition,
                            false);
                    }
                }
            }
        }

        void ApplyTerrainAnchor(
            Transform pieceTransform,
            PieceAnchor anchor,
            Vector3 observerLocalPosition,
            int committedTransition,
            bool force)
        {
            if (patchSystem == null)
            {
                return;
            }

            if (anchor.AppliedTransition != committedTransition)
            {
                if (!force &&
                    anchor.HasSurfaceAnchor &&
                    patchSystem.IsSurfaceAnchorCurrent(anchor.Direction, anchor.SurfaceAnchor))
                {
                    anchor.AppliedTransition = committedTransition;
                }
                else if (force || terrainAnchorRefreshBudget > 0)
                {
                    if (!force)
                    {
                        terrainAnchorRefreshBudget--;
                    }

                    anchor.AppliedTransition = committedTransition;
                    anchor.HasSurfaceAnchor = patchSystem.TryResolveSurfaceAnchor(
                        anchor.Direction,
                        out anchor.SurfaceAnchor);
                }
            }

            if (!anchor.HasSurfaceAnchor)
            {
                return;
            }

            float observerDistance = Vector3.Distance(
                observerLocalPosition,
                anchor.Direction * anchor.SurfaceAnchor.FineRadius);
            float offset = anchor.SurfaceAnchor.ResolveRadius(observerDistance) - anchor.AnalyticRadius;
            if (!force && Mathf.Abs(offset - anchor.AppliedOffset) < TerrainAnchorEpsilon)
            {
                return;
            }

            anchor.AppliedOffset = offset;
            pieceTransform.localPosition = anchor.BaseLocalPosition + anchor.Direction * offset;
        }

        bool SurfaceGeometryReady()
        {
            return patchSystem == null || patchSystem.SurfaceModeActive;
        }

        void HandleSurfaceChanged()
        {
            runtimeDirty = true;
        }

        void RebuildRuntimes()
        {
            ClearRuntime();
            for (int i = 0; i < profile.Rules.Count; i++)
            {
                SurfaceFormationRule rule = profile.Rules[i];
                if (rule == null || rule.Kit == null || !rule.Kit.HasOutcrop)
                {
                    continue;
                }

                ruleRuntimes.Add(new RuleRuntime(
                    rule,
                    SurfaceScatterPlacement.CalculateResolution(
                        body.Radius,
                        rule.Distribution.SpacingMeters)));
            }

            runtimeDirty = false;
        }

        float ResolveObserverSpeed(Vector3 cameraLocalPosition)
        {
            if (!hasPreviousCameraPosition)
            {
                hasPreviousCameraPosition = true;
                previousCameraLocalPosition = cameraLocalPosition;
                observerLocalVelocity = Vector3.zero;
                return 0f;
            }

            Vector3 delta = cameraLocalPosition - previousCameraLocalPosition;
            previousCameraLocalPosition = cameraLocalPosition;
            if (Time.deltaTime <= 0.0001f)
            {
                return observerLocalVelocity.magnitude;
            }

            Vector3 velocity = delta / Time.deltaTime;
            observerLocalVelocity = Vector3.Lerp(
                observerLocalVelocity,
                velocity,
                1f - Mathf.Exp(-6f * Time.deltaTime));
            return observerLocalVelocity.magnitude;
        }

        void ReleaseDistantFormations()
        {
            for (int i = 0; i < ruleRuntimes.Count; i++)
            {
                RuleRuntime runtime = ruleRuntimes[i];
                staleCells.Clear();
                foreach (KeyValuePair<SurfaceScatterCell, FormationInstance> pair in runtime.Instances)
                {
                    float reach = Mathf.Min(
                        NearestObserverDistance(pair.Value.LocalCenter),
                        Vector3.Distance(prefetchCenter, pair.Value.LocalCenter));
                    if (reach > pair.Value.ReleaseDistance)
                    {
                        staleCells.Add(pair.Key);
                    }
                }

                for (int stale = 0; stale < staleCells.Count; stale++)
                {
                    SurfaceScatterCell cell = staleCells[stale];
                    ReleaseFormation(runtime.Instances[cell]);
                    runtime.Instances.Remove(cell);
                    runtime.EvaluatedCells.Remove(cell);
                }
            }
        }

        void RefreshDesiredCells(Vector3 primaryObserver)
        {
            if (ruleRuntimes.Count == 0)
            {
                return;
            }

            for (int offset = 0; offset < ruleRuntimes.Count; offset++)
            {
                int i = (nextRefreshRuleIndex + offset) % ruleRuntimes.Count;
                RuleRuntime runtime = ruleRuntimes[i];
                float refreshDistance = Mathf.Max(
                    runtime.Rule.Distribution.SpacingMeters * 0.5f,
                    8f);
                if (runtime.HasAnchor &&
                    NearestObserverDistance(runtime.AnchorLocalPosition) < refreshDistance)
                {
                    continue;
                }

                runtime.AnchorLocalPosition = primaryObserver;
                runtime.HasAnchor = true;
                runtime.PendingCells.Clear();
                EnqueueCellsAround(runtime, prefetchCenter);
                for (int observer = 0; observer < observerLocalPositions.Count; observer++)
                {
                    if (!CanPlaceAroundObserver(observerLocalPositions[observer]))
                    {
                        continue;
                    }

                    EnqueueCellsAround(runtime, observerLocalPositions[observer]);
                }

                nextRefreshRuleIndex = (i + 1) % ruleRuntimes.Count;
                break;
            }
        }

        void EnqueueCellsAround(RuleRuntime runtime, Vector3 observerLocalPosition)
        {
            Vector3 observerDirection = observerLocalPosition.normalized;
            BuildTangentBasis(observerDirection, out Vector3 tangent, out Vector3 bitangent);
            float radius = Mathf.Max(0.01f, body.Radius);
            SurfaceScatterDistribution distribution = runtime.Rule.Distribution;
            float step = distribution.SpacingMeters;
            float searchDistance = distribution.FarVisibilityDistance;
            int radiusSteps = Mathf.CeilToInt(searchDistance / step);
            float searchDistanceSquared = searchDistance * searchDistance;
            int planetSeed = surfaceModel.CreateContext(body).PlanetSeed;
            for (int y = -radiusSteps; y <= radiusSteps; y++)
            {
                float offsetY = y * step;
                for (int x = -radiusSteps; x <= radiusSteps; x++)
                {
                    float offsetX = x * step;
                    if (offsetX * offsetX + offsetY * offsetY > searchDistanceSquared)
                    {
                        continue;
                    }

                    Vector3 direction = (observerDirection +
                        tangent * (offsetX / radius) +
                        bitangent * (offsetY / radius)).normalized;
                    SurfaceScatterCell cell =
                        SurfaceScatterPlacement.CellFromDirection(direction, runtime.Resolution);
                    if (runtime.EvaluatedCells.Contains(cell))
                    {
                        continue;
                    }

                    float visibility = SurfaceScatterPlacement.ResolveVisibilityDistance(
                        distribution,
                        planetSeed,
                        runtime.Rule.StableId,
                        cell);
                    Vector3 cellCenter = SurfaceScatterPlacement.CandidateDirection(
                        cell,
                        planetSeed,
                        runtime.Rule.StableId) * radius;
                    float reach = Mathf.Min(
                        NearestObserverDistance(cellCenter),
                        Vector3.Distance(observerLocalPosition, cellCenter));
                    if (reach <= visibility)
                    {
                        runtime.PendingCells.Enqueue(cell);
                    }
                }
            }
        }

        void ProcessPendingCells()
        {
            int budget = profile.MaximumFormationBuildsPerFrame;
            ResolveOcean(out bool hasOcean, out float oceanRadius);
            for (int guard = 0; guard < budget; guard++)
            {
                RuleRuntime runtime = ResolveNextPendingRuntime();
                if (runtime == null)
                {
                    return;
                }

                SurfaceScatterCell cell = runtime.PendingCells.Dequeue();
                if (runtime.EvaluatedCells.Contains(cell))
                {
                    continue;
                }

                runtime.EvaluatedCells.Add(cell);
                TryBuildFormation(runtime, cell, hasOcean, oceanRadius);
            }
        }

        RuleRuntime ResolveNextPendingRuntime()
        {
            for (int i = 0; i < ruleRuntimes.Count; i++)
            {
                if (ruleRuntimes[i].PendingCells.Count > 0)
                {
                    return ruleRuntimes[i];
                }
            }

            return null;
        }

        void TryBuildFormation(
            RuleRuntime runtime,
            SurfaceScatterCell cell,
            bool hasOcean,
            float oceanRadius)
        {
            SurfaceFormationRule rule = runtime.Rule;
            PlanetGenerationContext context = surfaceModel.CreateContext(body);
            Vector3 direction = SurfaceScatterPlacement.CandidateDirection(
                cell,
                context.PlanetSeed,
                rule.StableId);
            if (!surfaceModel.TrySamplePlanetSurface(direction, out PlanetSurfaceSample sample))
            {
                return;
            }

            if (rule.Suitability.EvaluateCoarse(sample, hasOcean, oceanRadius) <= 0f)
            {
                return;
            }

            float cluster = SurfaceScatterPlacement.EvaluateCluster(
                direction,
                context.Radius,
                rule.Distribution,
                context.PlanetSeed,
                rule.StableId,
                "surface.formation.cluster");
            if (cluster <= 0f)
            {
                return;
            }

            surfaceModel.TrySampleGeology(direction, out CelestialGeologySample geology);
            float geologyWeight = rule.EvaluateGeologyWeight(geology);
            if (geologyWeight <= 0f)
            {
                return;
            }

            float biomeWeight = rule.Suitability.ResolveAllowedBiomeWeight(
                surfaceModel,
                sample,
                biomeWeights);
            float suitability = rule.Suitability.Evaluate(
                sample,
                biomeWeight,
                hasOcean,
                oceanRadius);
            if (suitability <= 0f)
            {
                return;
            }

            float slopeBreak = SurfaceFormationPlacement.EvaluateSlopeBreak(
                surfaceModel,
                direction,
                sample.SurfaceRadius,
                sample.Surface.SlopeAngleDegrees,
                rule);
            float probability = rule.Distribution.SpawnChance *
                suitability * cluster * slopeBreak * geologyWeight;
            if (SurfaceScatterPlacement.Hash01(context.PlanetSeed, rule.StableId, cell, 2) >= probability)
            {
                return;
            }

            SurfaceFormationPlacement.BuildComposition(
                rule,
                surfaceModel,
                transform,
                direction,
                geology,
                cell,
                context.PlanetSeed,
                compositionBuffer);
            if (compositionBuffer.Count == 0)
            {
                return;
            }

            FormationInstance instance = new(
                direction * sample.SurfaceRadius,
                rule.Distribution.ResolveReleaseDistance(
                    SurfaceScatterPlacement.ResolveVisibilityDistance(
                        rule.Distribution,
                        context.PlanetSeed,
                        rule.StableId,
                        cell)),
                compositionBuffer.Count);
            for (int i = 0; i < compositionBuffer.Count; i++)
            {
                SurfaceFormationPlacementItem item = compositionBuffer[i];
                IReadOnlyList<SurfaceFormationPiece> pieces = rule.Kit.Resolve(item.Role);
                if (item.PieceIndex < 0 || item.PieceIndex >= pieces.Count)
                {
                    continue;
                }

                SurfaceFormationPiece piece = pieces[item.PieceIndex];
                if (piece?.Prefab == null)
                {
                    continue;
                }

                GameObject spawned = Rent(piece.Prefab);
                Transform pieceTransform = spawned.transform;
                pieceTransform.SetParent(formationRoot, false);
                Vector3 baseLocalPosition = item.LocalPosition +
                    item.LocalUp * ResolveSeatLift(
                        piece.Prefab,
                        item.LocalRotation,
                        item.Scale,
                        item.LocalUp);
                PieceAnchor anchor = BuildPieceAnchor(item.SurfaceLocalPosition, baseLocalPosition);
                pieceTransform.localPosition = baseLocalPosition;
                pieceTransform.localRotation = item.LocalRotation;
                pieceTransform.localScale = item.Scale;
                ApplySurfaceShading(spawned, rule, sample);
                ApplySurfaceLayer(spawned.transform);
                spawned.GetComponentsInChildren(true, pieceColliders);
                for (int collider = 0; collider < pieceColliders.Count; collider++)
                {
                    pieceColliders[collider].enabled = false;
                    instance.Colliders.Add(pieceColliders[collider]);
                }

                spawned.SetActive(true);
                ApplyTerrainAnchor(
                    pieceTransform,
                    anchor,
                    observerLocalPositions.Count > 0
                        ? observerLocalPositions[0]
                        : anchor.Direction * anchor.AnalyticRadius,
                    patchSystem != null ? patchSystem.CommittedTransitionCount : -1,
                    true);
                instance.Add(piece.Prefab, spawned, anchor);
            }

            if (instance.PieceCount == 0)
            {
                ReleaseFormation(instance);
                return;
            }

            runtime.Instances[cell] = instance;
        }

        void RefreshFormationCollision()
        {
            bool collisionSurfaceReady = patchSystem == null ||
                patchSystem.CollisionAuthority ==
                    CelestialSurfaceCollisionAuthority.LocalAuthoritative;
            for (int i = 0; i < ruleRuntimes.Count; i++)
            {
                RuleRuntime runtime = ruleRuntimes[i];
                float distance = runtime.Rule.CollisionDistance;
                foreach (FormationInstance instance in runtime.Instances.Values)
                {
                    bool shouldCollide =
                        collisionSurfaceReady &&
                        NearestObserverDistance(instance.LocalCenter) <= distance;
                    if (shouldCollide == instance.CollidersEnabled)
                    {
                        continue;
                    }

                    instance.CollidersEnabled = shouldCollide;
                    for (int collider = 0; collider < instance.Colliders.Count; collider++)
                    {
                        Collider target = instance.Colliders[collider];
                        if (target != null)
                        {
                            target.enabled = shouldCollide;
                        }
                    }
                }
            }
        }

        void ApplySurfaceShading(
            GameObject spawned,
            SurfaceFormationRule rule,
            in PlanetSurfaceSample sample)
        {
            SurfaceScatterShaderBinding binding = profile.ShaderBinding;
            if (binding == null || !binding.IsBound)
            {
                return;
            }

            SurfaceVisualProfile visualProfile = ResolveVisualProfile();
            shadingBlock ??= new MaterialPropertyBlock();
            spawned.GetComponentsInChildren(true, shadingRenderers);
            for (int i = 0; i < shadingRenderers.Count; i++)
            {
                Renderer renderer = shadingRenderers[i];
                renderer.GetPropertyBlock(shadingBlock);
                binding.Apply(
                    shadingBlock,
                    visualProfile,
                    sample,
                    rule.SurfaceTintStrength,
                    rule.SnowResponse,
                    rule.MossResponse);
                renderer.SetPropertyBlock(shadingBlock);
            }
        }

        SurfaceVisualProfile ResolveVisualProfile()
        {
            return bodyVisual != null &&
                bodyVisual.SurfaceProfile is TerrestrialSurfaceProfile terrestrial
                    ? terrestrial.SurfaceVisualProfile
                    : null;
        }

        float ResolveSeatLift(GameObject prefab, Quaternion rotation, Vector3 scale, Vector3 up)
        {
            Bounds bounds = ResolvePrefabBounds(prefab);
            float lowest = float.PositiveInfinity;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 local = new(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                lowest = Mathf.Min(
                    lowest,
                    Vector3.Dot(rotation * Vector3.Scale(local, scale), up));
            }

            return float.IsPositiveInfinity(lowest) ? 0f : -lowest;
        }

        Bounds ResolvePrefabBounds(GameObject prefab)
        {
            if (prefabBounds.TryGetValue(prefab, out Bounds cached))
            {
                return cached;
            }

            Bounds bounds = default;
            bool measured = false;
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Bounds meshBounds = mesh.bounds;
                meshBounds.center += filters[i].transform.localPosition;
                if (measured)
                {
                    bounds.Encapsulate(meshBounds);
                }
                else
                {
                    bounds = meshBounds;
                    measured = true;
                }
            }

            prefabBounds.Add(prefab, bounds);
            return bounds;
        }

        static void ApplySurfaceLayer(Transform root)
        {
            root.gameObject.layer = FarionLayers.CelestialSurface;
            for (int i = 0; i < root.childCount; i++)
            {
                ApplySurfaceLayer(root.GetChild(i));
            }
        }

        void ResolveOcean(out bool hasOcean, out float oceanRadius)
        {
            hasOcean = false;
            oceanRadius = 0f;
            if (environmentSource is ICelestialEnvironmentProvider cached &&
                cached.TryGetEnvironment(body, out CelestialEnvironmentSample cachedSample))
            {
                hasOcean = cachedSample.HasOcean;
                oceanRadius = cachedSample.OceanRadius;
                return;
            }

            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is not ICelestialEnvironmentProvider provider ||
                    !provider.TryGetEnvironment(body, out CelestialEnvironmentSample sample))
                {
                    continue;
                }

                environmentSource = behaviours[i];
                hasOcean = sample.HasOcean;
                oceanRadius = sample.OceanRadius;
                return;
            }
        }

        GameObject Rent(GameObject prefab)
        {
            if (pool.TryGetValue(prefab, out Stack<GameObject> stack))
            {
                while (stack.Count > 0)
                {
                    GameObject candidate = stack.Pop();
                    if (candidate != null)
                    {
                        return candidate;
                    }
                }
            }

            return Instantiate(prefab, formationRoot);
        }

        void ReleaseFormation(FormationInstance instance)
        {
            for (int i = 0; i < instance.PieceCount; i++)
            {
                GameObject prefab = instance.Prefabs[i];
                GameObject spawned = instance.Pieces[i];
                if (spawned == null)
                {
                    continue;
                }

                spawned.SetActive(false);
                if (!pool.TryGetValue(prefab, out Stack<GameObject> stack))
                {
                    stack = new Stack<GameObject>();
                    pool.Add(prefab, stack);
                }

                stack.Push(spawned);
            }

            instance.Clear();
        }

        void ClearRuntime()
        {
            for (int i = 0; i < ruleRuntimes.Count; i++)
            {
                foreach (FormationInstance instance in ruleRuntimes[i].Instances.Values)
                {
                    ReleaseFormation(instance);
                }
            }

            ruleRuntimes.Clear();
            hasPreviousCameraPosition = false;
            activeFormationCount = 0;
            activePieceCount = 0;
        }

        void UpdateRuntimeCounts()
        {
            activeFormationCount = 0;
            activePieceCount = 0;
            pendingCellCount = 0;
            activeColliderCount = 0;
            for (int i = 0; i < ruleRuntimes.Count; i++)
            {
                RuleRuntime runtime = ruleRuntimes[i];
                activeFormationCount += runtime.Instances.Count;
                pendingCellCount += runtime.PendingCells.Count;
                foreach (FormationInstance instance in runtime.Instances.Values)
                {
                    activePieceCount += instance.PieceCount;
                    if (instance.CollidersEnabled)
                    {
                        activeColliderCount += instance.Colliders.Count;
                    }
                }
            }
        }

        static void BuildTangentBasis(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
        {
            Vector3 reference = Mathf.Abs(normal.y) < 0.95f ? Vector3.up : Vector3.right;
            tangent = Vector3.Cross(reference, normal).normalized;
            bitangent = Vector3.Cross(normal, tangent).normalized;
        }

        sealed class RuleRuntime
        {
            public RuleRuntime(SurfaceFormationRule rule, int resolution)
            {
                Rule = rule;
                Resolution = resolution;
            }

            public SurfaceFormationRule Rule { get; }
            public int Resolution { get; }
            public Dictionary<SurfaceScatterCell, FormationInstance> Instances { get; } = new();
            public HashSet<SurfaceScatterCell> EvaluatedCells { get; } = new();
            public Queue<SurfaceScatterCell> PendingCells { get; } = new();
            public Vector3 AnchorLocalPosition { get; set; }
            public bool HasAnchor { get; set; }
        }

        sealed class PieceAnchor
        {
            public Vector3 Direction;
            public Vector3 BaseLocalPosition;
            public float AnalyticRadius;
            public float AppliedOffset;
            public bool HasSurfaceAnchor;
            public CelestialSurfaceAnchor SurfaceAnchor;
            public int AppliedTransition;
        }

        sealed class FormationInstance
        {
            public FormationInstance(Vector3 localCenter, float releaseDistance, int capacity)
            {
                LocalCenter = localCenter;
                ReleaseDistance = releaseDistance;
                Prefabs = new List<GameObject>(capacity);
                Pieces = new List<GameObject>(capacity);
                Anchors = new List<PieceAnchor>(capacity);
                Colliders = new List<Collider>(capacity);
            }

            public Vector3 LocalCenter { get; }
            public float ReleaseDistance { get; }
            public List<GameObject> Prefabs { get; }
            public List<GameObject> Pieces { get; }
            public List<PieceAnchor> Anchors { get; }
            public List<Collider> Colliders { get; }
            public bool CollidersEnabled { get; set; }
            public int PieceCount => Pieces.Count;

            public void Add(GameObject prefab, GameObject spawned, PieceAnchor anchor)
            {
                Prefabs.Add(prefab);
                Pieces.Add(spawned);
                Anchors.Add(anchor);
            }

            public void Clear()
            {
                Prefabs.Clear();
                Pieces.Clear();
                Anchors.Clear();
                Colliders.Clear();
                CollidersEnabled = false;
            }
        }
    }
}
