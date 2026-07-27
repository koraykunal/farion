using System;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class SpacecraftThrusterBeam : MonoBehaviour
    {
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");

        [Header("Shape")]
        [Min(3)]
        [SerializeField] int segments = 28;
        [Min(0.01f)]
        [SerializeField] float idleLength = 0.45f;
        [Min(0.01f)]
        [SerializeField] float fullLength = 6.5f;
        [Min(0.001f)]
        [SerializeField] float nozzleRadius = 0.24f;
        [Min(0.001f)]
        [SerializeField] float fullTipRadius = 1.25f;
        [SerializeField] bool extendAlongNegativeZ = true;
        [Range(0f, 1f)]
        [SerializeField] float plumeStart = 0.18f;
        [Range(0f, 0.35f)]
        [SerializeField] float radiusFlutter = 0.08f;
        [Range(0f, 0.35f)]
        [SerializeField] float lengthFlutter = 0.12f;
        [Min(0f)]
        [SerializeField] float flutterSpeed = 12f;

        [Header("Look")]
        [ColorUsage(showAlpha: true, hdr: true)]
        [SerializeField] Color baseColor = new(1f, 0.32f, 0.08f, 0.72f);
        [ColorUsage(showAlpha: true, hdr: true)]
        [SerializeField] Color coreColor = new(1f, 0.92f, 0.58f, 1f);
        [Range(0f, 1f)]
        [SerializeField] float idleAlpha = 0f;
        [Range(0f, 1f)]
        [SerializeField] float fullAlpha = 0.68f;
        [Min(0f)]
        [SerializeField] float fullIntensity = 2.4f;
        [Range(0f, 1f)]
        [SerializeField] float previewIntensity;

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        Mesh beamMesh;
        MaterialPropertyBlock propertyBlock;
        float currentIntensity = -1f;
        float runtimeSeed;
        int cachedSegments = -1;
        float cachedMeshIntensity = -1f;
        Vector3[] vertices = Array.Empty<Vector3>();
        Vector3[] normals = Array.Empty<Vector3>();
        Color[] colors = Array.Empty<Color>();
        int[] triangles = Array.Empty<int>();

        void Awake()
        {
            ResolveComponents();
            runtimeSeed = UnityEngine.Random.value * 1000f;
            InvalidateMesh();
            ApplyIntensity(Application.isPlaying ? 0f : previewIntensity);
        }

        void OnEnable()
        {
            ResolveComponents();
            InvalidateMesh();
            ApplyIntensity(Application.isPlaying ? Mathf.Max(0f, currentIntensity) : previewIntensity);
        }

        void OnValidate()
        {
            segments = Mathf.Max(3, segments);
            idleLength = Mathf.Max(0.01f, idleLength);
            fullLength = Mathf.Max(idleLength, fullLength);
            nozzleRadius = Mathf.Max(0.001f, nozzleRadius);
            fullTipRadius = Mathf.Max(0.001f, fullTipRadius);
            plumeStart = Mathf.Clamp01(plumeStart);
            radiusFlutter = Mathf.Clamp(radiusFlutter, 0f, 0.35f);
            lengthFlutter = Mathf.Clamp(lengthFlutter, 0f, 0.35f);
            flutterSpeed = Mathf.Max(0f, flutterSpeed);
            fullIntensity = Mathf.Max(0f, fullIntensity);
            previewIntensity = Mathf.Clamp01(previewIntensity);
            ResolveComponents();
            InvalidateMesh();
            ApplyIntensity(Application.isPlaying ? Mathf.Max(0f, currentIntensity) : previewIntensity);
        }

        void OnDestroy()
        {
            if (beamMesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(beamMesh);
            }
            else
            {
                DestroyImmediate(beamMesh);
            }
        }

        public void SetIntensity(float intensity)
        {
            ApplyIntensity(Mathf.Clamp01(intensity));
        }

        void ResolveComponents()
        {
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }

            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }

            propertyBlock ??= new MaterialPropertyBlock();
        }

        void InvalidateMesh()
        {
            cachedSegments = -1;
            cachedMeshIntensity = -1f;
        }

        void RebuildMesh(bool force)
        {
            ResolveComponents();
            float meshIntensity = Mathf.Clamp01(currentIntensity < 0f ? previewIntensity : currentIntensity);
            float dynamicPhase = Application.isPlaying
                ? UnityEngine.Time.time * flutterSpeed + runtimeSeed
                : 0f;
            float flutter = Application.isPlaying
                ? Mathf.Sin(dynamicPhase) * 0.5f + Mathf.Sin(dynamicPhase * 1.73f + 1.9f) * 0.5f
                : 0f;
            float plumeIntensity = Mathf.InverseLerp(plumeStart, 1f, meshIntensity);
            float lengthPulse = 1f + flutter * lengthFlutter * plumeIntensity;
            float radiusPulse = 1f + flutter * radiusFlutter * plumeIntensity;
            if (!force &&
                beamMesh != null &&
                cachedSegments == segments &&
                Mathf.Approximately(cachedMeshIntensity, meshIntensity) &&
                (!Application.isPlaying || plumeIntensity <= 0.001f))
            {
                return;
            }

            bool topologyChanged = beamMesh == null ||
                cachedSegments != segments ||
                vertices.Length != segments * 3;
            if (beamMesh == null)
            {
                beamMesh = new Mesh
                {
                    name = $"{nameof(SpacecraftThrusterBeam)} Mesh",
                    hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
                };
                beamMesh.MarkDynamic();
            }
            else if (topologyChanged)
            {
                beamMesh.Clear();
            }

            cachedSegments = segments;
            cachedMeshIntensity = meshIntensity;
            int ringVertexCount = segments;
            if (vertices.Length != ringVertexCount * 3)
            {
                vertices = new Vector3[ringVertexCount * 3];
                normals = new Vector3[vertices.Length];
                colors = new Color[vertices.Length];
            }

            if (triangles.Length != segments * 12)
            {
                triangles = new int[segments * 12];
                topologyChanged = true;
            }

            float direction = extendAlongNegativeZ ? -1f : 1f;
            float length = Mathf.Lerp(idleLength, fullLength, plumeIntensity) * lengthPulse;
            float midLength = length * 0.42f;
            float tipRadius = Mathf.Lerp(nozzleRadius * 0.72f, fullTipRadius, plumeIntensity) * radiusPulse;
            float midRadius = Mathf.Lerp(nozzleRadius, tipRadius, 0.58f + plumeIntensity * 0.12f);

            for (int i = 0; i < segments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / segments;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                Vector3 radial = new(cos, sin, 0f);

                int nozzleIndex = i;
                int midIndex = i + ringVertexCount;
                int tipIndex = i + ringVertexCount * 2;

                vertices[nozzleIndex] = new Vector3(cos * nozzleRadius, sin * nozzleRadius, 0f);
                float wave = Application.isPlaying
                    ? Mathf.Sin(dynamicPhase + angle * 2.5f) * 0.06f * plumeIntensity
                    : 0f;
                float midWaveRadius = midRadius * (1f + wave);
                float tipWaveRadius = tipRadius * (1f - wave * 0.7f);

                vertices[midIndex] = new Vector3(cos * midWaveRadius, sin * midWaveRadius, direction * midLength);
                vertices[tipIndex] = new Vector3(cos * tipWaveRadius, sin * tipWaveRadius, direction * length);

                normals[nozzleIndex] = radial;
                normals[midIndex] = radial;
                normals[tipIndex] = radial;

                colors[nozzleIndex] = new Color(1f, 0f, 0f, 0.95f);
                colors[midIndex] = new Color(0.45f, 0f, 0f, Mathf.Lerp(0.18f, 0.38f, plumeIntensity));
                colors[tipIndex] = new Color(0.05f, 0f, 0f, 0f);
            }

            if (topologyChanged)
            {
                int triangleIndex = 0;
                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    int nozzleA = i;
                    int nozzleB = next;
                    int midA = i + ringVertexCount;
                    int midB = next + ringVertexCount;
                    int tipA = i + ringVertexCount * 2;
                    int tipB = next + ringVertexCount * 2;

                    triangles[triangleIndex++] = nozzleA;
                    triangles[triangleIndex++] = midA;
                    triangles[triangleIndex++] = nozzleB;
                    triangles[triangleIndex++] = nozzleB;
                    triangles[triangleIndex++] = midA;
                    triangles[triangleIndex++] = midB;

                    triangles[triangleIndex++] = midA;
                    triangles[triangleIndex++] = tipA;
                    triangles[triangleIndex++] = midB;
                    triangles[triangleIndex++] = midB;
                    triangles[triangleIndex++] = tipA;
                    triangles[triangleIndex++] = tipB;
                }
            }

            beamMesh.vertices = vertices;
            beamMesh.normals = normals;
            beamMesh.colors = colors;
            if (topologyChanged)
            {
                beamMesh.triangles = triangles;
            }
            beamMesh.RecalculateBounds();
            meshFilter.sharedMesh = beamMesh;
        }

        void ApplyIntensity(float intensity)
        {
            ResolveComponents();
            if (Application.isPlaying && !gameObject.activeSelf && intensity > 0.001f)
            {
                gameObject.SetActive(true);
            }

            currentIntensity = intensity;
            RebuildMesh(force: false);

            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(IntensityId, Mathf.Lerp(0f, fullIntensity, intensity));
            propertyBlock.SetFloat(AlphaId, Mathf.Lerp(idleAlpha, fullAlpha, intensity));
            propertyBlock.SetColor(BaseColorId, baseColor);
            propertyBlock.SetColor(CoreColorId, coreColor);
            meshRenderer.SetPropertyBlock(propertyBlock);
            meshRenderer.enabled = intensity > 0.001f || !Application.isPlaying;
        }
    }
}
