using Farion.Simulation.Celestial;
using UnityEngine;
using UnityEngine.Serialization;

namespace Farion.Gameplay.Flight
{
    [CreateAssetMenu(menuName = "Farion/Gameplay/Spacecraft Ocean Interaction Profile", fileName = "SO_SpacecraftOceanInteractionProfile")]
    public sealed class SpacecraftOceanInteractionProfile : ScriptableObject
    {
        [Header("Hull")]
        [Min(0.01f)]
        [SerializeField] float effectiveHullRadius = 1.5f;

        [Header("Buoyancy")]
        [Min(0f)]
        [FormerlySerializedAs("buoyancyAcceleration")]
        [SerializeField] float buoyancyToGravityRatio = 1.08f;

        [Header("Drag")]
        [Min(0f)]
        [SerializeField] float verticalDrag = 5f;
        [Min(0f)]
        [SerializeField] float tangentialDrag = 2f;
        [Min(0f)]
        [SerializeField] float quadraticDrag = 0.08f;
        [Min(0f)]
        [SerializeField] float maxDragAcceleration = 80f;
        [Min(0f)]
        [SerializeField] float angularDamping = 3f;

        [Header("Pressure")]
        [Min(0f)]
        [SerializeField] float pressureWarningDepth = 12f;
        [Min(0f)]
        [SerializeField] float crushDepth = 40f;
        [Min(0f)]
        [SerializeField] float safeWaterEntrySpeed = 10f;

        public float EffectiveHullRadius => effectiveHullRadius;
        public float AngularDamping => angularDamping;

        void OnValidate()
        {
            effectiveHullRadius = Mathf.Max(0.01f, effectiveHullRadius);
            buoyancyToGravityRatio = Mathf.Max(0f, buoyancyToGravityRatio);
            verticalDrag = Mathf.Max(0f, verticalDrag);
            tangentialDrag = Mathf.Max(0f, tangentialDrag);
            quadraticDrag = Mathf.Max(0f, quadraticDrag);
            maxDragAcceleration = Mathf.Max(0f, maxDragAcceleration);
            angularDamping = Mathf.Max(0f, angularDamping);
            pressureWarningDepth = Mathf.Max(0f, pressureWarningDepth);
            crushDepth = Mathf.Max(pressureWarningDepth, crushDepth);
            safeWaterEntrySpeed = Mathf.Max(0f, safeWaterEntrySpeed);
        }

        public SpacecraftOceanInteractionSample Evaluate(CelestialFrameSample frame)
        {
            if (!frame.HasBody || !frame.HasOcean)
            {
                return SpacecraftOceanInteractionSample.Empty(frame);
            }

            Vector3 radialUp = frame.RadialUp.sqrMagnitude > 0.0001f ? frame.RadialUp : frame.LocalUp;
            float immersionDepth = Mathf.Clamp(effectiveHullRadius - frame.OceanAltitude, 0f, effectiveHullRadius * 2f);
            float submergedFraction = CalculateSphereSubmergedFraction(immersionDepth, effectiveHullRadius);
            float waterDepth = Mathf.Max(0f, -frame.OceanAltitude);
            Vector3 waterRelativeVelocity = frame.SurfaceRelativeVelocity;
            Vector3 verticalVelocity = Vector3.Project(waterRelativeVelocity, radialUp);
            Vector3 tangentialVelocity = waterRelativeVelocity - verticalVelocity;
            float entrySpeed = Mathf.Max(0f, -Vector3.Dot(waterRelativeVelocity, radialUp));

            Vector3 buoyancy = frame.GravityAcceleration.sqrMagnitude > 0.0001f
                ? -frame.GravityAcceleration * (buoyancyToGravityRatio * submergedFraction)
                : radialUp * (9.81f * buoyancyToGravityRatio * submergedFraction);
            Vector3 linearDrag = -verticalVelocity * (verticalDrag * submergedFraction) -
                tangentialVelocity * (tangentialDrag * submergedFraction);
            Vector3 quadratic = waterRelativeVelocity.sqrMagnitude > 0.0001f
                ? -waterRelativeVelocity.normalized * (waterRelativeVelocity.sqrMagnitude * quadraticDrag * submergedFraction)
                : Vector3.zero;
            Vector3 drag = linearDrag + quadratic;
            if (maxDragAcceleration > 0f)
            {
                drag = Vector3.ClampMagnitude(drag, maxDragAcceleration);
            }

            float pressureStress = crushDepth > pressureWarningDepth
                ? Mathf.InverseLerp(pressureWarningDepth, crushDepth, waterDepth)
                : (waterDepth > pressureWarningDepth ? 1f : 0f);

            return new SpacecraftOceanInteractionSample(
                frame,
                submergedFraction,
                waterDepth,
                entrySpeed,
                buoyancy,
                drag,
                Mathf.Clamp01(pressureStress),
                waterDepth >= crushDepth && crushDepth > 0f,
                submergedFraction > 0f && entrySpeed > safeWaterEntrySpeed);
        }

        static float CalculateSphereSubmergedFraction(float immersionDepth, float radius)
        {
            float safeRadius = Mathf.Max(0.01f, radius);
            float h = Mathf.Clamp(immersionDepth, 0f, safeRadius * 2f);
            return Mathf.Clamp01((h * h * (3f * safeRadius - h)) / (4f * safeRadius * safeRadius * safeRadius));
        }
    }
}
