using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    public readonly struct CelestialOceanEffectData
    {
        public CelestialOceanEffectData(
            Vector3 center,
            float bodyRadius,
            float oceanRadius,
            float waveAmplitude,
            float waveLength,
            Vector3 wavePhases,
            Matrix4x4 worldToLocalRotation,
            CelestialOceanProfile profile,
            float atmosphereRadius = 0f,
            CelestialAtmosphereProfile atmosphereProfile = null)
        {
            Center = center;
            BodyRadius = bodyRadius;
            OceanRadius = oceanRadius;
            WaveAmplitude = waveAmplitude;
            WaveLength = waveLength;
            WavePhases = wavePhases;
            WorldToLocalRotation = worldToLocalRotation;
            Profile = profile;
            AtmosphereRadius = atmosphereRadius;
            AtmosphereProfile = atmosphereProfile;
        }

        public Vector3 Center { get; }
        public float BodyRadius { get; }
        public float OceanRadius { get; }
        public float WaveAmplitude { get; }
        public float WaveLength { get; }
        public Vector3 WavePhases { get; }
        public Matrix4x4 WorldToLocalRotation { get; }
        public CelestialOceanProfile Profile { get; }
        public float AtmosphereRadius { get; }
        public CelestialAtmosphereProfile AtmosphereProfile { get; }

        public float GetCameraSortDistance(Vector3 cameraPosition)
        {
            return Vector3.Distance(cameraPosition, Center) - OceanRadius;
        }

        public float GetSurfaceRadiusAt(Vector3 worldPosition)
        {
            Vector3 relativePosition = worldPosition - Center;
            return OceanRadius + OceanWaveField.SampleHeight(
                WorldToLocalRotation.MultiplyVector(relativePosition),
                WaveLength,
                WaveAmplitude,
                WavePhases);
        }

        public bool IsPointUnderwater(Vector3 worldPosition)
        {
            return GetSignedSurfaceDistance(worldPosition) < 0f;
        }

        public float GetSignedSurfaceDistance(Vector3 worldPosition)
        {
            float surfaceRadius = Mathf.Max(0f, GetSurfaceRadiusAt(worldPosition));
            return Vector3.Distance(worldPosition, Center) - surfaceRadius;
        }

    }
}
