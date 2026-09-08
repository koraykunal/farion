using System;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    internal readonly struct SurfaceScatterCell : IEquatable<SurfaceScatterCell>
    {
        public SurfaceScatterCell(CelestialCubeFace face, int resolution, int x, int y)
        {
            Face = face;
            Resolution = resolution;
            X = x;
            Y = y;
        }

        public CelestialCubeFace Face { get; }
        public int Resolution { get; }
        public int X { get; }
        public int Y { get; }

        public bool Equals(SurfaceScatterCell other)
        {
            return Face == other.Face && Resolution == other.Resolution && X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is SurfaceScatterCell other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Face, Resolution, X, Y);
        }
    }

    internal static class SurfaceScatterPlacement
    {
        public static int CalculateResolution(float radius, float spacingMeters)
        {
            float faceArcLength = Mathf.PI * Mathf.Max(0.01f, radius) * 0.5f;
            return Mathf.Clamp(Mathf.CeilToInt(faceArcLength / Mathf.Max(0.25f, spacingMeters)), 1, 1048576);
        }

        public static SurfaceScatterCell CellFromDirection(Vector3 direction, int resolution)
        {
            CelestialCubeProjection.Project(direction, out CelestialCubeFace face, out float u, out float v);
            int safeResolution = Mathf.Max(1, resolution);
            int x = Mathf.Clamp(Mathf.FloorToInt((u + 1f) * 0.5f * safeResolution), 0, safeResolution - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((v + 1f) * 0.5f * safeResolution), 0, safeResolution - 1);
            return new SurfaceScatterCell(face, safeResolution, x, y);
        }

        public static Vector3 CandidateDirection(
            SurfaceScatterCell cell,
            int planetSeed,
            string ruleId)
        {
            float cellSize = 2f / Mathf.Max(1, cell.Resolution);
            float u = -1f + (cell.X + Hash01(planetSeed, ruleId, cell, 0)) * cellSize;
            float v = -1f + (cell.Y + Hash01(planetSeed, ruleId, cell, 1)) * cellSize;
            return CelestialCubeProjection.ToDirection(cell.Face, u, v);
        }

        public static float Hash01(
            int planetSeed,
            string ruleId,
            SurfaceScatterCell cell,
            int channel)
        {
            unchecked
            {
                uint hash = (uint)SeedUtility.Derive(planetSeed, ruleId ?? string.Empty);
                hash = Mix(hash, (int)cell.Face);
                hash = Mix(hash, cell.Resolution);
                hash = Mix(hash, cell.X);
                hash = Mix(hash, cell.Y);
                hash = Mix(hash, channel);
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;
                return (hash & 0x00FFFFFFu) / 16777215f;
            }
        }

        public static float EvaluateCluster(
            Vector3 direction,
            float planetRadius,
            SurfaceScatterDistribution distribution,
            int planetSeed,
            string ruleId,
            string channel)
        {
            float circumference = Mathf.PI * 2f * Mathf.Max(0.01f, planetRadius);
            float noiseScale = circumference / distribution.ClusterScaleMeters;
            float noise = PlanetarySampling.SampleFractal01(
                direction,
                noiseScale,
                3,
                2f,
                0.5f,
                SeedUtility.Derive(planetSeed, $"{channel}.{ruleId}"));
            return Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    distribution.ClusterCutoff - distribution.ClusterFeather,
                    distribution.ClusterCutoff + distribution.ClusterFeather,
                    noise));
        }

        public static float ResolveVisibilityDistance(
            SurfaceScatterDistribution distribution,
            int planetSeed,
            string ruleId,
            SurfaceScatterCell cell)
        {
            float importance = Hash01(planetSeed, ruleId, cell, 11);
            return Mathf.Lerp(
                distribution.NearVisibilityDistance,
                distribution.FarVisibilityDistance,
                importance * importance);
        }

        public const int FootprintSampleCount = 8;

        public static bool TryResolveFootprintSeat(
            Func<Vector3, float> sampleRadius,
            Vector3 direction,
            float centerRadius,
            float footprintRadiusMeters,
            float normalAlignment,
            out Vector3 surfaceNormal,
            out Vector3 placementUp,
            out float seatDrop)
        {
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
            surfaceNormal = direction;
            placementUp = direction;
            seatDrop = 0f;
            if (sampleRadius == null || footprintRadiusMeters <= 0.01f || centerRadius <= 0.01f)
            {
                return false;
            }

            BuildTangentBasis(direction, out Vector3 tangent, out Vector3 bitangent);
            float angularRadius = footprintRadiusMeters / centerRadius;
            Vector3 center = direction * centerRadius;
            Span<Vector3> ring = stackalloc Vector3[FootprintSampleCount];
            for (int i = 0; i < FootprintSampleCount; i++)
            {
                float angle = i * (Mathf.PI * 2f / FootprintSampleCount);
                Vector3 ringDirection = (direction +
                    (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * angularRadius).normalized;
                float radius = sampleRadius(ringDirection);
                if (radius <= 0f)
                {
                    return false;
                }

                ring[i] = ringDirection * radius;
            }

            Vector3 normal = Vector3.zero;
            for (int i = 0; i < FootprintSampleCount; i++)
            {
                normal += Vector3.Cross(ring[i] - center, ring[(i + 1) % FootprintSampleCount] - center);
            }

            if (normal.sqrMagnitude < 0.000001f)
            {
                normal = direction;
            }

            normal.Normalize();
            if (Vector3.Dot(normal, direction) < 0f)
            {
                normal = -normal;
            }

            surfaceNormal = normal;
            placementUp = ResolvePlacementUp(direction, normal, normalAlignment);
            float lowest = 0f;
            for (int i = 0; i < FootprintSampleCount; i++)
            {
                lowest = Mathf.Min(lowest, Vector3.Dot(ring[i] - center, placementUp));
            }

            seatDrop = lowest;
            return true;
        }

        public static void BuildTangentBasis(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
        {
            Vector3 reference = Mathf.Abs(normal.y) < 0.95f ? Vector3.up : Vector3.right;
            tangent = Vector3.Cross(reference, normal).normalized;
            bitangent = Vector3.Cross(normal, tangent).normalized;
        }

        public static Vector3 ResolvePlacementUp(
            Vector3 radialUp,
            Vector3 surfaceNormal,
            float normalAlignment)
        {
            float alignment = Mathf.Clamp01(normalAlignment);
            if (alignment <= 0f)
            {
                return radialUp.normalized;
            }

            if (alignment >= 1f)
            {
                return surfaceNormal.normalized;
            }

            return Vector3.Slerp(
                radialUp.normalized,
                surfaceNormal.normalized,
                alignment).normalized;
        }

        static uint Mix(uint hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value + 0x9E3779B9u + (hash << 6) + (hash >> 2);
                return hash;
            }
        }
    }
}
