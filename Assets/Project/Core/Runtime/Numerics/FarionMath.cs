using UnityEngine;

namespace Farion.Core.Numerics
{
    public static class FarionMath
    {
        public static float Smooth(float current, float target, float response, float deltaTime)
        {
            return Mathf.Lerp(current, target, SmoothFactor(response, deltaTime));
        }

        public static float SmoothFactor(float response, float deltaTime)
        {
            return response <= 0f
                ? 1f
                : 1f - Mathf.Exp(-response * Mathf.Max(0f, deltaTime));
        }

        public static float SmoothMin(float a, float b, float k)
        {
            if (k <= 0f)
            {
                return Mathf.Min(a, b);
            }

            float h = Mathf.Clamp01((b - a + k) / (2f * k));
            return a * h + b * (1f - h) - k * h * (1f - h);
        }

        public static float SmoothMax(float a, float b, float k)
        {
            if (k <= 0f)
            {
                return Mathf.Max(a, b);
            }

            return -SmoothMin(-a, -b, k);
        }

        public static bool IsSameRotation(Quaternion current, Quaternion target)
        {
            const float ComponentEpsilon = 1e-7f;
            return Mathf.Abs(current.x - target.x) <= ComponentEpsilon &&
                Mathf.Abs(current.y - target.y) <= ComponentEpsilon &&
                Mathf.Abs(current.z - target.z) <= ComponentEpsilon &&
                Mathf.Abs(current.w - target.w) <= ComponentEpsilon;
        }
    }
}
