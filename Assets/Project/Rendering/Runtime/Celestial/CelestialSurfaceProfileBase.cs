using UnityEngine;

namespace Farion.Rendering.Celestial
{
    public abstract class CelestialSurfaceProfileBase : ScriptableObject
    {
        public event System.Action Changed;

        public abstract Material Material { get; }

        public virtual float EvaluateRadius(float baseRadius, Vector3 unitDirection)
        {
            return Mathf.Max(0.01f, baseRadius);
        }

        public abstract void ApplyMaterialProperties(
            MaterialPropertyBlock propertyBlock,
            float bodyRadius,
            Vector2 radiusMinMax);

        protected void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
