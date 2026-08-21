using System.Collections.Generic;
using Farion.Core.Physics;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
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
        readonly Dictionary<GameObject, float> pivotOffsets = new();
        readonly List<Renderer> shadingRenderers = new();
        readonly List<Collider> pieceColliders = new();
        MaterialPropertyBlock shadingBlock;
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
            ResolveComponents();
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

            Vector3 primaryObserver = observerLocalPositions[0];
            float observerSpeed = ResolveObserverSpeed(primaryObserver);
            prefetchCenter = primaryObserver + observerLocalVelocity * profile.PrefetchSeconds;
            float altitude = Mathf.Max(0f, primaryObserver.magnitude - body.Radius);
            placementSuspended =
                !SurfaceGeometryReady() ||
                (profile.PlacementPauseSpeed > 0f && observerSpeed > profile.PlacementPauseSpeed) ||
                altitude > profile.PlacementPauseAltitude;

            ReleaseDistantFormations();
            if (!placementSuspended)
            {
                RefreshDesiredCells();
                ProcessPendingCells();
            }

            RefreshFormationCollision();
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

        void ResolveComponents()
        {
            body ??= GetComponent<CelestialBody>();
            surfaceModel ??= GetComponent<PlanetSurfaceModel>();
            patchSystem ??= GetComponent<CelestialSurfacePatchSystem>();
            bodyVisual ??= GetComponent<CelestialBodyVisual>();
            if (formationRoot == null || formationRoot == transform)
            {
                formationRoot = ResolveFormationRoot();
            }
        }

        Transform ResolveFormationRoot()
        {
            Transform existing = transform.Find(FormationContainerName);
            if (existing != null)
            {
                return existing;
            }

            if (!Application.isPlaying)
            {
                return transform;
            }

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
                    if (NearestObserverDistance(pair.Value.LocalCenter) > pair.Value.ReleaseDistance)
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

        void RefreshDesiredCells()
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

                runtime.AnchorLocalPosition = observerLocalPositions[0];
                runtime.HasAnchor = true;
                runtime.PendingCells.Clear();
                EnqueueCellsAround(runtime, prefetchCenter);
                for (int observer = 1; observer < observerLocalPositions.Count; observer++)
                {
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
                Vector3 pieceUp = item.LocalRotation * Vector3.up;
                pieceTransform.localPosition = item.LocalPosition +
                    pieceUp * (ResolvePivotOffset(piece.Prefab) * item.Scale.y);
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
                instance.Add(piece.Prefab, spawned);
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
            for (int i = 0; i < ruleRuntimes.Count; i++)
            {
                RuleRuntime runtime = ruleRuntimes[i];
                float distance = runtime.Rule.CollisionDistance;
                foreach (FormationInstance instance in runtime.Instances.Values)
                {
                    bool shouldCollide =
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

        float ResolvePivotOffset(GameObject prefab)
        {
            if (pivotOffsets.TryGetValue(prefab, out float cached))
            {
                return cached;
            }

            float lowest = 0f;
            bool measured = false;
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                float bottom = mesh.bounds.min.y + filters[i].transform.localPosition.y;
                lowest = measured ? Mathf.Min(lowest, bottom) : bottom;
                measured = true;
            }

            float offset = measured ? -lowest : 0f;
            pivotOffsets.Add(prefab, offset);
            return offset;
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

        sealed class FormationInstance
        {
            public FormationInstance(Vector3 localCenter, float releaseDistance, int capacity)
            {
                LocalCenter = localCenter;
                ReleaseDistance = releaseDistance;
                Prefabs = new List<GameObject>(capacity);
                Pieces = new List<GameObject>(capacity);
                Colliders = new List<Collider>(capacity);
            }

            public Vector3 LocalCenter { get; }
            public float ReleaseDistance { get; }
            public List<GameObject> Prefabs { get; }
            public List<GameObject> Pieces { get; }
            public List<Collider> Colliders { get; }
            public bool CollidersEnabled { get; set; }
            public int PieceCount => Pieces.Count;

            public void Add(GameObject prefab, GameObject spawned)
            {
                Prefabs.Add(prefab);
                Pieces.Add(spawned);
            }

            public void Clear()
            {
                Prefabs.Clear();
                Pieces.Clear();
                Colliders.Clear();
                CollidersEnabled = false;
            }
        }
    }
}
