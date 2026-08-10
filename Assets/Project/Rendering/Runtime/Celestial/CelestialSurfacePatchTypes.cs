using System;
using System.Collections.Generic;
using Farion.Core.Physics;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using UnityEngine;
using UnityEngine.Rendering;

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
                float patchWorldSize)
            {
                Key = key;
                UMin = uMin;
                VMin = vMin;
                Size = size;
                CenterDirection = centerDirection;
                PatchWorldSize = patchWorldSize;
            }

            public PatchKey Key { get; }
            public float UMin { get; }
            public float VMin { get; }
            public float Size { get; }
            public Vector3 CenterDirection { get; }
            public float PatchWorldSize { get; }
        }

        readonly struct PatchBuildWork
        {
            public PatchBuildWork(
                PatchDescriptor descriptor,
                bool rebuildGeometry,
                bool prepareCollision)
            {
                Descriptor = descriptor;
                RebuildGeometry = rebuildGeometry;
                PrepareCollision = prepareCollision;
            }

            public PatchDescriptor Descriptor { get; }
            public bool RebuildGeometry { get; }
            public bool PrepareCollision { get; }
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
            public Mesh Mesh { get; }
            public PatchDescriptor Descriptor { get; set; }
            public bool CollisionBaked { get; set; }
        }
    }
}
