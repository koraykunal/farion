using Farion.Core.Physics;
using UnityEngine;

namespace Farion.Simulation.Celestial
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CelestialBody))]
    public sealed class CelestialBodyDefinitionAuthoring : MonoBehaviour
    {
        [SerializeField] CelestialBodyDefinition definition;
        [SerializeField] bool applyInEditMode = true;
        [SerializeField] bool applyOnAwake = true;
        [SerializeField] bool syncTransformScaleToRadius = true;

        CelestialBody body;

        public CelestialBodyDefinition Definition => definition;

        void Awake()
        {
            if (applyOnAwake)
            {
                Apply();
            }
        }

        void OnValidate()
        {
            if (!Application.isPlaying && applyInEditMode)
            {
                Apply();
            }
        }

        [ContextMenu("Apply Definition")]
        public void Apply()
        {
            if (definition == null)
            {
                return;
            }

            Body.ApplyDefinition(definition);

            if (syncTransformScaleToRadius)
            {
                float diameter = Body.Radius * 2f;
                transform.localScale = Vector3.one * diameter;
            }
        }

        CelestialBody Body
        {
            get
            {
                if (body == null)
                {
                    body = GetComponent<CelestialBody>();
                }

                return body;
            }
        }
    }
}
