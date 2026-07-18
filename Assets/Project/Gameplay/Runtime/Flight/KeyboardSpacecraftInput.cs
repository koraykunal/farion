using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DisallowMultipleComponent]
    public sealed class KeyboardSpacecraftInput : MonoBehaviour, ISpacecraftInputSource
    {
        [Header("Translation")]
        [SerializeField] KeyCode forwardKey = KeyCode.W;
        [SerializeField] KeyCode backwardKey = KeyCode.S;
        [SerializeField] KeyCode leftKey = KeyCode.A;
        [SerializeField] KeyCode rightKey = KeyCode.D;
        [SerializeField] KeyCode ascendKey = KeyCode.Space;
        [SerializeField] KeyCode descendKey = KeyCode.LeftControl;
        [SerializeField] KeyCode boostKey = KeyCode.LeftShift;

        [Header("Rotation")]
        [SerializeField] KeyCode rollLeftKey = KeyCode.Q;
        [SerializeField] KeyCode rollRightKey = KeyCode.E;
        [SerializeField] float mouseSensitivity = 1f;
        [SerializeField] bool lockCursorOnPlay = true;

        public SpacecraftInputState CurrentInput { get; private set; }

        void OnEnable()
        {
            if (Application.isPlaying && lockCursorOnPlay)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void OnDisable()
        {
            if (Application.isPlaying && lockCursorOnPlay)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void Update()
        {
            Vector3 translation = new(
                Axis(leftKey, rightKey),
                Axis(descendKey, ascendKey),
                Axis(backwardKey, forwardKey));

            Vector2 look = new(
                Input.GetAxisRaw("Mouse X") * mouseSensitivity,
                Input.GetAxisRaw("Mouse Y") * mouseSensitivity);

            CurrentInput = new SpacecraftInputState(
                translation,
                look,
                Axis(rollLeftKey, rollRightKey),
                Input.GetKey(boostKey));
        }

        static int Axis(KeyCode negative, KeyCode positive)
        {
            int value = 0;
            if (Input.GetKey(positive))
            {
                value++;
            }

            if (Input.GetKey(negative))
            {
                value--;
            }

            return value;
        }
    }
}
