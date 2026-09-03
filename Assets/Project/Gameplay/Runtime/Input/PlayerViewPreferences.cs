using UnityEngine;

namespace Farion.Gameplay.Input
{
    public static class PlayerViewPreferences
    {
        public const float MinimumFieldOfView = 60f;
        public const float MaximumFieldOfView = 110f;
        public const float ReferenceFieldOfView = 75f;

        const string FieldOfViewKey = "farion.view.fieldOfView";
        const string ReducedMotionKey = "farion.ui.reducedMotion";

        static float fieldOfView = float.NaN;

        public static float FieldOfView
        {
            get
            {
                if (float.IsNaN(fieldOfView))
                {
                    fieldOfView = Clamp(
                        PlayerPrefs.GetFloat(FieldOfViewKey, ReferenceFieldOfView));
                }

                return fieldOfView;
            }
            set
            {
                float clamped = Clamp(value);
                if (Mathf.Approximately(FieldOfView, clamped))
                {
                    return;
                }

                fieldOfView = clamped;
                PlayerPrefs.SetFloat(FieldOfViewKey, clamped);
                PlayerPrefs.Save();
            }
        }

        public static float FieldOfViewOffset => FieldOfView - ReferenceFieldOfView;

        public static bool ReducedMotion =>
            PlayerPrefs.GetInt(ReducedMotionKey, 0) != 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            fieldOfView = float.NaN;
        }

        public static float ResolveFieldOfView(float authoredFieldOfView)
        {
            return Mathf.Clamp(
                authoredFieldOfView + FieldOfViewOffset,
                MinimumFieldOfView,
                MaximumFieldOfView);
        }

        static float Clamp(float value)
        {
            return Mathf.Clamp(value, MinimumFieldOfView, MaximumFieldOfView);
        }
    }
}
