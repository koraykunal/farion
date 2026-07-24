using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    public readonly struct BoardingInputState
    {
        public BoardingInputState(bool exitVehicle)
        {
            ExitVehicle = exitVehicle;
        }

        public bool ExitVehicle { get; }

        public static BoardingInputState None => new(false);
    }
}
