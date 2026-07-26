using System.Collections.Generic;
using System.Text;
using Farion.Core.Physics;
using Farion.Gameplay.Interaction;
using Farion.Simulation.Celestial;
using Farion.Simulation.Planetary;
using Farion.Simulation.World.Identity;
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
        [SerializeField, Min(0f)] float surfaceOffset = 0.85f;
        [SerializeField] bool skipOceanCoveredDeposits = true;
        [SerializeField, Min(0f)] float oceanSurfaceClearance = 0.05f;

        readonly List<ResourceDepositData> deposits = new();
        readonly List<DepositStreamingCandidate> spawnCandidates = new();
        readonly Dictionary<GeneratedEntityId, SpawnedDepositNode> spawnedNodes = new();
        readonly List<GeneratedEntityId> nodesToRemove = new();
        readonly ResourceDepositDeltaStore depositDeltaStore = new();
        MaterialPropertyBlock propertyBlock;
        bool hasGeneratedDeposits;
        bool loggedMissingTrackingTargetWarning;
        bool loggedNoSpawnWarning;
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
            generatedDepositCount = deposits.Count;
            hasGeneratedDeposits = true;
            RefreshStreaming();
        }

        public void SetTrackingTarget(Transform target)
        {
            trackingTarget = target;
            loggedMissingTrackingTargetWarning = false;
            loggedNoSpawnWarning = false;
            RefreshStreaming();
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
                if (generatedDepositCount > 0 && !loggedMissingTrackingTargetWarning)
                {
                    Debug.LogWarning($"{name}: resource streaming requires an explicit tracking target.", this);
                    loggedMissingTrackingTargetWarning = true;
                }

                return;
            }

            loggedMissingTrackingTargetWarning = false;
            DespawnOutOfRangeNodes(trackingDirection);
            CollectSpawnCandidates(trackingDirection);

            for (int i = 0; i < spawnCandidates.Count; i++)
            {
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

                GameObject node = CreateNodeObject(deposit, position, rotation);
                if (node == null)
                {
                    continue;
                }

                spawnedNodes.Add(deposit.DepositId, new SpawnedDepositNode(deposit, node));
            }

            spawnedNodeCount = spawnedNodes.Count;
            trackedDepositDeltaCount = depositDeltaStore.Count;
            if (spawnedNodeCount == 0 && !loggedNoSpawnWarning)
            {
                Debug.LogWarning(
                    $"{name}: resource generation produced {generatedDepositCount} deposits but spawned 0 nodes. " +
                    $"Target: {activeTrackingTargetName}. Within radius: {depositsWithinSpawnRadiusCount}. " +
                    $"Nearest available distance: {nearestAvailableDepositDistance:0.##}. " +
                    $"Skipped pose/ocean: {skippedPoseOrOceanCount}, depleted: {skippedDepletedDepositCount}, " +
                    $"out of range: {skippedOutOfRangeDepositCount}, missing prefab: {skippedMissingPrefabCount}.",
                    this);
                loggedNoSpawnWarning = true;
            }
            else if (spawnedNodeCount > 0)
            {
                loggedNoSpawnWarning = false;
            }
        }

        [ContextMenu("Clear Resource Nodes")]
        public void ClearSpawned()
        {
            RemoveGeneratedChildren();
            spawnedNodes.Clear();
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
            PlanetaryGenerationProfile generationProfile = surfaceModel != null
                ? surfaceModel.GenerationProfile
                : null;
            if (body == null || surfaceModel == null || generationProfile == null || resourceDistribution == null)
            {
                Debug.LogWarning($"{name}: resource distribution report skipped because body, surface model, generation profile, or resource distribution is missing.", this);
                return;
            }

            int sampleCount = resourceDistribution.CalculateSurfaceSampleCount(body.Radius);
            PlanetGenerationContext context = generationProfile.CreateContext(body.Radius, body.SurfaceGravity);
            List<ResourceSpawnRule> allowedRules = new();
            List<ResourceDepositData> reportDeposits = new();
            Dictionary<string, int> biomeCounts = new();
            Dictionary<string, int> featureCounts = new();
            Dictionary<string, int> allowedRuleCounts = new();
            Dictionary<string, int> depositCounts = new();

            int sampledSurfaceCount = 0;
            int missingSurfaceCount = 0;
            int missingBiomeCount = 0;
            int missingFeatureCount = 0;
            int samplesWithAllowedRules = 0;
            float minAltitude = float.PositiveInfinity;
            float maxAltitude = float.NegativeInfinity;
            float minSlope = float.PositiveInfinity;
            float maxSlope = float.NegativeInfinity;
            float minTemperature = float.PositiveInfinity;
            float maxTemperature = float.NegativeInfinity;
            float minMoisture = float.PositiveInfinity;
            float maxMoisture = float.NegativeInfinity;
            float minRadiation = float.PositiveInfinity;
            float maxRadiation = float.NegativeInfinity;

            int resourceSeed = SeedUtility.Derive(context.PlanetSeed, resourceDistribution.BaseSeed, "resources");
            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                Vector3 localDirection = FibonacciSphereDirection(sampleIndex, sampleCount);
                Vector3 worldDirection = body.transform.TransformDirection(localDirection.normalized);
                Vector3 probePosition = body.Position + worldDirection * Mathf.Max(0.01f, body.Radius);
                if (!surfaceModel.TrySamplePlanetSurface(body, probePosition, out PlanetSurfaceSample sample))
                {
                    missingSurfaceCount++;
                    continue;
                }

                sampledSurfaceCount++;
                minAltitude = Mathf.Min(minAltitude, sample.TerrainAltitude);
                maxAltitude = Mathf.Max(maxAltitude, sample.TerrainAltitude);
                minSlope = Mathf.Min(minSlope, sample.Surface.SlopeAngleDegrees);
                maxSlope = Mathf.Max(maxSlope, sample.Surface.SlopeAngleDegrees);
                minTemperature = Mathf.Min(minTemperature, sample.Climate.TemperatureCelsius);
                maxTemperature = Mathf.Max(maxTemperature, sample.Climate.TemperatureCelsius);
                minMoisture = Mathf.Min(minMoisture, sample.Climate.Moisture);
                maxMoisture = Mathf.Max(maxMoisture, sample.Climate.Moisture);
                minRadiation = Mathf.Min(minRadiation, sample.Climate.Radiation);
                maxRadiation = Mathf.Max(maxRadiation, sample.Climate.Radiation);

                BiomeDefinition biome = sample.Biome.Biome;
                if (biome == null)
                {
                    missingBiomeCount++;
                }
                else
                {
                    IncrementCount(biomeCounts, biome.DisplayName);
                }

                TerrainFeatureDefinition terrainFeature = sample.TerrainFeature.Feature;
                if (terrainFeature == null)
                {
                    missingFeatureCount++;
                }
                else
                {
                    IncrementCount(featureCounts, terrainFeature.DisplayName);
                }

                float resourceNoise = SampleDirectionalNoise(
                    localDirection,
                    resourceDistribution.ResourceNoiseScale,
                    SeedUtility.Derive(resourceSeed, sampleIndex, "resource.noise"));
                resourceDistribution.CollectAllowedRules(
                    biome,
                    terrainFeature,
                    sample.TerrainAltitude,
                    sample.Surface.SlopeAngleDegrees,
                    resourceNoise,
                    allowedRules);
                if (allowedRules.Count <= 0)
                {
                    continue;
                }

                samplesWithAllowedRules++;
                for (int i = 0; i < allowedRules.Count; i++)
                {
                    ResourceNodeDefinition resource = allowedRules[i].Resource;
                    IncrementCount(allowedRuleCounts, resource != null ? resource.DisplayName : "Missing Resource");
                }
            }

            ResourceDepositGenerator.Generate(resourceDistribution, generationProfile, body, surfaceModel, reportDeposits);
            for (int i = 0; i < reportDeposits.Count; i++)
            {
                ResourceDepositData deposit = reportDeposits[i];
                IncrementCount(depositCounts, deposit.Resource != null ? deposit.Resource.DisplayName : "Missing Resource");
            }

            StringBuilder report = new(768);
            report.Append(name);
            report.Append(": resource distribution report. samples=");
            report.Append(sampleCount);
            report.Append(", sampledSurface=");
            report.Append(sampledSurfaceCount);
            report.Append(", missingSurface=");
            report.Append(missingSurfaceCount);
            report.Append(", missingBiome=");
            report.Append(missingBiomeCount);
            report.Append(", missingFeature=");
            report.Append(missingFeatureCount);
            report.Append(", samplesWithAllowedRules=");
            report.Append(samplesWithAllowedRules);
            report.Append(", generatedDeposits=");
            report.Append(reportDeposits.Count);
            report.Append(", altitude=");
            AppendRange(report, minAltitude, maxAltitude);
            report.Append(", slope=");
            AppendRange(report, minSlope, maxSlope);
            report.Append(", temperatureC=");
            AppendRange(report, minTemperature, maxTemperature);
            report.Append(", moisture=");
            AppendRange(report, minMoisture, maxMoisture);
            report.Append(", radiation=");
            AppendRange(report, minRadiation, maxRadiation);
            report.Append(", biomes=[");
            AppendCounts(report, biomeCounts);
            report.Append("], terrainFeatures=[");
            AppendCounts(report, featureCounts);
            report.Append("], allowedRules=[");
            AppendCounts(report, allowedRuleCounts);
            report.Append("], deposits=[");
            AppendCounts(report, depositCounts);
            report.Append(']');
            Debug.Log(report.ToString(), this);
        }

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

                DestroyNode(spawned.Node);
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
            GameObject farthestNode = null;
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
                farthestNode = pair.Value.Node;
            }

            if (farthestNode == null)
            {
                return false;
            }

            DestroyNode(farthestNode);
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

        GameObject CreateNodeObject(ResourceDepositData deposit, Vector3 position, Quaternion rotation)
        {
            if (!deposit.Resource.HasVisualPrefab)
            {
                skippedMissingPrefabCount++;
                Debug.LogWarning(
                    $"{name}: skipped resource node '{deposit.Resource.DisplayName}' because its resource definition has no visual prefab.",
                    this);
                return null;
            }

            Transform parent = container != null ? container : body != null ? body.transform : transform;
            GameObject node = Instantiate(deposit.Resource.VisualPrefab, position, rotation, parent);
            node.name = $"Resource Node - {deposit.Resource.DisplayName} ({deposit.DepositId})";
            if (!Application.isPlaying)
            {
                node.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }

            ResourceNodeInteractable interactable = node.GetComponent<ResourceNodeInteractable>();
            if (interactable == null)
            {
                interactable = node.AddComponent<ResourceNodeInteractable>();
            }

            interactable.Configure(deposit.Resource, deposit.InitialReserve, deposit.GenerationSeed, deposit.DepositId, depositDeltaStore);
            EnsureInteractionCollider(node);
            ApplyResourceColor(node, deposit);
            return node;
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

        static void EnsureInteractionCollider(GameObject node)
        {
            Collider collider = node.GetComponentInChildren<Collider>();
            if (collider == null)
            {
                collider = node.AddComponent<SphereCollider>();
            }

            collider.isTrigger = true;
        }

        static Vector3 FibonacciSphereDirection(int index, int count)
        {
            if (count <= 1)
            {
                return Vector3.up;
            }

            float t = index / (float)(count - 1);
            float y = 1f - 2f * t;
            float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float theta = index * Mathf.PI * (3f - Mathf.Sqrt(5f));
            return new Vector3(Mathf.Cos(theta) * radius, y, Mathf.Sin(theta) * radius).normalized;
        }

        static float SampleDirectionalNoise(Vector3 direction, float scale, int seed)
        {
            float u = Mathf.Atan2(direction.z, direction.x) / (Mathf.PI * 2f) + 0.5f;
            float v = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) / Mathf.PI + 0.5f;
            float seedOffset = (seed & 2047) * 0.00731f;
            return Mathf.PerlinNoise(u * Mathf.Max(0.001f, scale) + seedOffset, v * Mathf.Max(0.001f, scale) + seedOffset * 1.731f);
        }

        static void IncrementCount(Dictionary<string, int> counts, string key)
        {
            if (counts == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            key = key.Trim();
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }

        static void AppendCounts(StringBuilder builder, Dictionary<string, int> counts)
        {
            if (counts == null || counts.Count == 0)
            {
                builder.Append("none");
                return;
            }

            bool first = true;
            foreach (KeyValuePair<string, int> pair in counts)
            {
                if (!first)
                {
                    builder.Append(", ");
                }

                builder.Append(pair.Key);
                builder.Append('=');
                builder.Append(pair.Value);
                first = false;
            }
        }

        static void AppendRange(StringBuilder builder, float min, float max)
        {
            if (float.IsInfinity(min) || float.IsInfinity(max))
            {
                builder.Append("n/a");
                return;
            }

            builder.Append(min.ToString("0.###"));
            builder.Append("..");
            builder.Append(max.ToString("0.###"));
        }

        void ApplyResourceColor(GameObject node, ResourceDepositData deposit)
        {
            Color color = deposit.Biome != null ? deposit.Biome.PreviewColor : Color.yellow;
            if (deposit.TerrainFeature != null)
            {
                color = Color.Lerp(color, deposit.TerrainFeature.PreviewColor, 0.35f);
            }

            color = Color.Lerp(color, Color.white, 0.25f);
            Renderer[] renderers = node.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                MaterialPropertyBlock block = ResolvePropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                renderer.SetPropertyBlock(block);
            }
        }

        MaterialPropertyBlock ResolvePropertyBlock()
        {
            propertyBlock ??= new MaterialPropertyBlock();
            return propertyBlock;
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
