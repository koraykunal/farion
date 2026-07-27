using System.Collections.Generic;
using Farion.Core.Physics;
using UnityEngine;

namespace Farion.Simulation.Celestial
{
    [DisallowMultipleComponent]
    public sealed class CelestialFrameProvider : MonoBehaviour
    {
        [Header("Simulation")]
        [SerializeField] GravitySimulation simulation;

        [Header("Environment Sources")]
        [SerializeField] List<MonoBehaviour> environmentSources = new();

        [Header("Surface Sources")]
        [SerializeField] List<MonoBehaviour> surfaceSources = new();

        static readonly List<CelestialFrameProvider> EnabledProviders = new();
        static CelestialFrameProvider active;

        public static CelestialFrameProvider Active => active;
        public GravitySimulation Simulation => simulation != null ? simulation : GravitySimulation.Active;

        void OnEnable()
        {
            EnabledProviders.Remove(this);
            if (EnabledProviders.Count > 0)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"Multiple {nameof(CelestialFrameProvider)} instances detected. Using {name} as active.",
                    this);
#endif
            }

            EnabledProviders.Add(this);
            active = this;
        }

        void OnDisable()
        {
            EnabledProviders.Remove(this);
            active = EnabledProviders.Count > 0 ? EnabledProviders[^1] : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            EnabledProviders.Clear();
            active = null;
        }

        void OnValidate()
        {
            environmentSources ??= new List<MonoBehaviour>();
            environmentSources.RemoveAll(source => source == null || source is not ICelestialEnvironmentProvider);
            surfaceSources ??= new List<MonoBehaviour>();
            surfaceSources.RemoveAll(source => source == null || source is not ICelestialSurfaceProvider);
        }

        public CelestialFrameSample Sample(Vector3 position, Vector3 velocity)
        {
            GravitySimulation source = Simulation;
            if (source == null)
            {
                return CelestialFrameSample.Empty(position, velocity);
            }

            GravitySample gravitySample = source.FindDominantBody(position);
            if (!gravitySample.HasBody)
            {
                return CelestialFrameSample.Empty(position, velocity);
            }

            CelestialBody body = gravitySample.Body;
            CelestialSurfaceSample surfaceSample = ResolveSurface(body, position);
            CelestialEnvironmentSample environmentSample = ResolveEnvironment(body);

            return new CelestialFrameSample(
                body,
                position,
                velocity,
                body.Position,
                body.Velocity,
                body.AngularVelocity,
                surfaceSample.Point,
                surfaceSample.Normal,
                gravitySample.Acceleration,
                surfaceSample.CenterDistance,
                surfaceSample.SurfaceDistance,
                surfaceSample.SlopeAngleDegrees,
                environmentSample);
        }

        public bool TrySample(Vector3 position, Vector3 velocity, out CelestialFrameSample sample)
        {
            sample = Sample(position, velocity);
            return sample.HasBody;
        }

        CelestialSurfaceSample ResolveSurface(CelestialBody body, Vector3 position)
        {
            for (int i = 0; i < surfaceSources.Count; i++)
            {
                MonoBehaviour source = surfaceSources[i];
                if (source is ICelestialSurfaceProvider provider &&
                    provider.TrySampleSurface(body, position, out CelestialSurfaceSample sample))
                {
                    return sample;
                }
            }

            return body.SampleSurface(position);
        }

        CelestialEnvironmentSample ResolveEnvironment(CelestialBody body)
        {
            for (int i = 0; i < environmentSources.Count; i++)
            {
                MonoBehaviour source = environmentSources[i];
                if (source is ICelestialEnvironmentProvider provider &&
                    provider.TryGetEnvironment(body, out CelestialEnvironmentSample sample))
                {
                    return sample;
                }
            }

            return CelestialEnvironmentSample.Empty(body);
        }
    }
}
