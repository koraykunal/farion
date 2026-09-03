using Farion.Gameplay.Character;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Farion.Gameplay.Session
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class AnimationArenaCameraRig : MonoBehaviour
    {
        [SerializeField] FirstPersonMotor target;
        [SerializeField] KeyboardFirstPersonInput inputSource;
        [SerializeField, Min(0f)] float pivotHeight = 0.65f;
        [SerializeField, Min(0.1f)] float distance = 6.5f;
        [SerializeField] Vector2 distanceRange = new(2.5f, 12f);
        [SerializeField] Vector2 pitchRange = new(-15f, 70f);
        [SerializeField, Min(0f)] float orbitSensitivity = 0.18f;
        [SerializeField, Min(0f)] float zoomSensitivity = 0.01f;
        [SerializeField, Min(0f)] float followResponsiveness = 18f;

        float yaw = 25f;
        float pitch = 18f;
        bool snapNextFrame = true;

        void OnEnable()
        {
            ResolveInput();
            inputSource?.SetLookInputEnabled(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            snapNextFrame = true;
        }

        void OnDisable()
        {
            inputSource?.SetLookInputEnabled(true);
        }

        void Update()
        {
            inputSource?.SetLookInputEnabled(Mouse.current?.leftButton.isPressed == true);
        }

        void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            ReadViewInput();

            Vector3 up = target.LocalUp.sqrMagnitude > 0.0001f
                ? target.LocalUp.normalized
                : Vector3.up;
            Vector3 pivot = target.transform.position + up * pivotHeight;
            Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.right, up).normalized;
            }

            forward = Quaternion.AngleAxis(yaw, up) * forward;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            forward = Quaternion.AngleAxis(pitch, right) * forward;

            Vector3 desiredPosition = pivot - forward * distance;
            Quaternion desiredRotation = Quaternion.LookRotation(pivot - desiredPosition, up);
            bool snap = snapNextFrame || Vector3.Distance(transform.position, desiredPosition) > 12f;
            float blend = snap ? 1f : 1f - Mathf.Exp(-followResponsiveness * Time.deltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, desiredPosition, blend),
                Quaternion.Slerp(transform.rotation, desiredRotation, blend));
            snapNextFrame = false;
        }

        void ReadViewInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue() * orbitSensitivity;
                    yaw += delta.x;
                    pitch = Mathf.Clamp(pitch - delta.y, pitchRange.x, pitchRange.y);
                }

                distance = Mathf.Clamp(
                    distance - mouse.scroll.ReadValue().y * zoomSensitivity,
                    distanceRange.x,
                    distanceRange.y);
            }

            if (Keyboard.current?.fKey.wasPressedThisFrame == true)
            {
                yaw = 25f;
                pitch = 18f;
                distance = 6.5f;
                snapNextFrame = true;
            }
        }

        void ResolveInput()
        {
            if (inputSource == null && target != null)
            {
                inputSource = target.GetComponent<KeyboardFirstPersonInput>();
            }
        }
    }
}
