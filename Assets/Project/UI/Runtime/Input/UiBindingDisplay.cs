using System;
using UnityEngine.InputSystem;

namespace Farion.UI.Input
{
    public static class UiBindingDisplay
    {
        public static string GetDisplayString(
            InputAction action,
            UiInputDeviceKind deviceKind)
        {
            if (action == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite)
                {
                    continue;
                }

                string path = string.IsNullOrWhiteSpace(binding.effectivePath)
                    ? binding.path
                    : binding.effectivePath;
                if (!MatchesDevice(path, deviceKind))
                {
                    continue;
                }

                string display = action.GetBindingDisplayString(i);
                if (!string.IsNullOrWhiteSpace(display))
                {
                    return display.Trim();
                }
            }

            return action.GetBindingDisplayString().Trim();
        }

        static bool MatchesDevice(string path, UiInputDeviceKind deviceKind)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            bool gamepad = path.IndexOf(
                "<Gamepad>",
                StringComparison.OrdinalIgnoreCase) >= 0;
            return deviceKind == UiInputDeviceKind.Gamepad
                ? gamepad
                : !gamepad;
        }
    }
}
