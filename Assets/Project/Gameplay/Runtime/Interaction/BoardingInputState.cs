using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    public readonly struct BoardingInputState
    {
        public BoardingInputState(bool exitVehicle)
            : this(exitVehicle, togglePilotCamera: false)
        {
        }

        public BoardingInputState(bool exitVehicle, bool togglePilotCamera)
            : this(exitVehicle, togglePilotCamera, toggleHud: false)
        {
        }

        public BoardingInputState(bool exitVehicle, bool togglePilotCamera, bool toggleHud)
        {
            ExitVehicle = exitVehicle;
            TogglePilotCamera = togglePilotCamera;
            ToggleHud = toggleHud;
        }

        public bool ExitVehicle { get; }
        public bool TogglePilotCamera { get; }
        public bool ToggleHud { get; }

        public static BoardingInputState None => new(false, false, false);
    }
}
