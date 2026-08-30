using UnityEngine;

namespace Farion.Simulation.Planetary
{
    public static class PlanetarySampling
    {
        public static float ResolveFootprintFade(float featureFootprint, float sampleFootprint)
        {
            if (featureFootprint <= 0f || sampleFootprint <= 0f)
            {
                return 1f;
            }

            return 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(featureFootprint * 0.5f, featureFootprint, sampleFootprint));
        }

        public static float ResolveUsableOctaves(
            float scale,
            float lacunarity,
            int octaves,
            float sampleFootprint)
        {
            if (sampleFootprint <= 0f)
            {
                return octaves;
            }

            float nyquistFrequency = 1f / (2f * sampleFootprint);
            if (scale >= nyquistFrequency)
            {
                return 1f;
            }

            float safeLacunarity = Mathf.Max(1.0001f, lacunarity);
            float usable = 1f + Mathf.Log(nyquistFrequency / scale) / Mathf.Log(safeLacunarity);
            return Mathf.Clamp(usable, 1f, octaves);
        }

        public static float SampleBandLimitedFractalSigned(
            Vector3 direction,
            float scale,
            int octaves,
            float lacunarity,
            float persistence,
            int seed,
            Vector3 domainOffset,
            float sampleFootprint)
        {
            float usableOctaves = ResolveUsableOctaves(scale, lacunarity, octaves, sampleFootprint);
            int wholeOctaves = Mathf.Clamp(Mathf.FloorToInt(usableOctaves), 1, Mathf.Max(1, octaves));
            float value = SampleFractalSigned(
                direction,
                scale,
                wholeOctaves,
                lacunarity,
                persistence,
                seed,
                domainOffset);

            float blend = Mathf.Clamp01(usableOctaves - wholeOctaves);
            if (blend <= 0f || wholeOctaves >= octaves)
            {
                return value;
            }

            float finer = SampleFractalSigned(
                direction,
                scale,
                wholeOctaves + 1,
                lacunarity,
                persistence,
                seed,
                domainOffset);
            return Mathf.Lerp(value, finer, blend);
        }

        public static float EvaluateRange(Vector2 range, float value, float blend)
        {
            float minimum = Mathf.Min(range.x, range.y);
            float maximum = Mathf.Max(range.x, range.y);
            float feather = Mathf.Max(0f, blend);

            if (value < minimum - feather || value > maximum + feather)
            {
                return 0f;
            }

            if (feather <= 0f)
            {
                return value >= minimum && value <= maximum ? 1f : 0f;
            }

            float lower = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(minimum - feather, minimum + feather, value));
            float upper = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(maximum - feather, maximum + feather, value));
            return Mathf.Clamp01(Mathf.Min(lower, upper));
        }

        public static float SampleFractal01(
            Vector3 direction,
            float scale,
            int octaves,
            float lacunarity,
            float persistence,
            int seed)
        {
            return SampleFractal01(
                direction,
                scale,
                octaves,
                lacunarity,
                persistence,
                seed,
                Vector3.zero);
        }

        public static float SampleFractal01(
            Vector3 direction,
            float scale,
            int octaves,
            float lacunarity,
            float persistence,
            int seed,
            Vector3 domainOffset)
        {
            Vector3 point = NormalizeDirection(direction) * Mathf.Max(0.001f, scale) + domainOffset;
            int layerCount = Mathf.Clamp(octaves, 1, 8);
            float amplitude = 1f;
            float amplitudeSum = 0f;
            float valueSum = 0f;

            for (int octave = 0; octave < layerCount; octave++)
            {
                valueSum += SampleValueNoise(point, seed + octave * 1013) * amplitude;
                amplitudeSum += amplitude;
                amplitude *= Mathf.Clamp01(persistence);
                point = RotateDomain(point) * Mathf.Max(1f, lacunarity);
            }

            return amplitudeSum > 0f
                ? Mathf.Clamp01(valueSum / amplitudeSum)
                : 0.5f;
        }

        public static float SampleFractalSigned(
            Vector3 direction,
            float scale,
            int octaves,
            float lacunarity,
            float persistence,
            int seed)
        {
            return SampleFractal01(direction, scale, octaves, lacunarity, persistence, seed) * 2f - 1f;
        }

        public static float SampleFractalSigned(
            Vector3 direction,
            float scale,
            int octaves,
            float lacunarity,
            float persistence,
            int seed,
            Vector3 domainOffset)
        {
            return SampleFractal01(
                direction,
                scale,
                octaves,
                lacunarity,
                persistence,
                seed,
                domainOffset) * 2f - 1f;
        }

        public static float SampleRidged01(
            Vector3 direction,
            float scale,
            int octaves,
            float lacunarity,
            float persistence,
            float power,
            float gain,
            int seed,
            Vector3 domainOffset)
        {
            Vector3 point = NormalizeDirection(direction) * Mathf.Max(0.001f, scale) + domainOffset;
            int layerCount = Mathf.Clamp(octaves, 1, 8);
            float amplitude = 1f;
            float amplitudeSum = 0f;
            float valueSum = 0f;
            float ridgeWeight = 1f;

            for (int octave = 0; octave < layerCount; octave++)
            {
                float signedNoise = SampleValueNoise(point, seed + octave * 1013) * 2f - 1f;
                float ridge = Mathf.Pow(1f - Mathf.Abs(signedNoise), Mathf.Max(0.1f, power));
                ridge *= ridgeWeight;
                ridgeWeight = Mathf.Clamp01(ridge * Mathf.Max(0f, gain));

                valueSum += ridge * amplitude;
                amplitudeSum += amplitude;
                amplitude *= Mathf.Clamp01(persistence);
                point = RotateDomain(point) * Mathf.Max(1f, lacunarity);
            }

            return amplitudeSum > 0f
                ? Mathf.Clamp01(valueSum / amplitudeSum)
                : 0f;
        }

        static Vector3 NormalizeDirection(Vector3 direction)
        {
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
        }

        static Vector3 RotateDomain(Vector3 point)
        {
            return new Vector3(
                point.y * 0.8f + point.z * 0.6f,
                point.x * -0.8f + point.y * 0.36f - point.z * 0.48f,
                point.x * -0.6f - point.y * 0.48f + point.z * 0.64f);
        }

        static float SampleValueNoise(Vector3 point, int seed)
        {
            int x0 = Mathf.FloorToInt(point.x);
            int y0 = Mathf.FloorToInt(point.y);
            int z0 = Mathf.FloorToInt(point.z);
            int x1 = x0 + 1;
            int y1 = y0 + 1;
            int z1 = z0 + 1;

            float tx = Smooth(point.x - x0);
            float ty = Smooth(point.y - y0);
            float tz = Smooth(point.z - z0);

            float x00 = Mathf.Lerp(Hash01(x0, y0, z0, seed), Hash01(x1, y0, z0, seed), tx);
            float x10 = Mathf.Lerp(Hash01(x0, y1, z0, seed), Hash01(x1, y1, z0, seed), tx);
            float x01 = Mathf.Lerp(Hash01(x0, y0, z1, seed), Hash01(x1, y0, z1, seed), tx);
            float x11 = Mathf.Lerp(Hash01(x0, y1, z1, seed), Hash01(x1, y1, z1, seed), tx);
            float y0Value = Mathf.Lerp(x00, x10, ty);
            float y1Value = Mathf.Lerp(x01, x11, ty);
            return Mathf.Lerp(y0Value, y1Value, tz);
        }

        static float Smooth(float value)
        {
            return value * value * value * (value * (value * 6f - 15f) + 10f);
        }

        static float Hash01(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint hash = (uint)seed;
                hash ^= (uint)x * 0x9E3779B9u;
                hash = RotateLeft(hash, 13);
                hash ^= (uint)y * 0x85EBCA6Bu;
                hash = RotateLeft(hash, 11);
                hash ^= (uint)z * 0xC2B2AE35u;
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;
                return (hash & 0x00FFFFFFu) / 16777215f;
            }
        }

        static uint RotateLeft(uint value, int count)
        {
            return (value << count) | (value >> (32 - count));
        }
    }
}
