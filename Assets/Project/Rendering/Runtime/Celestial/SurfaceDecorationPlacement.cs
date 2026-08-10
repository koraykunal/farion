using System;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    internal readonly struct SurfaceDecorationCell : IEquatable<SurfaceDecorationCell>
    {
        public SurfaceDecorationCell(CelestialCubeFace face, int resolution, int x, int y)
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

        public bool Equals(SurfaceDecorationCell other)
        {
            return Face == other.Face && Resolution == other.Resolution && X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is SurfaceDecorationCell other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Face, Resolution, X, Y);
        }
    }

    internal static class SurfaceDecorationPlacement
    {
        public static int CalculateResolution(float radius, float spacingMeters)
        {
            float faceArcLength = Mathf.PI * Mathf.Max(0.01f, radius) * 0.5f;
            return Mathf.Clamp(Mathf.CeilToInt(faceArcLength / Mathf.Max(0.25f, spacingMeters)), 1, 1048576);
        }

        public static SurfaceDecorationCell CellFromDirection(Vector3 direction, int resolution)
        {
            CelestialCubeProjection.Project(direction, out CelestialCubeFace face, out float u, out float v);
            int safeResolution = Mathf.Max(1, resolution);
            int x = Mathf.Clamp(Mathf.FloorToInt((u + 1f) * 0.5f * safeResolution), 0, safeResolution - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((v + 1f) * 0.5f * safeResolution), 0, safeResolution - 1);
            return new SurfaceDecorationCell(face, safeResolution, x, y);
        }

        public static Vector3 CandidateDirection(
            SurfaceDecorationCell cell,
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
            SurfaceDecorationCell cell,
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
            SurfaceDecorationRule rule,
            int planetSeed)
        {
            float circumference = Mathf.PI * 2f * Mathf.Max(0.01f, planetRadius);
            float noiseScale = circumference / Mathf.Max(0.01f, rule.ClusterScaleMeters);
            float noise = PlanetarySampling.SampleFractal01(
                direction,
                noiseScale,
                3,
                2f,
                0.5f,
                SeedUtility.Derive(planetSeed, $"surface.decoration.cluster.{rule.StableId}"));
            return Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    rule.ClusterCutoff - rule.ClusterFeather,
                    rule.ClusterCutoff + rule.ClusterFeather,
                    noise));
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
