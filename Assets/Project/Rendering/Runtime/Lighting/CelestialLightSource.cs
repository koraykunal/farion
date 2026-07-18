using Farion.Core.Physics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Farion.Rendering.Lighting
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CelestialBody))]
    public sealed class CelestialLightSource : MonoBehaviour
    {
        [SerializeField] CelestialBody body;
        [SerializeField] Material emissionMaterial;
        [SerializeField] bool applyEmissionMaterial = true;
        [SerializeField] bool disableShadowCasting = true;
        [SerializeField] bool disableShadowReceiving = true;
        [SerializeField] bool disableProbeLighting = true;

        public CelestialBody Body => body != null ? body : body = GetComponent<CelestialBody>();
        public Vector3 Position => Body != null ? Body.Position : transform.position;
        public float Radius => Body != null ? Body.Radius : 1f;

        void OnEnable()
        {
            ApplyStarRendererSettings();
        }

        void OnValidate()
        {
            if (body == null)
            {
                body = GetComponent<CelestialBody>();
            }

            ApplyStarRendererSettings();
        }

        [ContextMenu("Apply Star Renderer Settings")]
        public void ApplyStarRendererSettings()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer starRenderer = renderers[i];
                if (applyEmissionMaterial && emissionMaterial != null)
                {
                    starRenderer.sharedMaterial = emissionMaterial;
                }

                if (disableShadowCasting)
                {
                    starRenderer.shadowCastingMode = ShadowCastingMode.Off;
                }

                if (disableShadowReceiving)
                {
                    starRenderer.receiveShadows = false;
                }

                if (disableProbeLighting)
                {
                    starRenderer.lightProbeUsage = LightProbeUsage.Off;
                    starRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
            }
        }
    }
}
