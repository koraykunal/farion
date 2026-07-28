using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Farion.UI.Input
{
    public enum UiInputDeviceKind
    {
        KeyboardMouse = 0,
        Gamepad = 10
    }

    [DefaultExecutionOrder(-600)]
    [DisallowMultipleComponent]
    public sealed class UiInputDeviceService : MonoBehaviour
    {
        [SerializeField] UiInputDeviceKind currentDevice = UiInputDeviceKind.KeyboardMouse;
        [Min(0f)]
        [SerializeField] float mouseMotionThreshold = 2f;
        [Range(0.1f, 1f)]
        [SerializeField] float gamepadStickThreshold = 0.5f;

        public event Action<UiInputDeviceKind> DeviceChanged;
        public UiInputDeviceKind CurrentDevice => currentDevice;

        void OnValidate()
        {
            mouseMotionThreshold = Mathf.Max(0f, mouseMotionThreshold);
            gamepadStickThreshold = Mathf.Clamp(gamepadStickThreshold, 0.1f, 1f);
        }

        void Update()
        {
            if (HasKeyboardOrMouseInput())
            {
                SetCurrentDevice(UiInputDeviceKind.KeyboardMouse);
            }

            if (HasGamepadInput())
            {
                SetCurrentDevice(UiInputDeviceKind.Gamepad);
            }
        }

        public void SetCurrentDevice(UiInputDeviceKind device)
        {
            if (currentDevice == device)
            {
                return;
            }

            currentDevice = device;
            DeviceChanged?.Invoke(currentDevice);
        }

        bool HasKeyboardOrMouseInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            {
                return true;
            }

            Mouse mouse = Mouse.current;
            return mouse != null &&
                   (mouse.leftButton.wasPressedThisFrame ||
                    mouse.rightButton.wasPressedThisFrame ||
                    mouse.middleButton.wasPressedThisFrame ||
                    mouse.scroll.ReadValue().sqrMagnitude > 0.01f ||
                    mouse.delta.ReadValue().sqrMagnitude >
                    mouseMotionThreshold * mouseMotionThreshold);
        }

        bool HasGamepadInput()
        {
            Gamepad gamepad = Gamepad.current;
            if (gamepad == null)
            {
                return false;
            }

            float thresholdSquared = gamepadStickThreshold * gamepadStickThreshold;
            return gamepad.buttonSouth.wasPressedThisFrame ||
                   gamepad.buttonNorth.wasPressedThisFrame ||
                   gamepad.buttonEast.wasPressedThisFrame ||
                   gamepad.buttonWest.wasPressedThisFrame ||
                   gamepad.startButton.wasPressedThisFrame ||
                   gamepad.selectButton.wasPressedThisFrame ||
                   gamepad.leftShoulder.wasPressedThisFrame ||
                   gamepad.rightShoulder.wasPressedThisFrame ||
                   gamepad.dpad.ReadValue().sqrMagnitude > 0.01f ||
                   gamepad.leftStick.ReadValue().sqrMagnitude > thresholdSquared ||
                   gamepad.rightStick.ReadValue().sqrMagnitude > thresholdSquared ||
                   gamepad.leftTrigger.ReadValue() > gamepadStickThreshold ||
                   gamepad.rightTrigger.ReadValue() > gamepadStickThreshold;
        }
    }
}
