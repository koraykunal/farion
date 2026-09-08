using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    public sealed partial class CelestialSurfacePatchSystem
    {
        readonly struct PatchKey : IEquatable<PatchKey>
        {
            public PatchKey(CelestialCubeFace face, int level, int x, int y)
            {
                Face = face;
                Level = level;
                X = x;
                Y = y;
            }

            public CelestialCubeFace Face { get; }
            public int Level { get; }
            public int X { get; }
            public int Y { get; }

            public bool Equals(PatchKey other)
            {
                return Face == other.Face && Level == other.Level && X == other.X && Y == other.Y;
            }

            public override bool Equals(object obj)
            {
                return obj is PatchKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)Face;
                    hash = hash * 397 ^ Level;
                    hash = hash * 397 ^ X;
                    return hash * 397 ^ Y;
                }
            }

            public override string ToString()
            {
                return $"{Face} L{Level} ({X},{Y})";
            }
        }

        readonly struct PatchDescriptor
        {
            public PatchDescriptor(
                PatchKey key,
                float uMin,
                float vMin,
                float size,
                Vector3 centerDirection,
                float patchWorldSize,
                int coarserEdgeMask = 0)
            {
                Key = key;
                UMin = uMin;
                VMin = vMin;
                Size = size;
                CenterDirection = centerDirection;
                PatchWorldSize = patchWorldSize;
                CoarserEdgeMask = coarserEdgeMask;
            }

            public PatchKey Key { get; }
            public float UMin { get; }
            public float VMin { get; }
            public float Size { get; }
            public Vector3 CenterDirection { get; }
            public float PatchWorldSize { get; }
            public int CoarserEdgeMask { get; }

            public PatchDescriptor WithEdgeTopology(int coarserEdgeMask)
            {
                return new PatchDescriptor(
                    Key,
                    UMin,
                    VMin,
                    Size,
                    CenterDirection,
                    PatchWorldSize,
                    coarserEdgeMask);
            }

            public bool HasSameEdgeTopology(PatchDescriptor other)
            {
                return CoarserEdgeMask == other.CoarserEdgeMask;
            }
        }

        enum PatchBuildStage
        {
            Queued,
            Sampling,
            CollisionPending,
            Complete
        }

        sealed class PatchGeometry
        {
            public Vector3[] Vertices = Array.Empty<Vector3>();
            public Vector3[] Normals = Array.Empty<Vector3>();
            public Vector4[] Shading = Array.Empty<Vector4>();
            public Vector4[] MorphOffsets = Array.Empty<Vector4>();
            public Vector4[] MorphNormals = Array.Empty<Vector4>();
            public int[] Triangles = Array.Empty<int>();
            public Vector3[] ExtendedPositions = Array.Empty<Vector3>();
            public int VertexCount;
            public int TriangleIndexCount;

            public void EnsureCapacity(int vertexCapacity, int triangleIndexCapacity, int extendedCapacity)
            {
                if (Vertices.Length < vertexCapacity)
                {
                    Array.Resize(ref Vertices, vertexCapacity);
                    Array.Resize(ref Normals, vertexCapacity);
                    Array.Resize(ref Shading, vertexCapacity);
                    Array.Resize(ref MorphOffsets, vertexCapacity);
                    Array.Resize(ref MorphNormals, vertexCapacity);
                }

                if (Triangles.Length < triangleIndexCapacity)
                {
                    Array.Resize(ref Triangles, triangleIndexCapacity);
                }

                if (ExtendedPositions.Length < extendedCapacity)
                {
                    Array.Resize(ref ExtendedPositions, extendedCapacity);
                }
            }
        }

        sealed class PatchBuildOperation
        {
            public PatchDescriptor Descriptor;
            public bool RebuildGeometry;
            public bool PrepareCollision;
            public SurfacePatch Patch;
            public PatchGeometry Geometry;
            public Task Task;
            public PatchBuildStage Stage;

            public bool TaskFinished => Task == null || Task.IsCompleted;

            public void Reset()
            {
                Patch = null;
                Geometry = null;
                Task = null;
                Stage = PatchBuildStage.Queued;
            }
        }

        sealed class SurfacePatch
        {
            public SurfacePatch(
                GameObject gameObject,
                MeshFilter filter,
                MeshRenderer renderer,
                MeshCollider collider,
                Mesh mesh)
            {
                GameObject = gameObject;
                Filter = filter;
                Renderer = renderer;
                Collider = collider;
                Mesh = mesh;
            }

            public GameObject GameObject { get; }
            public MeshFilter Filter { get; }
            public MeshRenderer Renderer { get; }
            public MeshCollider Collider { get; }
            public Mesh Mesh { get; set; }
            public PatchDescriptor Descriptor { get; set; }
            public bool CollisionBaked { get; set; }
            public Vector4 MorphRange { get; set; }
        }
    }
}
