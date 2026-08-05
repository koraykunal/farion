using UnityEngine;
using UnityEngine.InputSystem;

namespace Farion.Gameplay.Input
{
    public static class FarionInputActions
    {
        const string BindingOverridesKey = "farion.input.binding-overrides";
        const float ReferenceGamepadLookRate = 120f;

        static InputActionAsset asset;

        public static InputAction OnFootMove => Find("OnFoot", "Move");
        public static InputAction OnFootLook => Find("OnFoot", "Look");
        public static InputAction OnFootJump => Find("OnFoot", "Jump");
        public static InputAction OnFootSprint => Find("OnFoot", "Sprint");
        public static InputAction OnFootInteract => Find("OnFoot", "Interact");

        public static InputAction FlightTranslate => Find("Flight", "Translate");
        public static InputAction FlightVertical => Find("Flight", "Vertical");
        public static InputAction FlightLook => Find("Flight", "Look");
        public static InputAction FlightRoll => Find("Flight", "Roll");
        public static InputAction FlightBoost => Find("Flight", "Boost");
        public static InputAction FlightBrake => Find("Flight", "Brake");
        public static InputAction FlightToggleAssist => Find("Flight", "ToggleAssist");
        public static InputAction FlightToggleLandingGear => Find("Flight", "ToggleLandingGear");

        public static InputAction VehicleExit => Find("Vehicle", "Exit");
        public static InputAction VehicleToggleCamera => Find("Vehicle", "ToggleCamera");

        public static InputAction UiPause => Find("UI", "Pause");
        public static InputAction UiInventory => Find("UI", "Inventory");
        public static InputAction UiNavigate => Find("UI", "Navigate");
        public static InputAction UiSubmit => Find("UI", "Submit");
        public static InputAction UiCancel => Find("UI", "Cancel");
        public static InputAction UiPoint => Find("UI", "Point");
        public static InputAction UiLeftClick => Find("UI", "LeftClick");
        public static InputAction UiMiddleClick => Find("UI", "MiddleClick");
        public static InputAction UiRightClick => Find("UI", "RightClick");
        public static InputAction UiScrollWheel => Find("UI", "ScrollWheel");

        public static InputActionAsset Asset
        {
            get
            {
                EnsureInitialized();
                return asset;
            }
        }

        public static void Enable()
        {
            Asset.Enable();
        }

        public static Vector2 ReadLook(
            InputAction action,
            float mouseSensitivity,
            float gamepadDegreesPerSecond)
        {
            Vector2 value = action?.ReadValue<Vector2>() ?? Vector2.zero;
            return action?.activeControl?.device is Mouse
                ? ScaleMouseLook(value, mouseSensitivity)
                : ScaleGamepadLook(value, gamepadDegreesPerSecond);
        }

        internal static Vector2 ScaleMouseLook(
            Vector2 value,
            float mouseSensitivity)
        {
            return value * Mathf.Max(0f, mouseSensitivity);
        }

        internal static Vector2 ScaleGamepadLook(Vector2 value, float gamepadDegreesPerSecond)
        {
            return value * (Mathf.Max(0f, gamepadDegreesPerSecond) / ReferenceGamepadLookRate);
        }

        public static void SaveBindingOverrides()
        {
            string json = Asset.SaveBindingOverridesAsJson();
            if (string.IsNullOrEmpty(json))
            {
                PlayerPrefs.DeleteKey(BindingOverridesKey);
            }
            else
            {
                PlayerPrefs.SetString(BindingOverridesKey, json);
            }

            PlayerPrefs.Save();
        }

        public static void ResetBindingOverrides()
        {
            Asset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(BindingOverridesKey);
            PlayerPrefs.Save();
        }

        static InputAction Find(string mapName, string actionName)
        {
            EnsureInitialized();
            return asset.FindActionMap(mapName, throwIfNotFound: true)
                .FindAction(actionName, throwIfNotFound: true);
        }

        static void EnsureInitialized()
        {
            if (asset != null)
            {
                return;
            }

            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = nameof(FarionInputActions);
            asset.hideFlags = HideFlags.HideAndDontSave;
            BuildOnFootMap(asset);
            BuildFlightMap(asset);
            BuildVehicleMap(asset);
            BuildUiMap(asset);

            string overrides = PlayerPrefs.GetString(BindingOverridesKey, string.Empty);
            if (!string.IsNullOrEmpty(overrides))
            {
                asset.LoadBindingOverridesFromJson(overrides);
            }

            asset.Enable();
        }

        static void BuildOnFootMap(InputActionAsset inputAsset)
        {
            InputActionMap map = inputAsset.AddActionMap("OnFoot");
            InputAction move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            AddWasd(move);
            move.AddBinding("<Gamepad>/leftStick");

            InputAction look = map.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
            look.AddBinding("<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick");

            AddButton(map, "Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            AddButton(map, "Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            AddButton(map, "Interact", "<Keyboard>/e", "<Gamepad>/buttonWest");
        }

        static void BuildFlightMap(InputActionAsset inputAsset)
        {
            InputActionMap map = inputAsset.AddActionMap("Flight");
            InputAction translate = map.AddAction("Translate", InputActionType.Value, expectedControlLayout: "Vector2");
            AddWasd(translate);
            translate.AddBinding("<Gamepad>/leftStick")
                .WithProcessor("stickDeadzone(min=0.12,max=0.95)");

            InputAction vertical = map.AddAction("Vertical", InputActionType.Value, expectedControlLayout: "Axis");
            vertical.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftCtrl")
                .With("Positive", "<Keyboard>/space");
            vertical.AddCompositeBinding("1DAxis")
                .With("Negative", "<Gamepad>/leftTrigger")
                .With("Positive", "<Gamepad>/rightTrigger");

            InputAction look = map.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
            look.AddBinding("<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick")
                .WithProcessor("stickDeadzone(min=0.12,max=0.95)");

            InputAction roll = map.AddAction("Roll", InputActionType.Value, expectedControlLayout: "Axis");
            roll.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/q")
                .With("Positive", "<Keyboard>/e");
            roll.AddCompositeBinding("1DAxis")
                .With("Negative", "<Gamepad>/leftShoulder")
                .With("Positive", "<Gamepad>/rightShoulder");

            AddButton(map, "Boost", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            AddButton(map, "Brake", "<Keyboard>/x", "<Gamepad>/buttonSouth");
            AddButton(map, "ToggleAssist", "<Keyboard>/z", "<Gamepad>/buttonEast");
            AddButton(map, "ToggleLandingGear", "<Keyboard>/g", "<Gamepad>/dpad/down");
        }

        static void BuildVehicleMap(InputActionAsset inputAsset)
        {
            InputActionMap map = inputAsset.AddActionMap("Vehicle");
            AddButton(map, "Exit", "<Keyboard>/f", "<Gamepad>/buttonNorth");
            AddButton(map, "ToggleCamera", "<Keyboard>/c", "<Gamepad>/rightStickPress");
        }

        static void BuildUiMap(InputActionAsset inputAsset)
        {
            InputActionMap map = inputAsset.AddActionMap("UI");
            AddButton(map, "Pause", "<Keyboard>/escape", "<Gamepad>/start");
            AddButton(map, "Inventory", "<Keyboard>/i", "<Gamepad>/select");

            InputAction navigate = map.AddAction(
                "Navigate",
                InputActionType.Value,
                expectedControlLayout: "Vector2");
            AddWasd(navigate);
            navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            navigate.AddBinding("<Gamepad>/leftStick")
                .WithProcessor("stickDeadzone(min=0.2,max=0.95)");
            navigate.AddBinding("<Gamepad>/dpad");

            InputAction submit = AddButton(
                map,
                "Submit",
                "<Keyboard>/enter",
                "<Gamepad>/buttonSouth");
            submit.AddBinding("<Keyboard>/space");

            AddButton(map, "Cancel", "<Keyboard>/escape", "<Gamepad>/buttonEast");

            map.AddAction(
                "Point",
                InputActionType.PassThrough,
                "<Mouse>/position",
                expectedControlLayout: "Vector2");
            map.AddAction(
                "LeftClick",
                InputActionType.PassThrough,
                "<Mouse>/leftButton",
                expectedControlLayout: "Button");
            map.AddAction(
                "MiddleClick",
                InputActionType.PassThrough,
                "<Mouse>/middleButton",
                expectedControlLayout: "Button");
            map.AddAction(
                "RightClick",
                InputActionType.PassThrough,
                "<Mouse>/rightButton",
                expectedControlLayout: "Button");
            map.AddAction(
                "ScrollWheel",
                InputActionType.PassThrough,
                "<Mouse>/scroll",
                expectedControlLayout: "Vector2");
        }

        static void AddWasd(InputAction action)
        {
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
        }

        static InputAction AddButton(
            InputActionMap map,
            string name,
            string keyboardPath,
            string gamepadPath)
        {
            InputAction action = map.AddAction(name, InputActionType.Button, expectedControlLayout: "Button");
            action.AddBinding(keyboardPath);
            action.AddBinding(gamepadPath);
            return action;
        }
    }
}
