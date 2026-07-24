using Farion.Simulation.Celestial;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [CreateAssetMenu(menuName = "Farion/Rendering/Terrestrial Planet Visual Profile", fileName = "SO_TerrestrialPlanetVisualProfile")]
    public sealed class TerrestrialPlanetVisualProfile : ScriptableObject
    {
        [Header("Terrain")]
        [SerializeField] ContinentRidgeShapeProfile shapeProfile;
        [SerializeField] TerrestrialSurfaceProfile surfaceProfile;

        [Header("Ocean")]
        [SerializeField] bool hasOcean = true;
        [SerializeField] CelestialOceanProfile oceanProfile;
        [Tooltip("Semantic sea level used for coastline, ocean physics, atmosphere base, and terrain waterline. 0 uses the generated terrain minimum radius; 1 uses the body's base radius.")]
        [Range(0f, 1f)]
        [SerializeField] float oceanLevel = 0.483f;

        [Header("Atmosphere")]
        [SerializeField] CelestialAtmosphereProfile atmosphereProfile;

        public event System.Action Changed;

        public ContinentRidgeShapeProfile ShapeProfile => shapeProfile;
        public TerrestrialSurfaceProfile SurfaceProfile => surfaceProfile;
        public bool HasOcean => hasOcean && oceanProfile != null;
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
