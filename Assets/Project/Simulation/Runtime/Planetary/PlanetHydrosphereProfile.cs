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

        public bool HasSurfaceOcean => hasSurfaceOcean;
        public float OceanLevel => Mathf.Clamp01(oceanLevel);
        public float WaterAvailability => hasSurfaceOcean ? Mathf.Clamp01(waterAvailability) : 0f;
        public float OceanRadiusOffset => Mathf.Max(0f, oceanRadiusOffset);

        void OnValidate()
        {
            oceanLevel = Mathf.Clamp01(oceanLevel);
            waterAvailability = hasSurfaceOcean ? Mathf.Clamp01(waterAvailability) : 0f;
            oceanRadiusOffset = Mathf.Max(0f, oceanRadiusOffset);
        }
    }
}
