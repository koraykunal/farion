using UnityEngine;

namespace Farion.Gameplay.Character
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class FirstPersonCameraRig : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] FirstPersonMotor target;
        [SerializeField] MonoBehaviour inputSource;

        [Header("View")]
        [Min(0f)]
        [SerializeField] float eyeHeight = 1.65f;
        [Range(1f, 89f)]
        [SerializeField] float pitchLimit = 82f;
        [Min(0f)]
        [SerializeField] float positionResponsiveness = 28f;
        [Min(0f)]
        [SerializeField] float rotationResponsiveness = 36f;
        [Min(0f)]
        [SerializeField] float snapDistance = 4f;

        IFirstPersonInputSource resolvedInput;
        float pitch;
        bool snapNextFrame = true;

        void OnEnable()
        {
            ResolveInputSource();
            snapNextFrame = true;
        }

        void OnValidate()
        {
            eyeHeight = Mathf.Max(0f, eyeHeight);
            pitchLimit = Mathf.Clamp(pitchLimit, 1f, 89f);
            positionResponsiveness = Mathf.Max(0f, positionResponsiveness);
            rotationResponsiveness = Mathf.Max(0f, rotationResponsiveness);
            snapDistance = Mathf.Max(0f, snapDistance);
            if (inputSource != null && inputSource is not IFirstPersonInputSource)
            {
                inputSource = null;
            }
        }

        void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            ResolveInputSource();
            FirstPersonInputState input = resolvedInput?.CurrentInput ?? FirstPersonInputState.None;
            pitch = Mathf.Clamp(pitch - input.Look.y, -pitchLimit, pitchLimit);

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

            float positionT = ResponsivenessToLerp(positionResponsiveness);
            float rotationT = ResponsivenessToLerp(rotationResponsiveness);
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

        public void SetInputSource(IFirstPersonInputSource source)
        {
            resolvedInput = source;
            inputSource = source as MonoBehaviour;
        }

        void ResolveInputSource()
        {
            if (inputSource is IFirstPersonInputSource explicitSource)
            {
                resolvedInput = explicitSource;
                return;
            }

            if (target != null)
            {
                resolvedInput ??= target.GetComponent<IFirstPersonInputSource>();
            }
        }

        static float ResponsivenessToLerp(float responsiveness)
        {
            return responsiveness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-responsiveness * UnityEngine.Time.deltaTime);
        }
    }
}
