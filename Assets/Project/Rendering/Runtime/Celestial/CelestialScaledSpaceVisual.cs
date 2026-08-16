using UnityEngine;
using UnityEngine.Rendering;

namespace Farion.Rendering.Celestial
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CelestialBodyVisual))]
    public sealed class CelestialScaledSpaceVisual : MonoBehaviour
    {
        const string ProxyName = "Scaled Space Visual";

        [SerializeField] CelestialScaledSpaceProfile profile;
        [SerializeField] CelestialBodyVisual bodyVisual;
        [SerializeField] Camera observerCamera;
        [SerializeField] bool applyInEditMode = true;

        Transform proxyTransform;
        MeshFilter proxyMeshFilter;
        MeshRenderer proxyRenderer;

        public bool IsUsingScaledSpace { get; private set; }
        public Vector3 RenderCenter => IsUsingScaledSpace && proxyTransform != null
            ? proxyTransform.position
            : transform.position;
        public float RenderScale { get; private set; } = 1f;

        void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            RefreshProxy();
            UpdateProjection();
        }

        void OnValidate()
        {
            ResolveReferences();
        }

        void OnDisable()
        {
            Unsubscribe();
            SetScaledState(false);
        }

        void LateUpdate()
        {
            if (Application.isPlaying || applyInEditMode)
            {
                UpdateProjection();
            }
        }

        void UpdateProjection()
        {
            ResolveReferences();
            if (profile == null || bodyVisual == null || observerCamera == null)
            {
                SetScaledState(false);
                return;
            }

            Vector3 observerPosition = observerCamera.transform.position;
            Vector3 observerToBody = transform.position - observerPosition;
            float physicalDistance = observerToBody.magnitude;
            if (physicalDistance <= 0.001f
                || !profile.ShouldUseScaledSpace(physicalDistance, IsUsingScaledSpace))
            {
                SetScaledState(false);
                return;
            }

            RefreshProxy();
            if (proxyRenderer == null || proxyMeshFilter.sharedMesh == null)
            {
                SetScaledState(false);
                return;
            }

            float displayDistance = profile.ResolveDisplayDistance(observerCamera, physicalDistance);
            RenderScale = displayDistance / physicalDistance;
            proxyTransform.SetPositionAndRotation(
                observerPosition + observerToBody / physicalDistance * displayDistance,
                transform.rotation);
            proxyTransform.localScale = Vector3.one * RenderScale;
            SetScaledState(true);
        }

        void RefreshProxy()
        {
            if (bodyVisual == null)
            {
                return;
            }

            GetOrCreateProxy();
            proxyMeshFilter.sharedMesh = bodyVisual.GetLowestDetailRenderMesh();
            bodyVisual.ConfigureSurfaceRenderer(proxyRenderer);
        }

        void GetOrCreateProxy()
        {
            if (proxyTransform == null)
            {
                proxyTransform = transform.Find(ProxyName);
            }

            if (proxyTransform == null)
            {
                GameObject proxyObject = new(ProxyName);
                proxyTransform = proxyObject.transform;
                proxyTransform.SetParent(transform, false);
            }

            proxyTransform.gameObject.hideFlags = HideFlags.DontSave;

            proxyMeshFilter = proxyTransform.GetComponent<MeshFilter>();
            if (proxyMeshFilter == null)
            {
                proxyMeshFilter = proxyTransform.gameObject.AddComponent<MeshFilter>();
            }

            proxyRenderer = proxyTransform.GetComponent<MeshRenderer>();
            if (proxyRenderer == null)
            {
                proxyRenderer = proxyTransform.gameObject.AddComponent<MeshRenderer>();
            }

            proxyRenderer.shadowCastingMode = ShadowCastingMode.Off;
            proxyRenderer.receiveShadows = false;
            proxyRenderer.lightProbeUsage = LightProbeUsage.Off;
            proxyRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            proxyRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
        }

        void SetScaledState(bool enabled)
        {
            IsUsingScaledSpace = enabled;
            RenderScale = enabled ? RenderScale : 1f;
            if (proxyRenderer != null)
            {
                proxyRenderer.enabled = enabled;
            }

            if (bodyVisual != null)
            {
                bodyVisual.SetScaledSpaceRenderSuppressed(enabled);
            }
        }

        void ResolveReferences()
        {
            if (bodyVisual == null)
            {
                bodyVisual = GetComponent<CelestialBodyVisual>();
            }

            if (observerCamera == null)
            {
                observerCamera = Camera.main;
            }
        }

        void Subscribe()
        {
            if (bodyVisual == null)
            {
                return;
            }

            bodyVisual.Rebuilt -= HandleBodyVisualChanged;
            bodyVisual.MaterialPropertiesChanged -= HandleBodyVisualChanged;
            bodyVisual.Rebuilt += HandleBodyVisualChanged;
            bodyVisual.MaterialPropertiesChanged += HandleBodyVisualChanged;
        }

        void Unsubscribe()
        {
            if (bodyVisual == null)
            {
                return;
            }

            bodyVisual.Rebuilt -= HandleBodyVisualChanged;
            bodyVisual.MaterialPropertiesChanged -= HandleBodyVisualChanged;
        }

        void HandleBodyVisualChanged()
        {
            RefreshProxy();
            UpdateProjection();
        }
    }
}
