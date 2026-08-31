using UnityEngine;

namespace Farion.Rendering.Lighting
{
    public sealed class CelestialSkyReflectionBuilder
    {
        const int FaceSize = 16;
        const float MinimumRebuildInterval = 0.5f;
        const float StarDirectionRebuildDot = 0.99966f;
        const float UpDirectionRebuildDot = 0.9993f;
        const float DensityRebuildDelta = 0.05f;

        static readonly CubemapFace[] Faces =
        {
            CubemapFace.PositiveX,
            CubemapFace.NegativeX,
            CubemapFace.PositiveY,
            CubemapFace.NegativeY,
            CubemapFace.PositiveZ,
            CubemapFace.NegativeZ
        };

        Cubemap cubemap;
        Color[] facePixels;
        Vector3 lastStarDirection;
        Vector3 lastUp;
        float lastDensity = -1f;
        float lastBuildTime = float.NegativeInfinity;

        public Texture Update(in AtmosphericAmbientSample sample, Vector3 directionToStar)
        {
            EnsureCubemap();
            if (!NeedsRebuild(sample.Up, directionToStar, sample.DensityFactor))
            {
                return cubemap;
            }

            Build(sample);
            lastStarDirection = directionToStar;
            lastUp = sample.Up;
            lastDensity = sample.DensityFactor;
            lastBuildTime = Time.realtimeSinceStartup;
            return cubemap;
        }

        public void Release()
        {
            if (cubemap == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(cubemap);
            }
            else
            {
                Object.DestroyImmediate(cubemap);
            }

            cubemap = null;
            lastDensity = -1f;
            lastBuildTime = float.NegativeInfinity;
        }

        void EnsureCubemap()
        {
            if (cubemap != null)
            {
                return;
            }

            cubemap = new Cubemap(FaceSize, TextureFormat.RGBAHalf, true)
            {
                name = "Farion Analytic Sky Reflection",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear
            };
            facePixels = new Color[FaceSize * FaceSize];
            lastDensity = -1f;
            lastBuildTime = float.NegativeInfinity;
        }

        bool NeedsRebuild(Vector3 up, Vector3 directionToStar, float density)
        {
            if (lastDensity < 0f)
            {
                return true;
            }

            if (Time.realtimeSinceStartup - lastBuildTime < MinimumRebuildInterval)
            {
                return false;
            }

            return Vector3.Dot(lastStarDirection, directionToStar) < StarDirectionRebuildDot
                || Vector3.Dot(lastUp, up) < UpDirectionRebuildDot
                || Mathf.Abs(lastDensity - density) > DensityRebuildDelta;
        }

        void Build(in AtmosphericAmbientSample sample)
        {
            for (int faceIndex = 0; faceIndex < Faces.Length; faceIndex++)
            {
                CubemapFace face = Faces[faceIndex];
                for (int y = 0; y < FaceSize; y++)
                {
                    float v = (y + 0.5f) / FaceSize * 2f - 1f;
                    for (int x = 0; x < FaceSize; x++)
                    {
                        float u = (x + 0.5f) / FaceSize * 2f - 1f;
                        Vector3 direction = FaceDirection(face, u, v);
                        facePixels[y * FaceSize + x] = Shade(sample, direction);
                    }
                }

                cubemap.SetPixels(facePixels, face);
            }

            cubemap.Apply(true, false);
        }

        static Color Shade(in AtmosphericAmbientSample sample, Vector3 direction)
        {
            float upDot = Vector3.Dot(direction, sample.Up);
            Color color;
            if (upDot >= 0f)
            {
                float t = Mathf.Clamp01(upDot / 0.5f);
                color = Color.Lerp(sample.Equator, sample.Sky, t * t * (3f - 2f * t));
            }
            else
            {
                float t = Mathf.Clamp01(-upDot / 0.45f);
                color = Color.Lerp(sample.Equator, sample.Ground, t * t * (3f - 2f * t));
            }

            color.a = 1f;
            return color;
        }

        static Vector3 FaceDirection(CubemapFace face, float u, float v)
        {
            Vector3 direction = face switch
            {
                CubemapFace.PositiveX => new Vector3(1f, -v, -u),
                CubemapFace.NegativeX => new Vector3(-1f, -v, u),
                CubemapFace.PositiveY => new Vector3(u, 1f, v),
                CubemapFace.NegativeY => new Vector3(u, -1f, -v),
                CubemapFace.PositiveZ => new Vector3(u, -v, 1f),
                _ => new Vector3(-u, -v, -1f)
            };
            return direction.normalized;
        }
    }
}
