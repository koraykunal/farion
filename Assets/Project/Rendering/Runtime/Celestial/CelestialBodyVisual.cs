using Farion.Core.Physics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Farion.Rendering.Celestial
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CelestialBody))]
    public sealed class CelestialBodyVisual : MonoBehaviour
    {
        const string DefaultMeshObjectName = "Terrain Mesh";

        [Header("Source")]
        [SerializeField] CelestialBody body;
        [SerializeField] CelestialShapeProfile shapeProfile;
        [SerializeField] CelestialSurfaceProfileBase surfaceProfile;

        [Header("Mesh")]
        [SerializeField] string meshObjectName = DefaultMeshObjectName;
        [Range(0, 128)]
        [SerializeField] int renderResolution = 32;
        [SerializeField] CelestialLodProfile lodProfile;
        [Range(0, 2)]
        [SerializeField] int editModePreviewLod;

        [Header("Collision")]
        [SerializeField] bool syncSphereCollider = true;
        [SerializeField] bool generateMeshCollider;
        [Range(0, 64)]
        [SerializeField] int meshColliderResolution = 12;

        [Header("Material Override")]
        [SerializeField] Material material;

        [Header("Authoring Cleanup")]
        [SerializeField] bool normalizeBodyTransformScale = true;
        [SerializeField] bool disableRootRenderer = true;
        [SerializeField] bool disableRootColliders = true;
        [SerializeField] bool rebuildInEditMode = true;

        Mesh[] renderMeshes;
        Mesh collisionMesh;
        MaterialPropertyBlock propertyBlock;
        CelestialShapeProfile subscribedShapeProfile;
        CelestialSurfaceProfileBase subscribedSurfaceProfile;
        Vector2 renderRadiusMinMax;
        MeshFilter terrainMeshFilter;
        MeshRenderer terrainMeshRenderer;
        int activeLodIndex = -1;
#if UNITY_EDITOR
        bool editorRebuildQueued;
#endif

        public CelestialBody Body => body != null ? body : body = GetComponent<CelestialBody>();
        public Vector2 RenderRadiusMinMax => renderRadiusMinMax;
        public bool HasRenderRadiusRange => renderRadiusMinMax.x > 0f && renderRadiusMinMax.y >= renderRadiusMinMax.x;
        public int ActiveLodIndex => activeLodIndex;
        public event System.Action Rebuilt;

        public void Configure(
            CelestialShapeProfile newShapeProfile,
            CelestialSurfaceProfileBase newSurfaceProfile,
            bool rebuild = true)
        {
            shapeProfile = newShapeProfile;
            surfaceProfile = newSurfaceProfile;
            material = null;
            SyncProfileSubscriptions();

            if (rebuild)
            {
                Rebuild();
            }
        }

        void OnEnable()
        {
            SyncProfileSubscriptions();

            if (Application.isPlaying || rebuildInEditMode)
            {
                Rebuild();
            }
        }

        void OnValidate()
        {
            if (body == null)
            {
                body = GetComponent<CelestialBody>();
            }

            if (string.IsNullOrWhiteSpace(meshObjectName))
            {
                meshObjectName = DefaultMeshObjectName;
            }

            renderResolution = Mathf.Clamp(renderResolution, 0, 128);
            editModePreviewLod = Mathf.Clamp(editModePreviewLod, 0, GetLodCount() - 1);
            SyncProfileSubscriptions();

            if (!Application.isPlaying && rebuildInEditMode)
            {
                QueueEditorRebuild();
            }
        }

        [ContextMenu("Rebuild Visual Mesh")]
        public void Rebuild()
        {
            CelestialBody sourceBody = Body;
            if (sourceBody == null)
            {
                return;
            }

            ApplyAuthoringCleanup(sourceBody);

            GameObject meshObject = GetOrCreateMeshObject();
            terrainMeshFilter = meshObject.GetComponent<MeshFilter>();
            terrainMeshRenderer = meshObject.GetComponent<MeshRenderer>();

            ReplaceRenderMeshes();
            BuildRenderMeshes(sourceBody);
            activeLodIndex = -1;
            SetLodLevel(Application.isPlaying ? GetLodCount() - 1 : editModePreviewLod);

            Material resolvedMaterial = ResolveMaterial();
            if (resolvedMaterial != null)
            {
                terrainMeshRenderer.sharedMaterial = resolvedMaterial;
            }

            ApplyMaterialProperties(terrainMeshRenderer);
            ConfigureMeshCollider(meshObject, sourceBody);
            Rebuilt?.Invoke();
        }

        public void ApplyLodForScreenHeight(float screenHeight)
        {
            if (lodProfile == null)
            {
                return;
            }

            SetLodLevel(lodProfile.SelectLod(screenHeight));
        }

        public void SetLodLevel(int lodIndex)
        {
            if (renderMeshes == null || renderMeshes.Length == 0)
            {
                return;
            }

            lodIndex = Mathf.Clamp(lodIndex, 0, renderMeshes.Length - 1);
            if (activeLodIndex == lodIndex && terrainMeshFilter != null)
            {
                return;
            }

            terrainMeshFilter ??= GetOrCreateMeshObject().GetComponent<MeshFilter>();
            activeLodIndex = lodIndex;
            terrainMeshFilter.sharedMesh = renderMeshes[lodIndex];
        }

        void BuildRenderMeshes(CelestialBody sourceBody)
        {
            int lodCount = GetLodCount();
            renderMeshes = new Mesh[lodCount];

            for (int i = 0; i < lodCount; i++)
            {
                int resolution = GetLodResolution(i);
                renderMeshes[i] = CelestialSphereMeshBuilder.Build(
                    sourceBody.Radius,
                    resolution,
                    $"{sourceBody.BodyName} LOD{i} Render Mesh",
                    out Vector2 lodRadiusMinMax,
                    shapeProfile,
                    surfaceProfile);

                if (i == 0)
                {
                    renderRadiusMinMax = lodRadiusMinMax;
                }
            }
        }

        int GetLodCount()
        {
            return lodProfile != null ? lodProfile.LodCount : 1;
        }

        int GetLodResolution(int lodIndex)
        {
            return lodProfile != null ? lodProfile.GetResolution(lodIndex) : renderResolution;
        }

        void ConfigureMeshCollider(GameObject meshObject, CelestialBody sourceBody)
        {
            MeshCollider meshCollider = meshObject.GetComponent<MeshCollider>();
            bool canUseMeshCollider = generateMeshCollider && sourceBody.LockPosition;
            if (!canUseMeshCollider)
            {
                if (meshCollider != null)
                {
                    meshCollider.enabled = false;
                }

                ReplaceMesh(ref collisionMesh);
                return;
            }

            if (meshCollider == null)
            {
                meshCollider = meshObject.AddComponent<MeshCollider>();
            }

            meshCollider.enabled = true;
            ReplaceMesh(ref collisionMesh);
            collisionMesh = CelestialSphereMeshBuilder.Build(sourceBody.Radius, meshColliderResolution, $"{sourceBody.BodyName} Collision Mesh");
            meshCollider.sharedMesh = collisionMesh;
        }

        Material ResolveMaterial()
        {
            if (material != null)
            {
                return material;
            }

            return surfaceProfile != null ? surfaceProfile.Material : null;
        }

        void ApplyMaterialProperties(MeshRenderer meshRenderer)
        {
            if (surfaceProfile == null)
            {
                meshRenderer.SetPropertyBlock(null);
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.Clear();

            if (surfaceProfile is EarthLikeSurfaceProfile earthLikeSurface
                && TryGetComponent(out ICelestialOceanLevelProvider oceanLevelProvider)
                && oceanLevelProvider.TryGetOceanLevel(out float oceanLevel))
            {
                earthLikeSurface.ApplyMaterialProperties(propertyBlock, Body.Radius, renderRadiusMinMax, oceanLevel);
            }
            else
            {
                surfaceProfile.ApplyMaterialProperties(propertyBlock, Body.Radius, renderRadiusMinMax);
            }

            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        void ApplyAuthoringCleanup(CelestialBody sourceBody)
        {
            if (normalizeBodyTransformScale)
            {
                transform.localScale = Vector3.one;
            }

            if (disableRootRenderer && TryGetComponent(out MeshRenderer rootRenderer))
            {
                rootRenderer.enabled = false;
            }

            SphereCollider sphereCollider = null;
            if (syncSphereCollider)
            {
                sphereCollider = GetComponent<SphereCollider>();
                if (sphereCollider == null)
                {
                    sphereCollider = gameObject.AddComponent<SphereCollider>();
                }

                sphereCollider.enabled = true;
                sphereCollider.center = Vector3.zero;
                sphereCollider.radius = sourceBody.Radius;
            }

            if (!disableRootColliders)
            {
                return;
            }

            Collider[] rootColliders = GetComponents<Collider>();
            for (int i = 0; i < rootColliders.Length; i++)
            {
                if (rootColliders[i] == sphereCollider)
                {
                    continue;
                }

                rootColliders[i].enabled = false;
            }
        }

        GameObject GetOrCreateMeshObject()
        {
            Transform child = transform.Find(meshObjectName);
            if (child == null)
            {
                child = new GameObject(meshObjectName).transform;
                child.SetParent(transform, false);
            }

            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            child.gameObject.layer = gameObject.layer;

            if (!child.TryGetComponent(out MeshFilter _))
            {
                child.gameObject.AddComponent<MeshFilter>();
            }

            if (!child.TryGetComponent(out MeshRenderer _))
            {
                child.gameObject.AddComponent<MeshRenderer>();
            }

            return child.gameObject;
        }

        void OnDisable()
        {
            UnsubscribeFromProfiles();

            if (Application.isPlaying)
            {
                ReplaceRenderMeshes();
                ReplaceMesh(ref collisionMesh);
            }
        }

        void ReplaceRenderMeshes()
        {
            if (renderMeshes == null)
            {
                return;
            }

            for (int i = 0; i < renderMeshes.Length; i++)
            {
                ReplaceMesh(ref renderMeshes[i]);
            }

            renderMeshes = null;
            activeLodIndex = -1;
        }

        static void ReplaceMesh(ref Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(mesh);
            }
            else
            {
                DestroyImmediate(mesh);
            }

            mesh = null;
        }

        void SyncProfileSubscriptions()
        {
            if (subscribedShapeProfile != shapeProfile)
            {
                if (subscribedShapeProfile != null)
                {
                    subscribedShapeProfile.Changed -= HandleProfileChanged;
                }

                subscribedShapeProfile = shapeProfile;
                if (subscribedShapeProfile != null)
                {
                    subscribedShapeProfile.Changed += HandleProfileChanged;
                }
            }

            if (subscribedSurfaceProfile == surfaceProfile)
            {
                return;
            }

            if (subscribedSurfaceProfile != null)
            {
                subscribedSurfaceProfile.Changed -= HandleProfileChanged;
            }

            subscribedSurfaceProfile = surfaceProfile;
            if (subscribedSurfaceProfile != null)
            {
                subscribedSurfaceProfile.Changed += HandleProfileChanged;
            }
        }

        void UnsubscribeFromProfiles()
        {
            if (subscribedShapeProfile != null)
            {
                subscribedShapeProfile.Changed -= HandleProfileChanged;
                subscribedShapeProfile = null;
            }

            if (subscribedSurfaceProfile != null)
            {
                subscribedSurfaceProfile.Changed -= HandleProfileChanged;
                subscribedSurfaceProfile = null;
            }
        }

        void HandleProfileChanged()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Rebuild();
                return;
            }

            if (rebuildInEditMode)
            {
                QueueEditorRebuild();
            }
        }

        void QueueEditorRebuild()
        {
#if UNITY_EDITOR
            if (editorRebuildQueued)
            {
                return;
            }

            editorRebuildQueued = true;
            EditorApplication.delayCall += RunQueuedEditorRebuild;
#endif
        }

#if UNITY_EDITOR
        void RunQueuedEditorRebuild()
        {
            editorRebuildQueued = false;
            if (this == null || Application.isPlaying || !rebuildInEditMode)
            {
                return;
            }

            Rebuild();
        }
#endif
    }
}
