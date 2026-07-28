using UnityEngine;

namespace Farion.Simulation.Celestial
{
    public abstract class CelestialShapeProfile : ScriptableObject
    {
        public event System.Action Changed;

        public virtual float EvaluateRadius(float baseRadius, Vector3 unitDirection)
        {
            return EvaluateSample(baseRadius, unitDirection).Radius;
        }

        public virtual float EvaluateRadius(
            float baseRadius,
            Vector3 unitDirection,
            float angularSampleFootprint)
        {
            return EvaluateSample(baseRadius, unitDirection, angularSampleFootprint).Radius;
        }

        public virtual CelestialShapeSample EvaluateSample(float baseRadius, Vector3 unitDirection)
        {
            baseRadius = Mathf.Max(0.01f, baseRadius);
            unitDirection = unitDirection.sqrMagnitude > 0f ? unitDirection.normalized : Vector3.up;
            float radius = Mathf.Max(0.01f, baseRadius + EvaluateDisplacement(baseRadius, unitDirection));
            return new CelestialShapeSample(radius, Vector4.zero);
        }

        public virtual CelestialShapeSample EvaluateSample(
            float baseRadius,
            Vector3 unitDirection,
            float angularSampleFootprint)
        {
            return EvaluateSample(baseRadius, unitDirection);
        }

        public abstract float EvaluateDisplacement(float baseRadius, Vector3 unitDirection);

        protected void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
