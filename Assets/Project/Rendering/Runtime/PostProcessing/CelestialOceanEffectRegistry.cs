using System.Collections.Generic;
using Farion.Rendering.Celestial;
using UnityEngine;

namespace Farion.Rendering.PostProcessing
{
    public static class CelestialOceanEffectRegistry
    {
        static readonly List<ICelestialOceanEffectSource> Sources = new();

        public static bool HasSources => Sources.Count > 0;

        public static void Register(ICelestialOceanEffectSource source)
        {
            if (source == null || Sources.Contains(source))
            {
                return;
            }

            Sources.Add(source);
        }

        public static void Unregister(ICelestialOceanEffectSource source)
        {
            if (source == null)
            {
                return;
            }

            Sources.Remove(source);
        }

        public static void Collect(Camera camera, List<CelestialOceanEffectData> results)
        {
            results.Clear();

            for (int i = Sources.Count - 1; i >= 0; i--)
            {
                ICelestialOceanEffectSource source = Sources[i];
                if (source is not Object unityObject || unityObject == null)
                {
                    Sources.RemoveAt(i);
                    continue;
                }

                if (source.TryGetOceanEffectData(out CelestialOceanEffectData data))
                {
                    results.Add(data);
                }
            }

            if (camera == null || results.Count <= 1)
            {
                return;
            }

            Vector3 cameraPosition = camera.transform.position;
            results.Sort((left, right) =>
                right.GetCameraSortDistance(cameraPosition).CompareTo(left.GetCameraSortDistance(cameraPosition)));
        }

        public static bool IsCameraInsideOcean(Camera camera)
        {
            if (camera == null)
            {
                return false;
            }

            Vector3 cameraPosition = camera.transform.position;
            for (int i = Sources.Count - 1; i >= 0; i--)
            {
                ICelestialOceanEffectSource source = Sources[i];
                if (source is not Object unityObject || unityObject == null)
                {
                    Sources.RemoveAt(i);
                    continue;
                }

                if (!source.TryGetOceanEffectData(out CelestialOceanEffectData data) ||
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
    }
}
