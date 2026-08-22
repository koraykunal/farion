using Farion.Gameplay.Input;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DisallowMultipleComponent]
    public sealed class KeyboardSpacecraftInput : MonoBehaviour
    {
        [Header("Rotation")]
        [SerializeField] float mouseSensitivity = 1f;
        [Min(0f)]
        [SerializeField] float gamepadLookDegreesPerSecond = 120f;

        [Header("Throttle")]
        [Range(-1f, 1f)]
        [SerializeField] float initialThrottle;
        [Min(0.01f)]
        [SerializeField] float throttleChangePerSecond = 0.8f;
        [SerializeField, Range(-1f, 1f)] float currentThrottle;

        [Header("Control Lock")]
        [SerializeField] PlayerControlLock controlLock;

        public SpacecraftInputState CurrentInput { get; private set; }
        public float CurrentThrottle => currentThrottle;

        void OnEnable()
        {
            FarionInputActions.Enable();
            currentThrottle = Mathf.Clamp(initialThrottle, -1f, 1f);
        }

        void OnDisable()
        {
            CurrentInput = SpacecraftInputState.None;
        }

        void Update()
        {
            if (IsGameplayInputLocked())
            {
                CurrentInput = new SpacecraftInputState(
                    new Vector3(0f, 0f, currentThrottle),
                    Vector2.zero,
                    roll: 0f,
                    boost: false);
                return;
            }

            Vector2 planarTranslation = FarionInputActions.FlightTranslate.ReadValue<Vector2>();
            float throttleInput = Mathf.Clamp(planarTranslation.y, -1f, 1f);
            if (Mathf.Abs(throttleInput) > 0.001f)
            {
                currentThrottle = Mathf.MoveTowards(
                    currentThrottle,
                    throttleInput > 0f ? 1f : -1f,
                    throttleChangePerSecond * Time.unscaledDeltaTime);
            }

            bool brake = FarionInputActions.FlightBrake.IsPressed();
            if (FarionInputActions.FlightBrake.WasPressedThisFrame())
            {
                currentThrottle = 0f;
            }

            Vector3 translation = new(
                planarTranslation.x,
                FarionInputActions.FlightVertical.ReadValue<float>(),
                currentThrottle);

            CurrentInput = new SpacecraftInputState(
                translation,
                FarionInputActions.ReadLook(
                    FarionInputActions.FlightLook,
                    mouseSensitivity,
                    gamepadLookDegreesPerSecond),
                FarionInputActions.FlightRoll.ReadValue<float>(),
                FarionInputActions.FlightBoost.IsPressed(),
                brake,
                FarionInputActions.FlightToggleAssist.WasPressedThisFrame(),
                FarionInputActions.FlightToggleLandingGear.WasPressedThisFrame(),
                FarionInputActions.FlightToggleFloodlights.WasPressedThisFrame());
        }

        void OnValidate()
        {
            initialThrottle = Mathf.Clamp(initialThrottle, -1f, 1f);
            throttleChangePerSecond = Mathf.Max(0.01f, throttleChangePerSecond);
        }

        public void SetControlLock(PlayerControlLock nextControlLock)
        {
            controlLock = nextControlLock;
        }

        bool IsGameplayInputLocked()
        {
            return controlLock != null && controlLock.IsGameplayInputLocked;
        }

    }
}
