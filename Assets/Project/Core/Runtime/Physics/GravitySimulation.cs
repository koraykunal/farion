using System.Collections.Generic;
using Farion.Core.Time;
using UnityEngine;

namespace Farion.Core.Physics
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class GravitySimulation : MonoBehaviour
    {
        public const float DefaultGravitationalConstant = 0.0001f;

        [SerializeField] GravitySettings settings;
        [SerializeField] bool autoDiscoverBodies;
        [SerializeField] List<CelestialBody> registeredBodies = new();

        static GravitySimulation active;
        readonly List<CelestialBody> simulationBodies = new();

        public static GravitySimulation Active => active;
        public IReadOnlyList<CelestialBody> Bodies => simulationBodies;
        public GravitySettings Settings => settings;
        public float GravitationalConstant => settings != null ? settings.GravitationalConstant : DefaultGravitationalConstant;
        public float MinimumInteractionDistance => settings != null && settings.UseMinimumInteractionDistance ? settings.MinimumInteractionDistance : 0f;
        public float MaxAcceleration => settings != null && settings.ClampAcceleration ? settings.MaxAcceleration : 0f;

        void Awake()
        {
            if (active != null && active != this)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"Multiple {nameof(GravitySimulation)} instances detected. Using {name} as active.");
#endif
            }

            active = this;
            RefreshBodies();
            ConfigureBodies();

            if (settings != null && settings.ApplyFixedTimeStepOnStart)
            {
                SimulationClock.ApplyFixedStep(settings.FixedTimeStep);
            }
        }

        void OnDestroy()
        {
            if (active == this)
            {
                active = null;
            }
        }

        void FixedUpdate()
        {
            if (simulationBodies.Count == 0)
            {
                return;
            }

            int substeps = settings != null ? settings.SolverSubsteps : 1;
            float substepDelta = UnityEngine.Time.fixedDeltaTime / substeps;

            for (int step = 0; step < substeps; step++)
            {
                IntegrateVelocities(substepDelta);
                IntegratePositions(substepDelta);
            }
        }

        public void RefreshBodies()
        {
            simulationBodies.Clear();

            if (autoDiscoverBodies)
            {
                var discovered = FindObjectsByType<CelestialBody>(FindObjectsInactive.Exclude);
                simulationBodies.AddRange(discovered);
            }

            foreach (CelestialBody body in registeredBodies)
            {
                if (body != null && !simulationBodies.Contains(body))
                {
                    simulationBodies.Add(body);
                }
            }
        }

        public Vector3 CalculateAcceleration(Vector3 point, CelestialBody ignoredBody = null)
        {
            Vector3 acceleration = Vector3.zero;

            foreach (CelestialBody body in simulationBodies)
            {
                if (body == null || body == ignoredBody || !body.ParticipatesInNBody)
                {
                    continue;
                }

                acceleration += CalculateAccelerationFromBody(point, body);
            }

            return MaxAcceleration > 0f ? Vector3.ClampMagnitude(acceleration, MaxAcceleration) : acceleration;
        }

        public Vector3 CalculateAccelerationFromBody(Vector3 point, CelestialBody body)
        {
            Vector3 offset = body.Position - point;
            float minimumDistance = MinimumInteractionDistance;
            float sqrDistance = minimumDistance > 0f
                ? Mathf.Max(offset.sqrMagnitude, minimumDistance * minimumDistance)
                : offset.sqrMagnitude;

            if (sqrDistance <= Mathf.Epsilon)
            {
                return Vector3.zero;
            }

            return offset.normalized * (GravitationalConstant * body.Mass / sqrDistance);
        }

        public GravitySample FindDominantBody(Vector3 point, CelestialBody ignoredBody = null)
        {
            CelestialBody dominantBody = null;
            Vector3 dominantAcceleration = Vector3.zero;
            float dominantAccelerationSqr = 0f;
            float centerDistance = 0f;
            float surfaceDistance = 0f;

            foreach (CelestialBody body in simulationBodies)
            {
                if (body == null || body == ignoredBody || !body.ParticipatesInNBody)
                {
                    continue;
                }

                Vector3 acceleration = CalculateAccelerationFromBody(point, body);
                float accelerationSqr = acceleration.sqrMagnitude;
                if (accelerationSqr <= dominantAccelerationSqr)
                {
                    continue;
                }

                dominantBody = body;
                dominantAcceleration = acceleration;
                dominantAccelerationSqr = accelerationSqr;
                CelestialSurfaceSample surfaceSample = body.SampleSurface(point);
                centerDistance = surfaceSample.CenterDistance;
                surfaceDistance = surfaceSample.SurfaceDistance;
            }

            return dominantBody != null
                ? new GravitySample(dominantBody, dominantAcceleration, centerDistance, surfaceDistance, dominantBody.SampleSurface(point).Normal)
                : GravitySample.Empty;
        }

        public GravitySample FindNearestSurface(Vector3 point)
        {
            CelestialBody nearestBody = null;
            float nearestCenterDistance = 0f;
            float nearestSurfaceDistance = float.PositiveInfinity;
            Vector3 acceleration = Vector3.zero;

            foreach (CelestialBody body in simulationBodies)
            {
                if (body == null)
                {
                    continue;
                }

                CelestialSurfaceSample surfaceSample = body.SampleSurface(point);
                if (surfaceSample.SurfaceDistance >= nearestSurfaceDistance)
                {
                    continue;
                }

                nearestBody = body;
                nearestCenterDistance = surfaceSample.CenterDistance;
                nearestSurfaceDistance = surfaceSample.SurfaceDistance;
                acceleration = body.ParticipatesInNBody ? CalculateAccelerationFromBody(point, body) : Vector3.zero;
            }

            return nearestBody != null
                ? new GravitySample(nearestBody, acceleration, nearestCenterDistance, nearestSurfaceDistance, nearestBody.SampleSurface(point).Normal)
                : GravitySample.Empty;
        }

        public static bool TryCalculateAcceleration(Vector3 point, out Vector3 acceleration, CelestialBody ignoredBody = null)
        {
            if (active == null)
            {
                acceleration = Vector3.zero;
                return false;
            }

            acceleration = active.CalculateAcceleration(point, ignoredBody);
            return true;
        }

        public void CaptureSnapshots(List<CelestialBodySnapshot> results)
        {
            if (results == null)
            {
                return;
            }

            RefreshBodies();
            for (int i = 0; i < simulationBodies.Count; i++)
            {
                CelestialBody body = simulationBodies[i];
                if (body != null)
                {
                    results.Add(body.CaptureSnapshot());
                }
            }
        }

        public bool ApplySnapshots(IReadOnlyList<CelestialBodySnapshot> snapshots)
        {
            if (!CanApplySnapshots(snapshots))
            {
                return false;
            }

            RefreshBodies();
            for (int i = 0; i < snapshots.Count; i++)
            {
                CelestialBodySnapshot snapshot = snapshots[i];
                if (!snapshot.IsValid)
                {
                    return false;
                }

                CelestialBody body = FindBody(snapshot);
                if (body == null || !body.ApplySnapshot(snapshot))
                {
                    return false;
                }
            }

            UnityEngine.Physics.SyncTransforms();
            return true;
        }

        public bool CanApplySnapshots(IReadOnlyList<CelestialBodySnapshot> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
            {
                return false;
            }

            RefreshBodies();
            for (int i = 0; i < snapshots.Count; i++)
            {
                CelestialBodySnapshot snapshot = snapshots[i];
                if (!snapshot.IsValid || FindBody(snapshot) == null)
                {
                    return false;
                }
            }

            return true;
        }

        void ConfigureBodies()
        {
            foreach (CelestialBody body in simulationBodies)
            {
                if (body == null)
                {
                    continue;
                }

                body.RecalculateMass(GravitationalConstant);
                body.ConfigureRigidbody();
                body.ResetSimulationState();
            }
        }

        CelestialBody FindBody(CelestialBodySnapshot snapshot)
        {
            for (int i = 0; i < simulationBodies.Count; i++)
            {
                CelestialBody body = simulationBodies[i];
                if (body != null && body.SnapshotMatches(snapshot))
                {
                    return body;
                }
            }

            return null;
        }

        void IntegrateVelocities(float deltaTime)
        {
            for (int i = 0; i < simulationBodies.Count; i++)
            {
                CelestialBody body = simulationBodies[i];
                if (body == null || !body.ParticipatesInNBody)
                {
                    continue;
                }

                Vector3 acceleration = CalculateAcceleration(body.Position, body);
                body.IntegrateVelocity(acceleration, deltaTime);
            }
        }

        void IntegratePositions(float deltaTime)
        {
            foreach (CelestialBody body in simulationBodies)
            {
                if (body == null || !body.ParticipatesInNBody)
                {
                    continue;
                }

                body.IntegratePosition(deltaTime);
            }
        }
    }
}
