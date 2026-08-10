using Farion.Core.Identity;
using Farion.Gameplay.Interaction;
using System.Collections.Generic;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.Planetary;
using Farion.Simulation.World;
using UnityEngine;

namespace Farion.Gameplay.Resources
{
    [DisallowMultipleComponent]
    public sealed class ResourceDepositRuntimeSpawner : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] CelestialBody body;
        [SerializeField] PlanetSurfaceModel surfaceModel;
        [SerializeField] CelestialFrameProvider frameProvider;
        [SerializeField] ResourceDistributionProfile resourceDistribution;

        [Header("Spawn")]
        [SerializeField] bool spawnOnStart = true;
        [SerializeField] Transform container;
        [SerializeField] Transform trackingTarget;
        [SerializeField, Min(1)] int maxSpawnedDeposits = 96;
        [SerializeField, Min(0f)] float spawnRadius = 160f;
        [SerializeField, Min(0f)] float despawnRadius = 220f;
        [SerializeField, Min(0.05f)] float streamRefreshInterval = 0.35f;
        [SerializeField, Min(1)] int maximumSpawnsPerRefresh = 8;
        [SerializeField, Min(1)] int maximumCandidateEvaluationsPerRefresh = 32;
        [SerializeField, Min(0f)] float surfaceOffset = 0.85f;
        [SerializeField] bool skipOceanCoveredDeposits = true;
        [SerializeField, Min(0f)] float oceanSurfaceClearance = 0.05f;

        readonly List<ResourceDepositData> deposits = new();
        readonly List<DepositStreamingCandidate> spawnCandidates = new();
        readonly Dictionary<GeneratedEntityId, SpawnedDepositNode> spawnedNodes = new();
        readonly HashSet<GeneratedEntityId> generatedDepositIds = new();
        readonly Dictionary<GeneratedEntityId, GeneratedEntityId> currentIdByLegacyId = new();
        readonly Dictionary<ResourceNodeDefinition, Stack<GameObject>> pooledNodesByDefinition = new();
        readonly List<GeneratedEntityId> nodesToRemove = new();
        readonly ResourceDepositDeltaStore depositDeltaStore = new();
        bool hasGeneratedDeposits;
        float nextStreamRefreshTime;

        [System.NonSerialized] int generatedDepositCount;
        [System.NonSerialized] int spawnedNodeCount;
        [System.NonSerialized] int skippedPoseOrOceanCount;
        [System.NonSerialized] int skippedMissingPrefabCount;
        [System.NonSerialized] int skippedDepletedDepositCount;
        [System.NonSerialized] int skippedOutOfRangeDepositCount;
        [System.NonSerialized] int trackedDepositDeltaCount;
        [System.NonSerialized] int depositsWithinSpawnRadiusCount;
        [System.NonSerialized] float nearestAvailableDepositDistance;
        [System.NonSerialized] string activeTrackingTargetName;

        void Awake()
        {
            ResolveComponents();
        }

        void Start()
        {
            if (spawnOnStart)
            {
                Regenerate();
            }
        }

        void Update()
        {
            if (!Application.isPlaying || !hasGeneratedDeposits || Time.time < nextStreamRefreshTime)
            {
                return;
            }

            RefreshStreaming();
        }

        void OnDisable()
        {
            ClearSpawned();
        }

        void OnValidate()
        {
            ResolveComponents();
            maxSpawnedDeposits = Mathf.Max(1, maxSpawnedDeposits);
            spawnRadius = Mathf.Max(0f, spawnRadius);
            despawnRadius = Mathf.Max(spawnRadius, despawnRadius);
            streamRefreshInterval = Mathf.Max(0.05f, streamRefreshInterval);
            maximumSpawnsPerRefresh = Mathf.Max(1, maximumSpawnsPerRefresh);
            maximumCandidateEvaluationsPerRefresh = Mathf.Max(
                maximumSpawnsPerRefresh,
                maximumCandidateEvaluationsPerRefresh);
            surfaceOffset = Mathf.Max(0f, surfaceOffset);
            oceanSurfaceClearance = Mathf.Max(0f, oceanSurfaceClearance);
        }

        [ContextMenu("Regenerate Resource Nodes")]
        public void Regenerate()
        {
            ResolveComponents();
            ClearSpawned();

            PlanetaryGenerationProfile generationProfile = surfaceModel != null
                ? surfaceModel.GenerationProfile
                : null;
            if (body == null || surfaceModel == null || generationProfile == null || resourceDistribution == null)
            {
                return;
            }

            ResourceDepositGenerator.Generate(resourceDistribution, generationProfile, body, surfaceModel, deposits);
            RebuildDepositIdentityLookup();
            generatedDepositCount = deposits.Count;
            hasGeneratedDeposits = true;
            RefreshStreaming();
        }

        public void SetTrackingTarget(Transform target)
        {
            trackingTarget = target;
            nextStreamRefreshTime = Application.isPlaying ? Time.time : 0f;
        }

        void RefreshStreaming()
        {
            nextStreamRefreshTime = Application.isPlaying
                ? Time.time + streamRefreshInterval
                : 0f;

            spawnedNodeCount = spawnedNodes.Count;
            skippedPoseOrOceanCount = 0;
            skippedMissingPrefabCount = 0;
            skippedDepletedDepositCount = 0;
            skippedOutOfRangeDepositCount = 0;
            trackedDepositDeltaCount = depositDeltaStore.Count;
            depositsWithinSpawnRadiusCount = 0;
            nearestAvailableDepositDistance = -1f;
            activeTrackingTargetName = trackingTarget != null ? trackingTarget.name : string.Empty;

            if (!TryResolveTrackingDirection(out Vector3 trackingDirection))
            {
                return;
            }

            DespawnOutOfRangeNodes(trackingDirection);
            CollectSpawnCandidates(trackingDirection);

            int spawnCount = 0;
            int evaluationCount = 0;
            for (int i = 0; i < spawnCandidates.Count; i++)
            {
                if (spawnCount >= maximumSpawnsPerRefresh ||
                    evaluationCount >= maximumCandidateEvaluationsPerRefresh)
                {
                    break;
                }

                evaluationCount++;
                ResourceDepositData deposit = spawnCandidates[i].Deposit;

                if (spawnedNodes.Count >= maxSpawnedDeposits &&
                    !TryFreeCapacityForCloserCandidate(spawnCandidates[i].Distance, trackingDirection))
                {
                    break;
                }

                if (!TryResolveDepositPose(deposit, out Vector3 position, out Quaternion rotation))
                {
                    skippedPoseOrOceanCount++;
                    continue;
                }

                if (!CreateNodeObject(deposit, position, rotation, out GameObject node))
                {
                    continue;
                }

                spawnedNodes.Add(deposit.DepositId, new SpawnedDepositNode(deposit, node));
                spawnCount++;
            }

            spawnedNodeCount = spawnedNodes.Count;
            trackedDepositDeltaCount = depositDeltaStore.Count;
        }

        public CelestialBody Body => body;

        public IReadOnlyList<ResourceDepositData> GeneratedDeposits => deposits;

        public bool TryGetSpawnedNode(
            GeneratedEntityId depositId,
            out ResourceNodeInteractable node)
        {
            node = null;
            return depositId.IsValid &&
                   spawnedNodes.TryGetValue(depositId, out SpawnedDepositNode spawned) &&
                   spawned.Node != null &&
                   spawned.Node.TryGetComponent(out node);
        }

        [ContextMenu("Clear Resource Nodes")]
        public void ClearSpawned()
        {
            foreach (KeyValuePair<GeneratedEntityId, SpawnedDepositNode> pair in spawnedNodes)
            {
                ReleaseNode(pair.Value);
            }

            spawnedNodes.Clear();
            if (!Application.isPlaying)
            {
                RemoveGeneratedChildren();
            }

            spawnedNodeCount = 0;
        }

        [ContextMenu("Clear Resource Deposit Deltas")]
        public void ClearDepositDeltas()
        {
            depositDeltaStore.Clear();
            ClearSpawned();
            trackedDepositDeltaCount = 0;
            RefreshStreaming();
        }

        public void CaptureDeltaSnapshot(List<ResourceDepositDeltaSnapshot> results)
        {
            depositDeltaStore.CaptureSnapshot(results);
        }

        public void ApplyDeltaSnapshot(
            IEnumerable<ResourceDepositDeltaSnapshot> snapshots,
            bool useLegacyIds = false)
        {
            depositDeltaStore.Clear();
            if (snapshots != null)
            {
                foreach (ResourceDepositDeltaSnapshot snapshot in snapshots)
                {
                    GeneratedEntityId depositId = ResolveSnapshotDepositId(snapshot.DepositId, useLegacyIds);
                    if (!depositId.IsValid)
                    {
                        continue;
                    }

                    depositDeltaStore.ApplySnapshot(
                        new ResourceDepositDeltaSnapshot(depositId, snapshot.ExtractedAmount));
                }
            }

            ClearSpawned();
            trackedDepositDeltaCount = depositDeltaStore.Count;
            RefreshStreaming();
        }

        void RebuildDepositIdentityLookup()
        {
            generatedDepositIds.Clear();
            currentIdByLegacyId.Clear();
            for (int i = 0; i < deposits.Count; i++)
            {
                ResourceDepositData deposit = deposits[i];
                if (!deposit.DepositId.IsValid)
                {
                    continue;
                }

                generatedDepositIds.Add(deposit.DepositId);
                if (deposit.LegacyDepositId.IsValid)
                {
                    currentIdByLegacyId[deposit.LegacyDepositId] = deposit.DepositId;
                }
            }
        }

        GeneratedEntityId ResolveSnapshotDepositId(GeneratedEntityId snapshotId, bool useLegacyIds)
        {
            if (!snapshotId.IsValid)
            {
                return GeneratedEntityId.None;
            }

            if (!useLegacyIds)
            {
                return generatedDepositIds.Contains(snapshotId) ? snapshotId : GeneratedEntityId.None;
            }

            return currentIdByLegacyId.TryGetValue(snapshotId, out GeneratedEntityId currentId)
                ? currentId
                : GeneratedEntityId.None;
        }

#if UNITY_EDITOR
        [ContextMenu("Log Resource Streaming Report")]
        public void LogResourceStreamingReport()
        {
            RefreshStreaming();
            Debug.Log(
                $"{name}: resource streaming report. target={activeTrackingTargetName}, " +
                $"generated={generatedDepositCount}, spawned={spawnedNodeCount}, withinSpawnRadius={depositsWithinSpawnRadiusCount}, " +
                $"nearestAvailableDistance={nearestAvailableDepositDistance:0.##}, skippedOutOfRange={skippedOutOfRangeDepositCount}, " +
                $"skippedDepleted={skippedDepletedDepositCount}, skippedPoseOrOcean={skippedPoseOrOceanCount}, " +
                $"skippedMissingPrefab={skippedMissingPrefabCount}, trackedDeltas={trackedDepositDeltaCount}, " +
                $"spawnRadius={spawnRadius:0.##}, despawnRadius={despawnRadius:0.##}.",
                this);
        }

        [ContextMenu("Log Resource Distribution Report")]
        public void LogResourceDistributionReport()
        {
            ResolveComponents();
            if (!ResourceDistributionReportBuilder.TryBuild(
                    name,
                    body,
                    surfaceModel,
                    resourceDistribution,
                    out string report))
            {
                return;
            }

            Debug.Log(report, this);
        }
#endif

        bool TryResolveDepositPose(ResourceDepositData deposit, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;
            Vector3 worldDirection = body.transform.TransformDirection(deposit.LocalDirection).normalized;
            Vector3 probePosition = body.Position + worldDirection * Mathf.Max(0.01f, body.Radius);
            if (!surfaceModel.TrySamplePlanetSurface(body, probePosition, out PlanetSurfaceSample sample))
            {
                return false;
            }

            if (skipOceanCoveredDeposits && IsOceanCovered(sample))
            {
                return false;
            }

            Vector3 normal = sample.Surface.Normal.sqrMagnitude > 0.0001f ? sample.Surface.Normal.normalized : worldDirection;
            Vector3 forward = Vector3.ProjectOnPlane(body.transform.forward, normal);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, normal);
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.right, normal);
            }

            position = sample.Surface.Point + normal * surfaceOffset;
            rotation = Quaternion.LookRotation(forward.normalized, normal);
            return true;
        }

        bool TryResolveTrackingDirection(out Vector3 localDirection)
        {
            localDirection = default;
            if (body == null || trackingTarget == null)
            {
                return false;
            }

            Vector3 worldOffset = trackingTarget.position - body.Position;
            if (worldOffset.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            localDirection = body.transform.InverseTransformDirection(worldOffset.normalized);
            localDirection = localDirection.sqrMagnitude > 0.0001f ? localDirection.normalized : Vector3.up;
            return true;
        }

        void DespawnOutOfRangeNodes(Vector3 trackingDirection)
        {
            nodesToRemove.Clear();
            foreach (KeyValuePair<GeneratedEntityId, SpawnedDepositNode> pair in spawnedNodes)
            {
                SpawnedDepositNode spawned = pair.Value;
                if (spawned.Node == null ||
                    depositDeltaStore.CalculateRemainingQuantity(spawned.Deposit) <= 0 ||
                    !IsWithinStreamingRadius(spawned.Deposit.LocalDirection, trackingDirection, despawnRadius))
                {
                    nodesToRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < nodesToRemove.Count; i++)
            {
                if (!spawnedNodes.TryGetValue(nodesToRemove[i], out SpawnedDepositNode spawned))
                {
                    continue;
                }

                ReleaseNode(spawned);
                spawnedNodes.Remove(nodesToRemove[i]);
            }

            nodesToRemove.Clear();
        }

        bool TryFreeCapacityForCloserCandidate(float candidateDistance, Vector3 trackingDirection)
        {
            if (spawnedNodes.Count < maxSpawnedDeposits)
            {
                return true;
            }

            GeneratedEntityId farthestId = default;
            SpawnedDepositNode farthestNode = default;
            bool hasFarthestNode = false;
            float farthestDistance = candidateDistance;

            foreach (KeyValuePair<GeneratedEntityId, SpawnedDepositNode> pair in spawnedNodes)
            {
                float distance = CalculateSurfaceDistance(pair.Value.Deposit.LocalDirection, trackingDirection);
                if (distance <= farthestDistance)
                {
                    continue;
                }

                farthestDistance = distance;
                farthestId = pair.Key;
                farthestNode = pair.Value;
                hasFarthestNode = farthestNode.Node != null;
            }

            if (!hasFarthestNode)
            {
                return false;
            }

            ReleaseNode(farthestNode);
            spawnedNodes.Remove(farthestId);
            return true;
        }

        void CollectSpawnCandidates(Vector3 trackingDirection)
        {
            spawnCandidates.Clear();
            float nearestDistance = float.PositiveInfinity;

            for (int i = 0; i < deposits.Count; i++)
            {
                ResourceDepositData deposit = deposits[i];
                if (!deposit.IsValid || spawnedNodes.ContainsKey(deposit.DepositId))
                {
                    continue;
                }

                int remainingQuantity = depositDeltaStore.CalculateRemainingQuantity(deposit);
                if (remainingQuantity <= 0)
                {
                    skippedDepletedDepositCount++;
                    continue;
                }

                float distance = CalculateSurfaceDistance(deposit.LocalDirection, trackingDirection);
                nearestDistance = Mathf.Min(nearestDistance, distance);
                if (distance > spawnRadius)
                {
                    skippedOutOfRangeDepositCount++;
                    continue;
                }

                depositsWithinSpawnRadiusCount++;
                spawnCandidates.Add(new DepositStreamingCandidate(deposit, distance));
            }

            nearestAvailableDepositDistance = float.IsInfinity(nearestDistance) ? -1f : nearestDistance;
            spawnCandidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        }

        bool IsWithinStreamingRadius(Vector3 depositDirection, Vector3 trackingDirection, float radius)
        {
            return CalculateSurfaceDistance(depositDirection, trackingDirection) <= radius;
        }

        float CalculateSurfaceDistance(Vector3 a, Vector3 b)
        {
            float dot = Mathf.Clamp(Vector3.Dot(a.normalized, b.normalized), -1f, 1f);
            return Mathf.Acos(dot) * Mathf.Max(0.01f, body != null ? body.Radius : 1f);
        }

        bool IsOceanCovered(PlanetSurfaceSample sample)
        {
            if (frameProvider == null)
            {
                return false;
            }

            if (!frameProvider.TrySample(sample.Surface.Point, Vector3.zero, out CelestialFrameSample frame) ||
                !frame.HasOcean)
            {
                return false;
            }

            return frame.Environment.OceanRadius > sample.SurfaceRadius + oceanSurfaceClearance;
        }

        bool CreateNodeObject(ResourceDepositData deposit, Vector3 position, Quaternion rotation, out GameObject node)
        {
            Transform parent = container != null ? container : body != null ? body.transform : transform;
            if (Application.isPlaying &&
                TryRentNode(deposit.Resource, out node) &&
                ResourceNodeFactory.TryConfigure(
                    node,
                    deposit,
                    position,
                    rotation,
                    parent,
                    depositDeltaStore))
            {
                return true;
            }

            if (!ResourceNodeFactory.TryCreate(deposit, position, rotation, parent, depositDeltaStore, out node))
            {
                skippedMissingPrefabCount++;
                return false;
            }

            return true;
        }

        bool TryRentNode(ResourceNodeDefinition definition, out GameObject node)
        {
            node = null;
            if (definition == null ||
                !pooledNodesByDefinition.TryGetValue(definition, out Stack<GameObject> pool))
            {
                return false;
            }

            while (pool.Count > 0 && node == null)
            {
                node = pool.Pop();
            }

            return node != null;
        }

        void ReleaseNode(SpawnedDepositNode spawned)
        {
            GameObject node = spawned.Node;
            if (node == null)
            {
                return;
            }

            ResourceNodeDefinition definition = spawned.Deposit.Resource;
            if (!Application.isPlaying || definition == null)
            {
                DestroyNode(node);
                return;
            }

            node.SetActive(false);
            Transform parent = container != null ? container : body != null ? body.transform : transform;
            node.transform.SetParent(parent, worldPositionStays: false);
            if (!pooledNodesByDefinition.TryGetValue(definition, out Stack<GameObject> pool))
            {
                pool = new Stack<GameObject>();
                pooledNodesByDefinition.Add(definition, pool);
            }

            pool.Push(node);
        }

        void RemoveGeneratedChildren()
        {
            Transform parent = container != null ? container : body != null ? body.transform : transform;
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child == null || !child.name.StartsWith("Resource Node - ", System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        static void DestroyNode(GameObject node)
        {
            if (node == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(node);
            }
            else
            {
                DestroyImmediate(node);
            }
        }

        void ResolveComponents()
        {
            if (body == null)
            {
                body = GetComponent<CelestialBody>();
            }

            if (surfaceModel == null)
            {
                surfaceModel = GetComponent<PlanetSurfaceModel>();
            }
        }

    }

    readonly struct SpawnedDepositNode
    {
        public SpawnedDepositNode(ResourceDepositData deposit, GameObject node)
        {
            Deposit = deposit;
            Node = node;
        }

        public ResourceDepositData Deposit { get; }
        public GameObject Node { get; }
    }

    readonly struct DepositStreamingCandidate
    {
        public DepositStreamingCandidate(ResourceDepositData deposit, float distance)
        {
            Deposit = deposit;
            Distance = distance;
        }

        public ResourceDepositData Deposit { get; }
        public float Distance { get; }
    }
}
