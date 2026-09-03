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

        public static void Spring(
            ref float position,
            ref float velocity,
            float target,
            float stiffness,
            float damping,
            float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            velocity += (stiffness * (target - position) - damping * velocity) * deltaTime;
            position += velocity * deltaTime;
        }

        public static void Spring(
            ref Vector3 position,
            ref Vector3 velocity,
            Vector3 target,
            float stiffness,
            float damping,
            float deltaTime)
        {
            Spring(ref position.x, ref velocity.x, target.x, stiffness, damping, deltaTime);
            Spring(ref position.y, ref velocity.y, target.y, stiffness, damping, deltaTime);
            Spring(ref position.z, ref velocity.z, target.z, stiffness, damping, deltaTime);
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
