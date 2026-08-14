using UnityEngine;

namespace Farion.Core
{
    /// <summary>
    /// Shared scalar helpers that were duplicated across shape profiles and thruster VFX.
    /// </summary>
    public static class FarionMath
    {
        /// <summary>
        /// Frame-rate independent exponential approach: the fraction of the remaining
        /// distance to close this frame, for a given response rate.
        /// </summary>
        public static float Smooth(float current, float target, float response, float deltaTime)
        {
            float amount = response <= 0f
                ? 1f
                : 1f - Mathf.Exp(-response * Mathf.Max(0f, deltaTime));
            return Mathf.Lerp(current, target, amount);
        }

        /// <summary>Polynomial smooth minimum; <paramref name="k"/> is the blend width.</summary>
        public static float SmoothMin(float a, float b, float k)
        {
            if (k <= 0f)
            {
                return Mathf.Min(a, b);
            }

            float h = Mathf.Clamp01((b - a + k) / (2f * k));
            return a * h + b * (1f - h) - k * h * (1f - h);
        }

        /// <summary>Polynomial smooth maximum; <paramref name="k"/> is the blend width.</summary>
        public static float SmoothMax(float a, float b, float k)
        {
            if (k <= 0f)
            {
                return Mathf.Max(a, b);
            }

            return -SmoothMin(-a, -b, k);
        }
    }
}
