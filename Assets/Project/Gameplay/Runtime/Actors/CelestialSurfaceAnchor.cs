using Farion.Core.Physics;
using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Gameplay.Actors
{
    public enum CelestialSurfaceAnchorUpdateMode
    {
        LockedSurfacePose = 0,
        ResampleSurfaceEveryFrame = 1
    }

    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class CelestialSurfaceAnchor : MonoBehaviour
    {
        [Header("Surface")]
        [SerializeField] CelestialBody body;
        [SerializeField] MonoBehaviour surfaceProvider;
        [Min(0f)]
        [SerializeField] float surfaceOffset = 0.25f;

        [Header("Captured Pose")]
        [SerializeField] Vector3 localSurfaceDirection = Vector3.up;
        [SerializeField] Vector3 localSurfaceNormal = Vector3.up;
        [SerializeField] Vector3 localForward = Vector3.forward;
        [Min(0.01f)]
        [SerializeField] float capturedSurfaceRadius = 1f;

        [Header("Runtime")]
        [SerializeField] CelestialSurfaceAnchorUpdateMode updateMode = CelestialSurfaceAnchorUpdateMode.LockedSurfacePose;
        [SerializeField] bool alignToSurface = true;
        [SerializeField] bool applyInEditMode = true;

        ICelestialSurfaceProvider resolvedSurfaceProvider;

        void Awake()
        {
            ResolveSurfaceProvider();
            if (body != null && localSurfaceDirection.sqrMagnitude <= 0.0001f)
            {
                CaptureCurrentPose();
            }
        }

        void OnValidate()
        {
            surfaceOffset = Mathf.Max(0f, surfaceOffset);
            capturedSurfaceRadius = Mathf.Max(0.01f, capturedSurfaceRadius);
            if (surfaceProvider != null && surfaceProvider is not ICelestialSurfaceProvider)
            {
                surfaceProvider = null;
            }
        }

        void LateUpdate()
        {
            if (Application.isPlaying || applyInEditMode)
            {
                ApplyAnchorPose();
            }
        }

        [ContextMenu("Capture Current Pose")]
        public void CaptureCurrentPose()
        {
            if (body == null)
            {
                return;
            }

            Vector3 centerToObject = transform.position - body.Position;
            Vector3 worldDirection = centerToObject.sqrMagnitude > 0.0001f
                ? centerToObject.normalized
                : body.transform.up;

            localSurfaceDirection = body.transform.InverseTransformDirection(worldDirection).normalized;

            Vector3 worldForward = Vector3.ProjectOnPlane(transform.forward, worldDirection);
            if (worldForward.sqrMagnitude <= 0.0001f)
            {
                worldForward = Vector3.ProjectOnPlane(body.transform.forward, worldDirection);
            }

            if (worldForward.sqrMagnitude <= 0.0001f)
            {
                worldForward = Vector3.ProjectOnPlane(Vector3.forward, worldDirection);
            }

            localForward = body.transform.InverseTransformDirection(worldForward.normalized).normalized;

            if (TrySampleSurface(worldDirection, out CelestialSurfaceSample sample))
            {
                surfaceOffset = Mathf.Max(0f, Vector3.Dot(transform.position - sample.Point, sample.Normal));
                capturedSurfaceRadius = Mathf.Max(0.01f, Vector3.Distance(body.Position, sample.Point));
                localSurfaceNormal = body.transform.InverseTransformDirection(sample.Normal).normalized;
            }
            else
            {
                surfaceOffset = Mathf.Max(0f, centerToObject.magnitude - body.Radius);
                capturedSurfaceRadius = Mathf.Max(0.01f, body.Radius);
                localSurfaceNormal = localSurfaceDirection;
            }
        }

        [ContextMenu("Apply Anchor Pose")]
        public void ApplyAnchorPose()
        {
            if (body == null)
            {
                return;
            }

            Vector3 worldDirection = body.transform.TransformDirection(ResolveLocalSurfaceDirection()).normalized;
            Vector3 surfaceNormal = body.transform.TransformDirection(ResolveLocalSurfaceNormal()).normalized;
            Vector3 surfacePoint = body.Position + worldDirection * ResolveSurfaceRadius();

            if (updateMode == CelestialSurfaceAnchorUpdateMode.ResampleSurfaceEveryFrame &&
                TrySampleSurface(worldDirection, out CelestialSurfaceSample sample))
            {
                surfacePoint = sample.Point;
                surfaceNormal = sample.Normal;
            }

            transform.position = surfacePoint + surfaceNormal * surfaceOffset;

            if (!alignToSurface)
            {
                return;
            }

            Vector3 worldForward = body.transform.TransformDirection(ResolveLocalForward());
            worldForward = Vector3.ProjectOnPlane(worldForward, surfaceNormal);
            if (worldForward.sqrMagnitude <= 0.0001f)
            {
                worldForward = Vector3.ProjectOnPlane(body.transform.forward, surfaceNormal);
            }

            if (worldForward.sqrMagnitude <= 0.0001f)
            {
                worldForward = Vector3.ProjectOnPlane(Vector3.forward, surfaceNormal);
            }

            transform.rotation = Quaternion.LookRotation(worldForward.normalized, surfaceNormal);
        }

        bool TrySampleSurface(Vector3 worldDirection, out CelestialSurfaceSample sample)
        {
            ResolveSurfaceProvider();
            if (body != null && resolvedSurfaceProvider != null)
            {
                Vector3 probePosition = body.Position + worldDirection.normalized * body.Radius;
                return resolvedSurfaceProvider.TrySampleSurface(body, probePosition, out sample);
            }

            sample = default;
            return false;
        }

        void ResolveSurfaceProvider()
        {
            if (surfaceProvider is ICelestialSurfaceProvider explicitProvider)
            {
                resolvedSurfaceProvider = explicitProvider;
                return;
            }

            resolvedSurfaceProvider = body != null
                ? body.GetComponent<ICelestialSurfaceProvider>()
                : null;
        }

        Vector3 ResolveLocalSurfaceDirection()
        {
            return localSurfaceDirection.sqrMagnitude > 0.0001f ? localSurfaceDirection.normalized : Vector3.up;
        }

        Vector3 ResolveLocalSurfaceNormal()
        {
            return localSurfaceNormal.sqrMagnitude > 0.0001f ? localSurfaceNormal.normalized : ResolveLocalSurfaceDirection();
        }

        Vector3 ResolveLocalForward()
        {
            return localForward.sqrMagnitude > 0.0001f ? localForward.normalized : Vector3.forward;
        }

        float ResolveSurfaceRadius()
        {
            if (capturedSurfaceRadius > 0.01f)
            {
                return capturedSurfaceRadius;
            }

            return body != null ? Mathf.Max(0.01f, body.Radius) : 1f;
        }
    }
}
