using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [CreateAssetMenu(
        menuName = "Farion/Rendering/Celestial/Surface Patch Profile",
        fileName = "SO_CelestialSurfacePatchProfile")]
    public sealed class CelestialSurfacePatchProfile : ScriptableObject
    {
        [Header("Activation")]
        [Min(0f)]
        [SerializeField] float enterAltitudeRatio = 0.7f;
        [Min(0f)]
        [SerializeField] float exitAltitudeRatio = 0.9f;

        [Header("Patch Detail")]
        [Range(4, 32)]
        [SerializeField] int patchResolution = 16;
        [Range(0, 7)]
        [SerializeField] int maxSubdivisionLevel = 6;
        [Min(0.1f)]
        [SerializeField] float splitDistanceMultiplier = 1.5f;
        [Range(0f, 0.5f)]
        [SerializeField] float splitHysteresisRatio = 0.15f;
        [Range(64, 2048)]
        [SerializeField] int maxActivePatches = 384;
        [Min(0f)]
        [SerializeField] float skirtDepthRatio = 0.0025f;

        [Header("Collision")]
        [Min(0f)]
        [SerializeField] float collisionRadiusRatio = 0.16f;
        [Range(0, 7)]
        [SerializeField] int minimumCollisionLevel = 6;
        [SerializeField] bool bakeCollisionMeshes = true;
        [Min(0.05f)]
        [SerializeField] float collisionPredictionSeconds = 0.75f;
        [Range(0.1f, 1f)]
        [SerializeField] float collisionCoverageSafetyRatio = 0.5f;
        [Range(0f, 0.1f)]
        [SerializeField] float collisionSafetyMarginRatio = 0.015f;

        [Header("Refresh")]
        [Min(0f)]
        [SerializeField] float observerMoveThresholdRatio = 0.001f;
        [Range(0f, 10f)]
        [SerializeField] float observerAngleThresholdDegrees = 0.08f;

        [Header("Runtime Build Budget")]
        [Range(1, 32)]
        [SerializeField] int maximumPatchBuildsPerFrame = 6;
        [Range(0.25f, 8f)]
        [SerializeField] float patchBuildBudgetMilliseconds = 2f;

        public float EnterAltitudeRatio => Mathf.Max(0f, enterAltitudeRatio);
        public float ExitAltitudeRatio => Mathf.Max(EnterAltitudeRatio, exitAltitudeRatio);
        public int PatchResolution => Mathf.Clamp(patchResolution, 4, 32);
        public int MaxSubdivisionLevel => Mathf.Clamp(maxSubdivisionLevel, 0, 7);
        public float SplitDistanceMultiplier => Mathf.Max(0.1f, splitDistanceMultiplier);
        public float SplitHysteresisRatio => Mathf.Clamp(splitHysteresisRatio, 0f, 0.5f);
        public int MaxActivePatches => Mathf.Clamp(maxActivePatches, 64, 2048);
        public float SkirtDepthRatio => Mathf.Max(0f, skirtDepthRatio);
        public float CollisionRadiusRatio => Mathf.Max(0f, collisionRadiusRatio);
        public int MinimumCollisionLevel => Mathf.Clamp(minimumCollisionLevel, 0, MaxSubdivisionLevel);
        public bool BakeCollisionMeshes => bakeCollisionMeshes;
        public float CollisionPredictionSeconds => Mathf.Max(0.05f, collisionPredictionSeconds);
        public float CollisionCoverageSafetyRatio =>
            Mathf.Clamp(collisionCoverageSafetyRatio, 0.1f, 1f);
        public float CollisionSafetyMarginRatio =>
            Mathf.Clamp(collisionSafetyMarginRatio, 0f, 0.1f);
        public float ObserverMoveThresholdRatio => Mathf.Max(0f, observerMoveThresholdRatio);
        public float ObserverAngleThresholdDegrees => Mathf.Clamp(observerAngleThresholdDegrees, 0f, 10f);
        public int MaximumPatchBuildsPerFrame => Mathf.Clamp(maximumPatchBuildsPerFrame, 1, 32);
        public float PatchBuildBudgetMilliseconds => Mathf.Clamp(patchBuildBudgetMilliseconds, 0.25f, 8f);

        void OnValidate()
        {
            enterAltitudeRatio = Mathf.Max(0f, enterAltitudeRatio);
            exitAltitudeRatio = Mathf.Max(enterAltitudeRatio, exitAltitudeRatio);
            patchResolution = Mathf.Clamp(patchResolution, 4, 32);
            maxSubdivisionLevel = Mathf.Clamp(maxSubdivisionLevel, 0, 7);
            splitDistanceMultiplier = Mathf.Max(0.1f, splitDistanceMultiplier);
            splitHysteresisRatio = Mathf.Clamp(splitHysteresisRatio, 0f, 0.5f);
            maxActivePatches = Mathf.Clamp(maxActivePatches, 64, 2048);
            skirtDepthRatio = Mathf.Max(0f, skirtDepthRatio);
            collisionRadiusRatio = Mathf.Max(0f, collisionRadiusRatio);
            minimumCollisionLevel = Mathf.Clamp(minimumCollisionLevel, 0, maxSubdivisionLevel);
            collisionPredictionSeconds = Mathf.Max(0.05f, collisionPredictionSeconds);
            collisionCoverageSafetyRatio = Mathf.Clamp(collisionCoverageSafetyRatio, 0.1f, 1f);
            collisionSafetyMarginRatio = Mathf.Clamp(collisionSafetyMarginRatio, 0f, 0.1f);
            observerMoveThresholdRatio = Mathf.Max(0f, observerMoveThresholdRatio);
            observerAngleThresholdDegrees = Mathf.Clamp(observerAngleThresholdDegrees, 0f, 10f);
            maximumPatchBuildsPerFrame = Mathf.Clamp(maximumPatchBuildsPerFrame, 1, 32);
            patchBuildBudgetMilliseconds = Mathf.Clamp(patchBuildBudgetMilliseconds, 0.25f, 8f);
        }
    }
}
