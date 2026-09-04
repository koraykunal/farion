using UnityEngine;

namespace Farion.Rendering.Celestial
{
    internal static class PaletteVariation
    {
        public static Color Shift(Color color, float hueShiftDegrees, float valueScale)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            hue = Mathf.Repeat(hue + hueShiftDegrees / 360f, 1f);
            Color shifted = Color.HSVToRGB(hue, saturation, Mathf.Clamp01(value * valueScale), true);
            shifted.a = color.a;
            return shifted;
        }
    }
}
