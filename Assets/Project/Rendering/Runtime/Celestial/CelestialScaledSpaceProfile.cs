using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [CreateAssetMenu(
        menuName = "Farion/Rendering/Celestial/Scaled Space Profile",
        fileName = "SO_CelestialScaledSpaceProfile")]
    public sealed class CelestialScaledSpaceProfile : ScriptableObject
    {
        [Min(1f)]
        [SerializeField] float transitionDistance = 10000f;
        [Min(1f)]
        [SerializeField] float compressionScale = 4000f;
        [Min(0f)]
        [SerializeField] float hysteresis = 1000f;
        [Range(0.5f, 0.95f)]
        [SerializeField] float farClipFraction = 0.9f;

        public float TransitionDistance => transitionDistance;

        public bool ShouldUseScaledSpace(float physicalDistance, bool currentlyUsingScaledSpace)
        {
            float exitDistance = Mathf.Max(0f, transitionDistance - hysteresis);
            return physicalDistance > (currentlyUsingScaledSpace ? exitDistance : transitionDistance);
        }

        public float ResolveDisplayDistance(Camera observer, float physicalDistance)
        {
            physicalDistance = Mathf.Max(0f, physicalDistance);
            if (physicalDistance <= transitionDistance)
            {
                return physicalDistance;
            }

            float compressedDistance = transitionDistance
                + compressionScale * Mathf.Log(
                    1f + (physicalDistance - transitionDistance) / compressionScale);
            if (observer == null)
            {
                return compressedDistance;
            }

            return Mathf.Min(compressedDistance, observer.farClipPlane * farClipFraction);
        }

        void OnValidate()
        {
            transitionDistance = Mathf.Max(1f, transitionDistance);
            compressionScale = Mathf.Max(1f, compressionScale);
            hysteresis = Mathf.Clamp(hysteresis, 0f, transitionDistance);
            farClipFraction = Mathf.Clamp(farClipFraction, 0.5f, 0.95f);
        }
    }
}
