using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [CreateAssetMenu(
        menuName = "Farion/Gameplay/Spacecraft Atmosphere Interaction Profile",
        fileName = "SO_SpacecraftAtmosphereInteractionProfile")]
    public sealed class SpacecraftAtmosphereInteractionProfile : ScriptableObject
    {
        [Header("Density")]
        [Min(0f)]
        [SerializeField] float seaLevelDensity = 1.225f;
        [Min(0.01f)]
        [SerializeField] float densityExponent = 2.5f;

        [Header("Drag")]
        [Min(0f)]
        [SerializeField] float dragAccelerationCoefficient = 0.00035f;
        [Min(0f)]
        [SerializeField] float maxDragAcceleration = 60f;

        [Header("Load")]
        [Min(0.01f)]
        [SerializeField] float referenceDynamicPressure = 25000f;
        [Min(0.01f)]
        [SerializeField] float referenceHeatingRate = 4000000f;
        [Min(0f)]
        [SerializeField] float heatingSpeedThreshold = 60f;
        [Min(1f)]
        [SerializeField] float maximumEvaluatedSpeed = 1000f;

        void OnValidate()
        {
            seaLevelDensity = Mathf.Max(0f, seaLevelDensity);
            densityExponent = Mathf.Max(0.01f, densityExponent);
            dragAccelerationCoefficient = Mathf.Max(0f, dragAccelerationCoefficient);
            maxDragAcceleration = Mathf.Max(0f, maxDragAcceleration);
            referenceDynamicPressure = Mathf.Max(0.01f, referenceDynamicPressure);
            referenceHeatingRate = Mathf.Max(0.01f, referenceHeatingRate);
            heatingSpeedThreshold = Mathf.Max(0f, heatingSpeedThreshold);
            maximumEvaluatedSpeed = Mathf.Max(1f, maximumEvaluatedSpeed);
        }

        public SpacecraftAtmosphereInteractionSample Evaluate(CelestialFrameSample frame)
        {
            if (!frame.HasBody ||
                !frame.IsInsideAtmosphere ||
                frame.IsBelowOceanLevel)
            {
                return SpacecraftAtmosphereInteractionSample.Empty(frame);
            }

            float normalizedDensity = Mathf.Pow(
                Mathf.Clamp01(frame.AtmosphereNormalizedDepth),
                densityExponent);
            float massDensity = normalizedDensity * seaLevelDensity;
            Vector3 relativeVelocity = frame.SurfaceRelativeVelocity;
            float speed = Mathf.Min(relativeVelocity.magnitude, maximumEvaluatedSpeed);
            float dynamicPressure = 0.5f * massDensity * speed * speed;
            float heatingSpeed = Mathf.Max(0f, speed - heatingSpeedThreshold);
            float heatingRate = massDensity * heatingSpeed * heatingSpeed * heatingSpeed;

            Vector3 drag = relativeVelocity.sqrMagnitude > 0.0001f
                ? -relativeVelocity.normalized *
                    (speed * speed * dragAccelerationCoefficient * massDensity)
                : Vector3.zero;
            if (maxDragAcceleration > 0f)
            {
                drag = Vector3.ClampMagnitude(drag, maxDragAcceleration);
            }

            return new SpacecraftAtmosphereInteractionSample(
                frame,
                normalizedDensity,
                massDensity,
                dynamicPressure,
                dynamicPressure / referenceDynamicPressure,
                heatingRate,
                heatingRate / referenceHeatingRate,
                drag);
        }
    }
}
