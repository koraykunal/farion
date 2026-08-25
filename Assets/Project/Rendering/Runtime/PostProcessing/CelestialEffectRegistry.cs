using System.Collections.Generic;
using Farion.Rendering.Celestial;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Farion.Rendering.PostProcessing
{
    public static class CelestialEffectRegistry
    {
        const float MinAtmosphereScreenPixelRadius = 6f;
        const float MinOceanScreenPixelRadius = 12f;
        const float MinCloudScreenPixelRadius = 12f;
        const int MaxTrackedCameras = 8;

        static readonly List<TerrestrialPlanetVisual> Sources = new();
        static readonly Plane[] FrustumPlanes = new Plane[6];
        static readonly Dictionary<EntityId, bool> CameraUnderwaterStates = new();

        public static bool HasSources => Sources.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Initialize()
        {
            Sources.Clear();
            CameraUnderwaterStates.Clear();
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        static bool CoversEnoughScreen(
            Camera camera,
            Vector3 center,
            float radius,
            float minimumPixelRadius)
        {
            if (radius <= 0f)
            {
                return false;
            }

            Bounds bounds = new(center, Vector3.one * (radius * 2f));
            if (!GeometryUtility.TestPlanesAABB(FrustumPlanes, bounds))
            {
                return false;
            }

            float distance = Vector3.Distance(camera.transform.position, center);
            if (distance <= radius)
            {
                return true;
            }

            float halfFovTangent = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            if (halfFovTangent <= 0.0001f)
            {
                return true;
            }

            float pixelRadius = radius / distance * (camera.pixelHeight * 0.5f) / halfFovTangent;
            return pixelRadius >= minimumPixelRadius;
        }

        public static void Register(TerrestrialPlanetVisual source)
        {
            if (source != null && !Sources.Contains(source))
            {
                Sources.Add(source);
            }
        }

        public static void Unregister(TerrestrialPlanetVisual source)
        {
            if (source != null)
            {
                Sources.Remove(source);
            }
        }

        public static bool IsCameraInsideOcean(Camera camera)
        {
            if (camera == null)
            {
                return false;
            }

            return CameraUnderwaterStates.TryGetValue(camera.GetEntityId(), out bool cached)
                ? cached
                : ComputeCameraInsideOcean(camera);
        }

        static bool ComputeCameraInsideOcean(Camera camera)
        {
            Vector3 cameraPosition = camera.transform.position;
            for (int i = PruneDestroyed() - 1; i >= 0; i--)
            {
                if (Sources[i].TryGetOceanEffectData(out CelestialOceanEffectData ocean) &&
                    ocean.IsPointUnderwater(cameraPosition))
                {
                    return true;
                }
            }

            return false;
        }

        static void OnBeginCameraRendering(ScriptableRenderContext _, Camera camera)
        {
            if (camera == null || camera.cameraType != CameraType.Game || Sources.Count == 0)
            {
                return;
            }

            bool isUnderwater = ComputeCameraInsideOcean(camera);
            EntityId cameraId = camera.GetEntityId();
            if (!CameraUnderwaterStates.TryGetValue(cameraId, out bool wasUnderwater))
            {
                if (CameraUnderwaterStates.Count >= MaxTrackedCameras)
                {
                    CameraUnderwaterStates.Clear();
                }
            }
            else if (wasUnderwater != isUnderwater &&
                camera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
            {
                cameraData.resetHistory = true;
            }

            CameraUnderwaterStates[cameraId] = isUnderwater;
        }

        public static void CollectAtmosphere(Camera camera, List<CelestialAtmosphereEffectData> results)
        {
            results.Clear();
            if (camera == null)
            {
                return;
            }

            GeometryUtility.CalculateFrustumPlanes(camera, FrustumPlanes);
            for (int i = PruneDestroyed() - 1; i >= 0; i--)
            {
                if (Sources[i].TryGetAtmosphereEffectData(out CelestialAtmosphereEffectData data) &&
                    CoversEnoughScreen(
                        camera,
                        data.Center,
                        data.AtmosphereRadius,
                        MinAtmosphereScreenPixelRadius))
                {
                    results.Add(data);
                }
            }

            Vector3 cameraPosition = camera.transform.position;
            results.Sort((left, right) =>
                right.GetCameraSortDistance(cameraPosition).CompareTo(left.GetCameraSortDistance(cameraPosition)));
        }

        public static void CollectOcean(Camera camera, List<CelestialOceanEffectData> results)
        {
            results.Clear();
            if (camera == null)
            {
                return;
            }

            GeometryUtility.CalculateFrustumPlanes(camera, FrustumPlanes);
            for (int i = PruneDestroyed() - 1; i >= 0; i--)
            {
                if (Sources[i].TryGetOceanEffectData(out CelestialOceanEffectData data) &&
                    CoversEnoughScreen(camera, data.Center, data.OceanRadius, MinOceanScreenPixelRadius))
                {
                    results.Add(data);
                }
            }

            if (results.Count <= 1)
            {
                return;
            }

            Vector3 cameraPosition = camera.transform.position;
            results.Sort((left, right) =>
                right.GetCameraSortDistance(cameraPosition).CompareTo(left.GetCameraSortDistance(cameraPosition)));
        }

        public static bool TryGetClosestCloud(Camera camera, out CelestialCloudEffectData closest)
        {
            closest = default;
            if (camera == null)
            {
                return false;
            }

            bool found = false;
            float closestDistance = float.PositiveInfinity;
            Vector3 cameraPosition = camera.transform.position;

            GeometryUtility.CalculateFrustumPlanes(camera, FrustumPlanes);
            for (int i = PruneDestroyed() - 1; i >= 0; i--)
            {
                if (!Sources[i].TryGetCloudEffectData(out CelestialCloudEffectData candidate) ||
                    !CoversEnoughScreen(
                        camera,
                        candidate.Center,
                        candidate.OuterRadius,
                        MinCloudScreenPixelRadius))
                {
                    continue;
                }

                float distance = candidate.GetCameraSortDistance(cameraPosition);
                if (!found || distance < closestDistance)
                {
                    found = true;
                    closestDistance = distance;
                    closest = candidate;
                }
            }

            return found;
        }

        static int PruneDestroyed()
        {
            for (int i = Sources.Count - 1; i >= 0; i--)
            {
                if (Sources[i] == null)
                {
                    Sources.RemoveAt(i);
                }
            }

            return Sources.Count;
        }
    }
}
