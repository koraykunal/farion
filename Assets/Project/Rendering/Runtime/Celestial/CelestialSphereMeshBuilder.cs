using System.Collections.Generic;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    internal static class CelestialSphereMeshBuilder
    {
        static readonly Dictionary<int, SphereData> Cache = new();

        public static Mesh Build(
            float radius,
            int resolution,
            string meshName,
            CelestialShapeProfile shapeProfile = null,
            CelestialSurfaceProfileBase surfaceProfile = null)
        {
            return Build(radius, resolution, meshName, out _, shapeProfile, surfaceProfile);
        }

        public static Mesh Build(
            float radius,
            int resolution,
            string meshName,
            out Vector2 radiusMinMax,
            CelestialShapeProfile shapeProfile = null,
            CelestialSurfaceProfileBase surfaceProfile = null)
        {
            radius = Mathf.Max(0.01f, radius);
            SphereData data = GetSphereData(resolution);

            Vector3[] vertices = new Vector3[data.Vertices.Length];
            Vector4[] shadingData = new Vector4[data.Vertices.Length];
            float minRadius = float.PositiveInfinity;
            float maxRadius = float.NegativeInfinity;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 unitDirection = data.Vertices[i];
                float vertexRadius;
                if (shapeProfile != null)
                {
                    CelestialShapeSample shapeSample = shapeProfile.EvaluateSample(radius, unitDirection);
                    vertexRadius = shapeSample.Radius;
                    shadingData[i] = shapeSample.ShadingData;
                }
                else
                {
                    vertexRadius = surfaceProfile != null
                    ? surfaceProfile.EvaluateRadius(radius, unitDirection)
                    : radius;
                    shadingData[i] = new Vector4(0f, 1f, 0f, 999f);
                }

                vertices[i] = unitDirection * vertexRadius;
                minRadius = Mathf.Min(minRadius, vertexRadius);
                maxRadius = Mathf.Max(maxRadius, vertexRadius);
            }

            radiusMinMax = new Vector2(minRadius, maxRadius);

            Mesh mesh = new()
            {
                name = meshName,
                indexFormat = vertices.Length <= 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt16
                    : UnityEngine.Rendering.IndexFormat.UInt32,
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };

            mesh.SetVertices(vertices);
            mesh.SetTriangles(data.Triangles, 0, true);
            mesh.SetUVs(0, shadingData);

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        static SphereData GetSphereData(int resolution)
        {
            resolution = Mathf.Clamp(resolution, 0, 128);
            if (!Cache.TryGetValue(resolution, out SphereData data))
            {
                data = new SphereData(resolution);
                Cache.Add(resolution, data);
            }

            return data;
        }

        sealed class SphereData
        {
            static readonly int[] VertexPairs =
            {
                0, 1, 0, 2, 0, 3, 0, 4,
                1, 2, 2, 3, 3, 4, 4, 1,
                5, 1, 5, 2, 5, 3, 5, 4
            };

            static readonly int[] EdgeTriplets =
            {
                0, 1, 4, 1, 2, 5, 2, 3, 6, 3, 0, 7,
                8, 9, 4, 9, 10, 5, 10, 11, 6, 11, 8, 7
            };

            static readonly Vector3[] BaseVertices =
            {
                Vector3.up,
                Vector3.left,
                Vector3.back,
                Vector3.right,
                Vector3.forward,
                Vector3.down
            };

            readonly FixedSizeList<Vector3> vertices;
            readonly FixedSizeList<int> triangles;
            readonly int divisions;
            readonly int verticesPerFace;

            public SphereData(int resolution)
            {
                divisions = Mathf.Max(0, resolution);
                verticesPerFace = ((divisions + 3) * (divisions + 3) - (divisions + 3)) / 2;

                int vertexCount = verticesPerFace * 8 - (divisions + 2) * 12 + 6;
                int trianglesPerFace = (divisions + 1) * (divisions + 1);

                vertices = new FixedSizeList<Vector3>(vertexCount);
                triangles = new FixedSizeList<int>(trianglesPerFace * 8 * 3);

                vertices.AddRange(BaseVertices);
                Edge[] edges = CreateEdges();
                CreateFaces(edges);

                Vertices = vertices.Items;
                Triangles = triangles.Items;
            }

            public Vector3[] Vertices { get; }
            public int[] Triangles { get; }

            Edge[] CreateEdges()
            {
                Edge[] edges = new Edge[12];
                for (int i = 0; i < VertexPairs.Length; i += 2)
                {
                    Vector3 startVertex = vertices.Items[VertexPairs[i]];
                    Vector3 endVertex = vertices.Items[VertexPairs[i + 1]];

                    int[] edgeVertexIndices = new int[divisions + 2];
                    edgeVertexIndices[0] = VertexPairs[i];

                    for (int divisionIndex = 0; divisionIndex < divisions; divisionIndex++)
                    {
                        float t = (divisionIndex + 1f) / (divisions + 1f);
                        edgeVertexIndices[divisionIndex + 1] = vertices.NextIndex;
                        vertices.Add(Vector3.Slerp(startVertex, endVertex, t));
                    }

                    edgeVertexIndices[divisions + 1] = VertexPairs[i + 1];
                    edges[i / 2] = new Edge(edgeVertexIndices);
                }

                return edges;
            }

            void CreateFaces(Edge[] edges)
            {
                for (int i = 0; i < EdgeTriplets.Length; i += 3)
                {
                    int faceIndex = i / 3;
                    bool reverse = faceIndex >= 4;
                    CreateFace(
                        edges[EdgeTriplets[i]],
                        edges[EdgeTriplets[i + 1]],
                        edges[EdgeTriplets[i + 2]],
                        reverse);
                }
            }

            void CreateFace(Edge sideA, Edge sideB, Edge bottom, bool reverse)
            {
                int pointsInEdge = sideA.VertexIndices.Length;
                FixedSizeList<int> vertexMap = new(verticesPerFace);
                vertexMap.Add(sideA.VertexIndices[0]);

                for (int i = 1; i < pointsInEdge - 1; i++)
                {
                    vertexMap.Add(sideA.VertexIndices[i]);

                    Vector3 sideAVertex = vertices.Items[sideA.VertexIndices[i]];
                    Vector3 sideBVertex = vertices.Items[sideB.VertexIndices[i]];
                    int innerPoints = i - 1;

                    for (int j = 0; j < innerPoints; j++)
                    {
                        float t = (j + 1f) / (innerPoints + 1f);
                        vertexMap.Add(vertices.NextIndex);
                        vertices.Add(Vector3.Slerp(sideAVertex, sideBVertex, t));
                    }

                    vertexMap.Add(sideB.VertexIndices[i]);
                }

                for (int i = 0; i < pointsInEdge; i++)
                {
                    vertexMap.Add(bottom.VertexIndices[i]);
                }

                int rows = divisions + 1;
                for (int row = 0; row < rows; row++)
                {
                    int topVertex = ((row + 1) * (row + 1) - row - 1) / 2;
                    int bottomVertex = ((row + 2) * (row + 2) - row - 2) / 2;
                    int trianglesInRow = 1 + 2 * row;

                    for (int column = 0; column < trianglesInRow; column++)
                    {
                        int v0;
                        int v1;
                        int v2;

                        if (column % 2 == 0)
                        {
                            v0 = topVertex;
                            v1 = bottomVertex + 1;
                            v2 = bottomVertex;
                            topVertex++;
                            bottomVertex++;
                        }
                        else
                        {
                            v0 = topVertex;
                            v1 = bottomVertex;
                            v2 = topVertex - 1;
                        }

                        triangles.Add(vertexMap.Items[v0]);
                        triangles.Add(vertexMap.Items[reverse ? v2 : v1]);
                        triangles.Add(vertexMap.Items[reverse ? v1 : v2]);
                    }
                }
            }
        }

        sealed class Edge
        {
            public Edge(int[] vertexIndices)
            {
                VertexIndices = vertexIndices;
            }

            public int[] VertexIndices { get; }
        }

        sealed class FixedSizeList<T>
        {
            public FixedSizeList(int size)
            {
                Items = new T[size];
            }

            public T[] Items { get; }
            public int NextIndex { get; private set; }

            public void Add(T item)
            {
                Items[NextIndex] = item;
                NextIndex++;
            }

            public void AddRange(IEnumerable<T> items)
            {
                foreach (T item in items)
                {
                    Add(item);
                }
            }
        }
    }
}
