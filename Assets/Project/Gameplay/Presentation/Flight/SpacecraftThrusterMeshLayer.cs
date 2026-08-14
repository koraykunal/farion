using Farion.Gameplay.Flight;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting.APIUpdating;

namespace Farion.Gameplay.Presentation.Flight
{
    public enum SpacecraftThrusterMeshLayerKind
    {
        CoreGlow = 0,
        InnerPlasma = 1,
        OuterPlasma = 2,
        ShockDiamonds = 3,
        Distortion = 4
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [MovedFrom(true, "Farion.Gameplay.Flight", "Farion.Gameplay.Runtime")]
    public sealed class SpacecraftThrusterMeshLayer : MonoBehaviour
    {
        [SerializeField] SpacecraftThrusterMeshLayerKind layerKind;
        [SerializeField] Material material;

        [Header("Shape")]
        [Min(0.001f)]
        [SerializeField] float radius = 0.5f;
        [Min(0.001f)]
        [SerializeField] float minimumLength = 0.05f;
        [Min(0.001f)]
        [SerializeField] float maximumLength = 4f;
        [Range(0f, 1f)]
        [SerializeField] float idleVisibility;
        [Range(0f, 1f)]
        [SerializeField] float opacity = 1f;
        [Min(0f)]
        [SerializeField] float response = 16f;

        [Header("Nozzle Expansion")]
        [Min(1f)]
        [SerializeField] float vacuumLengthScale = 1.22f;
        [Min(1f)]
        [SerializeField] float vacuumRadiusScale = 1.28f;
        [Range(0f, 2f)]
        [SerializeField] float vacuumBellExpansion = 0.55f;
        [Min(1f)]
        [SerializeField] float speedReference = 320f;
        [Range(0f, 1f)]
        [SerializeField] float speedStretch = 0.16f;

        [Header("Editor Preview")]
        [Range(0f, 1f)]
        [SerializeField] float previewLoad = 0.65f;
        [Range(0f, 1f)]
        [SerializeField] float previewBoost = 0.15f;

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        MaterialPropertyBlock properties;
        float currentVisibility;
        float runtimeSeed = -1f;

        static Mesh discMesh;
        static Mesh coneMesh;

        public SpacecraftThrusterMeshLayerKind LayerKind => layerKind;
        public Material Material => material;
        public bool HasValidAuthoring =>
            material != null &&
            GetComponent<MeshFilter>() != null &&
            GetComponent<MeshRenderer>() != null;

        void Reset()
        {
            ResolveComponents();
            ConfigureDefaultsFromName();
            ApplyMesh();
            ApplyPreview();
        }

        void OnEnable()
        {
            ResolveComponents();
            ApplyMesh();
            if (!Application.isPlaying)
            {
                ApplyPreview();
            }
        }

        void OnValidate()
        {
            radius = Mathf.Max(0.001f, radius);
            minimumLength = Mathf.Max(0.001f, minimumLength);
            maximumLength = Mathf.Max(minimumLength, maximumLength);
            idleVisibility = Mathf.Clamp01(idleVisibility);
            opacity = Mathf.Clamp01(opacity);
            response = Mathf.Max(0f, response);
            vacuumLengthScale = Mathf.Max(1f, vacuumLengthScale);
            vacuumRadiusScale = Mathf.Max(1f, vacuumRadiusScale);
            vacuumBellExpansion = Mathf.Clamp(vacuumBellExpansion, 0f, 2f);
            speedReference = Mathf.Max(1f, speedReference);
            speedStretch = Mathf.Clamp01(speedStretch);
            previewLoad = Mathf.Clamp01(previewLoad);
            previewBoost = Mathf.Clamp01(previewBoost);

            ResolveComponents();
            ApplyMesh();
            if (!Application.isPlaying)
            {
                ApplyPreview();
            }
        }

        public void Configure(
            SpacecraftThrusterMeshLayerKind kind,
            Material authoredMaterial,
            float authoredRadius,
            float authoredMinimumLength,
            float authoredMaximumLength,
            float authoredIdleVisibility,
            float authoredOpacity,
            float authoredResponse)
        {
            layerKind = kind;
            material = authoredMaterial;
            radius = Mathf.Max(0.001f, authoredRadius);
            minimumLength = Mathf.Max(0.001f, authoredMinimumLength);
            maximumLength = Mathf.Max(minimumLength, authoredMaximumLength);
            idleVisibility = Mathf.Clamp01(authoredIdleVisibility);
            opacity = Mathf.Clamp01(authoredOpacity);
            response = Mathf.Max(0f, authoredResponse);

            ResolveComponents();
            ApplyMesh();
            ApplyPreview();
        }

        public void ApplyFrame(
            SpacecraftThrusterVfxFrame frame,
            float nozzleLoad,
            float ignitionFlare,
            float deltaTime)
        {
            ResolveComponents();

            float load = Mathf.Clamp01(nozzleLoad);
            float targetVisibility = Mathf.Max(idleVisibility, load);
            if (layerKind == SpacecraftThrusterMeshLayerKind.Distortion)
            {
                targetVisibility *= Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(frame.Heat + frame.Boost));
            }

            currentVisibility = Smooth(
                currentVisibility,
                targetVisibility,
                response,
                Mathf.Max(0f, deltaTime));

            ApplyPresentation(
                currentVisibility,
                frame.Boost,
                frame.Heat,
                frame.AtmosphereDensity,
                Mathf.Clamp01(frame.RelativeSpeed / speedReference),
                Mathf.Clamp01(ignitionFlare));
        }

        public void ClearRuntimeState()
        {
            currentVisibility = 0f;
            if (!Application.isPlaying)
            {
                ApplyPreview();
            }
        }

        void ResolveComponents()
        {
            meshFilter ??= GetComponent<MeshFilter>();
            meshRenderer ??= GetComponent<MeshRenderer>();
            properties ??= new MaterialPropertyBlock();
        }

        void ApplyMesh()
        {
            if (meshFilter == null || meshRenderer == null)
            {
                return;
            }

            meshFilter.sharedMesh = layerKind == SpacecraftThrusterMeshLayerKind.CoreGlow
                ? GetDiscMesh()
                : GetConeMesh();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        void ApplyPreview()
        {
            currentVisibility = Mathf.Max(idleVisibility, previewLoad);
            ApplyPresentation(
                currentVisibility,
                previewBoost,
                heat: 0.15f,
                atmosphereDensity: 0.6f,
                speedBlend: 0f,
                ignitionFlare: 0f);
        }

        void ApplyPresentation(
            float visibility,
            float boost,
            float heat,
            float atmosphereDensity,
            float speedBlend,
            float ignitionFlare)
        {
            if (meshRenderer == null)
            {
                return;
            }

            bool visible = material != null && visibility > 0.001f;
            meshRenderer.enabled = visible;
            if (!visible)
            {
                return;
            }

            float ambientPressure = Mathf.Clamp01(atmosphereDensity);
            float vacuumBlend = 1f - ambientPressure;
            float shapedLoad = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(visibility));
            float boostStretch = Mathf.Lerp(1f, 1.28f, Mathf.Clamp01(boost));
            float pressureStretch = Mathf.Lerp(1f, vacuumLengthScale, vacuumBlend);
            float speedTrail = 1f + Mathf.Clamp01(speedBlend) * speedStretch;
            float flareStretch = 1f + Mathf.Clamp01(ignitionFlare) * 0.35f;
            float length = Mathf.Lerp(minimumLength, maximumLength, shapedLoad) *
                boostStretch *
                pressureStretch *
                speedTrail *
                flareStretch;

            float layerSeed = ResolveRuntimeSeed();
            float radialPulse = Application.isPlaying
                ? 1f + Mathf.Sin((Time.realtimeSinceStartup + layerSeed * 13f) * 17f) * 0.018f
                : 1f;
            bool isCoreGlow = layerKind == SpacecraftThrusterMeshLayerKind.CoreGlow;
            float pressureRadius = isCoreGlow
                ? 1f
                : Mathf.Lerp(1f, vacuumRadiusScale, vacuumBlend);
            float layerRadius = radius *
                Mathf.Lerp(0.82f, 1.08f, shapedLoad) *
                radialPulse *
                pressureRadius;

            transform.localScale = isCoreGlow
                ? new Vector3(layerRadius, layerRadius, Mathf.Max(0.01f, length))
                : new Vector3(layerRadius, layerRadius, length);

            meshRenderer.GetPropertyBlock(properties);
            properties.SetFloat(ShaderIds.Throttle, shapedLoad);
            properties.SetFloat(ShaderIds.Boost, Mathf.Clamp01(boost));
            properties.SetFloat(ShaderIds.Heat, Mathf.Clamp01(heat));
            properties.SetFloat(ShaderIds.Opacity, opacity * shapedLoad);
            properties.SetFloat(ShaderIds.LayerSeed, layerSeed);
            properties.SetFloat(ShaderIds.AtmosphereDensity, ambientPressure);
            properties.SetFloat(ShaderIds.SpeedBlend, Mathf.Clamp01(speedBlend));
            properties.SetFloat(ShaderIds.Flare, Mathf.Clamp01(ignitionFlare));
            properties.SetFloat(
                ShaderIds.BellExpansion,
                isCoreGlow ? 0f : vacuumBellExpansion * vacuumBlend * shapedLoad);
            meshRenderer.SetPropertyBlock(properties);
        }

        float ResolveRuntimeSeed()
        {
            if (runtimeSeed >= 0f)
            {
                return runtimeSeed;
            }

            string parentName = transform.parent != null
                ? transform.parent.name
                : string.Empty;
            uint hash = unchecked((uint)Animator.StringToHash($"{parentName}/{name}"));
            runtimeSeed = hash % 997u / 997f;
            return runtimeSeed;
        }

        void ConfigureDefaultsFromName()
        {
            switch (name)
            {
                case "CoreGlow":
                    layerKind = SpacecraftThrusterMeshLayerKind.CoreGlow;
                    radius = 0.58f;
                    minimumLength = 0.025f;
                    maximumLength = 0.08f;
                    idleVisibility = 0.08f;
                    opacity = 1f;
                    response = 22f;
                    break;
                case "InnerCone":
                    layerKind = SpacecraftThrusterMeshLayerKind.InnerPlasma;
                    radius = 0.42f;
                    minimumLength = 0.35f;
                    maximumLength = 4.8f;
                    opacity = 0.92f;
                    response = 18f;
                    break;
                case "OuterPlasma":
                    layerKind = SpacecraftThrusterMeshLayerKind.OuterPlasma;
                    radius = 0.68f;
                    minimumLength = 0.5f;
                    maximumLength = 6.2f;
                    opacity = 0.42f;
                    response = 13f;
                    break;
                case "ShockDiamonds":
                    layerKind = SpacecraftThrusterMeshLayerKind.ShockDiamonds;
                    radius = 0.5f;
                    minimumLength = 0.8f;
                    maximumLength = 5.6f;
                    opacity = 0.48f;
                    response = 15f;
                    break;
                case "Distortion":
                    layerKind = SpacecraftThrusterMeshLayerKind.Distortion;
                    radius = 0.88f;
                    minimumLength = 0.9f;
                    maximumLength = 6.8f;
                    opacity = 0.34f;
                    response = 10f;
                    break;
            }
        }

        static float Smooth(float current, float target, float responseRate, float deltaTime)
        {
            float responseT = responseRate <= 0f
                ? 1f
                : 1f - Mathf.Exp(-responseRate * deltaTime);
            return Mathf.Lerp(current, target, responseT);
        }

        static Mesh GetDiscMesh()
        {
            if (discMesh != null)
            {
                return discMesh;
            }

            const int segments = 48;
            Vector3[] vertices = new Vector3[segments + 1];
            Vector3[] normals = new Vector3[segments + 1];
            Vector2[] uv = new Vector2[segments + 1];
            int[] triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            normals[0] = Vector3.forward;
            uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float y = Mathf.Sin(angle);
                vertices[i + 1] = new Vector3(x, y, 0f);
                normals[i + 1] = Vector3.forward;
                uv[i + 1] = new Vector2(x * 0.5f + 0.5f, y * 0.5f + 0.5f);

                int triangle = i * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = i + 1;
                triangles[triangle + 2] = (i + 1) % segments + 1;
            }

            discMesh = new Mesh
            {
                name = "Farion Thruster Core Disc",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = vertices,
                normals = normals,
                uv = uv,
                triangles = triangles,
                bounds = new Bounds(Vector3.zero, new Vector3(2.2f, 2.2f, 0.2f))
            };
            discMesh.UploadMeshData(markNoLongerReadable: true);
            return discMesh;
        }

        static Mesh GetConeMesh()
        {
            if (coneMesh != null)
            {
                return coneMesh;
            }

            const int segments = 40;
            const float tipRadius = 0.035f;
            Vector3[] vertices = new Vector3[(segments + 1) * 2];
            Vector3[] normals = new Vector3[vertices.Length];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments;
                float angle = u * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float y = Mathf.Sin(angle);
                Vector3 normal = new Vector3(x, y, 1f - tipRadius).normalized;

                int near = i * 2;
                int far = near + 1;
                vertices[near] = new Vector3(x, y, 0f);
                vertices[far] = new Vector3(x * tipRadius, y * tipRadius, 1f);
                normals[near] = normal;
                normals[far] = normal;
                uv[near] = new Vector2(u, 0f);
                uv[far] = new Vector2(u, 1f);

                if (i == segments)
                {
                    continue;
                }

                int triangle = i * 6;
                triangles[triangle] = near;
                triangles[triangle + 1] = near + 2;
                triangles[triangle + 2] = far;
                triangles[triangle + 3] = far;
                triangles[triangle + 4] = near + 2;
                triangles[triangle + 5] = far + 2;
            }

            coneMesh = new Mesh
            {
                name = "Farion Thruster Plasma Cone",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = vertices,
                normals = normals,
                uv = uv,
                triangles = triangles,
                bounds = new Bounds(new Vector3(0f, 0f, 0.5f), new Vector3(6.4f, 6.4f, 1.2f))
            };
            coneMesh.UploadMeshData(markNoLongerReadable: true);
            return coneMesh;
        }

        static class ShaderIds
        {
            public static readonly int Throttle = Shader.PropertyToID("_Throttle");
            public static readonly int Boost = Shader.PropertyToID("_Boost");
            public static readonly int Heat = Shader.PropertyToID("_Heat");
            public static readonly int Opacity = Shader.PropertyToID("_Opacity");
            public static readonly int LayerSeed = Shader.PropertyToID("_LayerSeed");
            public static readonly int AtmosphereDensity = Shader.PropertyToID("_AtmosphereDensity");
            public static readonly int SpeedBlend = Shader.PropertyToID("_SpeedBlend");
            public static readonly int Flare = Shader.PropertyToID("_Flare");
            public static readonly int BellExpansion = Shader.PropertyToID("_BellExpansion");
        }
    }
}
