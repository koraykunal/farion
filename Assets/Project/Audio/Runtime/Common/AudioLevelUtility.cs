using UnityEngine;

namespace Farion.Audio
{
    public static class AudioLevelUtility
    {
        const float SilentDb = -80f;

        public static float LinearToDecibels(float linear)
        {
            return linear <= 0.0001f
                ? SilentDb
                : Mathf.Log10(Mathf.Clamp01(linear)) * 20f;
        }

        public static float ResponsivenessToLerp(float responsiveness, float deltaTime)
        {
            return responsiveness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-responsiveness * Mathf.Max(0f, deltaTime));
        }
    }
}
