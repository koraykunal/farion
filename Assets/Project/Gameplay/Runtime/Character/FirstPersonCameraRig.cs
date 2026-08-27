using Farion.Core.Numerics;
using Farion.Gameplay.Input;
using UnityEngine;

namespace Farion.Gameplay.Character
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class FirstPersonCameraRig : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] FirstPersonMotor target;
        [SerializeField] KeyboardFirstPersonInput inputSource;

        [Header("View")]
        [Min(0f)]
        [SerializeField] float eyeHeight = 0.65f;
        [Range(1f, 89f)]
        [SerializeField] float pitchLimit = 82f;
        [Min(0f)]
        [SerializeField] float positionResponsiveness = 28f;
        [Min(0f)]
        [SerializeField] float rotationResponsiveness = 36f;
        [Min(0f)]
        [SerializeField] float snapDistance = 4f;

        [Header("Field Of View")]
        [SerializeField] Camera viewCamera;

        KeyboardFirstPersonInput resolvedInput;
        float authoredFieldOfView;
        float pitch;
        bool snapNextFrame = true;

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
            eyeHeight = Mathf.Max(0f, eyeHeight);
            pitchLimit = Mathf.Clamp(pitchLimit, 1f, 89f);
            positionResponsiveness = Mathf.Max(0f, positionResponsiveness);
            rotationResponsiveness = Mathf.Max(0f, rotationResponsiveness);
            snapDistance = Mathf.Max(0f, snapDistance);
        }

        void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            ResolveInputSource();
            ApplyFieldOfView();
            FirstPersonInputState input = resolvedInput?.CurrentInput ?? FirstPersonInputState.None;
            pitch = Mathf.Clamp(pitch - input.Look.y, -pitchLimit, pitchLimit);
            target.SetViewPitchDegrees(pitch);

            Vector3 up = target.LocalUp.sqrMagnitude > 0.0001f ? target.LocalUp : target.transform.up;
            Vector3 targetPosition = target.transform.position;
            Vector3 desiredLocalOffset = up * eyeHeight;
            Vector3 currentLocalOffset = transform.position - targetPosition;
            Quaternion targetRotation = target.transform.rotation;
            Quaternion desiredLocalRotation = Quaternion.AngleAxis(pitch, Vector3.right);
            Quaternion currentLocalRotation = Quaternion.Inverse(targetRotation) * transform.rotation;

            if (snapNextFrame || Vector3.Distance(currentLocalOffset, desiredLocalOffset) > snapDistance)
            {
                transform.SetPositionAndRotation(
                    targetPosition + desiredLocalOffset,
                    targetRotation * desiredLocalRotation);
                snapNextFrame = false;
                return;
            }

            float positionT = FarionMath.SmoothFactor(positionResponsiveness, UnityEngine.Time.deltaTime);
            float rotationT = FarionMath.SmoothFactor(rotationResponsiveness, UnityEngine.Time.deltaTime);
            Vector3 smoothedLocalOffset = Vector3.Lerp(
                currentLocalOffset,
                desiredLocalOffset,
                positionT);
            Quaternion smoothedLocalRotation = Quaternion.Slerp(
                currentLocalRotation,
                desiredLocalRotation,
                rotationT);
            transform.SetPositionAndRotation(
                targetPosition + smoothedLocalOffset,
                targetRotation * smoothedLocalRotation);
        }

        public void SetTarget(FirstPersonMotor nextTarget)
        {
            target = nextTarget;
            pitch = 0f;
            snapNextFrame = true;
        }

        public void SnapToTarget()
        {
            snapNextFrame = true;
        }

        public void SetInputSource(KeyboardFirstPersonInput source)
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

            float desiredFieldOfView =
                PlayerViewPreferences.ResolveFieldOfView(authoredFieldOfView);
            if (!Mathf.Approximately(viewCamera.fieldOfView, desiredFieldOfView))
            {
                viewCamera.fieldOfView = desiredFieldOfView;
            }
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
                resolvedInput ??= target.GetComponent<KeyboardFirstPersonInput>();
            }
        }

    }
}
