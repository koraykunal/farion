using System.Collections.Generic;
using Farion.Core.Physics;
using UnityEngine;

namespace Farion.Rendering.Celestial
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CelestialOrbitLineRenderer : MonoBehaviour
    {
        const string ContainerName = "Orbit Lines";

        [Header("Source")]
        [SerializeField] GravitySimulation simulation;

        [Header("Display")]
        [SerializeField] bool showOrbitLines = true;
        [SerializeField] bool updateInEditMode = true;
        [SerializeField] bool updateEveryFrame = true;
        [Range(16, 512)]
        [SerializeField] int sampleCount = 160;
        [Min(0.001f)]
        [SerializeField] float lineWidth = 0.12f;
        [SerializeField] Color lineColor = new(0.45f, 0.78f, 1f, 0.72f);
        [SerializeField] Material lineMaterial;

        [Header("Filtering")]
        [SerializeField] bool drawLockedBodies;
        [SerializeField] bool drawUnboundTrajectories;

        readonly Dictionary<CelestialBody, LineRenderer> linesByBody = new();
        readonly List<CelestialBody> staleBodies = new();
        Transform container;
        Material runtimeMaterial;
        bool ownsContainer;

        public bool ShowOrbitLines
        {
            get => showOrbitLines;
            set
            {
                showOrbitLines = value;
                SetAllLinesVisible(showOrbitLines);
            }
        }

        void OnEnable()
        {
            RefreshOrbitLines();
        }

        void OnDisable()
        {
            SetAllLinesVisible(false);
        }

        void OnDestroy()
        {
            DestroyRuntimeArtifacts();
        }

        void OnValidate()
        {
            sampleCount = Mathf.Clamp(sampleCount, 16, 512);
            lineWidth = Mathf.Max(0.001f, lineWidth);
        }

        void LateUpdate()
        {
            if (!updateEveryFrame)
            {
                return;
            }

            if (!Application.isPlaying && !updateInEditMode)
            {
                return;
            }

            RefreshOrbitLines();
        }

        [ContextMenu("Refresh Orbit Lines")]
        public void RefreshOrbitLines()
        {
            GravitySimulation source = simulation != null ? simulation : GravitySimulation.Active;
            if (source == null)
            {
                SetAllLinesVisible(false);
                return;
            }

            EnsureContainer();
            MarkAllLinesStale();

            IReadOnlyList<CelestialBody> bodies = source.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                CelestialBody body = bodies[i];
                if (!ShouldDrawBody(body))
                {
                    continue;
                }

                CelestialBody attractor = FindPrimaryAttractor(source, body);
                if (attractor == null)
                {
                    continue;
                }

                LineRenderer line = GetOrCreateLine(body);
                if (TryBuildOrbit(body, attractor, source.GravitationalConstant, line))
                {
                    line.enabled = showOrbitLines;
                    staleBodies.Remove(body);
                }
                else
                {
                    line.enabled = false;
                }
            }

            DisableStaleLines();
        }

        bool ShouldDrawBody(CelestialBody body)
        {
            if (body == null)
            {
                return false;
            }

            return drawLockedBodies || body.IntegratesOrbit;
        }

        CelestialBody FindPrimaryAttractor(GravitySimulation source, CelestialBody body)
        {
            CelestialBody attractor = null;
            float strongestAccelerationSqr = 0f;

            IReadOnlyList<CelestialBody> bodies = source.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                CelestialBody candidate = bodies[i];
                if (candidate == null || candidate == body || !candidate.ParticipatesInNBody)
                {
                    continue;
                }

                Vector3 acceleration = source.CalculateAccelerationFromBody(body.Position, candidate);
                float accelerationSqr = acceleration.sqrMagnitude;
                if (accelerationSqr <= strongestAccelerationSqr)
                {
                    continue;
                }

                strongestAccelerationSqr = accelerationSqr;
                attractor = candidate;
            }

            return attractor;
        }

        bool TryBuildOrbit(CelestialBody body, CelestialBody attractor, float gravitationalConstant, LineRenderer line)
        {
            Vector3 relativePosition = body.Position - attractor.Position;
            Vector3 relativeVelocity = body.Velocity - attractor.Velocity;
            float radius = relativePosition.magnitude;
            float mu = gravitationalConstant * Mathf.Max(0.0001f, attractor.Mass + body.Mass);
            if (radius <= 0.001f || mu <= 0f)
            {
                return false;
            }

            Vector3 angularMomentum = Vector3.Cross(relativePosition, relativeVelocity);
            if (angularMomentum.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            float speedSqr = relativeVelocity.sqrMagnitude;
            float specificEnergy = speedSqr * 0.5f - mu / radius;
            if (specificEnergy >= 0f && !drawUnboundTrajectories)
            {
                return false;
            }

            Vector3 eccentricityVector = Vector3.Cross(relativeVelocity, angularMomentum) / mu - relativePosition.normalized;
            float eccentricity = eccentricityVector.magnitude;
            if (eccentricity >= 1f && !drawUnboundTrajectories)
            {
                return false;
            }

            Vector3 planeNormal = angularMomentum.normalized;
            Vector3 periapsisDirection = eccentricityVector.sqrMagnitude > 0.0001f
                ? eccentricityVector.normalized
                : relativePosition.normalized;
            Vector3 semiMinorDirection = Vector3.Cross(planeNormal, periapsisDirection).normalized;
            float semiLatusRectum = angularMomentum.sqrMagnitude / mu;

            line.positionCount = sampleCount;
            ConfigureLine(line);
            line.loop = eccentricity < 1f;

            float angleRange = eccentricity < 1f ? Mathf.PI * 2f : Mathf.PI * 0.92f;
            float angleStart = eccentricity < 1f ? 0f : -angleRange;
            float angleEnd = eccentricity < 1f ? Mathf.PI * 2f : angleRange;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = sampleCount <= 1 ? 0f : i / (float)(sampleCount - 1);
                float theta = Mathf.Lerp(angleStart, angleEnd, t);
                float denominator = 1f + eccentricity * Mathf.Cos(theta);
                float orbitRadius = Mathf.Abs(denominator) > 0.0001f
                    ? semiLatusRectum / denominator
                    : radius;

                orbitRadius = Mathf.Clamp(orbitRadius, 0.001f, radius * 100f);
                Vector3 direction = Mathf.Cos(theta) * periapsisDirection + Mathf.Sin(theta) * semiMinorDirection;
                line.SetPosition(i, attractor.Position + direction * orbitRadius);
            }

            return true;
        }

        LineRenderer GetOrCreateLine(CelestialBody body)
        {
            if (linesByBody.TryGetValue(body, out LineRenderer existing) && existing != null)
            {
                return existing;
            }

            EnsureContainer();
            GameObject lineObject = new($"{body.BodyName} Orbit Line");
            lineObject.transform.SetParent(container, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            lineObject.hideFlags = HideFlags.DontSave;
            linesByBody[body] = line;
            return line;
        }

        void ConfigureLine(LineRenderer line)
        {
            line.useWorldSpace = true;
            line.loop = true;
            line.widthMultiplier = lineWidth;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = ResolveMaterial();
            line.startColor = lineColor;
            line.endColor = lineColor;
            line.loop = true;
        }

        Material ResolveMaterial()
        {
            if (lineMaterial != null)
            {
                return lineMaterial;
            }

            if (runtimeMaterial != null)
            {
                return runtimeMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            runtimeMaterial = new Material(shader)
            {
                name = "Farion Runtime Orbit Line Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            return runtimeMaterial;
        }

        void EnsureContainer()
        {
            if (container != null)
            {
                return;
            }

            Transform existing = transform.Find(ContainerName);
            if (existing != null)
            {
                container = existing;
                ownsContainer = (existing.gameObject.hideFlags & HideFlags.DontSave) != 0;
                return;
            }

            GameObject containerObject = new(ContainerName);
            containerObject.hideFlags = HideFlags.DontSave;
            containerObject.transform.SetParent(transform, false);
            container = containerObject.transform;
            ownsContainer = true;
        }

        void MarkAllLinesStale()
        {
            staleBodies.Clear();
            foreach (CelestialBody body in linesByBody.Keys)
            {
                staleBodies.Add(body);
            }
        }

        void DisableStaleLines()
        {
            for (int i = 0; i < staleBodies.Count; i++)
            {
                CelestialBody body = staleBodies[i];
                if (body != null && linesByBody.TryGetValue(body, out LineRenderer line) && line != null)
                {
                    line.enabled = false;
                }
            }
        }

        void SetAllLinesVisible(bool visible)
        {
            foreach (LineRenderer line in linesByBody.Values)
            {
                if (line != null)
                {
                    line.enabled = visible;
                }
            }
        }

        void DestroyRuntimeArtifacts()
        {
            if (ownsContainer && container != null)
            {
                DestroyUnityObject(container.gameObject);
            }
            else
            {
                foreach (LineRenderer line in linesByBody.Values)
                {
                    if (line != null)
                    {
                        DestroyUnityObject(line.gameObject);
                    }
                }
            }

            linesByBody.Clear();
            staleBodies.Clear();
            container = null;
            ownsContainer = false;

            if (runtimeMaterial != null)
            {
                DestroyUnityObject(runtimeMaterial);
                runtimeMaterial = null;
            }
        }

        static void DestroyUnityObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
