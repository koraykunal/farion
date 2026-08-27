using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [CreateAssetMenu(
        menuName = "Farion/Rendering/Celestial/Surface Patch Profile",
        fileName = "SO_CelestialSurfacePatchProfile")]
    public sealed class CelestialSurfacePatchProfile : ScriptableObject
    {
        public const int MaximumSubdivisionLevel = 14;

        const float EnterRadiusFraction = 0.5f;
        const float ExitRadiusFraction = 0.75f;

        [Header("Surface Mode")]
        [Tooltip("Patches take over from the global body mesh below this altitude, expressed as a multiple of the body's terrain relief. Relief-relative so the same profile behaves correctly on an asteroid and on a gas giant.")]
        [Min(1f)]
        [SerializeField] float enterAltitudeReliefMultiple = 32f;
        [Tooltip("Patches are released above this altitude. Must exceed the enter multiple so the transition does not oscillate.")]
        [Min(1f)]
        [SerializeField] float exitAltitudeReliefMultiple = 48f;
        [Tooltip("Floor applied to both altitudes so a near-smooth body still switches to patches before the observer reaches its surface.")]
        [Min(1f)]
        [SerializeField] float minimumSurfaceAltitudeMeters = 250f;

        [Header("Patch Detail")]
        [Range(4, 32)]
        [SerializeField] int patchResolution = 16;
        [Tooltip("Triangle edge length the deepest patches aim for, in metres. Subdivision depth is derived from this and the body radius, so terrain fidelity stays constant across body sizes.")]
        [Min(0.05f)]
        [SerializeField] float targetTriangleEdgeMeters = 2.5f;
        [Min(0.1f)]
        [SerializeField] float splitDistanceMultiplier = 2.2f;
        [Range(0f, 0.5f)]
        [SerializeField] float splitHysteresisRatio = 0.15f;
        [Range(64, 4096)]
        [SerializeField] int maxActivePatches = 1024;

        [Header("Collision")]
        [Tooltip("Coarsest triangle edge, in metres, that may carry a collider. Patches finer than this also collide; patches coarser than this never do. Sets how early collision geometry can be prepared during a descent.")]
        [Min(0.05f)]
        [SerializeField] float collisionTriangleEdgeMeters = 25f;
        [Tooltip("Surface radius around the collision observer that is kept collidable, in metres.")]
        [Min(0f)]
        [SerializeField] float collisionRadiusMeters = 400f;
        [SerializeField] bool bakeCollisionMeshes = true;
        [Min(0.05f)]
        [SerializeField] float collisionPredictionSeconds = 0.75f;
        [Tooltip("Extra collidable surface distance required beyond the predicted path, in metres.")]
        [Min(0f)]
        [SerializeField] float collisionSafetyMarginMeters = 24f;

        [Header("Refresh")]
        [Tooltip("Surface distance an observer must travel before the patch set is recomputed, in metres.")]
        [Min(0f)]
        [SerializeField] float observerMoveThresholdMeters = 2f;
        [Range(0f, 10f)]
        [SerializeField] float observerAngleThresholdDegrees = 0.08f;

        [Header("Runtime Build Budget")]
        [Range(1, 32)]
        [SerializeField] int maximumPatchBuildsPerFrame = 6;
        [Range(0.25f, 8f)]
        [SerializeField] float patchBuildBudgetMilliseconds = 2f;

        public int PatchResolution => Mathf.Clamp(patchResolution, 4, 32) & ~1;
        public float TargetTriangleEdgeMeters => Mathf.Max(0.05f, targetTriangleEdgeMeters);
        public float CollisionTriangleEdgeMeters =>
            Mathf.Max(TargetTriangleEdgeMeters, collisionTriangleEdgeMeters);
        public float SplitDistanceMultiplier => Mathf.Max(0.1f, splitDistanceMultiplier);
        public float SplitHysteresisRatio => Mathf.Clamp(splitHysteresisRatio, 0f, 0.5f);
        public int MaxActivePatches => Mathf.Clamp(maxActivePatches, 64, 4096);
        public float CollisionRadiusMeters => Mathf.Max(0f, collisionRadiusMeters);
        public bool BakeCollisionMeshes => bakeCollisionMeshes;
        public float CollisionPredictionSeconds => Mathf.Max(0.05f, collisionPredictionSeconds);
        public float CollisionSafetyMarginMeters => Mathf.Max(0f, collisionSafetyMarginMeters);
        public float ObserverMoveThresholdMeters => Mathf.Max(0f, observerMoveThresholdMeters);
        public float ObserverAngleThresholdDegrees =>
            Mathf.Clamp(observerAngleThresholdDegrees, 0f, 10f);
        public int MaximumPatchBuildsPerFrame => Mathf.Clamp(maximumPatchBuildsPerFrame, 1, 32);
        public float PatchBuildBudgetMilliseconds =>
            Mathf.Clamp(patchBuildBudgetMilliseconds, 0.25f, 8f);

        public int ResolveSubdivisionLevel(float baseRadius)
        {
            return ResolveLevelForTriangleEdge(baseRadius, TargetTriangleEdgeMeters);
        }

        public int ResolveCollisionLevel(float baseRadius)
        {
            return Mathf.Min(
                ResolveSubdivisionLevel(baseRadius),
                ResolveLevelForTriangleEdge(baseRadius, CollisionTriangleEdgeMeters));
        }

        public float ResolveEnterAltitude(float reliefMeters, float baseRadius)
        {
            float altitude = Mathf.Max(
                minimumSurfaceAltitudeMeters,
                Mathf.Max(0f, reliefMeters) * Mathf.Max(1f, enterAltitudeReliefMultiple));
            return Mathf.Min(altitude, Mathf.Max(0.01f, baseRadius) * EnterRadiusFraction);
        }

        public float ResolveExitAltitude(float reliefMeters, float baseRadius)
        {
            float altitude = Mathf.Max(
                ResolveEnterAltitude(reliefMeters, baseRadius),
                Mathf.Max(0f, reliefMeters) * Mathf.Max(1f, exitAltitudeReliefMultiple));
            return Mathf.Max(
                ResolveEnterAltitude(reliefMeters, baseRadius),
                Mathf.Min(altitude, Mathf.Max(0.01f, baseRadius) * ExitRadiusFraction));
        }

        public float ResolveTriangleEdgeMeters(float baseRadius, int level)
        {
            return FaceArcLength(baseRadius) / ((1 << Mathf.Max(0, level)) * PatchResolution);
        }

        int ResolveLevelForTriangleEdge(float baseRadius, float triangleEdgeMeters)
        {
            float patchesPerFaceEdge =
                FaceArcLength(baseRadius) / (PatchResolution * Mathf.Max(0.05f, triangleEdgeMeters));
            if (patchesPerFaceEdge <= 1f)
            {
                return 0;
            }

            return Mathf.Clamp(
                Mathf.CeilToInt(Mathf.Log(patchesPerFaceEdge, 2f)),
                0,
                MaximumSubdivisionLevel);
        }

        static float FaceArcLength(float baseRadius)
        {
            return Mathf.PI * 0.5f * Mathf.Max(0.01f, baseRadius);
        }

        void OnValidate()
        {
            enterAltitudeReliefMultiple = Mathf.Max(1f, enterAltitudeReliefMultiple);
            exitAltitudeReliefMultiple =
                Mathf.Max(enterAltitudeReliefMultiple, exitAltitudeReliefMultiple);
            minimumSurfaceAltitudeMeters = Mathf.Max(1f, minimumSurfaceAltitudeMeters);
            patchResolution = Mathf.Clamp(patchResolution, 4, 32) & ~1;
            targetTriangleEdgeMeters = Mathf.Max(0.05f, targetTriangleEdgeMeters);
            collisionTriangleEdgeMeters =
                Mathf.Max(targetTriangleEdgeMeters, collisionTriangleEdgeMeters);
            splitDistanceMultiplier = Mathf.Max(0.1f, splitDistanceMultiplier);
            splitHysteresisRatio = Mathf.Clamp(splitHysteresisRatio, 0f, 0.5f);
            maxActivePatches = Mathf.Clamp(maxActivePatches, 64, 4096);
            collisionRadiusMeters = Mathf.Max(0f, collisionRadiusMeters);
            collisionPredictionSeconds = Mathf.Max(0.05f, collisionPredictionSeconds);
            collisionSafetyMarginMeters = Mathf.Max(0f, collisionSafetyMarginMeters);
            observerMoveThresholdMeters = Mathf.Max(0f, observerMoveThresholdMeters);
            observerAngleThresholdDegrees = Mathf.Clamp(observerAngleThresholdDegrees, 0f, 10f);
            maximumPatchBuildsPerFrame = Mathf.Clamp(maximumPatchBuildsPerFrame, 1, 32);
            patchBuildBudgetMilliseconds = Mathf.Clamp(patchBuildBudgetMilliseconds, 0.25f, 8f);
        }
    }
}
