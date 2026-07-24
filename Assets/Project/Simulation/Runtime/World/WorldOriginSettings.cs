using UnityEngine;

namespace Farion.Simulation.World
{
    [CreateAssetMenu(menuName = "Farion/Simulation/World/World Origin Settings", fileName = "SO_WorldOriginSettings")]
    public sealed class WorldOriginSettings : ScriptableObject
    {
        [Min(1f)]
        [SerializeField] float rebaseDistance = 1000f;

        [Tooltip("When enabled, very small accumulated vertical drift can still be removed. Space gameplay should normally keep this enabled.")]
        [SerializeField] bool rebaseAllAxes = true;

        public float RebaseDistance => Mathf.Max(1f, rebaseDistance);
        public bool RebaseAllAxes => rebaseAllAxes;

        void OnValidate()
        {
            rebaseDistance = Mathf.Max(1f, rebaseDistance);
        }
    }
}
