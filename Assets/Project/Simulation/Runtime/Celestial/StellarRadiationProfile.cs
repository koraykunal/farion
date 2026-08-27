using UnityEngine;

namespace Farion.Simulation.Celestial
{
    [CreateAssetMenu(menuName = "Farion/Simulation/Celestial/Stellar Radiation Profile", fileName = "SO_StellarRadiation")]
    public sealed class StellarRadiationProfile : ScriptableObject
    {
        const float AbsoluteZeroCelsius = -273.15f;

        [Header("Star")]
        [SerializeField, Min(0f)] float luminosity = 1f;
        [SerializeField, Min(1000f)] float colorTemperatureKelvin = 5778f;

        [Header("Reference Orbit")]
        [Tooltip("Orbit distance at which the reference equilibrium temperature is reached, measured in star radii. Expressed relative to the star so a system keeps its climate when every length in it is rescaled.")]
        [SerializeField, Min(0.001f)] float referenceOrbitDistanceInStarRadii = 3.33f;
        [SerializeField] float referenceEquilibriumTemperatureCelsius = -18f;
        [SerializeField, Range(0f, 1f)] float referenceBondAlbedo = 0.3f;

        [Header("Limits")]
        [SerializeField] Vector2 normalizedIrradianceRange = new(0.001f, 128f);
        [SerializeField] Vector2 equilibriumTemperatureRangeCelsius = new(-220f, 180f);

        public float Luminosity => Mathf.Max(0f, luminosity);
        public float ColorTemperatureKelvin => Mathf.Max(1000f, colorTemperatureKelvin);
        public float ReferenceOrbitDistanceInStarRadii =>
            Mathf.Max(0.001f, referenceOrbitDistanceInStarRadii);
        public float ReferenceEquilibriumTemperatureCelsius => Mathf.Max(AbsoluteZeroCelsius, referenceEquilibriumTemperatureCelsius);
        public float ReferenceBondAlbedo => Mathf.Clamp01(referenceBondAlbedo);

        void OnValidate()
        {
            luminosity = Mathf.Max(0f, luminosity);
            colorTemperatureKelvin = Mathf.Max(1000f, colorTemperatureKelvin);
            referenceOrbitDistanceInStarRadii = Mathf.Max(0.001f, referenceOrbitDistanceInStarRadii);
            referenceEquilibriumTemperatureCelsius = Mathf.Max(AbsoluteZeroCelsius, referenceEquilibriumTemperatureCelsius);
            referenceBondAlbedo = Mathf.Clamp01(referenceBondAlbedo);
            NormalizePositiveRange(ref normalizedIrradianceRange, 0.001f, 128f);
            NormalizeTemperatureRange(ref equilibriumTemperatureRangeCelsius);
        }

        public float EvaluateNormalizedIrradiance(float distance, float starRadius)
        {
            float safeDistance = Mathf.Max(0.001f, distance);
            float referenceDistance =
                ReferenceOrbitDistanceInStarRadii * Mathf.Max(0.001f, starRadius);
            float distanceScale = referenceDistance / safeDistance;
            float irradiance = Luminosity * distanceScale * distanceScale;
            return Mathf.Clamp(irradiance, normalizedIrradianceRange.x, normalizedIrradianceRange.y);
        }

        public float EvaluateEquilibriumTemperatureCelsius(
            float distance,
            float bondAlbedo,
            float starRadius)
        {
            float irradiance = EvaluateNormalizedIrradiance(distance, starRadius);
            float referenceKelvin = ReferenceEquilibriumTemperatureCelsius - AbsoluteZeroCelsius;
            float albedoRatio = Mathf.Clamp01(1f - bondAlbedo) / Mathf.Max(0.0001f, 1f - ReferenceBondAlbedo);
            float kelvin = referenceKelvin * Mathf.Pow(Mathf.Max(0.0001f, irradiance * albedoRatio), 0.25f);
            float celsius = kelvin + AbsoluteZeroCelsius;
            return Mathf.Clamp(celsius, equilibriumTemperatureRangeCelsius.x, equilibriumTemperatureRangeCelsius.y);
        }

        static void NormalizePositiveRange(ref Vector2 range, float fallbackMin, float fallbackMax)
        {
            range.x = Mathf.Max(0.0001f, range.x);
            range.y = Mathf.Max(range.x, range.y);
            if (range.y <= 0.0001f)
            {
                range = new Vector2(fallbackMin, fallbackMax);
            }
        }

        static void NormalizeTemperatureRange(ref Vector2 range)
        {
            range.x = Mathf.Max(AbsoluteZeroCelsius, range.x);
            range.y = Mathf.Max(range.x, range.y);
        }
    }
}
