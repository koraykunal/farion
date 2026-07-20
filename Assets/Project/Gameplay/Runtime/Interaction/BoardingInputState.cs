using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    public readonly struct BoardingInputState
    {
        public BoardingInputState(bool exitVehicle, bool interact)
        {
            ExitVehicle = exitVehicle;
            Interact = interact;
        }

        public bool ExitVehicle { get; }
        public bool Interact { get; }

        public static BoardingInputState None => new(false, false);
    }
}
