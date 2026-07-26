using UnityEngine;
using Farion.Gameplay.Input;
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
        [SerializeField] Key inputSystemBrakeKey = Key.X;
        [SerializeField] Key inputSystemFlightAssistKey = Key.Z;

        [Header("Rotation")]
        [SerializeField] Key inputSystemRollLeftKey = Key.Q;
        [SerializeField] Key inputSystemRollRightKey = Key.E;
        [SerializeField] float mouseSensitivity = 1f;

        [Header("Control Lock")]
        [SerializeField] PlayerControlLock controlLock;

        public SpacecraftInputState CurrentInput { get; private set; }

        void OnDisable()
        {
            CurrentInput = SpacecraftInputState.None;
        }

        void Update()
        {
            ResolveControlLock();
            if (IsGameplayInputLocked())
            {
                CurrentInput = SpacecraftInputState.None;
                return;
            }

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
                IsPressed(keyboard, inputSystemBoostKey),
                IsPressed(keyboard, inputSystemBrakeKey),
                WasPressedThisFrame(keyboard, inputSystemFlightAssistKey));
        }

        public void SetControlLock(PlayerControlLock nextControlLock)
        {
            controlLock = nextControlLock;
        }

        void ResolveControlLock()
        {
            if (controlLock == null)
            {
                controlLock = PlayerControlLock.Active;
            }
        }

        bool IsGameplayInputLocked()
        {
            return controlLock != null && controlLock.IsGameplayInputLocked;
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

        static bool WasPressedThisFrame(Keyboard keyboard, Key key)
        {
            KeyControl control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }
    }
}
