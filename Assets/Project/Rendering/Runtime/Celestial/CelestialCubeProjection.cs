using UnityEngine;

namespace Farion.Rendering.Celestial
{
    public enum CelestialCubeFace
    {
        PositiveX,
        NegativeX,
        PositiveY,
        NegativeY,
        PositiveZ,
        NegativeZ
    }

    public static class CelestialCubeProjection
    {
        const float WarpScale = Mathf.PI * 0.25f;

        public static void Project(
            Vector3 direction,
            out CelestialCubeFace face,
            out float u,
            out float v)
        {
            ProjectLinear(direction, out face, out float linearU, out float linearV);
            u = Unwarp(linearU);
            v = Unwarp(linearV);
        }

        public static Vector3 ToDirection(CelestialCubeFace face, float u, float v)
        {
            float warpedU = Warp(u);
            float warpedV = Warp(v);
            Vector3 direction = face switch
            {
                CelestialCubeFace.PositiveX => new Vector3(1f, -warpedV, -warpedU),
                CelestialCubeFace.NegativeX => new Vector3(-1f, -warpedV, warpedU),
                CelestialCubeFace.PositiveY => new Vector3(warpedU, 1f, warpedV),
                CelestialCubeFace.NegativeY => new Vector3(warpedU, -1f, -warpedV),
                CelestialCubeFace.PositiveZ => new Vector3(warpedU, -warpedV, 1f),
                _ => new Vector3(-warpedU, -warpedV, -1f)
            };
            return direction.normalized;
        }

        static float Warp(float value)
        {
            return Mathf.Tan(Mathf.Clamp(value, -1.5f, 1.5f) * WarpScale);
        }

        static float Unwarp(float value)
        {
            return Mathf.Atan(value) / WarpScale;
        }

        static void ProjectLinear(
            Vector3 direction,
            out CelestialCubeFace face,
            out float u,
            out float v)
        {
            Vector3 normalized = direction.sqrMagnitude > 0.000001f
                ? direction.normalized
                : Vector3.up;
            Vector3 absolute = new(
                Mathf.Abs(normalized.x),
                Mathf.Abs(normalized.y),
                Mathf.Abs(normalized.z));

            if (absolute.x >= absolute.y && absolute.x >= absolute.z)
            {
                float denominator = Mathf.Max(0.000001f, absolute.x);
                if (normalized.x >= 0f)
                {
                    face = CelestialCubeFace.PositiveX;
                    u = -normalized.z / denominator;
                    v = -normalized.y / denominator;
                }
                else
                {
                    face = CelestialCubeFace.NegativeX;
                    u = normalized.z / denominator;
                    v = -normalized.y / denominator;
                }

                return;
            }

            if (absolute.y >= absolute.z)
            {
                float denominator = Mathf.Max(0.000001f, absolute.y);
                if (normalized.y >= 0f)
                {
                    face = CelestialCubeFace.PositiveY;
                    u = normalized.x / denominator;
                    v = normalized.z / denominator;
                }
                else
                {
                    face = CelestialCubeFace.NegativeY;
                    u = normalized.x / denominator;
                    v = -normalized.z / denominator;
                }

                return;
            }

            float zDenominator = Mathf.Max(0.000001f, absolute.z);
            if (normalized.z >= 0f)
            {
                face = CelestialCubeFace.PositiveZ;
                u = normalized.x / zDenominator;
                v = -normalized.y / zDenominator;
            }
            else
            {
                face = CelestialCubeFace.NegativeZ;
                u = -normalized.x / zDenominator;
                v = -normalized.y / zDenominator;
            }
        }
    }
}
