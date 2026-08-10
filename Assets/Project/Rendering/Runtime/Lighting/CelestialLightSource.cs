using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using UnityEngine;

namespace Farion.Rendering.Lighting
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CelestialBody))]
    public sealed class CelestialLightSource : MonoBehaviour
    {
        [SerializeField] CelestialBody body;
        [SerializeField] CelestialRadiationSource radiationSource;

        public CelestialBody Body => body != null ? body : body = GetComponent<CelestialBody>();
        public CelestialRadiationSource RadiationSource => radiationSource != null ? radiationSource : radiationSource = GetComponent<CelestialRadiationSource>();
        public bool HasRadiationProfile => RadiationSource != null && RadiationSource.Profile != null;
        public float ColorTemperatureKelvin => HasRadiationProfile ? RadiationSource.Profile.ColorTemperatureKelvin : 5778f;
        public Vector3 Position => Body != null ? Body.Position : transform.position;
        public float Radius => Body != null ? Body.Radius : 1f;

        void OnValidate()
        {
            if (body == null)
            {
                body = GetComponent<CelestialBody>();
            }

            if (radiationSource == null)
            {
                radiationSource = GetComponent<CelestialRadiationSource>();
            }
        }
    }
}
