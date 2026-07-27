using UnityEngine;
using Farion.Gameplay.Input;

namespace Farion.Gameplay.Character
{
    [DisallowMultipleComponent]
    public sealed class KeyboardFirstPersonInput : MonoBehaviour, IFirstPersonInputSource
    {
        [Header("Look")]
        [Min(0f)]
        [SerializeField] float mouseSensitivity = 2.5f;
        [Min(0f)]
        [SerializeField] float gamepadLookDegreesPerSecond = 150f;

        [Header("Control Lock")]
        [SerializeField] PlayerControlLock controlLock;

        public FirstPersonInputState CurrentInput { get; private set; }

        void OnEnable()
        {
            FarionInputActions.Enable();
        }

        void Update()
        {
            ResolveControlLock();
            if (IsGameplayInputLocked())
            {
                CurrentInput = FirstPersonInputState.None;
                return;
            }

            CurrentInput = new FirstPersonInputState(
                FarionInputActions.OnFootMove.ReadValue<Vector2>(),
                FarionInputActions.ReadLook(
                    FarionInputActions.OnFootLook,
                    mouseSensitivity,
                    gamepadLookDegreesPerSecond),
                FarionInputActions.OnFootJump.IsPressed(),
                FarionInputActions.OnFootSprint.IsPressed(),
                FarionInputActions.OnFootInteract.WasPressedThisFrame());
        }

        void OnDisable()
        {
            CurrentInput = FirstPersonInputState.None;
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

    }
}
