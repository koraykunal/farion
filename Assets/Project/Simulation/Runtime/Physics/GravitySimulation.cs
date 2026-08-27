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
        public const float DominanceHysteresisBias = 1.1f;

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
        [SerializeField] Vector3 referenceFrameAngularVelocity;

        readonly List<CelestialBody> simulationBodies = new();
        readonly List<CelestialBody> analyticOrder = new();
        readonly List<CelestialSurfaceCollisionObserverState> referenceObservers = new();
        readonly List<GameObject> frameShiftRoots = new();
        readonly List<Rigidbody> frameShiftBodies = new();
        MonoBehaviour physicsReferenceObserverSource;
        ICelestialSurfaceCollisionObserver physicsReferenceObserver;
        ICelestialSurfaceCollisionObserverGroup physicsReferenceObserverGroup;
        double simulationTime;
        bool externalTimeSource;
        Quaternion referenceFrameRotation = Quaternion.identity;
        Quaternion referenceBodyWorldRotation = Quaternion.identity;
        Vector3 referenceFrameOrigin;
        bool referenceRotationInitialized;

        public bool IntegrationEnabled { get; private set; } = true;
        public double SimulationTime => simulationTime;
        public bool UsesExternalTimeSource => externalTimeSource;

        public IReadOnlyList<CelestialBody> Bodies => simulationBodies;
        public GravitySettings Settings => settings;
        public float GravitationalConstant => settings != null ? settings.GravitationalConstant : DefaultGravitationalConstant;
        public float MinimumInteractionDistance => settings != null && settings.UseMinimumInteractionDistance ? settings.MinimumInteractionDistance : 0f;
        public float MaxAcceleration => settings != null && settings.ClampAcceleration ? settings.MaxAcceleration : 0f;
        public CelestialBody PhysicsReferenceBody => physicsReferenceBody;
        public bool HasPhysicsReferenceFrame => ResolvedPhysicsReferenceBody != null;
        public int PhysicsReferenceStableId =>
            ResolvedPhysicsReferenceBody != null ? ResolvedPhysicsReferenceBody.StableId : 0;
        public Vector3 ReferenceFrameVelocity => referenceFrameVelocity;
        public Vector3 ReferenceFrameAcceleration => referenceFrameAcceleration;
        public Vector3 ReferenceFrameAngularVelocity => referenceFrameAngularVelocity;
        public Quaternion ReferenceFrameRotation => referenceFrameRotation;

        void Awake()
        {
            RefreshBodies();
            ConfigureBodies();
            RebuildAnalyticOrder();

            if (settings != null &&
                settings.ApplyFixedTimeStepOnStart &&
                settings.FixedTimeStep > 0f)
            {
                UnityEngine.Time.fixedDeltaTime = settings.FixedTimeStep;
            }
        }

        void FixedUpdate()
        {
            if (simulationBodies.Count == 0)
            {
                return;
            }

            RefreshPhysicsReferenceBody();
            if (!externalTimeSource)
            {
                simulationTime += System.Math.Round(
                    (double)UnityEngine.Time.fixedDeltaTime,
                    6);
            }

            ApplyAnalyticMotion();

            if (!IntegrationEnabled)
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

        public void RegisterBody(CelestialBody body)
        {
            if (body == null || simulationBodies.Contains(body))
            {
                return;
            }

            if (!registeredBodies.Contains(body))
            {
                registeredBodies.Add(body);
            }

            simulationBodies.Add(body);
            body.RecalculateMass(GravitationalConstant);
            body.ConfigureRigidbody();
            body.ResetSimulationState();
            RebuildAnalyticOrder();
            UpdateReferenceFrameAcceleration();
            UpdateReferenceFrameVelocity();
        }

        public void UnregisterBody(CelestialBody body)
        {
            if (body == null)
            {
                return;
            }

            registeredBodies.Remove(body);
            simulationBodies.Remove(body);
            RebuildAnalyticOrder();
            UpdateReferenceFrameAcceleration();
            UpdateReferenceFrameVelocity();
        }

        public void SetPhysicsReferenceBody(CelestialBody body)
        {
            if (body == physicsReferenceBody)
            {
                return;
            }

            if (body != null &&
                (!simulationBodies.Contains(body) || !body.UsesAnalyticMotion))
            {
                Debug.LogWarning(
                    $"Celestial body '{body.BodyName}' cannot become the physics reference because it is not a registered analytic body.",
                    body);
                return;
            }

            Vector3 previousFrameVelocity = referenceFrameVelocity;
            Vector3 previousFrameAngularVelocity = referenceFrameAngularVelocity;
            Vector3 previousFrameOrigin = referenceFrameOrigin;
            physicsReferenceBody = body;
            referenceRotationInitialized = false;
            InitializeReferenceRotation();
            UpdateReferenceFrameAcceleration();
            UpdateReferenceFrameVelocity();
            ShiftDynamicBodyVelocities(
                previousFrameVelocity,
                previousFrameAngularVelocity,
                previousFrameOrigin,
                referenceFrameVelocity,
                referenceFrameAngularVelocity,
                referenceFrameOrigin);
        }

        public bool ApplyNetworkPhysicsReferenceBody(int stableId)
        {
            CelestialBody target = null;
            if (stableId != 0)
            {
                for (int i = 0; i < simulationBodies.Count; i++)
                {
                    if (simulationBodies[i] != null && simulationBodies[i].StableId == stableId)
                    {
                        target = simulationBodies[i];
                        break;
                    }
                }

                if (target == null)
                {
                    return false;
                }
            }

            if (target == ResolvedPhysicsReferenceBody)
            {
                return false;
            }

            SetPhysicsReferenceBody(target);
            return true;
        }

        public void SetPhysicsReferenceObserverSource(MonoBehaviour source)
        {
            physicsReferenceObserverSource = source;
            physicsReferenceObserver = source as ICelestialSurfaceCollisionObserver;
            physicsReferenceObserverGroup = source as ICelestialSurfaceCollisionObserverGroup;
            referenceObservers.Clear();
        }

        public bool RefreshPhysicsReferenceBody()
        {
            GatherPhysicsReferenceObservers();
            CelestialBody candidate = ResolvePhysicsReferenceBodyCandidate(referenceObservers);

            if (candidate == null || candidate == ResolvedPhysicsReferenceBody)
            {
                return false;
            }

            SetPhysicsReferenceBody(candidate);
            return candidate == ResolvedPhysicsReferenceBody;
        }

        public CelestialBody ResolvePhysicsReferenceBodyCandidate(
            IReadOnlyList<CelestialSurfaceCollisionObserverState> observers)
        {
            if (observers == null || observers.Count == 0)
            {
                return null;
            }

            CelestialBody candidate = null;
            for (int i = 0; i < observers.Count; i++)
            {
                CelestialSurfaceCollisionObserverState observer = observers[i];
                if (!observer.IsValid || observer.Rigidbody.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                GravitySample sample = FindDominantBody(
                    observer.Position,
                    null,
                    ResolvedPhysicsReferenceBody,
                    DominanceHysteresisBias);
                if (!sample.HasBody)
                {
                    continue;
                }

                if (candidate == null)
                {
                    candidate = sample.Body;
                }
                else if (candidate != sample.Body)
                {
                    return ResolvedPhysicsReferenceBody;
                }
            }

            return candidate;
        }

        public void SetSimulationTime(double seconds)
        {
            simulationTime = seconds >= 0d ? seconds : 0d;
            if (simulationBodies.Count > 0)
            {
                RefreshPhysicsReferenceBody();
                ApplyAnalyticMotion();
            }
        }

        public void SetExternalTimeSource(bool enabled)
        {
            externalTimeSource = enabled;
        }

        public void RebuildAnalyticOrder()
        {
            analyticOrder.Clear();
            int guard = simulationBodies.Count + 1;
            for (int depth = 0; depth < guard && analyticOrder.Count < simulationBodies.Count; depth++)
            {
                for (int i = 0; i < simulationBodies.Count; i++)
                {
                    CelestialBody body = simulationBodies[i];
                    if (body == null || !body.UsesAnalyticMotion || analyticOrder.Contains(body))
                    {
                        continue;
                    }

                    CelestialBody attractor = body.OrbitAttractor;
                    bool attractorReady = attractor == null ||
                        !attractor.UsesAnalyticMotion ||
                        analyticOrder.Contains(attractor);
                    if (attractorReady)
                    {
                        analyticOrder.Add(body);
                    }
                }
            }

            for (int i = 0; i < simulationBodies.Count; i++)
            {
                CelestialBody body = simulationBodies[i];
                if (body == null || !body.UsesAnalyticMotion)
                {
                    continue;
                }

                if (!analyticOrder.Contains(body))
                {
                    Debug.LogWarning(
                        $"Celestial body '{body.BodyName}' could not join the analytic order (attractor cycle or unresolved attractor); it will stay frozen at its epoch pose.",
                        body);
                }
                else if (body.OrbitAttractor != null && !body.OrbitAttractor.UsesAnalyticMotion)
                {
                    Debug.LogWarning(
                        $"Celestial body '{body.BodyName}' orbits dynamic body '{body.OrbitAttractor.BodyName}'; dynamic attractors carry no system position and are unsupported for analytic children.",
                        body);
                }
            }

            for (int i = 0; i < analyticOrder.Count; i++)
            {
                CelestialBody body = analyticOrder[i];
                if (body.HasAnalyticReference && simulationTime > 0d)
                {
                    continue;
                }

                CelestialBody attractor = body.OrbitAttractor;
                body.CaptureAnalyticReference(
                    attractor != null ? attractor.SystemPosition : body.Position,
                    attractor != null ? attractor.SystemVelocity : Vector3.zero,
                    GravitationalConstant,
                    simulationTime);
            }
        }

        void ApplyAnalyticMotion()
        {
            if (analyticOrder.Count == 0)
            {
                return;
            }

            for (int i = 0; i < analyticOrder.Count; i++)
            {
                CelestialBody body = analyticOrder[i];
                if (body == null)
                {
                    continue;
                }

                CelestialBody attractor = body.OrbitAttractor;
                body.EvaluateAnalyticMotion(
                    simulationTime,
                    attractor != null ? attractor.SystemPosition : body.AnalyticEpochOrigin,
                    attractor != null ? attractor.SystemVelocity : Vector3.zero);
            }

            CelestialBody anchor = ResolveAnalyticAnchor();
            if (anchor == null)
            {
                return;
            }

            InitializeReferenceRotation();
            CelestialBody referenceBody = ResolvedPhysicsReferenceBody;
            if (referenceBody != null)
            {
                Quaternion inertialReferenceRotation =
                    referenceBody.EvaluateAnalyticRotation(simulationTime);
                referenceFrameRotation =
                    referenceBodyWorldRotation * Quaternion.Inverse(inertialReferenceRotation);
            }
            else
            {
                referenceFrameRotation = Quaternion.identity;
                referenceFrameAngularVelocity = Vector3.zero;
            }

            Vector3 anchorWorld = anchor.Rigidbody != null
                ? anchor.Rigidbody.position
                : anchor.transform.position;
            Vector3 anchorSystem = anchor.SystemPosition;
            for (int i = 0; i < analyticOrder.Count; i++)
            {
                CelestialBody body = analyticOrder[i];
                if (body == null)
                {
                    continue;
                }

                Vector3 worldPosition = body == anchor
                    ? anchorWorld
                    : anchorWorld + referenceFrameRotation *
                        (body.SystemPosition - anchorSystem);
                Quaternion worldRotation = referenceFrameRotation *
                    body.EvaluateAnalyticRotation(simulationTime);
                body.ApplyAnalyticPose(worldPosition, worldRotation);
            }

            UpdateReferenceFrameAcceleration();
            UpdateReferenceFrameVelocity();
        }

        CelestialBody ResolveAnalyticAnchor()
        {
            CelestialBody referenceBody = ResolvedPhysicsReferenceBody;
            if (referenceBody != null && referenceBody.UsesAnalyticMotion)
            {
                return referenceBody;
            }

            return analyticOrder.Count > 0 ? analyticOrder[0] : null;
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
            return CalculateReferenceFrameAcceleration(
                point,
                Vector3.zero,
                ignoredBody);
        }

        public Vector3 CalculateReferenceFrameAcceleration(
            Vector3 point,
            Vector3 frameRelativeVelocity,
            CelestialBody ignoredBody = null)
        {
            Vector3 acceleration = CalculateAcceleration(point, ignoredBody) -
                referenceFrameAcceleration;
            if (referenceFrameAngularVelocity.sqrMagnitude <= 0.00000001f)
            {
                return acceleration;
            }

            Vector3 relativePosition = point - referenceFrameOrigin;
            acceleration -= 2f * Vector3.Cross(
                referenceFrameAngularVelocity,
                frameRelativeVelocity);
            acceleration -= Vector3.Cross(
                referenceFrameAngularVelocity,
                Vector3.Cross(referenceFrameAngularVelocity, relativePosition));
            return acceleration;
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
            return FindDominantBody(point, ignoredBody, null, DominanceHysteresisBias);
        }

        public GravitySample FindDominantBody(
            Vector3 point,
            CelestialBody ignoredBody,
            CelestialBody incumbent,
            float incumbentBias)
        {
            CelestialBody dominantBody = null;
            float dominantSoiRadius = float.PositiveInfinity;
            float dominantAccelerationSqr = 0f;

            foreach (CelestialBody body in simulationBodies)
            {
                if (body == null || body == ignoredBody || !body.ParticipatesInNBody)
                {
                    continue;
                }

                float soiRadius = CalculateSphereOfInfluenceRadius(body);
                if (!float.IsPositiveInfinity(soiRadius))
                {
                    float effectiveSoi = body == incumbent
                        ? soiRadius * Mathf.Max(1f, incumbentBias)
                        : soiRadius;
                    if ((point - body.Position).sqrMagnitude > effectiveSoi * effectiveSoi)
                    {
                        continue;
                    }
                }

                bool wins;
                if (dominantBody == null || soiRadius < dominantSoiRadius)
                {
                    wins = true;
                }
                else if (float.IsPositiveInfinity(soiRadius) && float.IsPositiveInfinity(dominantSoiRadius))
                {
                    wins = CalculateAccelerationFromBody(point, body).sqrMagnitude > dominantAccelerationSqr;
                }
                else
                {
                    wins = false;
                }

                if (!wins)
                {
                    continue;
                }

                dominantBody = body;
                dominantSoiRadius = soiRadius;
                dominantAccelerationSqr = CalculateAccelerationFromBody(point, body).sqrMagnitude;
            }

            if (dominantBody == null)
            {
                return GravitySample.Empty;
            }

            CelestialSurfaceSample dominantSurface = dominantBody.SampleSurface(point);
            return new GravitySample(
                dominantBody,
                CalculateAccelerationFromBody(point, dominantBody),
                dominantSurface.CenterDistance,
                dominantSurface.SurfaceDistance,
                dominantSurface.Normal);
        }

        public float CalculateSphereOfInfluenceRadius(CelestialBody body)
        {
            CelestialBody attractor = body != null ? body.OrbitAttractor : null;
            if (attractor == null ||
                !attractor.ParticipatesInNBody ||
                attractor.Mass <= 0f ||
                body.Mass <= 0f)
            {
                return float.PositiveInfinity;
            }

            float distance = Vector3.Distance(body.Position, attractor.Position);
            return distance * Mathf.Pow(body.Mass / attractor.Mass, 0.4f);
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

            if (autoDiscoverBodies)
            {
                RefreshBodies();
            }

            for (int i = 0; i < simulationBodies.Count; i++)
            {
                CelestialBody body = simulationBodies[i];
                if (body != null)
                {
                    results.Add(body.CaptureSnapshot());
                }
            }
        }

        public int ComputeLayoutHash()
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < simulationBodies.Count; i++)
                {
                    CelestialBody body = simulationBodies[i];
                    if (body == null)
                    {
                        continue;
                    }

                    hash = hash * 31 + StableHash(body.BodyName);
                    hash = hash * 31 + StableHash(body.OrbitAttractor != null ? body.OrbitAttractor.BodyName : string.Empty);
                    hash = hash * 31 + body.Radius.GetHashCode();
                    hash = hash * 31 + body.SurfaceGravity.GetHashCode();
                    hash = hash * 31 + body.InitialVelocity.GetHashCode();
                    hash = hash * 31 + body.InitialAngularVelocityDegreesPerSecond.GetHashCode();
                    hash = hash * 31 + body.EpochLocalOffset.GetHashCode();
                }

                return hash;
            }
        }

        static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                if (string.IsNullOrEmpty(value))
                {
                    return hash;
                }

                for (int i = 0; i < value.Length; i++)
                {
                    hash = hash * 31 + value[i];
                }

                return hash;
            }
        }

        public bool ApplySnapshots(IReadOnlyList<CelestialBodySnapshot> snapshots)
        {
            if (!CanApplySnapshots(snapshots))
            {
                return false;
            }

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

            if (autoDiscoverBodies)
            {
                RefreshBodies();
            }

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

            InitializeReferenceRotation();
            UpdateReferenceFrameAcceleration();
            UpdateReferenceFrameVelocity();
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
                if (body == null || !body.ParticipatesInNBody || body.UsesAnalyticMotion)
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
                if (body == null || body.UsesAnalyticMotion)
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
                ? referenceFrameRotation * referenceBody.InertialVelocity
                : Vector3.zero;
            referenceFrameAngularVelocity = referenceBody != null
                ? referenceFrameRotation * referenceBody.InertialAngularVelocity
                : Vector3.zero;
            referenceFrameOrigin = referenceBody != null
                ? referenceBody.Position
                : Vector3.zero;
            ApplyReferenceFrameMotionToBodies();
        }

        void ApplyReferenceFrameMotionToBodies()
        {
            for (int i = 0; i < simulationBodies.Count; i++)
            {
                CelestialBody body = simulationBodies[i];
                if (body != null)
                {
                    Vector3 relativePosition = body.Position - referenceFrameOrigin;
                    Vector3 linearVelocity =
                        referenceFrameRotation * body.InertialVelocity -
                        referenceFrameVelocity -
                        Vector3.Cross(referenceFrameAngularVelocity, relativePosition);
                    Vector3 angularVelocity =
                        referenceFrameRotation * body.InertialAngularVelocity -
                        referenceFrameAngularVelocity;
                    body.SetPhysicsFrameMotion(linearVelocity, angularVelocity);
                }
            }
        }

        void GatherPhysicsReferenceObservers()
        {
            referenceObservers.Clear();
            if (physicsReferenceObserverSource == null)
            {
                physicsReferenceObserver = null;
                physicsReferenceObserverGroup = null;
                return;
            }

            physicsReferenceObserverGroup ??=
                physicsReferenceObserverSource as ICelestialSurfaceCollisionObserverGroup;
            if (physicsReferenceObserverGroup != null)
            {
                physicsReferenceObserverGroup.GetSurfaceCollisionObservers(referenceObservers);
                return;
            }

            physicsReferenceObserver ??=
                physicsReferenceObserverSource as ICelestialSurfaceCollisionObserver;
            if (physicsReferenceObserver != null &&
                physicsReferenceObserver.TryGetSurfaceCollisionObserver(
                    out CelestialSurfaceCollisionObserverState observer) &&
                observer.IsValid)
            {
                referenceObservers.Add(observer);
            }
        }

        void ShiftDynamicBodyVelocities(
            Vector3 previousLinearVelocity,
            Vector3 previousAngularVelocity,
            Vector3 previousOrigin,
            Vector3 nextLinearVelocity,
            Vector3 nextAngularVelocity,
            Vector3 nextOrigin)
        {
            if (!gameObject.scene.IsValid())
            {
                return;
            }

            gameObject.scene.GetRootGameObjects(frameShiftRoots);
            for (int rootIndex = 0; rootIndex < frameShiftRoots.Count; rootIndex++)
            {
                frameShiftRoots[rootIndex]
                    .GetComponentsInChildren(true, frameShiftBodies);
                for (int i = 0; i < frameShiftBodies.Count; i++)
                {
                    Rigidbody body = frameShiftBodies[i];
                    if (body != null && !body.isKinematic)
                    {
                        Vector3 previousPointVelocity = previousLinearVelocity +
                            Vector3.Cross(
                                previousAngularVelocity,
                                body.position - previousOrigin);
                        Vector3 nextPointVelocity = nextLinearVelocity +
                            Vector3.Cross(
                                nextAngularVelocity,
                                body.position - nextOrigin);
                        body.linearVelocity -= nextPointVelocity - previousPointVelocity;
                    }
                }
            }

            frameShiftRoots.Clear();
            frameShiftBodies.Clear();
        }

        void InitializeReferenceRotation()
        {
            CelestialBody referenceBody = ResolvedPhysicsReferenceBody;
            if (referenceBody == null)
            {
                referenceRotationInitialized = false;
                referenceFrameRotation = Quaternion.identity;
                referenceBodyWorldRotation = Quaternion.identity;
                referenceFrameOrigin = Vector3.zero;
                return;
            }

            if (referenceRotationInitialized)
            {
                return;
            }

            Quaternion inertialRotation =
                referenceBody.EvaluateAnalyticRotation(simulationTime);
            Quaternion currentWorldRotation = referenceBody.Rigidbody != null
                ? referenceBody.Rigidbody.rotation
                : referenceBody.transform.rotation;
            referenceFrameRotation =
                currentWorldRotation * Quaternion.Inverse(inertialRotation);
            referenceBodyWorldRotation = currentWorldRotation;
            referenceFrameOrigin = referenceBody.Position;
            referenceRotationInitialized = true;
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
