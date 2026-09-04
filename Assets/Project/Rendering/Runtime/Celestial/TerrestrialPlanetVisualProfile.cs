using System.Collections.Generic;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [CreateAssetMenu(menuName = "Farion/Rendering/Celestial/Terrestrial Planet Visual Profile", fileName = "SO_PlanetVisual")]
    public sealed class TerrestrialPlanetVisualProfile : ScriptableObject
    {
        [Header("Templates")]
        [Tooltip("Cloned per planet; the planet seed picks texture variants and tints inside the surface visual library.")]
        [SerializeField] TerrestrialSurfaceProfile surfaceProfile;
        [SerializeField] CelestialOceanProfile oceanProfile;
        [SerializeField] CelestialAtmosphereProfile atmosphereProfile;
        [SerializeField] CelestialCloudProfile cloudProfile;

        [Header("Seed Variation")]
        [Tooltip("Maximum nanometres each scattering wavelength drifts up or down.")]
        [Min(0f)]
        [SerializeField] float atmosphereWavelengthShift;
        [SerializeField] Vector2 atmosphereScatteringScaleRange = new(1f, 1f);
        [Range(0f, 180f)]
        [SerializeField] float oceanHueShift;
        [SerializeField] Vector2 cloudCoverageScaleRange = new(1f, 1f);
        [Range(0f, 180f)]
        [SerializeField] float surfaceHueShift;
        [Range(0f, 0.5f)]
        [SerializeField] float surfaceValueJitter;

        public event System.Action Changed;

        public TerrestrialSurfaceProfile SurfaceProfile => surfaceProfile;
        public CelestialOceanProfile OceanProfile => oceanProfile;
        public CelestialAtmosphereProfile AtmosphereProfile => atmosphereProfile;
        public CelestialCloudProfile CloudProfile => cloudProfile;

        void OnValidate()
        {
            Changed?.Invoke();
        }

        public DerivedPlanetVisual Derive(int seed, IReadOnlyList<SurfaceMaterialDefinition> materials)
        {
            SurfaceVisualProfile surfaceVisual = surfaceProfile != null && surfaceProfile.SurfaceVisualProfile != null
                ? surfaceProfile.SurfaceVisualProfile.CreateVariant(materials, seed, surfaceHueShift, surfaceValueJitter)
                : null;
            TerrestrialSurfaceProfile surface = surfaceProfile != null
                ? surfaceProfile.CreateVariant(surfaceVisual)
                : null;
            CelestialOceanProfile ocean = oceanProfile != null
                ? oceanProfile.CreateVariant((SeedUtility.Unit01(seed, "visual.ocean.hue") * 2f - 1f) * oceanHueShift)
                : null;
            CelestialAtmosphereProfile atmosphere = atmosphereProfile != null
                ? atmosphereProfile.CreateVariant(
                    new Vector3(
                        Signed(seed, "visual.atmosphere.r"),
                        Signed(seed, "visual.atmosphere.g"),
                        Signed(seed, "visual.atmosphere.b")) * atmosphereWavelengthShift,
                    SeedUtility.Range(seed, "visual.atmosphere.scattering", atmosphereScatteringScaleRange))
                : null;
            CelestialCloudProfile clouds = cloudProfile != null
                ? cloudProfile.CreateVariant(SeedUtility.Range(seed, "visual.cloud.coverage", cloudCoverageScaleRange))
                : null;
            return new DerivedPlanetVisual(surfaceVisual, surface, ocean, atmosphere, clouds);
        }

        static float Signed(int seed, string stream)
        {
            return SeedUtility.Unit01(seed, stream) * 2f - 1f;
        }
    }
}
