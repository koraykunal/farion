using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Farion.Simulation.Physics
{
    [MovedFrom(true, "Farion.Core.Physics", "Farion.Core.Runtime")]
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class GravitySimulation : MonoBehaviour
    {
        public const float DefaultGravitationalConstant = 0.0001f;

        [SerializeField] GravitySettings settings;

        [Header("Physics Reference Frame")]
        [Tooltip("Optional translating frame used by local Unity physics. The selected body's orbital state remains inertial while its local collider stays stationary.")]
        [SerializeField] CelestialBody physicsReferenceBody;

        [Header("Bodies")]
        [SerializeField] bool autoDiscoverBodies;
        [SerializeField] List<CelestialBody> registeredBodies = new();

        [Header("Runtime Reference Frame")]
        [SerializeField] Vector3 referenceFrameVelocity;
        [SerializeField] Vector3 referenceFrameAcceleration;

        readonly List<CelestialBody> simulationBodies = new();

        public bool IntegrationEnabled { get; private set; } = true;

        public IReadOnlyList<CelestialBody> Bodies => simulationBodies;
        public GravitySettings Settings => settings;
        public float GravitationalConstant => settings != null ? settings.GravitationalConstant : DefaultGravitationalConstant;
        public float MinimumInteractionDistance => settings != null && settings.UseMinimumInteractionDistance ? settings.MinimumInteractionDistance : 0f;
        public float MaxAcceleration => settings != null && settings.ClampAcceleration ? settings.MaxAcceleration : 0f;
        public CelestialBody PhysicsReferenceBody => physicsReferenceBody;
        public bool HasPhysicsReferenceFrame => ResolvedPhysicsReferenceBody != null;
        public Vector3 ReferenceFrameVelocity => referenceFrameVelocity;
        public Vector3 ReferenceFrameAcceleration => referenceFrameAcceleration;

        void Awake()
        {
            RefreshBodies();
            ConfigureBodies();

            if (settings != null &&
                settings.ApplyFixedTimeStepOnStart &&
                settings.FixedTimeStep > 0f)
            {
                UnityEngine.Time.fixedDeltaTime = settings.FixedTimeStep;
            }
        }

        void FixedUpdate()
        {
            if (!IntegrationEnabled || simulationBodies.Count == 0)
            {
                return;
            }

            int substeps = settings != null ? settings.SolverSubsteps : 1;
            float substepDelta = UnityEngine.Time.fixedDeltaTime / substeps;

            for (int step = 0; step < substeps; step++)
            {
                UpdateReferenceFrameAcceleration();
                IntegrateVelocities(substepDelta);
                UpdateReferenceFrameVelocity();
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

        public void SetIntegrationEnabled(bool enabled)
        {
            IntegrationEnabled = enabled;
            if (simulationBodies.Count > 0)
            {
                UpdateReferenceFrameAcceleration();
                UpdateReferenceFrameVelocity();
            }
        }

        public Vector3 CalculateAcceleration(Vector3 point, CelestialBody ignoredBody = null)
        {
            return ClampAcceleration(CalculateUnclampedAcceleration(point, ignoredBody));
        }

        public Vector3 CalculateReferenceFrameAcceleration(
            Vector3 point,
            CelestialBody ignoredBody = null)
        {
            return CalculateAcceleration(point, ignoredBody) -
                referenceFrameAcceleration;
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

        public Vector3 CalculateOrbitalAcceleration(CelestialBody body)
        {
            if (body == null || !body.ParticipatesInNBody)
            {
                return Vector3.zero;
            }

            CelestialBody attractor = body.OrbitAttractor;
            return body.MotionMode == CelestialBodyMotionMode.KinematicOrbit &&
                   attractor != null &&
                   attractor.ParticipatesInNBody
                ? CalculateAccelerationFromBody(body.Position, attractor)
                : CalculateAcceleration(body.Position, body);
        }

        public GravitySample FindDominantBody(Vector3 point, CelestialBody ignoredBody = null)
        {
            CelestialBody dominantBody = null;
            Vector3 dominantAcceleration = Vector3.zero;
            float dominantAccelerationSqr = 0f;
            CelestialSurfaceSample dominantSurface = default;

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
                dominantSurface = body.SampleSurface(point);
            }

            return dominantBody != null
                ? new GravitySample(
                    dominantBody,
                    dominantAcceleration,
                    dominantSurface.CenterDistance,
                    dominantSurface.SurfaceDistance,
                    dominantSurface.Normal)
                : GravitySample.Empty;
        }

        public GravitySample FindNearestSurface(Vector3 point)
        {
            CelestialBody nearestBody = null;
            CelestialSurfaceSample nearestSurface = default;
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
                nearestSurface = surfaceSample;
                nearestSurfaceDistance = surfaceSample.SurfaceDistance;
                acceleration = body.ParticipatesInNBody ? CalculateAccelerationFromBody(point, body) : Vector3.zero;
            }

            return nearestBody != null
                ? new GravitySample(
                    nearestBody,
                    acceleration,
                    nearestSurface.CenterDistance,
                    nearestSurface.SurfaceDistance,
                    nearestSurface.Normal)
                : GravitySample.Empty;
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

            UpdateReferenceFrameAcceleration();
            UpdateReferenceFrameVelocity();
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

            UpdateReferenceFrameAcceleration();
            UpdateReferenceFrameVelocity();
            ApplyReferenceFrameVelocityToBodies();
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

                body.IntegrateVelocity(CalculateOrbitalAcceleration(body), deltaTime);
            }
        }

        void IntegratePositions(float deltaTime)
        {
            foreach (CelestialBody body in simulationBodies)
            {
                if (body == null)
                {
                    continue;
                }

                body.IntegratePosition(deltaTime, referenceFrameVelocity);
            }
        }

        void UpdateReferenceFrameAcceleration()
        {
            CelestialBody referenceBody = ResolvedPhysicsReferenceBody;
            referenceFrameAcceleration = referenceBody != null
                ? CalculateAcceleration(referenceBody.Position, referenceBody)
                : Vector3.zero;
        }

        void UpdateReferenceFrameVelocity()
        {
            CelestialBody referenceBody = ResolvedPhysicsReferenceBody;
            referenceFrameVelocity = referenceBody != null
                ? referenceBody.InertialVelocity
                : Vector3.zero;
            ApplyReferenceFrameVelocityToBodies();
        }

        void ApplyReferenceFrameVelocityToBodies()
        {
            for (int i = 0; i < simulationBodies.Count; i++)
            {
                CelestialBody body = simulationBodies[i];
                if (body != null)
                {
                    body.SetPhysicsReferenceFrameVelocity(referenceFrameVelocity);
                }
            }
        }

        CelestialBody ResolvedPhysicsReferenceBody =>
            physicsReferenceBody != null && simulationBodies.Contains(physicsReferenceBody)
                ? physicsReferenceBody
                : null;

        Vector3 CalculateUnclampedAcceleration(
            Vector3 point,
            CelestialBody ignoredBody = null)
        {
            Vector3 acceleration = Vector3.zero;
            for (int i = 0; i < simulationBodies.Count; i++)
            {
                CelestialBody body = simulationBodies[i];
                if (body == null ||
                    body == ignoredBody ||
                    !body.ParticipatesInNBody)
                {
                    continue;
                }

                acceleration += CalculateAccelerationFromBody(point, body);
            }

            return acceleration;
        }

        Vector3 ClampAcceleration(Vector3 acceleration)
        {
            return MaxAcceleration > 0f
                ? Vector3.ClampMagnitude(acceleration, MaxAcceleration)
                : acceleration;
        }
    }
}
