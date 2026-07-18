using UnityEngine;

namespace Farion.Core.Time
{
    public static class SimulationClock
    {
        public static float FixedStep => UnityEngine.Time.fixedDeltaTime;

        public static void ApplyFixedStep(float fixedStep)
        {
            if (fixedStep <= 0f)
            {
                Debug.LogWarning($"Ignoring invalid fixed timestep: {fixedStep}");
                return;
            }

            UnityEngine.Time.fixedDeltaTime = fixedStep;
        }
    }
}
