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

        [Header("Environment Rendering")]
        [SerializeField] CelestialOceanProfile oceanProfile;
        [SerializeField] CelestialAtmosphereProfile atmosphereProfile;

        public event System.Action Changed;

        public ContinentRidgeShapeProfile ShapeProfile => shapeProfile;
        public TerrestrialSurfaceProfile SurfaceProfile => surfaceProfile;
        public CelestialOceanProfile OceanProfile => oceanProfile;
        public CelestialAtmosphereProfile AtmosphereProfile => atmosphereProfile;

        void OnValidate()
        {
            Changed?.Invoke();
        }
    }
}
