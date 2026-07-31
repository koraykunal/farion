using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Farion.Gameplay.Flight
{
    [DefaultExecutionOrder(335)]
    [DisallowMultipleComponent]
    public sealed class SpacecraftReentryVfxController : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] SpacecraftAtmosphereInteractor atmosphereInteractor;
        [SerializeField] Material material;

        [Header("Response")]
        [Min(0f)]
        [SerializeField] float response = 6f;
        [Range(0f, 1f)]
        [SerializeField] float activationHeatLoad = 0.04f;
        [Range(0f, 1f)]
        [SerializeField] float activationDynamicPressureLoad = 0.08f;
        [Range(0f, 1f)]
        [SerializeField] float dynamicPressureContribution = 0.45f;

        [Header("Hull Envelope")]
        [SerializeField] Vector3 hullCenter;
        [SerializeField] Vector3 hullHalfExtents = new(6.8f, 3.2f, 10.5f);
        [FormerlySerializedAs("noseOffset")]
        [Min(0f)]
        [SerializeField] float shockStandoff = 0.75f;

        [Header("Bow Shape")]
        [Min(0.01f)]
        [SerializeField] float plasmaRadius = 7f;
        [Min(0.01f)]
        [SerializeField] float plasmaVerticalScale = 0.65f;
        [FormerlySerializedAs("plasmaLength")]
        [Min(0.01f)]
        [SerializeField] float plasmaTailLength = 2.5f;
        [Range(0f, 1f)]
        [SerializeField] float plasmaOpacity = 0.85f;
        [Min(0.01f)]
        [SerializeField] float shockRadius = 8.5f;
        [Min(0.01f)]
        [SerializeField] float shockVerticalScale = 0.72f;
        [FormerlySerializedAs("shockLength")]
        [Min(0.01f)]
        [SerializeField] float shockTailLength = 8f;
        [Range(0f, 1f)]
        [SerializeField] float shockOpacity = 0.3f;

        [Header("Runtime Reentry")]
        [SerializeField, Range(0f, 1f)] float currentIntensity;
        [SerializeField] Vector3 currentFlowDirection;

        MaterialPropertyBlock plasmaProperties;
        MaterialPropertyBlock shockProperties;
        float layerSeed;

        static Mesh bowShockMesh;

        public float CurrentIntensity => currentIntensity;
        public bool HasValidAuthoring => atmosphereInteractor != null && material != null;

        void Awake()
        {
            AutoAssignReferences();
            EnsureRuntimeState();
        }

        void OnValidate()
        {
            response = Mathf.Max(0f, response);
            activationHeatLoad = Mathf.Clamp01(activationHeatLoad);
            activationDynamicPressureLoad = Mathf.Clamp01(activationDynamicPressureLoad);
            dynamicPressureContribution = Mathf.Clamp01(dynamicPressureContribution);
            hullHalfExtents = new Vector3(
                Mathf.Max(0.01f, hullHalfExtents.x),
                Mathf.Max(0.01f, hullHalfExtents.y),
                Mathf.Max(0.01f, hullHalfExtents.z));
            shockStandoff = Mathf.Max(0f, shockStandoff);
            plasmaRadius = Mathf.Max(0.01f, plasmaRadius);
            plasmaVerticalScale = Mathf.Max(0.01f, plasmaVerticalScale);
            plasmaTailLength = Mathf.Max(0.01f, plasmaTailLength);
            plasmaOpacity = Mathf.Clamp01(plasmaOpacity);
            shockRadius = Mathf.Max(0.01f, shockRadius);
            shockVerticalScale = Mathf.Max(0.01f, shockVerticalScale);
            shockTailLength = Mathf.Max(0.01f, shockTailLength);
            shockOpacity = Mathf.Clamp01(shockOpacity);
            AutoAssignReferences();
        }

        void LateUpdate()
        {
            SpacecraftAtmosphereInteractionSample interaction =
                atmosphereInteractor != null
                    ? atmosphereInteractor.CurrentInteraction
                    : default;
            Vector3 relativeVelocity = interaction.Frame.SurfaceRelativeVelocity;
            float targetIntensity = material != null
                ? CalculateTargetIntensity(
                    interaction.HeatLoad,
                    interaction.DynamicPressureLoad,
                    activationHeatLoad,
                    activationDynamicPressureLoad,
                    dynamicPressureContribution)
                : 0f;
            currentIntensity = Smooth(
                currentIntensity,
                targetIntensity,
                response,
                Time.deltaTime);

            if (currentIntensity <= 0.001f || relativeVelocity.sqrMagnitude <= 1f)
            {
                currentFlowDirection = Vector3.zero;
                return;
            }

            EnsureRuntimeState();
            DrawReentryLayers(relativeVelocity.normalized);
        }

        void OnDisable()
        {
            currentIntensity = 0f;
            currentFlowDirection = Vector3.zero;
        }

        void AutoAssignReferences()
        {
            atmosphereInteractor ??= GetComponent<SpacecraftAtmosphereInteractor>();
        }

        void EnsureRuntimeState()
        {
            plasmaProperties ??= new MaterialPropertyBlock();
            shockProperties ??= new MaterialPropertyBlock();
            if (layerSeed <= 0f)
            {
                uint hash = unchecked((uint)Animator.StringToHash(name));
                layerSeed = (hash % 997u + 1u) / 998f;
            }
        }

        void DrawReentryLayers(Vector3 travelDirection)
        {
            currentFlowDirection = -travelDirection;
            Vector3 up = Mathf.Abs(Vector3.Dot(transform.up, currentFlowDirection)) < 0.98f
                ? transform.up
                : transform.right;
            Quaternion rotation = Quaternion.LookRotation(currentFlowDirection, up);
            Vector3 localTravelDirection = transform.InverseTransformDirection(travelDirection);
            float leadingDistance = CalculateLeadingDistance(
                localTravelDirection,
                hullHalfExtents,
                shockStandoff);
            Vector3 apex = transform.TransformPoint(hullCenter) +
                travelDirection * leadingDistance;
            Mesh mesh = GetBowShockMesh();

            DrawLayer(
                mesh,
                plasmaProperties,
                apex,
                rotation,
                new Vector3(
                    plasmaRadius,
                    plasmaRadius * plasmaVerticalScale,
                    leadingDistance + plasmaTailLength),
                layerMode: 0f,
                plasmaOpacity);
            DrawLayer(
                mesh,
                shockProperties,
                apex,
                rotation,
                new Vector3(
                    shockRadius,
                    shockRadius * shockVerticalScale,
                    leadingDistance + shockTailLength),
                layerMode: 1f,
                shockOpacity);
        }

        void DrawLayer(
            Mesh mesh,
            MaterialPropertyBlock properties,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            float layerMode,
            float opacity)
        {
            properties.Clear();
            properties.SetFloat(ShaderIds.EffectStrength, currentIntensity);
            properties.SetFloat(ShaderIds.LayerMode, layerMode);
            properties.SetFloat(ShaderIds.Opacity, opacity);
            properties.SetFloat(ShaderIds.LayerSeed, layerSeed);

            Graphics.DrawMesh(
                mesh,
                Matrix4x4.TRS(position, rotation, scale),
                material,
                gameObject.layer,
                null,
                0,
                properties,
                ShadowCastingMode.Off,
                receiveShadows: false,
                probeAnchor: null,
                lightProbeUsage: LightProbeUsage.Off,
                lightProbeProxyVolume: null);
        }

        internal static float CalculateTargetIntensity(
            float heatLoad,
            float dynamicPressureLoad,
            float heatThreshold,
            float dynamicPressureThreshold,
            float pressureContribution)
        {
            float heat = Mathf.InverseLerp(
                Mathf.Clamp01(heatThreshold),
                1f,
                Mathf.Clamp01(heatLoad));
            float pressure = Mathf.InverseLerp(
                Mathf.Clamp01(dynamicPressureThreshold),
                1f,
                Mathf.Clamp01(dynamicPressureLoad));
            return Mathf.Clamp01(Mathf.Max(
                heat,
                pressure * Mathf.Clamp01(pressureContribution)));
        }

        internal static float CalculateLeadingDistance(
            Vector3 localTravelDirection,
            Vector3 hullHalfExtents,
            float standoff)
        {
            Vector3 direction = localTravelDirection.sqrMagnitude > 0.0001f
                ? localTravelDirection.normalized
                : Vector3.forward;
            Vector3 extents = new(
                Mathf.Abs(hullHalfExtents.x),
                Mathf.Abs(hullHalfExtents.y),
                Mathf.Abs(hullHalfExtents.z));
            float hullSupport =
                Mathf.Abs(direction.x) * extents.x +
                Mathf.Abs(direction.y) * extents.y +
                Mathf.Abs(direction.z) * extents.z;
            return hullSupport + Mathf.Max(0f, standoff);
        }

        static float Smooth(float current, float target, float responseRate, float deltaTime)
        {
            float responseT = responseRate <= 0f
                ? 1f
                : 1f - Mathf.Exp(-responseRate * Mathf.Max(0f, deltaTime));
            return Mathf.Lerp(current, target, responseT);
        }

        static Mesh GetBowShockMesh()
        {
            if (bowShockMesh != null)
            {
                return bowShockMesh;
            }

            const int segments = 48;
            const int rings = 14;
            int stride = segments + 1;
            Vector3[] vertices = new Vector3[(rings + 1) * stride];
            Vector3[] normals = new Vector3[vertices.Length];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[rings * segments * 6];

            for (int ring = 0; ring <= rings; ring++)
            {
                float t = ring / (float)rings;
                float radius = Mathf.Lerp(0.035f, 1f, Mathf.Sqrt(t));
                float radiusSlope = 0.5f / Mathf.Sqrt(Mathf.Max(t, 0.001f));
                for (int segment = 0; segment <= segments; segment++)
                {
                    float u = segment / (float)segments;
                    float angle = u * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle);
                    float y = Mathf.Sin(angle);
                    int vertex = ring * stride + segment;
                    vertices[vertex] = new Vector3(x * radius, y * radius, t);
                    normals[vertex] = new Vector3(x, y, -radiusSlope).normalized;
                    uv[vertex] = new Vector2(u, t);

                    if (ring == rings || segment == segments)
                    {
                        continue;
                    }

                    int triangle = (ring * segments + segment) * 6;
                    int nextRing = vertex + stride;
                    triangles[triangle] = vertex;
                    triangles[triangle + 1] = vertex + 1;
                    triangles[triangle + 2] = nextRing;
                    triangles[triangle + 3] = vertex + 1;
                    triangles[triangle + 4] = nextRing + 1;
                    triangles[triangle + 5] = nextRing;
                }
            }

            bowShockMesh = new Mesh
            {
                name = "Farion Spacecraft Bow Shock",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = vertices,
                normals = normals,
                uv = uv,
                triangles = triangles,
                bounds = new Bounds(
                    new Vector3(0f, 0f, 0.5f),
                    new Vector3(2.2f, 2.2f, 1.1f))
            };
            bowShockMesh.UploadMeshData(markNoLongerReadable: true);
            return bowShockMesh;
        }

        static class ShaderIds
        {
            public static readonly int EffectStrength = Shader.PropertyToID("_EffectStrength");
            public static readonly int LayerMode = Shader.PropertyToID("_LayerMode");
            public static readonly int Opacity = Shader.PropertyToID("_Opacity");
            public static readonly int LayerSeed = Shader.PropertyToID("_LayerSeed");
        }
    }
}
