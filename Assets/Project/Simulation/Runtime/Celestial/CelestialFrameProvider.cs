using System.Collections.Generic;
using Farion.Simulation.Physics;
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

        public GravitySimulation Simulation => simulation;

        public bool UsesSimulation(GravitySimulation candidate)
        {
            return candidate != null && ReferenceEquals(simulation, candidate);
        }

        void OnValidate()
        {
            environmentSources ??= new List<MonoBehaviour>();
            surfaceSources ??= new List<MonoBehaviour>();
        }

        public void AddEnvironmentSource(MonoBehaviour source)
        {
            if (source is ICelestialEnvironmentProvider && !environmentSources.Contains(source))
            {
                environmentSources.Add(source);
            }
        }

        public void AddSurfaceSource(MonoBehaviour source)
        {
            if (source is ICelestialSurfaceProvider && !surfaceSources.Contains(source))
            {
                surfaceSources.Add(source);
            }
        }

        public void RemoveEnvironmentSource(MonoBehaviour source)
        {
            environmentSources.Remove(source);
        }

        public void RemoveSurfaceSource(MonoBehaviour source)
        {
            surfaceSources.Remove(source);
        }

        public CelestialFrameSample Sample(Vector3 position, Vector3 velocity)
        {
            GravitySimulation source = Simulation;
            return source != null
                ? Sample(position, velocity, source.SimulationTime)
                : CelestialFrameSample.Empty(position, velocity);
        }

        public CelestialFrameSample Sample(
            Vector3 position,
            Vector3 velocity,
            double simulationTime,
            CelestialBody incumbent = null)
        {
            GravitySimulation source = Simulation;
            if (source == null)
            {
                return CelestialFrameSample.Empty(position, velocity);
            }

            GravitySample gravitySample = source.FindDominantBody(
                position,
                null,
                incumbent,
                GravitySimulation.DominanceHysteresisBias);
            if (!gravitySample.HasBody)
            {
                return CelestialFrameSample.Empty(position, velocity);
            }

            CelestialBody body = gravitySample.Body;
            CelestialSurfaceSample surfaceSample = ResolveSurface(body, position);
            CelestialEnvironmentSample environmentSample = ResolveEnvironment(body)
                .AtSimulationTime(simulationTime);

            return new CelestialFrameSample(
                body,
                position,
                velocity,
                body.Position,
                body.Velocity,
                body.AngularVelocity,
                surfaceSample.Point,
                surfaceSample.Normal,
                source.CalculateReferenceFrameAcceleration(position, velocity),
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

        public CelestialSurfaceSample SampleSurface(CelestialBody body, Vector3 position)
        {
            return body != null
                ? ResolveSurface(body, position)
                : CelestialSurfaceSample.Empty;
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
