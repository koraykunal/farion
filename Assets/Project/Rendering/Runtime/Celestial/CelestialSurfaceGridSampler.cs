using UnityEngine;

namespace Farion.Rendering.Celestial
{
    internal readonly struct CelestialSurfaceAnchor
    {
        public CelestialSurfaceAnchor(int level, float fineRadius, float coarseRadius)
        {
            Level = level;
            FineRadius = fineRadius;
            CoarseRadius = coarseRadius;
        }

        public int Level { get; }
        public float FineRadius { get; }
        public float CoarseRadius { get; }
        public bool IsValid => FineRadius > 0f;
    }

    internal static class CelestialSurfaceGridSampler
    {
        public static float EvaluateRenderedRadius(
            in CelestialSurfaceSampler sampler,
            Vector3 unitDirection,
            int level,
            int resolution)
        {
            unitDirection = unitDirection.sqrMagnitude > 0.000001f
                ? unitDirection.normalized
                : Vector3.up;
            CelestialCubeProjection.Project(
                unitDirection,
                out CelestialCubeFace face,
                out float u,
                out float v);

            int cells = (1 << Mathf.Max(0, level)) * Mathf.Max(1, resolution);
            float cellSize = 2f / cells;
            int cellX = Mathf.Clamp(Mathf.FloorToInt((u + 1f) / cellSize), 0, cells - 1);
            int cellY = Mathf.Clamp(Mathf.FloorToInt((v + 1f) / cellSize), 0, cells - 1);
            float uMin = -1f + cellX * cellSize;
            float vMin = -1f + cellY * cellSize;

            Vector3 corner00 = SampleCorner(sampler, face, uMin, vMin);
            Vector3 corner10 = SampleCorner(sampler, face, uMin + cellSize, vMin);
            Vector3 corner01 = SampleCorner(sampler, face, uMin, vMin + cellSize);

            float localU = Mathf.Clamp01((u - uMin) / cellSize);
            float localV = Mathf.Clamp01((v - vMin) / cellSize);

            Vector3 a;
            Vector3 b;
            Vector3 c;
            if (localU + localV <= 1f)
            {
                a = corner00;
                b = corner10;
                c = corner01;
            }
            else
            {
                a = corner10;
                b = SampleCorner(sampler, face, uMin + cellSize, vMin + cellSize);
                c = corner01;
            }

            Vector3 normal = Vector3.Cross(b - a, c - a);
            float denominator = Vector3.Dot(normal, unitDirection);
            if (Mathf.Abs(denominator) < 0.000001f)
            {
                return sampler.EvaluateRadius(unitDirection);
            }

            float radius = Vector3.Dot(normal, a) / denominator;
            return radius > 0f ? radius : sampler.EvaluateRadius(unitDirection);
        }

        static Vector3 SampleCorner(
            in CelestialSurfaceSampler sampler,
            CelestialCubeFace face,
            float u,
            float v)
        {
            Vector3 direction = CelestialCubeProjection.ToDirection(face, u, v);
            return direction * sampler.EvaluateRadius(direction);
        }
    }
}
