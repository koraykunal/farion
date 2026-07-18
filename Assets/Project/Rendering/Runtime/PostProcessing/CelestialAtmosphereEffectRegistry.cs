using System.Collections.Generic;
using Farion.Rendering.Celestial;
using UnityEngine;

namespace Farion.Rendering.PostProcessing
{
    public static class CelestialAtmosphereEffectRegistry
    {
        static readonly List<ICelestialAtmosphereEffectSource> Sources = new();

        public static bool HasSources => Sources.Count > 0;

        public static void Register(ICelestialAtmosphereEffectSource source)
        {
            if (source != null && !Sources.Contains(source))
            {
                Sources.Add(source);
            }
        }

        public static void Unregister(ICelestialAtmosphereEffectSource source)
        {
            if (source != null)
            {
                Sources.Remove(source);
            }
        }

        public static void Collect(Camera camera, List<CelestialAtmosphereEffectData> results)
        {
            results.Clear();
            if (camera == null)
            {
                return;
            }

            for (int i = Sources.Count - 1; i >= 0; i--)
            {
                ICelestialAtmosphereEffectSource source = Sources[i];
                if (source == null)
                {
                    Sources.RemoveAt(i);
                    continue;
                }

                if (source.TryGetAtmosphereEffectData(out CelestialAtmosphereEffectData data))
                {
                    results.Add(data);
                }
            }

            Vector3 cameraPosition = camera.transform.position;
            results.Sort((left, right) =>
                right.GetCameraSortDistance(cameraPosition).CompareTo(left.GetCameraSortDistance(cameraPosition)));
        }
    }
}
