using System;
using System.Collections.Generic;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    public sealed class PlanetSurfaceWeightMap : IDisposable
    {
        static readonly CubemapFace[] Faces =
        {
            CubemapFace.PositiveX,
            CubemapFace.NegativeX,
            CubemapFace.PositiveY,
            CubemapFace.NegativeY,
            CubemapFace.PositiveZ,
            CubemapFace.NegativeZ
        };

        readonly Cubemap weightsA;
        readonly Cubemap weightsB;
        readonly Cubemap surfaceState;
        readonly Cubemap surfaceNormal;
        readonly bool ownsTextures;

        PlanetSurfaceWeightMap(
            Cubemap weightsA,
            Cubemap weightsB,
            Cubemap surfaceState,
            Cubemap surfaceNormal,
            bool ownsTextures)
        {
            this.weightsA = weightsA;
            this.weightsB = weightsB;
            this.surfaceState = surfaceState;
            this.surfaceNormal = surfaceNormal;
            this.ownsTextures = ownsTextures;
        }

        public static PlanetSurfaceWeightMap FromBaked(PlanetSurfaceMapSet mapSet)
        {
            if (mapSet == null || !mapSet.IsComplete)
            {
                return null;
            }

            return new PlanetSurfaceWeightMap(
                mapSet.WeightsA,
                mapSet.WeightsB,
                mapSet.SurfaceState,
                mapSet.SurfaceNormal,
                false);
        }

        public static bool CanBake(PlanetSurfaceModel surfaceModel, SurfaceVisualProfile visualProfile)
        {
            return surfaceModel != null &&
                visualProfile != null &&
                surfaceModel.GenerationProfile != null &&
                surfaceModel.GenerationProfile.SurfaceMaterialDistribution != null;
        }

        public static PlanetSurfaceWeightMap Build(
            PlanetSurfaceModel surfaceModel,
            SurfaceVisualProfile visualProfile)
        {
            if (!CanBake(surfaceModel, visualProfile))
            {
                return null;
            }

            int resolution = visualProfile.SurfaceMapResolution;
            Cubemap mapA = CreateMap(resolution, "Planet Surface Weights A");
            Cubemap mapB = CreateMap(resolution, "Planet Surface Weights B");
            Cubemap stateMap = CreateMap(resolution, "Planet Surface State");

            try
            {
                Bake(surfaceModel, visualProfile, mapA, mapB, stateMap);
                return new PlanetSurfaceWeightMap(mapA, mapB, stateMap, null, true);
            }
            catch
            {
                Release(mapA);
                Release(mapB);
                Release(stateMap);
                throw;
            }
        }

        public static void Bake(
            PlanetSurfaceModel surfaceModel,
            SurfaceVisualProfile visualProfile,
            Cubemap mapA,
            Cubemap mapB,
            Cubemap stateMap)
        {
            int resolution = mapA.width;
            List<SurfaceMaterialWeight> weights = new(SurfaceVisualProfile.MaxSurfaceSlots);
            Color[] pixelsA = new Color[resolution * resolution];
            Color[] pixelsB = new Color[resolution * resolution];
            Color[] statePixels = new Color[resolution * resolution];

            for (int faceIndex = 0; faceIndex < Faces.Length; faceIndex++)
            {
                CubemapFace face = Faces[faceIndex];
                for (int y = 0; y < resolution; y++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        int pixelIndex = y * resolution + x;
                        Vector3 direction = CubemapDirection(face, x, y, resolution);
                        ResolveSample(
                            surfaceModel,
                            visualProfile,
                            direction,
                            weights,
                            out pixelsA[pixelIndex],
                            out pixelsB[pixelIndex],
                            out statePixels[pixelIndex]);
                    }
                }

                mapA.SetPixels(pixelsA, face);
                mapB.SetPixels(pixelsB, face);
                stateMap.SetPixels(statePixels, face);
            }

            mapA.Apply(true, false);
            mapB.Apply(true, false);
            stateMap.Apply(true, false);
        }

        public static void BakeNormals(PlanetSurfaceModel surfaceModel, Cubemap normalMap)
        {
            int resolution = normalMap.width;
            Color[] pixels = new Color[resolution * resolution];

            for (int faceIndex = 0; faceIndex < Faces.Length; faceIndex++)
            {
                CubemapFace face = Faces[faceIndex];
                for (int y = 0; y < resolution; y++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        Vector3 direction = CubemapDirection(face, x, y, resolution);
                        Vector3 localNormal = surfaceModel.TrySampleLocalTerrain(
                            direction,
                            out _,
                            out Vector3 sampledNormal)
                            ? sampledNormal
                            : direction;
                        pixels[y * resolution + x] = new Color(
                            localNormal.x * 0.5f + 0.5f,
                            localNormal.y * 0.5f + 0.5f,
                            localNormal.z * 0.5f + 0.5f,
                            1f);
                    }
                }

                normalMap.SetPixels(pixels, face);
            }

            normalMap.Apply(true, false);
        }

        public void Apply(Material target)
        {
            if (target == null)
            {
                return;
            }

            target.SetFloat("_SurfaceWeightMapEnabled", 1f);
            target.SetTexture("_SurfaceWeightsA", weightsA);
            target.SetTexture("_SurfaceWeightsB", weightsB);
            target.SetTexture("_SurfaceStateMap", surfaceState);
            if (surfaceNormal != null)
            {
                target.SetFloat("_SurfaceNormalMapEnabled", 1f);
                target.SetTexture("_SurfaceNormalMap", surfaceNormal);
            }
            else
            {
                target.SetFloat("_SurfaceNormalMapEnabled", 0f);
            }
        }

        public static void Clear(Material target)
        {
            if (target != null)
            {
                target.SetFloat("_SurfaceWeightMapEnabled", 0f);
                target.SetFloat("_SurfaceNormalMapEnabled", 0f);
            }
        }

        public void Dispose()
        {
            if (!ownsTextures)
            {
                return;
            }

            Release(weightsA);
            Release(weightsB);
            Release(surfaceState);
            Release(surfaceNormal);
        }

        static void ResolveSample(
            PlanetSurfaceModel surfaceModel,
            SurfaceVisualProfile visualProfile,
            Vector3 direction,
            List<SurfaceMaterialWeight> weights,
            out Color weightsA,
            out Color weightsB,
            out Color state)
        {
            weightsA = Color.clear;
            weightsB = Color.clear;
            state = Color.clear;
            if (!surfaceModel.TrySamplePlanetSurface(direction, out PlanetSurfaceSample sample))
            {
                return;
            }

            SurfaceMaterialDistributionProfile distribution =
                surfaceModel.GenerationProfile.SurfaceMaterialDistribution;
            int count = distribution.SampleWeights(
                sample.Context,
                sample.Climate,
                sample.Biome.Biome,
                sample.LocalDirection,
                sample.TerrainAltitude,
                sample.Surface.SlopeAngleDegrees,
                weights);

            float mappedWeight = 0f;
            for (int i = 0; i < count; i++)
            {
                SurfaceMaterialWeight weight = weights[i];
                int slot = visualProfile.ResolveMaterialIndex(weight.Material);
                if (slot < 0 || weight.Weight <= 0f)
                {
                    continue;
                }

                if (slot < 4)
                {
                    weightsA[slot] += weight.Weight;
                }
                else
                {
                    weightsB[slot - 4] += weight.Weight;
                }

                mappedWeight += weight.Weight;
            }

            if (mappedWeight > 0f)
            {
                weightsA /= mappedWeight;
                weightsB /= mappedWeight;
            }

            state = new Color(
                sample.SurfaceState.VolcanicActivity,
                sample.SurfaceState.SnowCover,
                sample.SurfaceState.Wetness,
                1f);
        }

        static Cubemap CreateMap(int resolution, string mapName)
        {
            return new Cubemap(resolution, TextureFormat.RGBAHalf, true)
            {
                name = mapName,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 1,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        static Vector3 CubemapDirection(CubemapFace face, int x, int y, int resolution)
        {
            float u = ((x + 0.5f) / resolution) * 2f - 1f;
            float v = ((y + 0.5f) / resolution) * 2f - 1f;
            return face switch
            {
                CubemapFace.PositiveX => new Vector3(1f, -v, -u).normalized,
                CubemapFace.NegativeX => new Vector3(-1f, -v, u).normalized,
                CubemapFace.PositiveY => new Vector3(u, 1f, v).normalized,
                CubemapFace.NegativeY => new Vector3(u, -1f, -v).normalized,
                CubemapFace.PositiveZ => new Vector3(u, -v, 1f).normalized,
                CubemapFace.NegativeZ => new Vector3(-u, -v, -1f).normalized,
                _ => Vector3.up
            };
        }

        static void Release(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
