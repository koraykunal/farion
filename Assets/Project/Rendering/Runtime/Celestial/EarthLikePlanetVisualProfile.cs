using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [CreateAssetMenu(menuName = "Farion/Rendering/Earth-Like Planet Visual Profile", fileName = "SO_EarthLikePlanetVisualProfile")]
    public sealed class EarthLikePlanetVisualProfile : ScriptableObject
    {
        [Header("Terrain")]
        [SerializeField] EarthLikeShapeProfile shapeProfile;
        [SerializeField] EarthLikeSurfaceProfile surfaceProfile;

        [Header("Ocean")]
        [SerializeField] CelestialOceanProfile oceanProfile;
        [Range(0f, 1f)]
        [SerializeField] float oceanLevel = 0.483f;

        [Header("Atmosphere")]
        [SerializeField] CelestialAtmosphereProfile atmosphereProfile;

        public event System.Action Changed;

        public EarthLikeShapeProfile ShapeProfile => shapeProfile;
        public EarthLikeSurfaceProfile SurfaceProfile => surfaceProfile;
        public CelestialOceanProfile OceanProfile => oceanProfile;
        public float OceanLevel => oceanLevel;
        public CelestialAtmosphereProfile AtmosphereProfile => atmosphereProfile;

        void OnValidate()
        {
            oceanLevel = Mathf.Clamp01(oceanLevel);
            Changed?.Invoke();
        }
    }
}
