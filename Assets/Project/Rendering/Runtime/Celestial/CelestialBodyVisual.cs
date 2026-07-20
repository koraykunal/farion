using Farion.Core.Physics;
using Farion.Simulation.Celestial;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Farion.Rendering.Celestial
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CelestialBody))]
    public sealed class CelestialBodyVisual : MonoBehaviour, ICelestialSurfaceProvider
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
        [Range(0.0005f, 0.05f)]
        [SerializeField] float surfaceNormalSampleStep = 0.004f;

        [Header("Collision")]
        [SerializeField] bool syncSphereCollider = true;
        [SerializeField] bool generateMeshCollider;
        [Range(0, 64)]
        [SerializeField] int meshColliderResolution = 12;
        [SerializeField] bool bakeMeshCollider = true;

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
        [Header("Runtime Collision")]
        [SerializeField] bool meshColliderSkippedBecauseBodyIsDynamic;
#if UNITY_EDITOR
        bool editorRebuildQueued;
#endif

        public CelestialBody Body => body != null ? body : body = GetComponent<CelestialBody>();
        public Vector2 RenderRadiusMinMax => renderRadiusMinMax;
        public bool HasRenderRadiusRange => renderRadiusMinMax.x > 0f && renderRadiusMinMax.y >= renderRadiusMinMax.x;
        public int ActiveLodIndex => activeLodIndex;
        public bool MeshColliderSkippedBecauseBodyIsDynamic => meshColliderSkippedBecauseBodyIsDynamic;
        public event System.Action Rebuilt;

        public bool TrySampleSurface(CelestialBody sourceBody, Vector3 position, out CelestialSurfaceSample sample)
        {
            sample = default;
            if (sourceBody == null || sourceBody != Body)
            {
                return false;
            }

            Vector3 centerToPoint = position - sourceBody.Position;
            float centerDistance = centerToPoint.magnitude;
            Vector3 radialNormal = centerDistance > 0.0001f ? centerToPoint / centerDistance : transform.up;
            float surfaceRadius = EvaluateSurfaceRadius(sourceBody.Radius, radialNormal);
            Vector3 surfacePoint = sourceBody.Position + radialNormal * surfaceRadius;
            Vector3 surfaceNormal = EvaluateSurfaceNormal(sourceBody.Radius, radialNormal);
            float slopeAngle = Vector3.Angle(radialNormal, surfaceNormal);
            sample = new CelestialSurfaceSample(
                sourceBody,
                surfacePoint,
                surfaceNormal,
                centerDistance,
                centerDistance - surfaceRadius,
                slopeAngle);
            return true;
        }

        public void Configure(
            CelestialShapeProfile newShapeProfile,
            CelestialSurfaceProfileBase newSurfaceProfile,
            bool rebuild = true)
        {
            bool profilesChanged = shapeProfile != newShapeProfile || surfaceProfile != newSurfaceProfile;
            shapeProfile = newShapeProfile;
            surfaceProfile = newSurfaceProfile;
            if (profilesChanged)
            {
                material = null;
            }

            SyncProfileSubscriptions();

            if (rebuild && profilesChanged)
            {
                Rebuild();
            }
            else if (terrainMeshRenderer != null)
            {
                RefreshMaterialProperties();
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
            surfaceNormalSampleStep = Mathf.Clamp(surfaceNormalSampleStep, 0.0005f, 0.05f);
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

            SetLodLevel(lodProfile.SelectLod(screenHeight, activeLodIndex));
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

        public void RefreshMaterialProperties()
        {
            if (terrainMeshRenderer == null)
            {
                terrainMeshRenderer = GetOrCreateMeshObject().GetComponent<MeshRenderer>();
            }

            ApplyMaterialProperties(terrainMeshRenderer);
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

        float EvaluateSurfaceRadius(float bodyRadius, Vector3 unitDirection)
        {
            if (shapeProfile != null)
            {
                return shapeProfile.EvaluateSample(bodyRadius, unitDirection).Radius;
            }

            return surfaceProfile != null
                ? surfaceProfile.EvaluateRadius(bodyRadius, unitDirection)
                : bodyRadius;
        }

        Vector3 EvaluateSurfaceNormal(float bodyRadius, Vector3 unitDirection)
        {
            Vector3 tangentA = Vector3.ProjectOnPlane(Vector3.forward, unitDirection);
            if (tangentA.sqrMagnitude <= 0.0001f)
            {
                tangentA = Vector3.ProjectOnPlane(Vector3.right, unitDirection);
            }

            if (tangentA.sqrMagnitude <= 0.0001f)
            {
                return unitDirection;
            }

            tangentA.Normalize();
            Vector3 tangentB = Vector3.Cross(unitDirection, tangentA).normalized;
            float step = Mathf.Clamp(surfaceNormalSampleStep, 0.0005f, 0.05f);

            Vector3 pointA0 = EvaluateRelativeSurfacePoint(bodyRadius, (unitDirection - tangentA * step).normalized);
            Vector3 pointA1 = EvaluateRelativeSurfacePoint(bodyRadius, (unitDirection + tangentA * step).normalized);
            Vector3 pointB0 = EvaluateRelativeSurfacePoint(bodyRadius, (unitDirection - tangentB * step).normalized);
            Vector3 pointB1 = EvaluateRelativeSurfacePoint(bodyRadius, (unitDirection + tangentB * step).normalized);
            Vector3 derivativeA = pointA1 - pointA0;
            Vector3 derivativeB = pointB1 - pointB0;
            Vector3 normal = Vector3.Cross(derivativeA, derivativeB);

            if (normal.sqrMagnitude <= 0.0001f)
            {
                return unitDirection;
            }

            normal.Normalize();
            return Vector3.Dot(normal, unitDirection) >= 0f ? normal : -normal;
        }

        Vector3 EvaluateRelativeSurfacePoint(float bodyRadius, Vector3 unitDirection)
        {
            return unitDirection * EvaluateSurfaceRadius(bodyRadius, unitDirection);
        }

        void ConfigureMeshCollider(GameObject meshObject, CelestialBody sourceBody)
        {
            MeshCollider meshCollider = meshObject.GetComponent<MeshCollider>();
            bool canUseMeshCollider = generateMeshCollider && sourceBody.SupportsNonConvexSurfaceCollider;
            meshColliderSkippedBecauseBodyIsDynamic = generateMeshCollider && !sourceBody.SupportsNonConvexSurfaceCollider;
            if (!canUseMeshCollider)
            {
                if (meshCollider != null)
                {
                    meshCollider.sharedMesh = null;
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
            meshCollider.convex = false;
            ReplaceMesh(ref collisionMesh);
            collisionMesh = CelestialSphereMeshBuilder.Build(
                sourceBody.Radius,
                meshColliderResolution,
                $"{sourceBody.BodyName} Collision Mesh",
                shapeProfile,
                surfaceProfile);
            meshCollider.sharedMesh = null;
            if (bakeMeshCollider)
            {
                CelestialMeshColliderBaker.BakeImmediate(collisionMesh);
            }

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

            bool canUseMeshCollider = generateMeshCollider && sourceBody.SupportsNonConvexSurfaceCollider;
            SphereCollider sphereCollider = GetComponent<SphereCollider>();
            if (syncSphereCollider && !canUseMeshCollider)
            {
                if (sphereCollider == null)
                {
                    sphereCollider = gameObject.AddComponent<SphereCollider>();
                }

                sphereCollider.enabled = true;
                sphereCollider.center = Vector3.zero;
                sphereCollider.radius = sourceBody.Radius;
            }
            else if (sphereCollider != null)
            {
                sphereCollider.enabled = false;
            }

            if (!disableRootColliders)
            {
                return;
            }

            Collider[] rootColliders = GetComponents<Collider>();
            for (int i = 0; i < rootColliders.Length; i++)
            {
                if (syncSphereCollider && !canUseMeshCollider && rootColliders[i] == sphereCollider)
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
                    subscribedShapeProfile.Changed -= HandleShapeProfileChanged;
                }

                subscribedShapeProfile = shapeProfile;
                if (subscribedShapeProfile != null)
                {
                    subscribedShapeProfile.Changed += HandleShapeProfileChanged;
                }
            }

            if (subscribedSurfaceProfile == surfaceProfile)
            {
                return;
            }

            if (subscribedSurfaceProfile != null)
            {
                subscribedSurfaceProfile.Changed -= HandleSurfaceProfileChanged;
            }

            subscribedSurfaceProfile = surfaceProfile;
            if (subscribedSurfaceProfile != null)
            {
                subscribedSurfaceProfile.Changed += HandleSurfaceProfileChanged;
            }
        }

        void UnsubscribeFromProfiles()
        {
            if (subscribedShapeProfile != null)
            {
                subscribedShapeProfile.Changed -= HandleShapeProfileChanged;
                subscribedShapeProfile = null;
            }

            if (subscribedSurfaceProfile != null)
            {
                subscribedSurfaceProfile.Changed -= HandleSurfaceProfileChanged;
                subscribedSurfaceProfile = null;
            }
        }

        void HandleShapeProfileChanged()
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

        void HandleSurfaceProfileChanged()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (SurfaceProfileCanAffectGeometry())
            {
                HandleShapeProfileChanged();
                return;
            }

            RefreshMaterialProperties();
        }

        bool SurfaceProfileCanAffectGeometry()
        {
            return shapeProfile == null &&
                surfaceProfile is CelestialSurfaceProfile simpleSurfaceProfile &&
                simpleSurfaceProfile.HasDisplacement;
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
