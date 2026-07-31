using UnityEngine;

namespace Farion.Gameplay.Navigation
{
    [DisallowMultipleComponent]
    public sealed class FlightNavigationTarget : MonoBehaviour
    {
        const string DefaultDisplayName = "FLEET";

        [SerializeField] string displayName = DefaultDisplayName;
        [Min(0f)]
        [SerializeField] float arrivalRadius = 30f;

        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName)
                ? DefaultDisplayName
                : displayName.Trim();
        public float ArrivalRadius => arrivalRadius;
        public Vector3 Position => transform.position;

        void OnValidate()
        {
            displayName = DisplayName;
            arrivalRadius = Mathf.Max(0f, arrivalRadius);
        }
    }
}
