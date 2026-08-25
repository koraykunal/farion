using System.Collections.Generic;
using Farion.Rendering.Celestial;
using UnityEngine;

namespace Farion.Rendering.Lighting
{
    [DisallowMultipleComponent]
    public sealed class UnderwaterSpotLight : MonoBehaviour
    {
        static readonly List<UnderwaterSpotLight> Sources = new();

        [SerializeField] Light source;
        [Range(0f, 2f)]
        [SerializeField] float scatteringStrength = 0.85f;

        void Reset()
        {
            ResolveSource();
        }

        void OnEnable()
        {
            ResolveSource();
            if (!Sources.Contains(this))
            {
                Sources.Add(this);
            }
        }

        void OnDisable()
        {
            Sources.Remove(this);
        }

        void OnValidate()
        {
            scatteringStrength = Mathf.Clamp(scatteringStrength, 0f, 2f);
            ResolveSource();
        }

        internal bool TryBuildShaderData(
            CelestialOceanEffectData ocean,
            Vector3 cameraPosition,
            out UnderwaterSpotLightShaderData data)
        {
            data = default;
            if (source == null || !source.isActiveAndEnabled || source.type != LightType.Spot ||
                source.intensity <= 0f || source.range <= 0f || scatteringStrength <= 0f ||
                ocean.Profile == null ||
                !ocean.IsPointUnderwater(source.transform.position))
            {
                return false;
            }

            float relevanceRange = source.range * 2f;
            float cameraDistanceSquared = (source.transform.position - cameraPosition).sqrMagnitude;
            if (cameraDistanceSquared > relevanceRange * relevanceRange)
            {
                return false;
            }

            Color lightColor = source.useColorTemperature
                ? (source.color * Mathf.CorrelatedColorTemperatureToRGB(source.colorTemperature)).linear
                : source.color.linear;
            float intensityScale = scatteringStrength
                * Mathf.Sqrt(source.intensity / ocean.Profile.UnderwaterLightReferenceIntensity);
            data = new UnderwaterSpotLightShaderData(
                new Vector4(
                    source.transform.position.x,
                    source.transform.position.y,
                    source.transform.position.z,
                    source.range),
                new Vector4(
                    source.transform.forward.x,
                    source.transform.forward.y,
                    source.transform.forward.z,
                    Mathf.Cos(source.spotAngle * 0.5f * Mathf.Deg2Rad)),
                new Vector4(lightColor.r, lightColor.g, lightColor.b, intensityScale),
                Mathf.Cos(source.innerSpotAngle * 0.5f * Mathf.Deg2Rad),
                cameraDistanceSquared);
            return true;
        }

        internal static void Collect(
            CelestialOceanEffectData ocean,
            Vector3 cameraPosition,
            List<UnderwaterSpotLightShaderData> results)
        {
            results.Clear();
            for (int i = Sources.Count - 1; i >= 0; i--)
            {
                UnderwaterSpotLight candidate = Sources[i];
                if (candidate == null)
                {
                    Sources.RemoveAt(i);
                    continue;
                }

                if (candidate.TryBuildShaderData(ocean, cameraPosition, out UnderwaterSpotLightShaderData data))
                {
                    results.Add(data);
                }
            }

            results.Sort(static (left, right) =>
                left.CameraDistanceSquared.CompareTo(right.CameraDistanceSquared));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry()
        {
            Sources.Clear();
        }

        void ResolveSource()
        {
            source ??= GetComponentInChildren<Light>(true);
        }
    }

    internal readonly struct UnderwaterSpotLightShaderData
    {
        public UnderwaterSpotLightShaderData(
            Vector4 positionRange,
            Vector4 directionOuterCos,
            Vector4 colorStrength,
            float innerConeCos,
            float cameraDistanceSquared)
        {
            PositionRange = positionRange;
            DirectionOuterCos = directionOuterCos;
            ColorStrength = colorStrength;
            InnerConeCos = innerConeCos;
            CameraDistanceSquared = cameraDistanceSquared;
        }

        public Vector4 PositionRange { get; }
        public Vector4 DirectionOuterCos { get; }
        public Vector4 ColorStrength { get; }
        public float InnerConeCos { get; }
        public float CameraDistanceSquared { get; }
    }
}
