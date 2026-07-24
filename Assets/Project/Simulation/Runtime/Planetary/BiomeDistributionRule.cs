using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Farion.Simulation.Planetary
{
    [Serializable]
    public sealed class BiomeDistributionRule
    {
        [SerializeField] BiomeDefinition biome;
        [Min(0f)]
        [FormerlySerializedAs("weight")]
        [SerializeField] float selectionPriority = 1f;
        [SerializeField] Vector2 altitudeRange = new(-1000000f, 1000000f);
        [SerializeField] Vector2 slopeRange = new(0f, 90f);
        [Range(0f, 1f)]
        [SerializeField] float minTemperatureNoise;
        [Range(0f, 1f)]
        [SerializeField] float maxTemperatureNoise = 1f;
        [Range(0f, 1f)]
        [SerializeField] float minMoistureNoise;
        [Range(0f, 1f)]
        [SerializeField] float maxMoistureNoise = 1f;
        [SerializeField] Vector2 localTemperatureRange = new(-1000000f, 1000000f);
        [SerializeField] Vector2 localMoistureRange = new(0f, 1f);
        [SerializeField] Vector2 localRadiationRange = new(0f, 1f);

        public BiomeDefinition Biome => biome;
        public float SelectionPriority => Mathf.Max(0f, selectionPriority);

        public bool Allows(float altitude, float slopeDegrees, float temperatureNoise, float moistureNoise)
        {
            return Allows(altitude, slopeDegrees, temperatureNoise, moistureNoise, temperatureNoise, moistureNoise, 0f);
        }

        public bool Allows(
            float altitude,
            float slopeDegrees,
            float temperatureNoise,
            float moistureNoise,
            float localTemperatureCelsius,
            float localMoisture,
            float localRadiation)
        {
            if (biome == null || SelectionPriority <= 0f)
            {
                return false;
            }

            return altitude >= altitudeRange.x &&
                altitude <= altitudeRange.y &&
                slopeDegrees >= slopeRange.x &&
                slopeDegrees <= slopeRange.y &&
                temperatureNoise >= minTemperatureNoise &&
                temperatureNoise <= maxTemperatureNoise &&
                moistureNoise >= minMoistureNoise &&
                moistureNoise <= maxMoistureNoise &&
                ContainsConfiguredRange(localTemperatureRange, localTemperatureCelsius, -1000000f, 1000000f) &&
                ContainsConfiguredRange(localMoistureRange, localMoisture, 0f, 1f) &&
                ContainsConfiguredRange(localRadiationRange, localRadiation, 0f, 1f);
        }

        public float EvaluateSuitability(
            float altitude,
            float slopeDegrees,
            float temperatureNoise,
            float moistureNoise,
            float localTemperatureCelsius,
            float localMoisture,
            float localRadiation,
            BiomeDistributionProfile profile)
        {
            if (biome == null || SelectionPriority <= 0f)
            {
                return 0f;
            }

            float score = SelectionPriority;
            score *= EvaluateRange(altitudeRange, altitude, profile != null ? profile.AltitudeBlend : 0f, -1000000f, 1000000f);
            score *= EvaluateRange(slopeRange, slopeDegrees, profile != null ? profile.SlopeBlendDegrees : 0f, 0f, 90f);
            score *= EvaluateRange(new Vector2(minTemperatureNoise, maxTemperatureNoise), temperatureNoise, profile != null ? profile.NoiseBlend : 0f, 0f, 1f);
            score *= EvaluateRange(new Vector2(minMoistureNoise, maxMoistureNoise), moistureNoise, profile != null ? profile.NoiseBlend : 0f, 0f, 1f);
            score *= EvaluateRange(localTemperatureRange, localTemperatureCelsius, profile != null ? profile.LocalTemperatureBlendCelsius : 0f, -1000000f, 1000000f);
            score *= EvaluateRange(localMoistureRange, localMoisture, profile != null ? profile.LocalMoistureBlend : 0f, 0f, 1f);
            score *= EvaluateRange(localRadiationRange, localRadiation, profile != null ? profile.LocalRadiationBlend : 0f, 0f, 1f);
            return Mathf.Max(0f, score);
        }

        static bool ContainsConfiguredRange(Vector2 range, float value, float defaultMin, float defaultMax)
        {
            bool unconfiguredSerializedRange = Mathf.Approximately(range.x, 0f) && Mathf.Approximately(range.y, 0f);
            float min = unconfiguredSerializedRange ? defaultMin : range.x;
            float max = unconfiguredSerializedRange ? defaultMax : range.y;
            if (max < min)
            {
                max = min;
            }

            return value >= min && value <= max;
        }

        static float EvaluateRange(Vector2 range, float value, float blend, float defaultMin, float defaultMax)
        {
            bool unconfiguredSerializedRange = Mathf.Approximately(range.x, 0f) && Mathf.Approximately(range.y, 0f);
            float min = unconfiguredSerializedRange ? defaultMin : range.x;
            float max = unconfiguredSerializedRange ? defaultMax : range.y;
            if (max < min)
            {
                max = min;
            }

            if (value < min - blend || value > max + blend)
            {
                return 0f;
            }

            if (blend <= 0f)
            {
                return value >= min && value <= max ? 1f : 0f;
            }

            float lower = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(min - blend, min + blend, value));
            float upper = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(max - blend, max + blend, value));
            return Mathf.Clamp01(Mathf.Min(lower, upper));
        }
    }
}
