using UnityEngine;

namespace Farion.Simulation.Planetary
{
    public readonly struct PlanetSurfaceStateSample
    {
        public PlanetSurfaceStateSample(float volcanicActivity, float snowCover, float wetness)
        {
            VolcanicActivity = Mathf.Clamp01(volcanicActivity);
            SnowCover = Mathf.Clamp01(snowCover);
            Wetness = Mathf.Clamp01(wetness);
        }

        public float VolcanicActivity { get; }
        public float SnowCover { get; }
        public float Wetness { get; }
    }

    [CreateAssetMenu(
        menuName = "Farion/Simulation/Planetary/Surface State Profile",
        fileName = "SO_PlanetSurfaceState")]
    public sealed class PlanetSurfaceStateProfile : ScriptableObject
    {
        [Header("Volcanism")]
        [Range(0f, 1f)]
        [SerializeField] float globalVolcanicActivity = 0.06f;
        [Min(0.01f)]
        [SerializeField] float volcanicNoiseScale = 5f;
        [Range(0f, 1f)]
        [SerializeField] float volcanicThreshold = 0.78f;
        [Range(0.001f, 0.5f)]
        [SerializeField] float volcanicBlend = 0.08f;
        [Range(0f, 90f)]
        [SerializeField] float maximumVolcanicSlope = 55f;

        [Header("Snow")]
        [SerializeField] float snowStartCelsius = 2f;
        [SerializeField] float fullSnowCelsius = -12f;
        [Range(0f, 1f)]
        [SerializeField] float minimumSnowMoisture = 0.18f;

        public float GlobalVolcanicActivity => globalVolcanicActivity;

        internal PlanetSurfaceStateProfile CreateVariant(float volcanicActivity)
        {
            PlanetSurfaceStateProfile variant = ProfileVariants.Clone(this);
            variant.globalVolcanicActivity = Mathf.Clamp01(volcanicActivity);
            return variant;
        }

        public PlanetSurfaceStateSample Evaluate(
            PlanetGenerationContext context,
            PlanetClimateSample climate,
            Vector3 localDirection,
            float slopeDegrees)
        {
            float volcanicNoise = PlanetarySampling.SampleFractal01(
                localDirection,
                volcanicNoiseScale,
                4,
                2.1f,
                0.5f,
                SeedUtility.Derive(context.PlanetSeed, "surface.volcanism"));
            float effectiveThreshold = Mathf.Lerp(1f, volcanicThreshold, globalVolcanicActivity);
            float volcanism = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    effectiveThreshold - volcanicBlend,
                    effectiveThreshold + volcanicBlend,
                    volcanicNoise));
            float slopeMask = 1f - Mathf.SmoothStep(
                maximumVolcanicSlope - 10f,
                maximumVolcanicSlope,
                slopeDegrees);
            volcanism *= slopeMask;

            float snowTemperature = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    Mathf.Min(fullSnowCelsius, snowStartCelsius),
                    Mathf.Max(fullSnowCelsius, snowStartCelsius),
                    climate.TemperatureCelsius));
            float snowMoisture = Mathf.SmoothStep(
                minimumSnowMoisture,
                Mathf.Min(1f, minimumSnowMoisture + 0.2f),
                climate.EffectiveMoisture);
            float snow = snowTemperature * snowMoisture;
            float wetness = climate.EffectiveMoisture * (1f - snow) * (1f - volcanism);

            return new PlanetSurfaceStateSample(volcanism, snow, wetness);
        }

        void OnValidate()
        {
            globalVolcanicActivity = Mathf.Clamp01(globalVolcanicActivity);
            volcanicNoiseScale = Mathf.Max(0.01f, volcanicNoiseScale);
            volcanicThreshold = Mathf.Clamp01(volcanicThreshold);
            volcanicBlend = Mathf.Clamp(volcanicBlend, 0.001f, 0.5f);
            maximumVolcanicSlope = Mathf.Clamp(maximumVolcanicSlope, 0f, 90f);
            fullSnowCelsius = Mathf.Min(fullSnowCelsius, snowStartCelsius);
            minimumSnowMoisture = Mathf.Clamp01(minimumSnowMoisture);
        }
    }
}
