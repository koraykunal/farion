using UnityEngine;

namespace Farion.Simulation.Planetary
{
    /// <summary>
    /// Sum of three travelling plane waves evaluated in body-relative space, so the
    /// same surface is shared by buoyancy and by the ocean shader.
    /// Keep in sync with Assets/Project/Art/Shaders/Celestial/FarionOceanWaves.hlsl.
    /// </summary>
    public static class OceanWaveField
    {
        public const int WaveCount = 3;
        const float FullTurn = Mathf.PI * 2f;

        static readonly Vector3[] Directions =
        {
            new Vector3(1f, 0f, 0.35f).normalized,
            new Vector3(-0.4f, 0.9f, 0.2f).normalized,
            new Vector3(0.3f, -0.5f, 0.8f).normalized
        };

        static readonly float[] Frequencies = { 1f, 2.13f, 4.31f };
        static readonly float[] Amplitudes = { 1f, 0.55f, 0.3f };
        const float AmplitudeSum = 1.85f;

        public static Vector3 GetPhases(double time, float speed)
        {
            const double fullTurn = System.Math.PI * 2.0;
            Vector3 phases = default;
            for (int i = 0; i < WaveCount; i++)
            {
                double angularSpeed = speed * System.Math.Sqrt(Frequencies[i]);
                phases[i] = (float)((time * angularSpeed) % fullTurn);
            }

            return phases;
        }

        public static float SampleHeight(
            Vector3 relativePosition,
            float waveLength,
            float amplitude,
            Vector3 phases)
        {
            if (amplitude <= 0f || waveLength <= 0f)
            {
                return 0f;
            }

            float sum = 0f;
            for (int i = 0; i < WaveCount; i++)
            {
                float phase = Vector3.Dot(relativePosition, Directions[i]) *
                    (FullTurn / waveLength) * Frequencies[i] + phases[i];
                sum += Amplitudes[i] * Mathf.Sin(phase);
            }

            return amplitude * sum / AmplitudeSum;
        }

        public static float SampleVerticalVelocity(
            Vector3 relativePosition,
            float waveLength,
            float amplitude,
            float speed,
            Vector3 phases)
        {
            if (amplitude <= 0f || waveLength <= 0f || speed <= 0f)
            {
                return 0f;
            }

            float velocity = 0f;
            for (int i = 0; i < WaveCount; i++)
            {
                float phase = Vector3.Dot(relativePosition, Directions[i]) *
                    (FullTurn / waveLength) * Frequencies[i] + phases[i];
                float angularSpeed = speed * Mathf.Sqrt(Frequencies[i]);
                velocity += Amplitudes[i] * Mathf.Cos(phase) * angularSpeed;
            }

            return amplitude * velocity / AmplitudeSum;
        }
    }
}
