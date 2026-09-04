using UnityEngine;

namespace Farion.Simulation.Planetary
{
    [CreateAssetMenu(
        menuName = "Farion/Simulation/Planetary/Hydrosphere Profile",
        fileName = "SO_PlanetHydrosphere")]
    public sealed class PlanetHydrosphereProfile : ScriptableObject
    {
        [SerializeField] bool hasSurfaceOcean = true;
        [Tooltip("Sea level between the generated terrain minimum radius (0) and the body's base radius (1).")]
        [Range(0f, 1f)]
        [SerializeField] float oceanLevel = 0.5f;
        [Tooltip("Planet-scale water availability used by the climate model. This is not a second sea-level value.")]
        [Range(0f, 1f)]
        [SerializeField] float waterAvailability = 0.35f;
        [Tooltip("Physical offset applied to the computed sea-level radius.")]
        [Min(0f)]
        [SerializeField] float oceanRadiusOffset;

        [Header("Swell")]
        [Tooltip("Crest-to-sea-level height of the shared wave field, in world units. Drives both buoyancy and the ocean shader.")]
        [Min(0f)]
        [SerializeField] float waveAmplitude = 0.35f;
        [Tooltip("Distance between crests of the longest wave, in world units.")]
        [Min(0.1f)]
        [SerializeField] float waveLength = 26f;
        [Tooltip("Angular speed of the swell. Zero freezes the surface.")]
        [Min(0f)]
        [SerializeField] float waveSpeed = 0.7f;

        public bool HasSurfaceOcean => hasSurfaceOcean;
        public float OceanLevel => Mathf.Clamp01(oceanLevel);
        public float WaterAvailability => hasSurfaceOcean ? Mathf.Clamp01(waterAvailability) : 0f;
        public float OceanRadiusOffset => Mathf.Max(0f, oceanRadiusOffset);
        public float WaveAmplitude => Mathf.Max(0f, waveAmplitude);
        public float WaveLength => Mathf.Max(0.1f, waveLength);
        public float WaveSpeed => Mathf.Max(0f, waveSpeed);

        internal PlanetHydrosphereProfile CreateVariant(float newOceanLevel, float newWaterAvailability)
        {
            PlanetHydrosphereProfile variant = ProfileVariants.Clone(this);
            if (hasSurfaceOcean)
            {
                variant.oceanLevel = Mathf.Clamp01(newOceanLevel);
                variant.waterAvailability = Mathf.Clamp01(newWaterAvailability);
            }

            return variant;
        }

        void OnValidate()
        {
            oceanLevel = Mathf.Clamp01(oceanLevel);
            waterAvailability = hasSurfaceOcean ? Mathf.Clamp01(waterAvailability) : 0f;
            oceanRadiusOffset = Mathf.Max(0f, oceanRadiusOffset);
            waveAmplitude = Mathf.Max(0f, waveAmplitude);
            waveLength = Mathf.Max(0.1f, waveLength);
            waveSpeed = Mathf.Max(0f, waveSpeed);
        }
    }
}
