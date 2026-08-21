using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Gameplay.Actors
{
    public static class CelestialSurfaceSettling
    {
        public static bool TrySettle(
            CelestialFrameProvider frameProvider,
            Transform target)
        {
            CelestialSurfacePlacementOptions options = new(
                0f,
                0f,
                CelestialSurfacePlacementMode.TerrainSurface);
            if (target == null ||
                !CelestialSurfacePlacement.TryResolvePose(
                    frameProvider,
                    target.position,
                    Vector3.zero,
                    target.forward,
                    options,
                    out CelestialSurfacePlacementResult placement))
            {
                return false;
            }

            target.rotation = placement.Rotation;
            target.position = placement.Position +
                placement.PlacementUp *
                MeasureGroundOffset(target, placement.PlacementUp);
            return true;
        }

        static float MeasureGroundOffset(Transform target, Vector3 up)
        {
            Collider[] colliders = target.GetComponentsInChildren<Collider>();
            float lowest = 0f;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || collider.isTrigger || !collider.enabled)
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                float reach = Mathf.Abs(up.x) * bounds.extents.x +
                    Mathf.Abs(up.y) * bounds.extents.y +
                    Mathf.Abs(up.z) * bounds.extents.z;
                lowest = Mathf.Min(
                    lowest,
                    Vector3.Dot(bounds.center - target.position, up) - reach);
            }

            return -lowest;
        }
    }
}
