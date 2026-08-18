using Farion.Simulation.Physics;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Simulation.Celestial
{
    public readonly struct CelestialEnvironmentSample
    {
        public CelestialEnvironmentSample(
            CelestialBody body,
            bool hasOcean,
            float oceanRadius,
            bool hasAtmosphere,
            float atmosphereRadius,
            Vector2 terrainRadiusMinMax,
            float atmosphereBaseRadius,
            float waveAmplitude = 0f,
            float waveLength = 1f,
            float waveSpeed = 0f,
            double waveTime = 0d)
        {
            Body = body;
            HasOcean = hasOcean;
            OceanRadius = Mathf.Max(0f, oceanRadius);
            HasAtmosphere = hasAtmosphere;
            AtmosphereRadius = Mathf.Max(0f, atmosphereRadius);
            TerrainRadiusMinMax = terrainRadiusMinMax;
            AtmosphereBaseRadius = Mathf.Max(0f, atmosphereBaseRadius);
            WaveAmplitude = Mathf.Max(0f, waveAmplitude);
            WaveLength = Mathf.Max(0.0001f, waveLength);
            WaveSpeed = Mathf.Max(0f, waveSpeed);
            WaveTime = waveTime >= 0d ? waveTime : 0d;
            WavePhases = OceanWaveField.GetPhases(WaveTime, WaveSpeed);
            Quaternion bodyRotation = body != null && body.UsesAnalyticMotion
                ? body.EvaluateAnalyticRotation(WaveTime)
                : (body != null ? body.transform.rotation : Quaternion.identity);
            WorldToBodyRotation = Quaternion.Inverse(bodyRotation);
        }

        public CelestialBody Body { get; }
        public bool HasBody => Body != null;
        public bool HasOcean { get; }
        public float OceanRadius { get; }
        public bool HasAtmosphere { get; }
        public float AtmosphereRadius { get; }
        public Vector2 TerrainRadiusMinMax { get; }
        public float AtmosphereBaseRadius { get; }
        public float WaveAmplitude { get; }
        public float WaveLength { get; }
        public float WaveSpeed { get; }
        public double WaveTime { get; }
        public Vector3 WavePhases { get; }
        public Quaternion WorldToBodyRotation { get; }

        public float GetOceanRadiusAt(Vector3 relativePosition)
        {
            if (!HasOcean)
            {
                return OceanRadius;
            }

            return OceanRadius + OceanWaveField.SampleHeight(
                ToBodyLocal(relativePosition),
                WaveLength,
                WaveAmplitude,
                WavePhases);
        }

        public Vector3 GetOceanSurfaceVelocityAt(Vector3 relativePosition)
        {
            if (!HasOcean || WaveAmplitude <= 0f || WaveSpeed <= 0f)
            {
                return Vector3.zero;
            }

            Vector3 radialUp = relativePosition.sqrMagnitude > 0.0001f
                ? relativePosition.normalized
                : Vector3.up;
            float verticalVelocity = OceanWaveField.SampleVerticalVelocity(
                ToBodyLocal(relativePosition),
                WaveLength,
                WaveAmplitude,
                WaveSpeed,
                WavePhases);
            return radialUp * verticalVelocity;
        }

        public CelestialEnvironmentSample AtSimulationTime(double simulationTime) =>
            new(
                Body,
                HasOcean,
                OceanRadius,
                HasAtmosphere,
                AtmosphereRadius,
                TerrainRadiusMinMax,
                AtmosphereBaseRadius,
                WaveAmplitude,
                WaveLength,
                WaveSpeed,
                simulationTime);

        public static CelestialEnvironmentSample Empty(CelestialBody body)
        {
            float radius = body != null ? Mathf.Max(0.01f, body.Radius) : 0f;
            return new CelestialEnvironmentSample(
                body,
                false,
                0f,
                false,
                0f,
                new Vector2(radius, radius),
                radius);
        }

        Vector3 ToBodyLocal(Vector3 relativePosition) =>
            WorldToBodyRotation * relativePosition;
    }
}
