using System.Collections.Generic;
using Farion.Rendering.Celestial;
using UnityEngine;

namespace Farion.Rendering.PostProcessing
{
    public static class CelestialEffectRegistry
    {
        static readonly List<TerrestrialPlanetVisual> Sources = new();

        public static bool HasSources => Sources.Count > 0;

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

        public static void CollectAtmosphere(Camera camera, List<CelestialAtmosphereEffectData> results)
        {
            results.Clear();
            if (camera == null)
            {
                return;
            }

            for (int i = PruneDestroyed() - 1; i >= 0; i--)
            {
                if (Sources[i].TryGetAtmosphereEffectData(out CelestialAtmosphereEffectData data))
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

            for (int i = PruneDestroyed() - 1; i >= 0; i--)
            {
                if (Sources[i].TryGetOceanEffectData(out CelestialOceanEffectData data))
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

        // ponytail: render one cloud body; collect/sort multiple bodies only after profiling proves the need.
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

            for (int i = PruneDestroyed() - 1; i >= 0; i--)
            {
                if (!Sources[i].TryGetCloudEffectData(out CelestialCloudEffectData candidate))
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

        public static bool IsCameraInsideOcean(Camera camera)
        {
            if (camera == null)
            {
                return false;
            }

            Vector3 cameraPosition = camera.transform.position;
            for (int i = PruneDestroyed() - 1; i >= 0; i--)
            {
                if (!Sources[i].TryGetOceanEffectData(out CelestialOceanEffectData data) ||
                    data.OceanRadius <= 0f)
                {
                    continue;
                }

                if ((cameraPosition - data.Center).sqrMagnitude < data.OceanRadius * data.OceanRadius)
                {
                    return true;
                }
            }

            return false;
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
