using UnityEngine;

namespace Farion.Rendering.Celestial
{
    internal enum CelestialCubeFace
    {
        PositiveX,
        NegativeX,
        PositiveY,
        NegativeY,
        PositiveZ,
        NegativeZ
    }

    internal static class CelestialCubeProjection
    {
        public static void Project(
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

        public static Vector3 ToDirection(CelestialCubeFace face, float u, float v)
        {
            Vector3 direction = face switch
            {
                CelestialCubeFace.PositiveX => new Vector3(1f, -v, -u),
                CelestialCubeFace.NegativeX => new Vector3(-1f, -v, u),
                CelestialCubeFace.PositiveY => new Vector3(u, 1f, v),
                CelestialCubeFace.NegativeY => new Vector3(u, -1f, -v),
                CelestialCubeFace.PositiveZ => new Vector3(u, -v, 1f),
                _ => new Vector3(-u, -v, -1f)
            };
            return direction.normalized;
        }
    }
}
