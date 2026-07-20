using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Farion.Gameplay.Character
{
    [DisallowMultipleComponent]
    public sealed class KeyboardFirstPersonInput : MonoBehaviour, IFirstPersonInputSource
    {
        [Header("Look")]
        [Min(0f)]
        [SerializeField] float mouseSensitivity = 2.5f;

        [Header("Keys")]
        [SerializeField] Key inputSystemJumpKey = Key.Space;
        [SerializeField] Key inputSystemSprintKey = Key.LeftShift;
        [SerializeField] Key inputSystemInteractKey = Key.E;

        public FirstPersonInputState CurrentInput { get; private set; }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null)
            {
                CurrentInput = FirstPersonInputState.None;
                return;
            }

            Vector2 movement = new(
                Axis(keyboard.aKey, keyboard.dKey),
                Axis(keyboard.sKey, keyboard.wKey));
            Vector2 mouseDelta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
            Vector2 look = mouseDelta * mouseSensitivity;

            CurrentInput = new FirstPersonInputState(
                movement,
                look,
                IsPressed(keyboard, inputSystemJumpKey),
                IsPressed(keyboard, inputSystemSprintKey),
                WasPressedThisFrame(keyboard, inputSystemInteractKey));
        }

        void OnDisable()
        {
            CurrentInput = FirstPersonInputState.None;
        }

        static int Axis(KeyControl negative, KeyControl positive)
        {
            int value = 0;
            if (positive != null && positive.isPressed)
            {
                value++;
            }

            if (negative != null && negative.isPressed)
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

        static bool WasPressedThisFrame(Keyboard keyboard, Key key)
        {
            KeyControl control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }
    }
}
