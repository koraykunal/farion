using UnityEngine;

namespace Farion.Simulation.World
{
    public interface IWorldOriginShiftListener
    {
        void OnWorldOriginShifted(Vector3 originOffset);
    }
}
