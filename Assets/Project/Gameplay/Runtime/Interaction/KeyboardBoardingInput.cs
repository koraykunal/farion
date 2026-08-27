using Farion.Gameplay.Input;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class KeyboardBoardingInput : MonoBehaviour
    {
        [Header("Control Lock")]
        [SerializeField] PlayerControlLock controlLock;

        public BoardingInputState CurrentInput { get; private set; }

        void OnEnable()
        {
            FarionInputActions.Enable();
        }

        void Update()
        {
            if (controlLock != null && controlLock.IsGameplayInputLocked)
            {
                CurrentInput = BoardingInputState.None;
                return;
            }

            CurrentInput = new BoardingInputState(
                FarionInputActions.VehicleExit.WasPressedThisFrame(),
                FarionInputActions.VehicleToggleCamera.WasPressedThisFrame());
        }

        void OnDisable()
        {
            CurrentInput = BoardingInputState.None;
        }

        public void SetControlLock(PlayerControlLock nextControlLock)
        {
            controlLock = nextControlLock;
        }

    }
}
