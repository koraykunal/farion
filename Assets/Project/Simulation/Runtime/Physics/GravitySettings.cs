using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Farion.Simulation.Physics
{
    [MovedFrom(true, "Farion.Core.Physics", "Farion.Core.Runtime")]
    [CreateAssetMenu(menuName = "Farion/Physics/Gravity Settings", fileName = "GravitySettings")]
    public sealed class GravitySettings : ScriptableObject
    {
        [Min(0.000001f)]
        [SerializeField] float gravitationalConstant = 0.0001f;

        [Min(0.001f)]
        [SerializeField] float fixedTimeStep = 0.01f;

        [Min(0f)]
        [SerializeField] float minimumInteractionDistance = 1f;

        [SerializeField] bool useMinimumInteractionDistance;

        [Min(1)]
        [SerializeField] int solverSubsteps = 1;

        [Min(0f)]
        [SerializeField] float maxAcceleration = 10000f;

        [SerializeField] bool clampAcceleration;

        [SerializeField] bool applyFixedTimeStepOnStart = true;

        public float GravitationalConstant => gravitationalConstant;
        public float FixedTimeStep => fixedTimeStep;
        public float MinimumInteractionDistance => minimumInteractionDistance;
        public bool UseMinimumInteractionDistance => useMinimumInteractionDistance;
        public int SolverSubsteps => solverSubsteps;
        public float MaxAcceleration => maxAcceleration;
        public bool ClampAcceleration => clampAcceleration;
        public bool ApplyFixedTimeStepOnStart => applyFixedTimeStepOnStart;
    }
}
