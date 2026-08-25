using Farion.Simulation.Celestial;
using UnityEngine;
using UnityEngine.Serialization;

namespace Farion.Gameplay.Flight
{
    [CreateAssetMenu(menuName = "Farion/Gameplay/Flight/Spacecraft Ocean Interaction Profile", fileName = "SO_SpacecraftOceanInteractionProfile")]
    public sealed class SpacecraftOceanInteractionProfile : ScriptableObject
    {
        public const int MaxBuoyancyPoints = 8;

        [Header("Hull")]
        [Min(0.01f)]
        [SerializeField] float effectiveHullRadius = 1.5f;
        [Tooltip("Local-space float points. Leave empty to use a single point at the centre of mass. More points produce roll and pitch.")]
        [SerializeField] Vector3[] buoyancyPoints = System.Array.Empty<Vector3>();

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
        [Tooltip("Extra deceleration while the hull is punching through the surface, scaled by entry speed.")]
        [Min(0f)]
        [SerializeField] float slamDrag = 0.6f;

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
            slamDrag = Mathf.Max(0f, slamDrag);
            pressureWarningDepth = Mathf.Max(0f, pressureWarningDepth);
            crushDepth = Mathf.Max(pressureWarningDepth, crushDepth);
            safeWaterEntrySpeed = Mathf.Max(0f, safeWaterEntrySpeed);
            if (buoyancyPoints != null && buoyancyPoints.Length > MaxBuoyancyPoints)
            {
                System.Array.Resize(ref buoyancyPoints, MaxBuoyancyPoints);
            }
        }

        public int BuoyancyPointCount => buoyancyPoints == null || buoyancyPoints.Length == 0
            ? 1
            : Mathf.Min(buoyancyPoints.Length, MaxBuoyancyPoints);

        public Vector3 GetBuoyancyPointLocal(int index)
        {
            return buoyancyPoints == null || buoyancyPoints.Length == 0
                ? Vector3.zero
                : buoyancyPoints[Mathf.Clamp(index, 0, buoyancyPoints.Length - 1)];
        }

        public Vector3 EvaluateBuoyancyPoint(
            CelestialFrameSample frame,
            Vector3 pointWorldPosition,
            out float submergedFraction)
        {
            submergedFraction = 0f;
            if (!frame.HasBody || !frame.HasOcean)
            {
                return Vector3.zero;
            }

            int pointCount = BuoyancyPointCount;
            float pointRadius = effectiveHullRadius / Mathf.Pow(pointCount, 1f / 3f);
            float surfaceRadius = frame.Environment.GetOceanRadiusAt(pointWorldPosition - frame.BodyPosition);
            float altitude = Vector3.Distance(pointWorldPosition, frame.BodyPosition) - surfaceRadius;
            float immersionDepth = Mathf.Clamp(pointRadius - altitude, 0f, pointRadius * 2f);
            submergedFraction = CalculateSphereSubmergedFraction(immersionDepth, pointRadius);
            if (submergedFraction <= 0f)
            {
                return Vector3.zero;
            }

            Vector3 radialUp = frame.RadialUp.sqrMagnitude > 0.0001f ? frame.RadialUp : frame.LocalUp;
            float share = buoyancyToGravityRatio * submergedFraction / pointCount;
            return frame.GravityAcceleration.sqrMagnitude > 0.0001f
                ? -frame.GravityAcceleration * share
                : radialUp * (9.81f * share);
        }

        public SpacecraftOceanInteractionSample Evaluate(
            CelestialFrameSample frame,
            float submergedFraction,
            Vector3 buoyancyAcceleration)
        {
            if (!frame.HasBody || !frame.HasOcean)
            {
                return SpacecraftOceanInteractionSample.Empty(frame);
            }

            Vector3 radialUp = frame.RadialUp.sqrMagnitude > 0.0001f ? frame.RadialUp : frame.LocalUp;
            submergedFraction = Mathf.Clamp01(submergedFraction);
            float waterDepth = Mathf.Max(0f, -frame.OceanAltitude);
            Vector3 waterRelativeVelocity = frame.Velocity - frame.WaterPointVelocity;
            Vector3 verticalVelocity = Vector3.Project(waterRelativeVelocity, radialUp);
            Vector3 tangentialVelocity = waterRelativeVelocity - verticalVelocity;
            float entrySpeed = Mathf.Max(0f, -Vector3.Dot(waterRelativeVelocity, radialUp));

            Vector3 linearDrag = -verticalVelocity * (verticalDrag * submergedFraction) -
                tangentialVelocity * (tangentialDrag * submergedFraction);
            Vector3 quadratic = waterRelativeVelocity.sqrMagnitude > 0.0001f
                ? -waterRelativeVelocity.normalized * (waterRelativeVelocity.sqrMagnitude * quadraticDrag * submergedFraction)
                : Vector3.zero;
            Vector3 slam = Vector3.zero;
            if (slamDrag > 0f && entrySpeed > 0f)
            {
                float crossing = submergedFraction * (1f - submergedFraction) * 4f;
                slam = radialUp * (entrySpeed * entrySpeed * slamDrag * crossing);
            }

            Vector3 drag = linearDrag + quadratic + slam;
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
                buoyancyAcceleration,
                drag,
                Mathf.Clamp01(pressureStress),
                waterDepth >= crushDepth && crushDepth > 0f,
                submergedFraction > 0f && entrySpeed > safeWaterEntrySpeed);
        }

        public static float CalculateSphereSubmergedFraction(float immersionDepth, float radius)
        {
            float safeRadius = Mathf.Max(0.01f, radius);
            float h = Mathf.Clamp(immersionDepth, 0f, safeRadius * 2f);
            return Mathf.Clamp01((h * h * (3f * safeRadius - h)) / (4f * safeRadius * safeRadius * safeRadius));
        }
    }
}
