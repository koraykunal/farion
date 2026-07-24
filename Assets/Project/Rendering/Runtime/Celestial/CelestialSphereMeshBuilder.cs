using System.Collections.Generic;
using Farion.Simulation.Celestial;
using Farion.Simulation.Planetary;
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
            CelestialSurfaceProfileBase surfaceProfile = null,
            PlanetSurfaceModel surfaceModel = null)
        {
            return Build(radius, resolution, meshName, out _, shapeProfile, surfaceProfile, surfaceModel);
        }

        public static Mesh Build(
            float radius,
            int resolution,
            string meshName,
            out Vector2 radiusMinMax,
            CelestialShapeProfile shapeProfile = null,
            CelestialSurfaceProfileBase surfaceProfile = null,
            PlanetSurfaceModel surfaceModel = null)
        {
            radius = Mathf.Max(0.01f, radius);
            SphereData data = GetSphereData(resolution);

            Vector3[] vertices = new Vector3[data.Vertices.Length];
            Vector4[] shadingData = new Vector4[data.Vertices.Length];
            Vector4[] biomeWeightsA = new Vector4[data.Vertices.Length];
            Vector4[] biomeWeightsB = new Vector4[data.Vertices.Length];
            float minRadius = float.PositiveInfinity;
            float maxRadius = float.NegativeInfinity;
            BiomeVisualProfile biomeVisualProfile = surfaceProfile is TerrestrialSurfaceProfile terrestrialSurface
                ? terrestrialSurface.BiomeVisualProfile
                : null;
            List<BiomeWeight> biomeWeightScratch = new(BiomeVisualProfile.MaxBiomeSlots);

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
                ResolveBiomeVisualWeights(
                    surfaceModel,
                    biomeVisualProfile,
                    unitDirection,
                    biomeWeightScratch,
                    out biomeWeightsA[i],
                    out biomeWeightsB[i]);
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
            mesh.SetUVs(1, biomeWeightsA);
            mesh.SetUVs(2, biomeWeightsB);

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void ResolveBiomeVisualWeights(
            PlanetSurfaceModel surfaceModel,
            BiomeVisualProfile biomeVisualProfile,
            Vector3 localDirection,
            List<BiomeWeight> biomeWeightScratch,
            out Vector4 weightsA,
            out Vector4 weightsB)
        {
            weightsA = Vector4.zero;
            weightsB = Vector4.zero;

            if (surfaceModel == null || biomeVisualProfile == null)
            {
                return;
            }

            float totalSampleWeight = 0f;
            totalSampleWeight += AddBiomeVisualWeights(surfaceModel, biomeVisualProfile, localDirection, biomeWeightScratch, 1f, ref weightsA, ref weightsB);
            totalSampleWeight += AddFeatheredBiomeVisualWeights(surfaceModel, biomeVisualProfile, localDirection, biomeWeightScratch, ref weightsA, ref weightsB);
            ScaleWeights(ref weightsA, ref weightsB, totalSampleWeight);
        }

        static float AddFeatheredBiomeVisualWeights(
            PlanetSurfaceModel surfaceModel,
            BiomeVisualProfile biomeVisualProfile,
            Vector3 localDirection,
            List<BiomeWeight> biomeWeightScratch,
            ref Vector4 weightsA,
            ref Vector4 weightsB)
        {
            float featherStrength = biomeVisualProfile.EdgeFeatherStrength;
            float step = biomeVisualProfile.EdgeFeatherSampleStep;
            int sampleCount = biomeVisualProfile.EdgeFeatherSampleCount;
            if (featherStrength <= 0f || step <= 0f)
            {
                return 0f;
            }

            Vector3 normal = localDirection.sqrMagnitude > 0.0001f ? localDirection.normalized : Vector3.up;
            Vector3 tangentA = Vector3.ProjectOnPlane(Vector3.forward, normal);
            if (tangentA.sqrMagnitude <= 0.0001f)
            {
                tangentA = Vector3.ProjectOnPlane(Vector3.right, normal);
            }

            if (tangentA.sqrMagnitude <= 0.0001f)
            {
                return 0f;
            }

            tangentA.Normalize();
            Vector3 tangentB = Vector3.Cross(normal, tangentA).normalized;
            float sampleWeight = featherStrength / Mathf.Max(1, sampleCount);
            float totalWeight = 0f;
            const float Tau = Mathf.PI * 2f;
            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                float angle = Tau * sampleIndex / sampleCount;
                Vector3 sampleOffset = tangentA * Mathf.Cos(angle) + tangentB * Mathf.Sin(angle);
                totalWeight += AddBiomeVisualWeights(
                    surfaceModel,
                    biomeVisualProfile,
                    (normal + sampleOffset * step).normalized,
                    biomeWeightScratch,
                    sampleWeight,
                    ref weightsA,
                    ref weightsB);
            }

            return totalWeight;
        }

        static float AddBiomeVisualWeights(
            PlanetSurfaceModel surfaceModel,
            BiomeVisualProfile biomeVisualProfile,
            Vector3 localDirection,
            List<BiomeWeight> biomeWeightScratch,
            float sampleWeight,
            ref Vector4 weightsA,
            ref Vector4 weightsB)
        {
            if (sampleWeight <= 0f)
            {
                return 0f;
            }

            if (!TrySamplePlanetSurface(surfaceModel, localDirection, out PlanetSurfaceSample sample))
            {
                return 0f;
            }

            BiomeDistributionProfile distribution = surfaceModel.GenerationProfile != null
                ? surfaceModel.GenerationProfile.BiomeDistribution
                : null;
            int weightCount = distribution != null
                ? distribution.SampleBiomeWeights(
                    sample.Context,
                    sample.Climate,
                    sample.LocalDirection,
                    sample.TerrainAltitude,
                    sample.Surface.SlopeAngleDegrees,
                    biomeWeightScratch)
                : 0;

            if (weightCount <= 0 && sample.Biome.Biome != null)
            {
                biomeWeightScratch.Clear();
                biomeWeightScratch.Add(new BiomeWeight(sample.Biome.Biome, 1f));
                weightCount = 1;
            }

            float addedWeight = 0f;
            for (int i = 0; i < weightCount; i++)
            {
                BiomeWeight biomeWeight = biomeWeightScratch[i];
                addedWeight += AddBiomeVisualWeight(biomeVisualProfile, biomeWeight.Biome, biomeWeight.Weight * sampleWeight, ref weightsA, ref weightsB);
            }

            return addedWeight;
        }

        static float AddBiomeVisualWeight(
            BiomeVisualProfile biomeVisualProfile,
            BiomeDefinition biome,
            float weight,
            ref Vector4 weightsA,
            ref Vector4 weightsB)
        {
            int index = biomeVisualProfile.ResolveBiomeIndex(biome);
            if (index < 0 || weight <= 0f)
            {
                return 0f;
            }

            if (index < 4)
            {
                weightsA[index] += weight;
            }
            else
            {
                weightsB[index - 4] += weight;
            }

            return weight;
        }

        static void ScaleWeights(ref Vector4 weightsA, ref Vector4 weightsB, float totalSampleWeight)
        {
            if (totalSampleWeight <= 0f)
            {
                return;
            }

            weightsA /= totalSampleWeight;
            weightsB /= totalSampleWeight;
        }

        static bool TrySamplePlanetSurface(
            PlanetSurfaceModel surfaceModel,
            Vector3 localDirection,
            out PlanetSurfaceSample sample)
        {
            sample = default;
            if (surfaceModel == null)
            {
                return false;
            }

            if (surfaceModel.Body == null)
            {
                return false;
            }

            Vector3 worldDirection = surfaceModel.Body.transform.TransformDirection(localDirection.normalized);
            float probeRadius = surfaceModel.ShapeProfile != null
                ? surfaceModel.ShapeProfile.EvaluateSample(surfaceModel.Body.Radius, localDirection.normalized).Radius
                : surfaceModel.Body.Radius;
            Vector3 probePosition = surfaceModel.Body.Position + worldDirection * probeRadius;
            return surfaceModel.TrySamplePlanetSurface(surfaceModel.Body, probePosition, out sample);
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
