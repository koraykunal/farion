using Farion.Gameplay.Input;
using UnityEngine;

namespace Farion.Gameplay.Character
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class ExplorerInput : MonoBehaviour
    {
        [Header("Look")]
        [Tooltip("Degrees of orbit per mouse count before the sensitivity slider; look input is always delivered in degrees.")]
        [Min(0f)]
        [SerializeField] float mouseDegreesPerCount = 0.06f;
        [Min(0f)]
        [SerializeField] float gamepadLookDegreesPerSecond = 150f;

        [Header("Control Lock")]
        [SerializeField] PlayerControlLock controlLock;

        public ExplorerInputState CurrentInput { get; private set; }
        bool lookInputEnabled = true;

        void OnEnable()
        {
            FarionInputActions.Enable();
        }

        void Update()
        {
            if (IsGameplayInputLocked())
            {
                CurrentInput = ExplorerInputState.None;
                return;
            }

            CurrentInput = new ExplorerInputState(
                FarionInputActions.OnFootMove.ReadValue<Vector2>(),
                lookInputEnabled
                    ? FarionInputActions.ReadLookDegrees(
                        FarionInputActions.OnFootLook,
                        mouseDegreesPerCount,
                        gamepadLookDegreesPerSecond)
                    : Vector2.zero,
                FarionInputActions.OnFootJump.IsPressed(),
                FarionInputActions.OnFootSprint.IsPressed(),
                FarionInputActions.OnFootInteract.WasPressedThisFrame(),
                FarionInputActions.OnFootAim.IsPressed(),
                FarionInputActions.OnFootToggleTool.WasPressedThisFrame(),
                FarionInputActions.OnFootUseTool.IsPressed(),
                FarionInputActions.OnFootDive.IsPressed());
        }

        void OnDisable()
        {
            CurrentInput = ExplorerInputState.None;
        }

        public void SetControlLock(PlayerControlLock nextControlLock)
        {
            controlLock = nextControlLock;
        }

        public void SetLookInputEnabled(bool enabled)
        {
            lookInputEnabled = enabled;
        }

        bool IsGameplayInputLocked()
        {
            return controlLock != null && controlLock.IsGameplayInputLocked;
        }

    }
}
