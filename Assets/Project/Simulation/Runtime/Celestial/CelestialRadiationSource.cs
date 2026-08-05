using Farion.Simulation.Physics;
using UnityEngine;

namespace Farion.Simulation.Celestial
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CelestialBody))]
    public sealed class CelestialRadiationSource : MonoBehaviour
    {
        [SerializeField] CelestialBody body;
        [SerializeField] StellarRadiationProfile profile;

        public CelestialBody Body => body != null ? body : body = GetComponent<CelestialBody>();
        public StellarRadiationProfile Profile => profile;
        public Vector3 Position => Body != null ? Body.Position : transform.position;

        void OnValidate()
        {
            if (body == null)
            {
                body = GetComponent<CelestialBody>();
            }
        }

        public bool TrySampleInsolation(
            CelestialBody receiver,
            Vector3 worldPosition,
            Vector3 worldNormal,
            float bondAlbedo,
            out CelestialInsolationSample sample)
        {
            sample = CelestialInsolationSample.None;
            CelestialBody sourceBody = Body;
            if (sourceBody == null || profile == null || sourceBody == receiver)
            {
                return false;
            }

            Vector3 offsetToSource = Position - worldPosition;
            float distance = offsetToSource.magnitude;
            if (distance <= 0.0001f)
            {
                return false;
            }

            Vector3 directionToSource = offsetToSource / distance;
            Vector3 normal = worldNormal.sqrMagnitude > 0.0001f ? worldNormal.normalized : -directionToSource;
            float directExposure = Mathf.Clamp01(Vector3.Dot(normal, directionToSource));
            float irradiance = profile.EvaluateNormalizedIrradiance(distance);
            float equilibriumTemperature = profile.EvaluateEquilibriumTemperatureCelsius(distance, bondAlbedo);

            sample = new CelestialInsolationSample(
                sourceBody,
                Position,
                directionToSource,
                distance,
                irradiance,
                directExposure,
                equilibriumTemperature);
            return true;
        }
    }
}
