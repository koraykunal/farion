using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [CreateAssetMenu(menuName = "Farion/Gameplay/Spacecraft Entry Corridor Profile", fileName = "SO_SpacecraftEntryCorridorProfile")]
    public sealed class SpacecraftEntryCorridorProfile : ScriptableObject
    {
        [Header("Entry Angle")]
        [Min(0f)]
        [SerializeField] float minimumEntryAngle = 2f;
        [Min(0f)]
        [SerializeField] float maximumEntryAngle = 18f;

        [Header("Speed")]
        [Min(0f)]
        [SerializeField] float maximumEscapeSpeedRatio = 0.82f;
        [Min(0f)]
        [SerializeField] float maximumCircularSpeedRatio = 1.35f;

        [Header("Periapsis")]
        [SerializeField] float safePeriapsisAltitude = 4f;
        [SerializeField] float impactPeriapsisAltitude = -1f;

        public float MinimumEntryAngle => minimumEntryAngle;
        public float MaximumEntryAngle => maximumEntryAngle;
        public float MaximumEscapeSpeedRatio => maximumEscapeSpeedRatio;
        public float MaximumCircularSpeedRatio => maximumCircularSpeedRatio;
        public float SafePeriapsisAltitude => safePeriapsisAltitude;
        public float ImpactPeriapsisAltitude => impactPeriapsisAltitude;

        void OnValidate()
        {
            minimumEntryAngle = Mathf.Max(0f, minimumEntryAngle);
            maximumEntryAngle = Mathf.Max(minimumEntryAngle, maximumEntryAngle);
            maximumEscapeSpeedRatio = Mathf.Max(0f, maximumEscapeSpeedRatio);
            maximumCircularSpeedRatio = Mathf.Max(0f, maximumCircularSpeedRatio);
            safePeriapsisAltitude = Mathf.Max(impactPeriapsisAltitude, safePeriapsisAltitude);
        }
    }
}
