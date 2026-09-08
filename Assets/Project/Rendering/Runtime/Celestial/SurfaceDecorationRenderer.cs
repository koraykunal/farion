using System.Collections.Generic;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.Planetary;
using UnityEngine;
using UnityEngine.Rendering;

namespace Farion.Rendering.Celestial
{
    [DefaultExecutionOrder(380)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CelestialBody), typeof(PlanetSurfaceModel))]
    public sealed class SurfaceDecorationRenderer : MonoBehaviour
    {
        const int MaximumInstancesPerDraw = 511;

        [SerializeField] SurfaceDecorationProfile profile;
        [SerializeField] CelestialBody body;
        [SerializeField] PlanetSurfaceModel surfaceModel;
        [SerializeField] CelestialSurfacePatchSystem patchSystem;
        [SerializeField] SurfaceFormationSpawner formationSpawner;

        [Header("Runtime State")]
        [SerializeField] bool placementSuspended;
        [SerializeField] int activeInstanceCount;
        [SerializeField] int pendingCandidateCount;
        [SerializeField] int drawBatchCount;

        const float SurfaceAnchorEpsilon = 0.005f;
        const float TallVariantAspect = 1.35f;
        const float FootprintRingRatio = 0.8f;
        System.Func<Vector3, float> analyticRadiusSampler;

        float SampleAnalyticRadius(Vector3 direction)
        {
            return surfaceModel != null && surfaceModel.TrySampleLocalRadius(direction, out float radius)
                ? radius
                : -1f;
        }
        const float UprightLongAxisCosine = 0.7f;
        const int SurfaceAnchorRefreshBudget = 48;

        readonly List<RuleRuntime> ruleRuntimes = new();
        readonly List<BiomeWeight> biomeWeights = new();
        MonoBehaviour environmentSource;
        MaterialPropertyBlock shadingBlock;
        int surfaceAnchorRefreshBudget;
        CelestialBodyVisual bodyVisual;
        bool warnedUnsupportedInstancing;
        bool runtimeDirty = true;
        int nextRuleIndex;
        int nextRefreshRuleIndex;
        bool hasPreviousCameraPosition;
        Vector3 previousCameraLocalPosition;
        Vector3 observerLocalVelocity;
        float placementObserverSpeed;

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
            placementSuspended = ShouldSuspendPlacement(placementObserverSpeed, altitude, profile) ||
                (patchSystem != null && !patchSystem.SurfaceModeActive);
            if (!placementSuspended)
            {
                double placementStart = Time.realtimeSinceStartupAsDouble;
                double placementBudget =
                    profile.CandidateEvaluationBudgetMilliseconds / 1000d;
                double placementDeadline = placementStart + placementBudget;
                RefreshDesiredCells(
                    cameraLocalPosition,
                    cameraLocalPosition + observerLocalVelocity * profile.PrefetchSeconds,
                    placementStart + placementBudget * 0.5d);
                ProcessCandidates(hasOcean, oceanRadius, placementDeadline);
            }

            RenderInstances(camera, cameraLocalPosition);
            UpdateStatistics();
        }

        bool CanRender(out Camera camera)
        {
            camera = patchSystem != null ? patchSystem.TargetCamera : Camera.main;
            if (profile == null || body == null || surfaceModel == null || camera == null)
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

        void RefreshDesiredCells(
            Vector3 cameraLocalPosition,
            Vector3 scanCenter,
            double deadline)
        {
            RuleRuntime runtime = null;
            for (int i = 0; i < ruleRuntimes.Count; i++)
            {
                if (ruleRuntimes[i].ScanActive)
                {
                    runtime = ruleRuntimes[i];
                    break;
                }
            }

            if (runtime == null)
            {
                for (int offset = 0; offset < ruleRuntimes.Count; offset++)
                {
                    int index = (nextRefreshRuleIndex + offset) % ruleRuntimes.Count;
                    RuleRuntime candidate = ruleRuntimes[index];
                    float refreshDistance = Mathf.Max(
                        candidate.Rule.Distribution.SpacingMeters * 2f,
                        12f);
                    if (candidate.HasAnchor &&
                        Vector3.Distance(candidate.AnchorLocalPosition, cameraLocalPosition) <
                            refreshDistance)
                    {
                        continue;
                    }

                    BeginCellScan(candidate, cameraLocalPosition, scanCenter);
                    runtime = candidate;
                    nextRefreshRuleIndex = (index + 1) % ruleRuntimes.Count;
                    break;
                }
            }

            if (runtime == null)
            {
                return;
            }

            SurfaceScatterDistribution distribution = runtime.Rule.Distribution;
            while (runtime.ScanY <= runtime.ScanRadiusSteps &&
                Time.realtimeSinceStartupAsDouble < deadline)
            {
                int x = runtime.ScanX;
                int y = runtime.ScanY;
                runtime.ScanX++;
                if (runtime.ScanX > runtime.ScanRadiusSteps)
                {
                    runtime.ScanX = -runtime.ScanRadiusSteps;
                    runtime.ScanY++;
                }

                float offsetX = x * runtime.ScanStep;
                float offsetY = y * runtime.ScanStep;
                if (offsetX * offsetX + offsetY * offsetY > runtime.ScanDistanceSquared)
                {
                    continue;
                }

                Vector3 direction = (runtime.ScanDirection +
                    runtime.ScanTangent * (offsetX / runtime.ScanRadius) +
                    runtime.ScanBitangent * (offsetY / runtime.ScanRadius)).normalized;
                SurfaceScatterCell cell =
                    SurfaceScatterPlacement.CellFromDirection(direction, runtime.Resolution);
                if (!runtime.DesiredCells.Add(cell))
                {
                    continue;
                }

                float visibility = SurfaceScatterPlacement.ResolveVisibilityDistance(
                    distribution,
                    runtime.ScanPlanetSeed,
                    runtime.Rule.StableId,
                    cell);
                Vector3 cellCenter = SurfaceScatterPlacement.CandidateDirection(
                    cell,
                    runtime.ScanPlanetSeed,
                    runtime.Rule.StableId) * runtime.ScanRadius;
                float reach = Mathf.Min(
                    Vector3.Distance(cellCenter, runtime.AnchorLocalPosition),
                    Vector3.Distance(cellCenter, runtime.ScanCenter));
                if (reach > distribution.ResolveReleaseDistance(visibility))
                {
                    runtime.DesiredCells.Remove(cell);
                    continue;
                }

                if (!runtime.EvaluatedCells.Contains(cell))
                {
                    runtime.PendingCells.Add(new PendingCell(cell, reach * reach));
                }
            }

            if (runtime.ScanY <= runtime.ScanRadiusSteps)
            {
                return;
            }

            runtime.ScanActive = false;
            runtime.PendingCells.Sort(static (left, right) =>
                right.ReachSquared.CompareTo(left.ReachSquared));
            runtime.StaleCells.Clear();
            foreach (SurfaceScatterCell cell in runtime.EvaluatedCells)
            {
                if (runtime.DesiredCells.Contains(cell))
                {
                    continue;
                }

                float cellVisibility = SurfaceScatterPlacement.ResolveVisibilityDistance(
                    distribution,
                    runtime.ScanPlanetSeed,
                    runtime.Rule.StableId,
                    cell);
                Vector3 staleCellCenter = SurfaceScatterPlacement.CandidateDirection(
                    cell,
                    runtime.ScanPlanetSeed,
                    runtime.Rule.StableId) * runtime.ScanRadius;
                if (IsCellReleased(
                    staleCellCenter,
                    runtime.AnchorLocalPosition,
                    runtime.ScanCenter,
                    distribution.ResolveReleaseDistance(cellVisibility)))
                {
                    runtime.StaleCells.Add(cell);
                }
            }
        }

        void BeginCellScan(
            RuleRuntime runtime,
            Vector3 cameraLocalPosition,
            Vector3 scanCenter)
        {
            SurfaceScatterDistribution distribution = runtime.Rule.Distribution;
            runtime.AnchorLocalPosition = cameraLocalPosition;
            runtime.HasAnchor = true;
            runtime.DesiredCells.Clear();
            runtime.PendingCells.Clear();
            runtime.StaleCells.Clear();
            runtime.ScanCenter = scanCenter;
            runtime.ScanDirection = scanCenter.sqrMagnitude > 0.0001f
                ? scanCenter.normalized
                : cameraLocalPosition.normalized;
            BuildTangentBasis(
                runtime.ScanDirection,
                out Vector3 tangent,
                out Vector3 bitangent);
            runtime.ScanTangent = tangent;
            runtime.ScanBitangent = bitangent;
            runtime.ScanRadius = Mathf.Max(0.01f, body.Radius);
            runtime.ScanPlanetSeed = surfaceModel.CreateContext(body).PlanetSeed;
            runtime.ScanStep = distribution.SpacingMeters * 0.75f;
            float searchDistance = distribution.ResolveReleaseDistance(
                distribution.FarVisibilityDistance);
            runtime.ScanRadiusSteps = Mathf.CeilToInt(searchDistance / runtime.ScanStep);
            runtime.ScanDistanceSquared = searchDistance * searchDistance;
            runtime.ScanX = -runtime.ScanRadiusSteps;
            runtime.ScanY = -runtime.ScanRadiusSteps;
            runtime.ScanActive = true;
        }

        void ProcessCandidates(bool hasOcean, float oceanRadius, double deadline)
        {
            if (ruleRuntimes.Count == 0)
            {
                return;
            }

            int remainingBudget = profile.MaximumCandidateEvaluationsPerFrame;
            int emptyRulePasses = 0;
            while (remainingBudget > 0 &&
                emptyRulePasses < ruleRuntimes.Count &&
                Time.realtimeSinceStartupAsDouble < deadline)
            {
                RuleRuntime runtime = ruleRuntimes[nextRuleIndex];
                nextRuleIndex = (nextRuleIndex + 1) % ruleRuntimes.Count;
                if (runtime.PendingCells.Count == 0)
                {
                    emptyRulePasses++;
                    continue;
                }

                emptyRulePasses = 0;
                remainingBudget--;
                int pendingIndex = runtime.PendingCells.Count - 1;
                SurfaceScatterCell cell = runtime.PendingCells[pendingIndex].Cell;
                runtime.PendingCells.RemoveAt(pendingIndex);
                if (!runtime.DesiredCells.Contains(cell))
                {
                    continue;
                }

                runtime.EvaluatedCells.Add(cell);
                TryCreateInstance(runtime, cell, hasOcean, oceanRadius);
            }

            for (int i = 0; i < ruleRuntimes.Count; i++)
            {
                RuleRuntime runtime = ruleRuntimes[i];
                if (runtime.PendingCells.Count == 0)
                {
                    RemoveStaleCells(runtime);
                }
            }
        }

        static void RemoveStaleCells(RuleRuntime runtime)
        {
            for (int i = 0; i < runtime.StaleCells.Count; i++)
            {
                SurfaceScatterCell cell = runtime.StaleCells[i];
                runtime.EvaluatedCells.Remove(cell);
                runtime.Instances.Remove(cell);
            }

            runtime.StaleCells.Clear();
        }

        void TryCreateInstance(
            RuleRuntime runtime,
            SurfaceScatterCell cell,
            bool hasOcean,
            float oceanRadius)
        {
            SurfaceDecorationRule rule = runtime.Rule;
            PlanetGenerationContext context = surfaceModel.CreateContext(body);
            Vector3 direction = SurfaceScatterPlacement.CandidateDirection(
                cell,
                context.PlanetSeed,
                rule.StableId);
            float cluster = SurfaceScatterPlacement.EvaluateCluster(
                direction,
                context.Radius,
                rule.Distribution,
                context.PlanetSeed,
                rule.StableId,
                "surface.decoration.cluster");
            if (cluster <= 0f)
            {
                return;
            }

            if (!surfaceModel.TrySamplePlanetSurface(direction, out PlanetSurfaceSample sample))
            {
                return;
            }

            if (rule.Suitability.EvaluateCoarse(sample, hasOcean, oceanRadius) <= 0f)
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

            float surfaceRadius = sample.SurfaceRadius;
            CelestialSurfaceAnchor placementAnchor = default;
            bool hasPlacementAnchor = patchSystem != null &&
                patchSystem.TryResolveSurfaceAnchor(direction, out placementAnchor);
            if (hasPlacementAnchor)
            {
                surfaceRadius = placementAnchor.FineRadius;
            }

            suitability = rule.ApplyFormationAffinity(
                suitability,
                ResolveFormationInfluence(direction * surfaceRadius));
            if (suitability <= 0f)
            {
                return;
            }

            float probability = rule.Distribution.SpawnChance * suitability * cluster;
            if (SurfaceScatterPlacement.Hash01(context.PlanetSeed, rule.StableId, cell, 2) >= probability)
            {
                return;
            }

            int variantIndex = rule.ChooseVariant(
                SurfaceScatterPlacement.Hash01(context.PlanetSeed, rule.StableId, cell, 3));
            if (variantIndex < 0 || variantIndex >= rule.Variants.Count)
            {
                return;
            }

            SurfaceDecorationVariant variant = rule.Variants[variantIndex];
            Vector2 scaleRange = rule.UniformScaleRange;
            float scale = Mathf.Lerp(
                Mathf.Min(scaleRange.x, scaleRange.y),
                Mathf.Max(scaleRange.x, scaleRange.y),
                SurfaceScatterPlacement.Hash01(context.PlanetSeed, rule.StableId, cell, 5));
            scale *= variant.BaseScale;
            Vector3 normalLocal = transform.InverseTransformDirection(sample.Surface.Normal).normalized;
            Vector3 extents = variant.NearMesh.bounds.extents;
            float footprintRadius = rule.FootprintSeating * scale * FootprintRingRatio *
                Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
            float seatDrop = 0f;
            if (!SurfaceScatterPlacement.TryResolveFootprintSeat(
                    analyticRadiusSampler ??= SampleAnalyticRadius,
                    direction,
                    sample.SurfaceRadius,
                    footprintRadius,
                    rule.NormalAlignment,
                    out _,
                    out Vector3 placementUp,
                    out seatDrop))
            {
                placementUp = SurfaceScatterPlacement.ResolvePlacementUp(
                    direction,
                    normalLocal,
                    rule.NormalAlignment);
                seatDrop = 0f;
            }

            Quaternion alignment = Quaternion.FromToRotation(Vector3.up, placementUp);
            float yaw = SurfaceScatterPlacement.Hash01(context.PlanetSeed, rule.StableId, cell, 4) * 360f;
            Quaternion rotation = Quaternion.AngleAxis(yaw, placementUp) * alignment *
                Quaternion.Euler(variant.RotationOffset);
            if (rule.LayTallVariantsFlat)
            {
                rotation = ResolveRestingRotation(variant.NearMesh.bounds, rotation, placementUp);
            }

            Vector3 position = direction * surfaceRadius + normalLocal * rule.SurfaceOffset;
            position += placementUp * (seatDrop + ResolveMeshGroundingOffset(
                variant.NearMesh.bounds,
                rotation,
                placementUp,
                scale,
                rule.EmbedFraction));
            Matrix4x4 localMatrix = Matrix4x4.TRS(position, rotation, Vector3.one * scale);
            DecorationInstance created = new(
                variantIndex,
                direction,
                surfaceRadius,
                position,
                localMatrix,
                SurfaceScatterPlacement.ResolveVisibilityDistance(
                    rule.Distribution,
                    context.PlanetSeed,
                    rule.StableId,
                    cell));
            if (hasPlacementAnchor)
            {
                created.HasSurfaceAnchor = true;
                created.SurfaceAnchor = placementAnchor;
                created.AppliedTransition = patchSystem.CommittedTransitionCount;
            }

            runtime.Instances[cell] = created;
        }

        float ResolveFormationInfluence(Vector3 localPosition)
        {
            return formationSpawner != null
                ? formationSpawner.SampleFormationInfluence(localPosition)
                : 0f;
        }

        void RefreshInstanceSurfaceAnchor(
            DecorationInstance instance,
            Vector3 cameraLocalPosition)
        {
            if (patchSystem == null)
            {
                return;
            }

            int transition = patchSystem.CommittedTransitionCount;
            if (instance.AppliedTransition != transition)
            {
                if (instance.HasSurfaceAnchor &&
                    patchSystem.IsSurfaceAnchorCurrent(instance.Direction, instance.SurfaceAnchor))
                {
                    instance.AppliedTransition = transition;
                }
                else if (surfaceAnchorRefreshBudget > 0)
                {
                    surfaceAnchorRefreshBudget--;
                    instance.AppliedTransition = transition;
                    instance.HasSurfaceAnchor = patchSystem.TryResolveSurfaceAnchor(
                        instance.Direction,
                        out CelestialSurfaceAnchor resolved);
                    instance.SurfaceAnchor = resolved;
                }
            }

            if (!instance.HasSurfaceAnchor)
            {
                return;
            }

            float observerDistance = Vector3.Distance(
                cameraLocalPosition,
                instance.Direction * instance.SurfaceAnchor.FineRadius);
            float surfaceRadius = instance.SurfaceAnchor.ResolveRadius(observerDistance);
            if (Mathf.Abs(surfaceRadius - instance.SurfaceRadius) < SurfaceAnchorEpsilon)
            {
                return;
            }

            Vector3 offset = instance.Direction * (surfaceRadius - instance.SurfaceRadius);
            instance.SurfaceRadius = surfaceRadius;
            instance.LocalPosition += offset;
            Matrix4x4 matrix = instance.LocalMatrix;
            matrix.m03 += offset.x;
            matrix.m13 += offset.y;
            matrix.m23 += offset.z;
            instance.LocalMatrix = matrix;
        }

        void RenderInstances(Camera camera, Vector3 cameraLocalPosition)
        {
            drawBatchCount = 0;
            Matrix4x4 rootMatrix = transform.localToWorldMatrix;
            SurfaceScatterShaderBinding binding = profile.ShaderBinding;
            PlanetSurfaceSample shadingSample = default;
            bool hasShading = binding != null &&
                binding.IsBound &&
                surfaceModel.TrySamplePlanetSurface(
                    cameraLocalPosition.normalized,
                    out shadingSample);
            surfaceAnchorRefreshBudget = SurfaceAnchorRefreshBudget;
            SurfaceVisualProfile visualProfile = ResolveVisualProfile();
            float cullingRange = 0f;
            for (int i = 0; i < ruleRuntimes.Count; i++)
            {
                SurfaceScatterDistribution distribution = ruleRuntimes[i].Rule.Distribution;
                cullingRange = Mathf.Max(
                    cullingRange,
                    distribution.ResolveReleaseDistance(distribution.FarVisibilityDistance));
            }

            Bounds cullingBounds = new(
                camera.transform.position,
                Vector3.one * (cullingRange * 2f + 64f));
            for (int runtimeIndex = 0; runtimeIndex < ruleRuntimes.Count; runtimeIndex++)
            {
                RuleRuntime runtime = ruleRuntimes[runtimeIndex];
                runtime.ClearRenderLists();
                float shadowDistanceSquared = runtime.Rule.ShadowDistance * runtime.Rule.ShadowDistance;
                foreach (DecorationInstance instance in runtime.Instances.Values)
                {
                    RefreshInstanceSurfaceAnchor(instance, cameraLocalPosition);
                    float distanceSquared = (instance.LocalPosition - cameraLocalPosition).sqrMagnitude;
                    float retentionDistance = runtime.Rule.Distribution
                        .ResolveReleaseDistance(instance.VisibilityDistance);
                    if (distanceSquared > retentionDistance * retentionDistance)
                    {
                        continue;
                    }

                    float distance = Mathf.Sqrt(distanceSquared);
                    float visibilityScale = ResolveVisibilityScale(
                        distance,
                        instance.VisibilityDistance,
                        retentionDistance);
                    if (visibilityScale <= 0.001f)
                    {
                        continue;
                    }

                    Matrix4x4 worldMatrix = rootMatrix * instance.LocalMatrix *
                        Matrix4x4.Scale(Vector3.one * visibilityScale);
                    bool castsShadow = runtime.Rule.ShadowDistance > 0f && distanceSquared <= shadowDistanceSquared;
                    bool usesFarLod = instance.ResolveFarLod(
                        distance,
                        runtime.Rule.FarLodStartDistance);
                    runtime.GetRenderList(instance.VariantIndex, castsShadow, usesFarLod).Add(worldMatrix);
                }

                MaterialPropertyBlock ruleBlock = null;
                if (hasShading && runtime.Rule.SurfaceTintStrength > 0f)
                {
                    shadingBlock ??= new MaterialPropertyBlock();
                    shadingBlock.Clear();
                    binding.Apply(
                        shadingBlock,
                        visualProfile,
                        shadingSample,
                        runtime.Rule.SurfaceTintStrength,
                        runtime.Rule.SnowResponse,
                        runtime.Rule.MossResponse);
                    ruleBlock = shadingBlock;
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
                        cullingBounds,
                        variant,
                        variant.NearMesh,
                        runtime.NearShadowMatrices[variantIndex],
                        true,
                        ruleBlock);
                    DrawVariant(
                        camera,
                        cullingBounds,
                        variant,
                        variant.NearMesh,
                        runtime.NearMatrices[variantIndex],
                        false,
                        ruleBlock);
                    DrawVariant(
                        camera,
                        cullingBounds,
                        variant,
                        variant.FarMesh,
                        runtime.FarMatrices[variantIndex],
                        false,
                        ruleBlock);
                }
            }
        }

        SurfaceVisualProfile ResolveVisualProfile()
        {
            return bodyVisual != null &&
                bodyVisual.SurfaceProfile is TerrestrialSurfaceProfile terrestrial
                    ? terrestrial.SurfaceVisualProfile
                    : null;
        }

        void DrawVariant(
            Camera camera,
            Bounds cullingBounds,
            SurfaceDecorationVariant variant,
            Mesh mesh,
            List<Matrix4x4> matrices,
            bool castsShadows,
            MaterialPropertyBlock propertyBlock)
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
                    shadowCastingMode = castsShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    matProps = propertyBlock,
                    worldBounds = cullingBounds
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
                        SurfaceScatterPlacement.CalculateResolution(
                            body.Radius,
                            rule.Distribution.SpacingMeters)));
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
            float cameraSpeed = 0f;
            if (hasPreviousCameraPosition && Time.unscaledDeltaTime > 0.0001f)
            {
                Vector3 velocity =
                    (cameraLocalPosition - previousCameraLocalPosition) / Time.unscaledDeltaTime;
                observerLocalVelocity = Vector3.Lerp(
                    observerLocalVelocity,
                    velocity,
                    1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
                cameraSpeed = observerLocalVelocity.magnitude;
            }

            previousCameraLocalPosition = cameraLocalPosition;
            hasPreviousCameraPosition = true;

            Rigidbody observer = patchSystem != null ? patchSystem.CollisionObserverRigidbody : null;
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

        internal static bool IsCellReleased(
            Vector3 cellCenter,
            Vector3 anchorLocalPosition,
            Vector3 scanCenter,
            float releaseDistance)
        {
            float reach = Mathf.Min(
                Vector3.Distance(cellCenter, anchorLocalPosition),
                Vector3.Distance(cellCenter, scanCenter));
            return reach > releaseDistance;
        }

        internal static float ResolveVisibilityScale(
            float distance,
            float visibilityDistance,
            float releaseDistance)
        {
            if (distance <= visibilityDistance || releaseDistance <= visibilityDistance)
            {
                return 1f;
            }

            return Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(releaseDistance, visibilityDistance, distance));
        }

        internal static bool ResolveFarLod(
            bool currentlyFar,
            float distance,
            float switchDistance)
        {
            if (switchDistance <= 0f)
            {
                return true;
            }

            return currentlyFar
                ? distance >= switchDistance * 0.9f
                : distance >= switchDistance * 1.1f;
        }

        internal static Quaternion ResolveRestingRotation(
            Bounds bounds,
            Quaternion rotation,
            Vector3 placementUp)
        {
            Vector3 extents = bounds.extents;
            Vector3 longAxis = extents.x >= extents.y && extents.x >= extents.z
                ? Vector3.right
                : extents.y >= extents.z ? Vector3.up : Vector3.forward;
            float longest = Vector3.Dot(extents, new Vector3(Mathf.Abs(longAxis.x), Mathf.Abs(longAxis.y), Mathf.Abs(longAxis.z)));
            float shortest = Mathf.Min(extents.x, Mathf.Min(extents.y, extents.z));
            if (longest < shortest * TallVariantAspect)
            {
                return rotation;
            }

            Vector3 up = placementUp.normalized;
            Vector3 worldLongAxis = rotation * longAxis;
            if (Mathf.Abs(Vector3.Dot(worldLongAxis, up)) < UprightLongAxisCosine)
            {
                return rotation;
            }

            Vector3 flat = Vector3.ProjectOnPlane(worldLongAxis, up);
            if (flat.sqrMagnitude < 0.0001f)
            {
                flat = Vector3.ProjectOnPlane(rotation * Vector3.forward, up);
            }

            return Quaternion.FromToRotation(worldLongAxis, flat.normalized) * rotation;
        }

        internal static float ResolveMeshGroundingOffset(
            Bounds bounds,
            Quaternion rotation,
            Vector3 placementUp,
            float scale,
            float embedFraction = 0f)
        {
            Vector3 localUp = Quaternion.Inverse(rotation) * placementUp.normalized;
            Vector3 absoluteUp = new(
                Mathf.Abs(localUp.x),
                Mathf.Abs(localUp.y),
                Mathf.Abs(localUp.z));
            float halfExtent = Vector3.Dot(bounds.extents, absoluteUp);
            float lowestPoint = Vector3.Dot(bounds.center, localUp) - halfExtent;
            float embed = halfExtent * 2f * Mathf.Clamp01(embedFraction);
            return (-lowestPoint - embed) * Mathf.Max(0f, scale);
        }

        void ResolveComponents()
        {
            body ??= GetComponent<CelestialBody>();
            surfaceModel ??= GetComponent<PlanetSurfaceModel>();
            patchSystem ??= GetComponent<CelestialSurfacePatchSystem>();
            formationSpawner ??= GetComponent<SurfaceFormationSpawner>();
            bodyVisual ??= GetComponent<CelestialBodyVisual>();
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

        readonly struct PendingCell
        {
            public PendingCell(SurfaceScatterCell cell, float reachSquared)
            {
                Cell = cell;
                ReachSquared = reachSquared;
            }

            public SurfaceScatterCell Cell { get; }
            public float ReachSquared { get; }
        }

        sealed class DecorationInstance
        {
            public DecorationInstance(
                int variantIndex,
                Vector3 direction,
                float surfaceRadius,
                Vector3 localPosition,
                Matrix4x4 localMatrix,
                float visibilityDistance)
            {
                VariantIndex = variantIndex;
                Direction = direction;
                SurfaceRadius = surfaceRadius;
                LocalPosition = localPosition;
                LocalMatrix = localMatrix;
                VisibilityDistance = visibilityDistance;
                AppliedTransition = -1;
            }

            public int VariantIndex { get; }
            public Vector3 Direction { get; }
            public float SurfaceRadius { get; set; }
            public Vector3 LocalPosition { get; set; }
            public Matrix4x4 LocalMatrix { get; set; }
            public float VisibilityDistance { get; }
            public int AppliedTransition { get; set; }
            public bool HasSurfaceAnchor { get; set; }
            public CelestialSurfaceAnchor SurfaceAnchor { get; set; }

            public bool ResolveFarLod(float distance, float switchDistance)
            {
                usesFarLod = SurfaceDecorationRenderer.ResolveFarLod(
                    usesFarLod,
                    distance,
                    switchDistance);
                return usesFarLod;
            }

            bool usesFarLod;
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
            public bool ScanActive { get; set; }
            public Vector3 ScanCenter { get; set; }
            public Vector3 ScanDirection { get; set; }
            public Vector3 ScanTangent { get; set; }
            public Vector3 ScanBitangent { get; set; }
            public float ScanRadius { get; set; }
            public float ScanStep { get; set; }
            public float ScanDistanceSquared { get; set; }
            public int ScanPlanetSeed { get; set; }
            public int ScanRadiusSteps { get; set; }
            public int ScanX { get; set; }
            public int ScanY { get; set; }
            public HashSet<SurfaceScatterCell> DesiredCells { get; } = new();
            public HashSet<SurfaceScatterCell> EvaluatedCells { get; } = new();
            internal List<PendingCell> PendingCells { get; } = new();
            public Dictionary<SurfaceScatterCell, DecorationInstance> Instances { get; } = new();
            public List<SurfaceScatterCell> StaleCells { get; } = new();
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
