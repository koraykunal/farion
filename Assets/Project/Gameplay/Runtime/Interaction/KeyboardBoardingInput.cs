using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Farion.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class KeyboardBoardingInput : MonoBehaviour, IBoardingInputSource
    {
        [Header("Keys")]
        [SerializeField] Key inputSystemExitVehicleKey = Key.F;

        public BoardingInputState CurrentInput { get; private set; }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                CurrentInput = BoardingInputState.None;
                return;
            }

            CurrentInput = new BoardingInputState(WasPressedThisFrame(keyboard, inputSystemExitVehicleKey));
        }

        void OnDisable()
        {
            CurrentInput = BoardingInputState.None;
        }

        static bool WasPressedThisFrame(Keyboard keyboard, Key key)
        {
            KeyControl control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }
    }
}
