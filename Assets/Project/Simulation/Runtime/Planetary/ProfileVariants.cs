using UnityEngine;

namespace Farion.Simulation.Planetary
{
    public static class ProfileVariants
    {
        public static T Clone<T>(T template) where T : ScriptableObject
        {
            T variant = Object.Instantiate(template);
            variant.name = template.name;
            variant.hideFlags = HideFlags.HideAndDontSave;
            return variant;
        }

        public static void Destroy(Object variant)
        {
            if (variant == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(variant);
            }
            else
            {
                Object.DestroyImmediate(variant);
            }
        }
    }
}
