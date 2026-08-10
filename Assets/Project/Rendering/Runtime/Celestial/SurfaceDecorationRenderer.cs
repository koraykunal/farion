using System.Collections.Generic;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.Planetary;
using UnityEngine;
using UnityEngine.Rendering;

namespace Farion.Rendering.Celestial
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CelestialBody), typeof(PlanetSurfaceModel), typeof(CelestialSurfacePatchSystem))]
    public sealed class SurfaceDecorationRenderer : MonoBehaviour
    {
        const int MaximumInstancesPerDraw = 511;

        [SerializeField] SurfaceDecorationProfile profile;
        [SerializeField] CelestialBody body;
        [SerializeField] PlanetSurfaceModel surfaceModel;
        [SerializeField] CelestialSurfacePatchSystem patchSystem;

        readonly List<RuleRuntime> ruleRuntimes = new();
        readonly List<BiomeWeight> biomeWeights = new();
        MonoBehaviour environmentSource;
        bool warnedUnsupportedInstancing;
        bool runtimeDirty = true;
        int nextRuleIndex;
        int nextRefreshRuleIndex;
        int activeInstanceCount;
        int pendingCandidateCount;
        int drawBatchCount;
        bool hasPreviousCameraPosition;
        Vector3 previousCameraLocalPosition;
        float placementObserverSpeed;
        bool placementSuspended;

        public SurfaceDecorationProfile Profile => profile;
        public int ActiveInstanceCount => activeInstanceCount;
        public int PendingCandidateCount => pendingCandidateCount;
        public int DrawBatchCount => drawBatchCount;
        public float PlacementObserverSpeed => placementObserverSpeed;
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
            if (!CanRender(out Camera camera))
            {
                activeInstanceCount = 0;
                pendingCandidateCount = 0;
                drawBatchCount = 0;
                return;
            }

            if (runtimeDirty || ruleRuntimes.Count != profile.Rules.Count)
            {
                RebuildRuntimes();
            }

            Vector3 cameraLocalPosition = transform.InverseTransformPoint(camera.transform.position);
            if (cameraLocalPosition.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            ResolveOcean(out bool hasOcean, out float oceanRadius);
            placementObserverSpeed = ResolvePlacementObserverSpeed(cameraLocalPosition);
            float altitude = Mathf.Max(0f, cameraLocalPosition.magnitude - body.Radius);
            placementSuspended = ShouldSuspendPlacement(placementObserverSpeed, altitude, profile);
            if (!placementSuspended)
            {
                RefreshDesiredCells(cameraLocalPosition);
                ProcessCandidates(hasOcean, oceanRadius);
            }

            RenderInstances(camera, cameraLocalPosition);
            UpdateStatistics();
        }

        bool CanRender(out Camera camera)
        {
            camera = patchSystem != null ? patchSystem.TargetCamera : null;
            if (profile == null || body == null || surfaceModel == null || patchSystem == null ||
                !patchSystem.SurfaceModeActive || camera == null)
            {
                return false;
            }

            if (SystemInfo.supportsInstancing)
            {
                return true;
            }

            if (!warnedUnsupportedInstancing)
            {
                warnedUnsupportedInstancing = true;
                Debug.LogWarning($"{nameof(SurfaceDecorationRenderer)} requires GPU instancing and is disabled.", this);
            }

            return false;
        }

        void RefreshDesiredCells(Vector3 cameraLocalPosition)
        {
            Vector3 cameraDirection = cameraLocalPosition.normalized;
            BuildTangentBasis(cameraDirection, out Vector3 tangent, out Vector3 bitangent);
            float radius = Mathf.Max(0.01f, body.Radius);

            for (int offset = 0; offset < ruleRuntimes.Count; offset++)
            {
                int i = (nextRefreshRuleIndex + offset) % ruleRuntimes.Count;
                RuleRuntime runtime = ruleRuntimes[i];
                float refreshDistance = Mathf.Max(runtime.Rule.SpacingMeters * 2f, 4f);
                if (runtime.HasAnchor &&
                    Vector3.Distance(runtime.AnchorLocalPosition, cameraLocalPosition) < refreshDistance)
                {
                    continue;
                }

                runtime.AnchorLocalPosition = cameraLocalPosition;
                runtime.HasAnchor = true;
                runtime.DesiredCells.Clear();
                runtime.PendingCells.Clear();

                float step = runtime.Rule.SpacingMeters * 0.75f;
                float drawDistance = runtime.Rule.DrawDistance;
                int radiusSteps = Mathf.CeilToInt(drawDistance / step);
                float drawDistanceSquared = drawDistance * drawDistance;
                for (int y = -radiusSteps; y <= radiusSteps; y++)
                {
                    float offsetY = y * step;
                    for (int x = -radiusSteps; x <= radiusSteps; x++)
                    {
                        float offsetX = x * step;
                        if (offsetX * offsetX + offsetY * offsetY > drawDistanceSquared)
                        {
                            continue;
                        }

                        Vector3 direction = (cameraDirection +
                            tangent * (offsetX / radius) +
                            bitangent * (offsetY / radius)).normalized;
                        SurfaceDecorationCell cell =
                            SurfaceDecorationPlacement.CellFromDirection(direction, runtime.Resolution);
                        if (runtime.DesiredCells.Add(cell) && !runtime.EvaluatedCells.Contains(cell))
                        {
                            runtime.PendingCells.Enqueue(cell);
                        }
                    }
                }

                runtime.StaleCells.Clear();
                foreach (SurfaceDecorationCell cell in runtime.EvaluatedCells)
                {
                    if (!runtime.DesiredCells.Contains(cell))
                    {
                        runtime.StaleCells.Add(cell);
                    }
                }

                for (int staleIndex = 0; staleIndex < runtime.StaleCells.Count; staleIndex++)
                {
                    SurfaceDecorationCell staleCell = runtime.StaleCells[staleIndex];
                    runtime.EvaluatedCells.Remove(staleCell);
                    runtime.Instances.Remove(staleCell);
                }

                nextRefreshRuleIndex = (i + 1) % ruleRuntimes.Count;
                break;
            }
        }

        void ProcessCandidates(bool hasOcean, float oceanRadius)
        {
            if (ruleRuntimes.Count == 0)
            {
                return;
            }

            int remainingBudget = profile.MaximumCandidateEvaluationsPerFrame;
            double deadline = Time.realtimeSinceStartupAsDouble +
                profile.CandidateEvaluationBudgetMilliseconds / 1000d;
            int emptyRulePasses = 0;
            while (remainingBudget > 0 &&
                emptyRulePasses < ruleRuntimes.Count &&
                Time.realtimeSinceStartupAsDouble < deadline)
            {
                RuleRuntime runtime = ruleRuntimes[nextRuleIndex];
                nextRuleIndex = (nextRuleIndex + 1) % ruleRuntimes.Count;
                if (runtime.PendingCells.Count == 0 || runtime.Instances.Count >= runtime.Rule.MaxInstances)
                {
                    emptyRulePasses++;
                    continue;
                }

                emptyRulePasses = 0;
                remainingBudget--;
                SurfaceDecorationCell cell = runtime.PendingCells.Dequeue();
                if (!runtime.DesiredCells.Contains(cell))
                {
                    continue;
                }

                runtime.EvaluatedCells.Add(cell);
                TryCreateInstance(runtime, cell, hasOcean, oceanRadius);
            }
        }

        void TryCreateInstance(
            RuleRuntime runtime,
            SurfaceDecorationCell cell,
            bool hasOcean,
            float oceanRadius)
        {
            SurfaceDecorationRule rule = runtime.Rule;
            PlanetGenerationContext context = surfaceModel.CreateContext(body);
            Vector3 direction = SurfaceDecorationPlacement.CandidateDirection(
                cell,
                context.PlanetSeed,
                rule.StableId);
            if (!surfaceModel.TrySamplePlanetSurface(direction, out PlanetSurfaceSample sample))
            {
                return;
            }

            float biomeWeight = ResolveAllowedBiomeWeight(rule, sample);
            float suitability = rule.EvaluateSuitability(sample, biomeWeight, hasOcean, oceanRadius);
            if (suitability <= 0f)
            {
                return;
            }

            float cluster = SurfaceDecorationPlacement.EvaluateCluster(
                direction,
                context.Radius,
                rule,
                context.PlanetSeed);
            float probability = rule.SpawnChance * suitability * cluster;
            if (SurfaceDecorationPlacement.Hash01(context.PlanetSeed, rule.StableId, cell, 2) >= probability)
            {
                return;
            }

            int variantIndex = rule.ChooseVariant(
                SurfaceDecorationPlacement.Hash01(context.PlanetSeed, rule.StableId, cell, 3));
            if (variantIndex < 0 || variantIndex >= rule.Variants.Count)
            {
                return;
            }

            SurfaceDecorationVariant variant = rule.Variants[variantIndex];
            Vector3 normalLocal = transform.InverseTransformDirection(sample.Surface.Normal).normalized;
            Quaternion alignment = Quaternion.FromToRotation(Vector3.up, normalLocal);
            alignment = Quaternion.Slerp(Quaternion.identity, alignment, rule.NormalAlignment);
            float yaw = SurfaceDecorationPlacement.Hash01(context.PlanetSeed, rule.StableId, cell, 4) * 360f;
            Quaternion rotation = Quaternion.AngleAxis(yaw, normalLocal) * alignment *
                Quaternion.Euler(variant.RotationOffset);
            Vector2 scaleRange = rule.UniformScaleRange;
            float scale = Mathf.Lerp(
                Mathf.Min(scaleRange.x, scaleRange.y),
                Mathf.Max(scaleRange.x, scaleRange.y),
                SurfaceDecorationPlacement.Hash01(context.PlanetSeed, rule.StableId, cell, 5));
            scale *= variant.BaseScale;
            Vector3 position = direction * sample.SurfaceRadius + normalLocal * rule.SurfaceOffset;
            Matrix4x4 localMatrix = Matrix4x4.TRS(position, rotation, Vector3.one * scale);
            runtime.Instances[cell] = new DecorationInstance(variantIndex, position, localMatrix);
        }

        float ResolveAllowedBiomeWeight(SurfaceDecorationRule rule, PlanetSurfaceSample sample)
        {
            if (surfaceModel.GenerationProfile == null ||
                surfaceModel.GenerationProfile.BiomeDistribution == null)
            {
                return rule.AllowsBiome(sample.Biome.Biome) ? sample.Biome.Suitability : 0f;
            }

            biomeWeights.Clear();
            surfaceModel.GenerationProfile.BiomeDistribution.SampleBiomeWeights(
                sample.Context,
                sample.Climate,
                sample.TerrainAltitude,
                sample.Surface.SlopeAngleDegrees,
                biomeWeights);
            float total = 0f;
            for (int i = 0; i < biomeWeights.Count; i++)
            {
                BiomeWeight weight = biomeWeights[i];
                if (rule.AllowsBiome(weight.Biome))
                {
                    total += weight.Weight;
                }
            }

            return Mathf.Clamp01(total);
        }

        void RenderInstances(Camera camera, Vector3 cameraLocalPosition)
        {
            drawBatchCount = 0;
            Matrix4x4 rootMatrix = transform.localToWorldMatrix;
            for (int runtimeIndex = 0; runtimeIndex < ruleRuntimes.Count; runtimeIndex++)
            {
                RuleRuntime runtime = ruleRuntimes[runtimeIndex];
                runtime.ClearRenderLists();
                float drawDistanceSquared = runtime.Rule.DrawDistance * runtime.Rule.DrawDistance;
                float shadowDistanceSquared = runtime.Rule.ShadowDistance * runtime.Rule.ShadowDistance;
                float farLodDistanceSquared =
                    runtime.Rule.FarLodStartDistance * runtime.Rule.FarLodStartDistance;
                foreach (DecorationInstance instance in runtime.Instances.Values)
                {
                    float distanceSquared = (instance.LocalPosition - cameraLocalPosition).sqrMagnitude;
                    if (distanceSquared > drawDistanceSquared)
                    {
                        continue;
                    }

                    Matrix4x4 worldMatrix = rootMatrix * instance.LocalMatrix;
                    bool castsShadow = runtime.Rule.ShadowDistance > 0f && distanceSquared <= shadowDistanceSquared;
                    bool usesFarLod = distanceSquared >= farLodDistanceSquared;
                    runtime.GetRenderList(instance.VariantIndex, castsShadow, usesFarLod).Add(worldMatrix);
                }

                for (int variantIndex = 0; variantIndex < runtime.Rule.Variants.Count; variantIndex++)
                {
                    SurfaceDecorationVariant variant = runtime.Rule.Variants[variantIndex];
                    if (variant == null || variant.NearMesh == null)
                    {
                        continue;
                    }

                    DrawVariant(
                        camera,
                        variant,
                        variant.NearMesh,
                        runtime.NearShadowMatrices[variantIndex],
                        true);
                    DrawVariant(
                        camera,
                        variant,
                        variant.NearMesh,
                        runtime.NearMatrices[variantIndex],
                        false);
                    DrawVariant(
                        camera,
                        variant,
                        variant.FarMesh,
                        runtime.FarMatrices[variantIndex],
                        false);
                }
            }
        }

        void DrawVariant(
            Camera camera,
            SurfaceDecorationVariant variant,
            Mesh mesh,
            List<Matrix4x4> matrices,
            bool castsShadows)
        {
            if (matrices.Count == 0)
            {
                return;
            }

            int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                Material material = ResolveMaterial(variant, subMeshIndex);
                if (material == null || !material.enableInstancing)
                {
                    continue;
                }

                RenderParams parameters = new(material)
                {
                    camera = camera,
                    layer = gameObject.layer,
                    lightProbeUsage = LightProbeUsage.Off,
                    receiveShadows = true,
                    shadowCastingMode = castsShadows ? ShadowCastingMode.On : ShadowCastingMode.Off
                };
                for (int start = 0; start < matrices.Count; start += MaximumInstancesPerDraw)
                {
                    int count = Mathf.Min(MaximumInstancesPerDraw, matrices.Count - start);
                    Graphics.RenderMeshInstanced(parameters, mesh, subMeshIndex, matrices, count, start);
                    drawBatchCount++;
                }
            }
        }

        static Material ResolveMaterial(SurfaceDecorationVariant variant, int subMeshIndex)
        {
            if (variant.Materials.Count == 0)
            {
                return null;
            }

            int index = Mathf.Min(subMeshIndex, variant.Materials.Count - 1);
            return variant.Materials[index];
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

        void RebuildRuntimes()
        {
            ClearRuntime();
            if (profile == null || body == null)
            {
                return;
            }

            for (int i = 0; i < profile.Rules.Count; i++)
            {
                SurfaceDecorationRule rule = profile.Rules[i];
                if (rule != null)
                {
                    ruleRuntimes.Add(new RuleRuntime(
                        rule,
                        SurfaceDecorationPlacement.CalculateResolution(body.Radius, rule.SpacingMeters)));
                }
            }

            runtimeDirty = false;
            nextRuleIndex = 0;
            nextRefreshRuleIndex = 0;
        }

        void UpdateStatistics()
        {
            activeInstanceCount = 0;
            pendingCandidateCount = 0;
            for (int i = 0; i < ruleRuntimes.Count; i++)
            {
                activeInstanceCount += ruleRuntimes[i].Instances.Count;
                pendingCandidateCount += ruleRuntimes[i].PendingCells.Count;
            }
        }

        void ClearRuntime()
        {
            ruleRuntimes.Clear();
            biomeWeights.Clear();
            activeInstanceCount = 0;
            pendingCandidateCount = 0;
            drawBatchCount = 0;
            nextRefreshRuleIndex = 0;
            hasPreviousCameraPosition = false;
            placementObserverSpeed = 0f;
            placementSuspended = false;
        }

        float ResolvePlacementObserverSpeed(Vector3 cameraLocalPosition)
        {
            float cameraSpeed = hasPreviousCameraPosition && Time.unscaledDeltaTime > 0.0001f
                ? Vector3.Distance(cameraLocalPosition, previousCameraLocalPosition) / Time.unscaledDeltaTime
                : 0f;
            previousCameraLocalPosition = cameraLocalPosition;
            hasPreviousCameraPosition = true;

            Rigidbody observer = patchSystem.CollisionObserverRigidbody;
            if (observer == null)
            {
                return cameraSpeed;
            }

            Vector3 relativeVelocity = observer.linearVelocity -
                body.GetVelocityAtPoint(observer.worldCenterOfMass);
            return Mathf.Max(cameraSpeed, relativeVelocity.magnitude);
        }

        internal static bool ShouldSuspendPlacement(
            float observerSpeed,
            float altitude,
            SurfaceDecorationProfile decorationProfile)
        {
            if (decorationProfile == null)
            {
                return true;
            }

            bool speedLimited = decorationProfile.PlacementPauseSpeed > 0f &&
                observerSpeed >= decorationProfile.PlacementPauseSpeed;
            bool altitudeLimited = decorationProfile.PlacementPauseAltitude > 0f &&
                altitude >= decorationProfile.PlacementPauseAltitude;
            return speedLimited || altitudeLimited;
        }

        void ResolveComponents()
        {
            body ??= GetComponent<CelestialBody>();
            surfaceModel ??= GetComponent<PlanetSurfaceModel>();
            patchSystem ??= GetComponent<CelestialSurfacePatchSystem>();
        }

        void HandleSurfaceChanged()
        {
            runtimeDirty = true;
        }

        static void BuildTangentBasis(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
        {
            Vector3 reference = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.95f
                ? Vector3.right
                : Vector3.up;
            tangent = Vector3.Cross(reference, normal).normalized;
            bitangent = Vector3.Cross(normal, tangent).normalized;
        }

        readonly struct DecorationInstance
        {
            public DecorationInstance(int variantIndex, Vector3 localPosition, Matrix4x4 localMatrix)
            {
                VariantIndex = variantIndex;
                LocalPosition = localPosition;
                LocalMatrix = localMatrix;
            }

            public int VariantIndex { get; }
            public Vector3 LocalPosition { get; }
            public Matrix4x4 LocalMatrix { get; }
        }

        sealed class RuleRuntime
        {
            public RuleRuntime(SurfaceDecorationRule rule, int resolution)
            {
                Rule = rule;
                Resolution = resolution;
                int variantCount = rule.Variants.Count;
                NearShadowMatrices = new List<Matrix4x4>[variantCount];
                NearMatrices = new List<Matrix4x4>[variantCount];
                FarMatrices = new List<Matrix4x4>[variantCount];
                for (int i = 0; i < variantCount; i++)
                {
                    NearShadowMatrices[i] = new List<Matrix4x4>();
                    NearMatrices[i] = new List<Matrix4x4>();
                    FarMatrices[i] = new List<Matrix4x4>();
                }
            }

            public SurfaceDecorationRule Rule { get; }
            public int Resolution { get; }
            public bool HasAnchor { get; set; }
            public Vector3 AnchorLocalPosition { get; set; }
            public HashSet<SurfaceDecorationCell> DesiredCells { get; } = new();
            public HashSet<SurfaceDecorationCell> EvaluatedCells { get; } = new();
            public Queue<SurfaceDecorationCell> PendingCells { get; } = new();
            public Dictionary<SurfaceDecorationCell, DecorationInstance> Instances { get; } = new();
            public List<SurfaceDecorationCell> StaleCells { get; } = new();
            public List<Matrix4x4>[] NearShadowMatrices { get; }
            public List<Matrix4x4>[] NearMatrices { get; }
            public List<Matrix4x4>[] FarMatrices { get; }

            public void ClearRenderLists()
            {
                for (int i = 0; i < NearShadowMatrices.Length; i++)
                {
                    NearShadowMatrices[i].Clear();
                    NearMatrices[i].Clear();
                    FarMatrices[i].Clear();
                }
            }

            public List<Matrix4x4> GetRenderList(int variantIndex, bool castsShadow, bool usesFarLod)
            {
                if (castsShadow)
                {
                    return NearShadowMatrices[variantIndex];
                }

                return usesFarLod ? FarMatrices[variantIndex] : NearMatrices[variantIndex];
            }
        }
    }
}
