using System;
using System.Collections.Generic;
using Farion.Simulation.Planetary;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    internal sealed class PlanetSurfaceWeightMap : IDisposable
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

        PlanetSurfaceWeightMap(Cubemap weightsA, Cubemap weightsB, Cubemap surfaceState)
        {
            this.weightsA = weightsA;
            this.weightsB = weightsB;
            this.surfaceState = surfaceState;
        }

        public static PlanetSurfaceWeightMap Build(
            PlanetSurfaceModel surfaceModel,
            SurfaceVisualProfile visualProfile)
        {
            if (surfaceModel == null ||
                visualProfile == null ||
                surfaceModel.GenerationProfile == null ||
                surfaceModel.GenerationProfile.SurfaceMaterialDistribution == null)
            {
                return null;
            }

            int resolution = visualProfile.SurfaceMapResolution;
            Cubemap mapA = CreateMap(resolution, "Planet Surface Weights A");
            Cubemap mapB = CreateMap(resolution, "Planet Surface Weights B");
            Cubemap stateMap = CreateMap(resolution, "Planet Surface State");
            List<SurfaceMaterialWeight> weights = new(SurfaceVisualProfile.MaxSurfaceSlots);
            Color[] pixelsA = new Color[resolution * resolution];
            Color[] pixelsB = new Color[resolution * resolution];
            Color[] statePixels = new Color[resolution * resolution];

            try
            {
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
                return new PlanetSurfaceWeightMap(mapA, mapB, stateMap);
            }
            catch
            {
                Release(mapA);
                Release(mapB);
                Release(stateMap);
                throw;
            }
        }

        public void Apply(MaterialPropertyBlock propertyBlock)
        {
            if (propertyBlock == null)
            {
                return;
            }

            propertyBlock.SetFloat("_SurfaceWeightMapEnabled", 1f);
            propertyBlock.SetTexture("_SurfaceWeightsA", weightsA);
            propertyBlock.SetTexture("_SurfaceWeightsB", weightsB);
            propertyBlock.SetTexture("_SurfaceStateMap", surfaceState);
        }

        public static void Clear(MaterialPropertyBlock propertyBlock)
        {
            if (propertyBlock != null)
            {
                propertyBlock.SetFloat("_SurfaceWeightMapEnabled", 0f);
            }
        }

        public void Dispose()
        {
            Release(weightsA);
            Release(weightsB);
            Release(surfaceState);
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
