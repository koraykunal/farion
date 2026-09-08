using UnityEngine;

namespace Farion.Simulation.Planetary
{
    /// <summary>
    /// Sum of nine travelling plane waves in three dispersion groups, evaluated in
    /// body-relative space so buoyancy and the ocean shader share one surface.
    /// Each group advances with its own phase; the directions inside a group cross
    /// each other so the swell reads as interfering seas instead of parallel bands.
    /// Keep in sync with Assets/Project/Art/Shaders/Celestial/FarionOceanWaves.hlsl.
    /// </summary>
    public static class OceanWaveField
    {
        public const int WaveCount = 9;
        public const int GroupCount = 3;
        const float FullTurn = Mathf.PI * 2f;

        static readonly Vector3[] Directions =
        {
            new(0.9438584f, 0.0000000f, 0.3303504f),
            new(0.9282791f, 0.3094264f, 0.2062842f),
            new(0.7752025f, -0.3391511f, 0.5329517f),
            new(-0.3980149f, 0.8955335f, 0.1990074f),
            new(-0.0998752f, 0.9488147f, -0.2996257f),
            new(-0.5848442f, 0.6823183f, 0.4386332f),
            new(0.3030458f, -0.5050763f, 0.8081220f),
            new(0.5628090f, -0.3069867f, 0.7674668f),
            new(0.0504433f, -0.7062066f, 0.7062066f)
        };

        static readonly float[] GroupFrequencies = { 1f, 2.13f, 4.31f };
        static readonly float[] Frequencies =
            { 1f, 0.91f, 1.12f, 2.13f, 1.9383f, 2.3856f, 4.31f, 3.9221f, 4.8272f };
        static readonly float[] Amplitudes =
            { 0.5f, 0.3f, 0.25f, 0.28f, 0.18f, 0.14f, 0.15f, 0.1f, 0.08f };
        static readonly float[] PhaseOffsets =
            { 0f, 1.7f, 3.9f, 0.6f, 2.8f, 5.1f, 1.3f, 4.4f, 2.2f };
        const float AmplitudeSum = 1.98f;

        public static Vector3 GetPhases(double time, float speed)
        {
            const double fullTurn = System.Math.PI * 2.0;
            Vector3 phases = default;
            for (int i = 0; i < GroupCount; i++)
            {
                double angularSpeed = speed * System.Math.Sqrt(GroupFrequencies[i]);
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
                sum += Amplitudes[i] * Mathf.Sin(Phase(i, relativePosition, waveLength, phases));
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
                float angularSpeed = speed * Mathf.Sqrt(GroupFrequencies[i / GroupCount]);
                velocity += Amplitudes[i] * Mathf.Cos(Phase(i, relativePosition, waveLength, phases)) *
                    angularSpeed;
            }

            return amplitude * velocity / AmplitudeSum;
        }

        static float Phase(int wave, Vector3 relativePosition, float waveLength, Vector3 phases)
        {
            return Vector3.Dot(relativePosition, Directions[wave]) * (FullTurn / waveLength) *
                Frequencies[wave] + phases[wave / GroupCount] + PhaseOffsets[wave];
        }
    }
}
