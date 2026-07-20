using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Farion.Gameplay.Flight
{
    [DisallowMultipleComponent]
    public sealed class KeyboardSpacecraftInput : MonoBehaviour, ISpacecraftInputSource
    {
        [Header("Translation")]
        [SerializeField] Key inputSystemForwardKey = Key.W;
        [SerializeField] Key inputSystemBackwardKey = Key.S;
        [SerializeField] Key inputSystemLeftKey = Key.A;
        [SerializeField] Key inputSystemRightKey = Key.D;
        [SerializeField] Key inputSystemAscendKey = Key.Space;
        [SerializeField] Key inputSystemDescendKey = Key.LeftCtrl;
        [SerializeField] Key inputSystemBoostKey = Key.LeftShift;

        [Header("Rotation")]
        [SerializeField] Key inputSystemRollLeftKey = Key.Q;
        [SerializeField] Key inputSystemRollRightKey = Key.E;
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
            CurrentInput = SpacecraftInputState.None;

            if (Application.isPlaying && lockCursorOnPlay)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null)
            {
                CurrentInput = SpacecraftInputState.None;
                return;
            }

            Vector3 translation = new(
                Axis(keyboard, inputSystemLeftKey, inputSystemRightKey),
                Axis(keyboard, inputSystemDescendKey, inputSystemAscendKey),
                Axis(keyboard, inputSystemBackwardKey, inputSystemForwardKey));

            Vector2 mouseDelta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
            Vector2 look = mouseDelta * mouseSensitivity;

            CurrentInput = new SpacecraftInputState(
                translation,
                look,
                Axis(keyboard, inputSystemRollLeftKey, inputSystemRollRightKey),
                IsPressed(keyboard, inputSystemBoostKey));
        }

        static int Axis(Keyboard keyboard, Key negative, Key positive)
        {
            int value = 0;
            if (IsPressed(keyboard, positive))
            {
                value++;
            }

            if (IsPressed(keyboard, negative))
            {
                value--;
            }

            return value;
        }

        static bool IsPressed(Keyboard keyboard, Key key)
        {
            KeyControl control = keyboard[key];
            return control != null && control.isPressed;
        }
    }
}
