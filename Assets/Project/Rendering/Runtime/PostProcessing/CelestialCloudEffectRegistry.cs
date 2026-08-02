using System.Collections.Generic;
using Farion.Rendering.Celestial;
using UnityEngine;

namespace Farion.Rendering.PostProcessing
{
    public static class CelestialCloudEffectRegistry
    {
        static readonly List<ICelestialCloudEffectSource> Sources = new();

        public static bool HasSources => Sources.Count > 0;

        public static void Register(ICelestialCloudEffectSource source)
        {
            if (source != null && !Sources.Contains(source))
            {
                Sources.Add(source);
            }
        }

        public static void Unregister(ICelestialCloudEffectSource source)
        {
            if (source != null)
            {
                Sources.Remove(source);
            }
        }

        public static bool TryGetClosest(Camera camera, out CelestialCloudEffectData closest)
        {
            closest = default;
            if (camera == null)
            {
                return false;
            }

            bool found = false;
            float closestDistance = float.PositiveInfinity;
            Vector3 cameraPosition = camera.transform.position;

            // ponytail: render one cloud body; collect/sort multiple bodies only after profiling proves the need.
            for (int i = Sources.Count - 1; i >= 0; i--)
            {
                ICelestialCloudEffectSource source = Sources[i];
                if (source is not Object unityObject || unityObject == null)
                {
                    Sources.RemoveAt(i);
                    continue;
                }

                if (!source.TryGetCloudEffectData(out CelestialCloudEffectData candidate))
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
    }
}
