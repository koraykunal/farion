using UnityEngine;

namespace Farion.Core.Time
{
    public static class SimulationClock
    {
        public static float FixedStep => UnityEngine.Time.fixedDeltaTime;

        public static bool ApplyFixedStep(float fixedStep)
        {
            if (fixedStep <= 0f)
            {
                return false;
            }

            UnityEngine.Time.fixedDeltaTime = fixedStep;
            return true;
        }
    }
}
