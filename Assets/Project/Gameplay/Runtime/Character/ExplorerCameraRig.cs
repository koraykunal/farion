using Farion.Core.Numerics;
using Farion.Core.Physics;
using Farion.Gameplay.Input;
using UnityEngine;

namespace Farion.Gameplay.Character
{
    [DefaultExecutionOrder(70)]
    [DisallowMultipleComponent]
    public sealed class ExplorerCameraRig : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] ExplorerMotor target;
        [SerializeField] ExplorerInput inputSource;
        [SerializeField] Camera viewCamera;

        [Header("Orbit")]
        [Tooltip("Height of the orbit pivot above the character origin, in metres.")]
        [SerializeField] float pivotHeight = 0.5f;
        [SerializeField] float minimumPitch = -55f;
        [SerializeField] float maximumPitch = 75f;

        [Header("Explore")]
        [Min(0.1f)]
        [SerializeField] float exploreDistance = 3.2f;
        [Tooltip("Sideways offset of the pivot in camera space; positive looks over the right shoulder.")]
        [SerializeField] float exploreShoulder = 0.45f;

        [Header("Aim")]
        [Min(0.1f)]
        [SerializeField] float aimDistance = 1.7f;
        [SerializeField] float aimShoulder = 0.6f;
        [Tooltip("Field of view change while aiming; negative tightens the view.")]
        [SerializeField] float aimFovOffset = -8f;
        [Min(0f)]
        [SerializeField] float aimResponsiveness = 8f;

        [Header("Collision")]
        [Min(0.01f)]
        [SerializeField] float collisionRadius = 0.25f;
        [Min(0f)]
        [SerializeField] float collisionPadding = 0.05f;
        [Tooltip("How quickly the camera backs out again after an obstacle pushed it in. Pushing in is instant.")]
        [Min(0f)]
        [SerializeField] float distanceRecoverResponsiveness = 6f;

        [Header("Field Of View")]
        [Tooltip("Extra field of view blended in at full sprint speed; kept small to avoid an arcade feel.")]
        [Min(0f)]
        [SerializeField] float sprintFovIncrease = 5f;
        [Min(0f)]
        [SerializeField] float sprintFovResponsiveness = 6f;

        ExplorerInput resolvedInput;
        Transform anchor;
        Quaternion orbit = Quaternion.identity;
        float pitch = 12f;
        float aimBlend;
        float currentDistance;
        float authoredFieldOfView;
        float smoothedSprintFovKick;
        bool snapNextFrame = true;

        public ExplorerMotor Target => target;

        void OnEnable()
        {
            ResolveInputSource();
            ResolveViewCamera();
            snapNextFrame = true;
        }

        void OnDisable()
        {
            if (viewCamera != null && authoredFieldOfView > 0f)
            {
                viewCamera.fieldOfView = authoredFieldOfView;
            }
        }

        void OnValidate()
        {
            maximumPitch = Mathf.Clamp(maximumPitch, -89f, 89f);
            minimumPitch = Mathf.Clamp(minimumPitch, -89f, maximumPitch);
        }

        void Update()
        {
            if (target == null)
            {
                return;
            }

            ResolveInputSource();
            ApplyFieldOfView();

            Vector3 up = target.LocalUp.sqrMagnitude > 0.0001f ? target.LocalUp.normalized : target.transform.up;
            if (snapNextFrame)
            {
                orbit = Quaternion.LookRotation(Flatten(target.transform.forward, up), up);
            }

            ExplorerInputState input = resolvedInput?.CurrentInput ?? ExplorerInputState.None;
            Vector3 forward = Flatten(orbit * Vector3.forward, up);
            orbit = Quaternion.AngleAxis(input.Look.x, up) * Quaternion.LookRotation(forward, up);
            pitch = Mathf.Clamp(pitch - input.Look.y, minimumPitch, maximumPitch);
            Quaternion look = orbit * Quaternion.AngleAxis(pitch, Vector3.right);

            float deltaTime = Time.deltaTime;
            float desiredAim = target.Aiming ? 1f : 0f;
            aimBlend = snapNextFrame ? desiredAim : FarionMath.Smooth(aimBlend, desiredAim, aimResponsiveness, deltaTime);
            float shoulder = Mathf.Lerp(exploreShoulder, aimShoulder, aimBlend);
            float desiredDistance = Mathf.Lerp(exploreDistance, aimDistance, aimBlend);

            PhysicsScene physics = target.gameObject.scene.GetPhysicsScene();
            Vector3 basePivot = (anchor != null ? anchor.position : target.transform.position) + up * pivotHeight;
            Vector3 right = look * Vector3.right;
            Vector3 pivot = basePivot + right * ClearDistance(physics, basePivot, right, shoulder);
            Vector3 back = -(look * Vector3.forward);
            float clearDistance = ClearDistance(physics, pivot, back, desiredDistance);
            currentDistance = snapNextFrame || clearDistance < currentDistance
                ? clearDistance
                : FarionMath.Smooth(currentDistance, clearDistance, distanceRecoverResponsiveness, deltaTime);

            transform.SetPositionAndRotation(pivot + back * currentDistance, look);
            snapNextFrame = false;
        }

        float ClearDistance(PhysicsScene physics, Vector3 origin, Vector3 direction, float desired)
        {
            return ClearDistance(physics, origin, direction, desired, collisionRadius, collisionPadding);
        }

        internal static float ClearDistance(
            PhysicsScene physics,
            Vector3 origin,
            Vector3 direction,
            float desired,
            float radius,
            float padding)
        {
            if (desired <= 0f)
            {
                return desired;
            }

            return physics.SphereCast(origin, radius, direction, out RaycastHit hit, desired,
                FarionLayers.CameraObstacleMask, QueryTriggerInteraction.Ignore)
                ? Mathf.Clamp(hit.distance - padding, 0f, desired)
                : desired;
        }

        static Vector3 Flatten(Vector3 direction, Vector3 up)
        {
            Vector3 flat = Vector3.ProjectOnPlane(direction, up);
            if (flat.sqrMagnitude <= 0.0001f)
            {
                flat = Vector3.ProjectOnPlane(Vector3.forward, up);
            }

            if (flat.sqrMagnitude <= 0.0001f)
            {
                flat = Vector3.ProjectOnPlane(Vector3.right, up);
            }

            return flat.normalized;
        }

        public void SetTarget(ExplorerMotor nextTarget)
        {
            target = nextTarget;
            anchor = target != null ? target.transform.Find("VisualRoot") : null;
            resolvedInput = null;
            pitch = 12f;
            smoothedSprintFovKick = 0f;
            snapNextFrame = true;
        }

        public void SetInputSource(ExplorerInput source)
        {
            resolvedInput = source;
            inputSource = source;
        }

        void ResolveViewCamera()
        {
            viewCamera ??= GetComponentInChildren<Camera>(true);
            if (viewCamera != null && authoredFieldOfView <= 0f)
            {
                authoredFieldOfView = viewCamera.fieldOfView;
            }
        }

        void ApplyFieldOfView()
        {
            ResolveViewCamera();
            if (viewCamera == null || authoredFieldOfView <= 0f)
            {
                return;
            }

            float desired = Mathf.Min(
                PlayerViewPreferences.ResolveFieldOfView(authoredFieldOfView) +
                    aimFovOffset * aimBlend +
                    ResolveSprintFovKick(),
                PlayerViewPreferences.MaximumFieldOfView);
            if (!Mathf.Approximately(viewCamera.fieldOfView, desired))
            {
                viewCamera.fieldOfView = desired;
            }
        }

        float ResolveSprintFovKick()
        {
            float targetKick = 0f;
            if (sprintFovIncrease > 0f && !PlayerViewPreferences.ReducedMotion && target.Profile != null)
            {
                targetKick = Mathf.InverseLerp(
                    target.Profile.WalkSpeed,
                    target.Profile.SprintSpeed,
                    target.SurfaceVelocity.magnitude) * sprintFovIncrease;
            }

            smoothedSprintFovKick = FarionMath.Smooth(
                smoothedSprintFovKick,
                targetKick,
                sprintFovResponsiveness,
                Time.deltaTime);
            return smoothedSprintFovKick;
        }

        void ResolveInputSource()
        {
            if (inputSource != null)
            {
                resolvedInput = inputSource;
                return;
            }

            if (target != null)
            {
                resolvedInput ??= target.GetComponent<ExplorerInput>();
            }
        }
    }
}
