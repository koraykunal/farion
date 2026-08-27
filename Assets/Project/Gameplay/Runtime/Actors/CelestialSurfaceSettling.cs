using Farion.Core.Physics;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using UnityEngine;

namespace Farion.Gameplay.Actors
{
    public static class CelestialSurfaceSettling
    {
        const float ProbeStartHeight = 400f;
        const float ProbeLength = 1200f;
        static readonly RaycastHit[] ProbeHits = new RaycastHit[16];

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
            Physics.SyncTransforms();

            Vector3 groundPoint = ResolveCollisionGroundPoint(
                target,
                placement.Frame.Body,
                placement.Position,
                placement.PlacementUp);
            target.position = groundPoint +
                placement.PlacementUp *
                MeasureGroundOffset(target, placement.PlacementUp);
            Physics.SyncTransforms();
            return true;
        }

        static Vector3 ResolveCollisionGroundPoint(
            Transform target,
            CelestialBody body,
            Vector3 analyticPoint,
            Vector3 up)
        {
            Vector3 origin = analyticPoint + up * ProbeStartHeight;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                -up,
                ProbeHits,
                ProbeLength,
                FarionLayers.GroundMask,
                QueryTriggerInteraction.Ignore);

            Transform bodyRoot = body != null ? body.transform : null;
            float bestDistance = float.PositiveInfinity;
            Vector3 bestPoint = analyticPoint;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = ProbeHits[i];
                if (hit.collider == null ||
                    hit.collider.transform.IsChildOf(target) ||
                    hit.distance >= bestDistance ||
                    IsBodyTerrainCollider(hit.collider, bodyRoot) ||
                    Vector3.Dot(hit.point - analyticPoint, up) < 0f)
                {
                    continue;
                }

                bestDistance = hit.distance;
                bestPoint = hit.point;
            }

            return bestPoint;
        }

        static bool IsBodyTerrainCollider(Collider collider, Transform bodyRoot)
        {
            return bodyRoot != null &&
                collider is MeshCollider &&
                collider.transform.IsChildOf(bodyRoot);
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

                lowest = Mathf.Min(
                    lowest,
                    Vector3.Dot(ResolveCenter(collider) - target.position, up) -
                        ResolveSupportReach(collider, up));
            }

            return -lowest;
        }

        static Vector3 ResolveCenter(Collider collider)
        {
            return collider switch
            {
                BoxCollider box => box.transform.TransformPoint(box.center),
                SphereCollider sphere => sphere.transform.TransformPoint(sphere.center),
                CapsuleCollider capsule => capsule.transform.TransformPoint(capsule.center),
                _ => collider.bounds.center
            };
        }

        static float ResolveSupportReach(Collider collider, Vector3 up)
        {
            Transform colliderTransform = collider.transform;
            Vector3 scale = colliderTransform.lossyScale;
            switch (collider)
            {
                case BoxCollider box:
                {
                    Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, Abs(scale));
                    return Mathf.Abs(Vector3.Dot(up, colliderTransform.right)) * halfExtents.x +
                        Mathf.Abs(Vector3.Dot(up, colliderTransform.up)) * halfExtents.y +
                        Mathf.Abs(Vector3.Dot(up, colliderTransform.forward)) * halfExtents.z;
                }

                case SphereCollider sphere:
                    return sphere.radius * MaxAbsComponent(scale);

                case CapsuleCollider capsule:
                {
                    Vector3 axis = capsule.direction switch
                    {
                        0 => colliderTransform.right,
                        1 => colliderTransform.up,
                        _ => colliderTransform.forward
                    };
                    float radiusScale = capsule.direction switch
                    {
                        0 => Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)),
                        1 => Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)),
                        _ => Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y))
                    };
                    float heightScale = Mathf.Abs(capsule.direction switch
                    {
                        0 => scale.x,
                        1 => scale.y,
                        _ => scale.z
                    });
                    float radius = capsule.radius * radiusScale;
                    float halfCylinder = Mathf.Max(
                        0f,
                        capsule.height * 0.5f * heightScale - radius);
                    return halfCylinder * Mathf.Abs(Vector3.Dot(up, axis)) + radius;
                }

                default:
                {
                    Vector3 extents = collider.bounds.extents;
                    return Mathf.Abs(up.x) * extents.x +
                        Mathf.Abs(up.y) * extents.y +
                        Mathf.Abs(up.z) * extents.z;
                }
            }
        }

        static Vector3 Abs(Vector3 value)
        {
            return new Vector3(
                Mathf.Abs(value.x),
                Mathf.Abs(value.y),
                Mathf.Abs(value.z));
        }

        static float MaxAbsComponent(Vector3 value)
        {
            return Mathf.Max(
                Mathf.Abs(value.x),
                Mathf.Max(Mathf.Abs(value.y), Mathf.Abs(value.z)));
        }
    }
}
