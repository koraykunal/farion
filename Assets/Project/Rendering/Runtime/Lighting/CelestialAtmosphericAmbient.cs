using Farion.Rendering.Celestial;
using UnityEngine;

namespace Farion.Rendering.Lighting
{
    public readonly struct AtmosphericAmbientSample
    {
        public AtmosphericAmbientSample(
            Color sky,
            Color equator,
            Color ground,
            Vector3 up,
            float densityFactor)
        {
            Sky = sky;
            Equator = equator;
            Ground = ground;
            Up = up;
            DensityFactor = densityFactor;
        }

        public Color Sky { get; }
        public Color Equator { get; }
        public Color Ground { get; }
        public Vector3 Up { get; }
        public float DensityFactor { get; }
    }

    public static class CelestialAtmosphericAmbient
    {
        const float DayStartElevation = -0.08f;
        const float DayFullElevation = 0.25f;
        const float DuskBandWidth = 0.35f;
        const float DuskGateStart = -0.30f;
        const float DuskGateEnd = -0.05f;

        public static bool TrySample(
            in CelestialAtmosphereEffectData atmosphere,
            Vector3 focusPosition,
            Vector3 directionToStar,
            Color starColor,
            float starIntensity,
            CelestialLightingProfile profile,
            out AtmosphericAmbientSample sample)
        {
            sample = default;
            if (profile == null || atmosphere.Profile == null)
            {
                return false;
            }

            Vector3 offset = focusPosition - atmosphere.Center;
            float distance = offset.magnitude;
            float thickness = atmosphere.AtmosphereRadius - atmosphere.SurfaceRadius;
            if (distance <= 0.0001f || thickness <= 0.0001f)
            {
                return false;
            }

            Vector3 up = offset / distance;
            float height01 = Mathf.Clamp01((distance - atmosphere.SurfaceRadius) / thickness);
            float density = Mathf.Exp(-height01 * atmosphere.Profile.DensityFalloff) * (1f - height01);
            if (density <= 0f)
            {
                return false;
            }

            float elevation = Vector3.Dot(up, directionToStar);
            float day = SmoothStep(DayStartElevation, DayFullElevation, elevation);
            float dusk = (1f - Mathf.Clamp01(Mathf.Abs(elevation) / DuskBandWidth))
                * SmoothStep(DuskGateStart, DuskGateEnd, elevation);

            Vector3 coefficients = atmosphere.Profile.GetScatteringCoefficients();
            float maxCoefficient = Mathf.Max(coefficients.x, Mathf.Max(coefficients.y, coefficients.z));
            Vector3 skyHue = maxCoefficient > 0.0001f ? coefficients / maxCoefficient : Vector3.one;

            float energy = starIntensity * profile.AtmosphericAmbientIntensity;
            Color starEnergy = starColor * energy;
            Color dusklight = profile.DuskColor * (dusk * energy);

            Color sky = new Color(
                starEnergy.r * skyHue.x,
                starEnergy.g * skyHue.y,
                starEnergy.b * skyHue.z) * day
                + dusklight * 0.5f;
            Color ground = profile.GroundAlbedo * (sky * 0.7f + starEnergy * (0.25f * day));
            Color equator = sky * 0.55f + ground * 0.30f + dusklight * 0.35f;

            sample = new AtmosphericAmbientSample(
                Scale(sky, density),
                Scale(equator, density),
                Scale(ground, density),
                up,
                density);
            return true;
        }

        static Color Scale(Color color, float factor)
        {
            return new Color(color.r * factor, color.g * factor, color.b * factor, 1f);
        }

        static float SmoothStep(float edgeStart, float edgeEnd, float value)
        {
            float t = Mathf.Clamp01((value - edgeStart) / (edgeEnd - edgeStart));
            return t * t * (3f - 2f * t);
        }
    }
}
