using System;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [Serializable]
    public sealed class SurfaceScatterDistribution
    {
        [SerializeField, Min(0.25f)] float spacingMeters = 8f;
        [SerializeField, Range(0f, 1f)] float spawnChance = 0.35f;
        [SerializeField, Min(0.01f)] float clusterScaleMeters = 60f;
        [SerializeField, Range(0f, 1f)] float clusterCutoff = 0.42f;
        [SerializeField, Range(0.001f, 0.5f)] float clusterFeather = 0.16f;

        [Header("Visibility Ladder")]
        [SerializeField, Min(1f)] float nearVisibilityDistance = 120f;
        [SerializeField, Min(1f)] float farVisibilityDistance = 320f;
        [SerializeField, Range(1.02f, 2f)] float releaseHysteresis = 1.15f;

        public float SpacingMeters => Mathf.Max(0.25f, spacingMeters);
        public float SpawnChance => Mathf.Clamp01(spawnChance);
        public float ClusterScaleMeters => Mathf.Max(0.01f, clusterScaleMeters);
        public float ClusterCutoff => Mathf.Clamp01(clusterCutoff);
        public float ClusterFeather => Mathf.Clamp(clusterFeather, 0.001f, 0.5f);
        public float NearVisibilityDistance => Mathf.Max(1f, nearVisibilityDistance);
        public float FarVisibilityDistance =>
            Mathf.Max(NearVisibilityDistance, farVisibilityDistance);
        public float ReleaseHysteresis => Mathf.Clamp(releaseHysteresis, 1.02f, 2f);

        public float ResolveReleaseDistance(float visibilityDistance)
        {
            return visibilityDistance * ReleaseHysteresis;
        }

        internal void Validate()
        {
            spacingMeters = Mathf.Max(0.25f, spacingMeters);
            spawnChance = Mathf.Clamp01(spawnChance);
            clusterScaleMeters = Mathf.Max(0.01f, clusterScaleMeters);
            clusterCutoff = Mathf.Clamp01(clusterCutoff);
            clusterFeather = Mathf.Clamp(clusterFeather, 0.001f, 0.5f);
            nearVisibilityDistance = Mathf.Max(1f, nearVisibilityDistance);
            farVisibilityDistance = Mathf.Max(nearVisibilityDistance, farVisibilityDistance);
            releaseHysteresis = Mathf.Clamp(releaseHysteresis, 1.02f, 2f);
        }
    }
}
