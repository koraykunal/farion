using Farion.Gameplay.Input;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class KeyboardBoardingInput : MonoBehaviour
    {
        public BoardingInputState CurrentInput { get; private set; }

        void OnEnable()
        {
            FarionInputActions.Enable();
        }

        void Update()
        {
            CurrentInput = new BoardingInputState(
                FarionInputActions.VehicleExit.WasPressedThisFrame(),
                FarionInputActions.VehicleToggleCamera.WasPressedThisFrame());
        }

        void OnDisable()
        {
            CurrentInput = BoardingInputState.None;
        }

    }
}
