using Farion.Rendering.Lighting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Farion.Rendering.Celestial
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CelestialLightSource))]
    public sealed class CelestialStarVisual : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] CelestialStarVisualProfile profile;
        [SerializeField] CelestialScaledSpaceProfile scaledSpaceProfile;

        [Header("Source")]
        [SerializeField] CelestialLightSource lightSource;

        [Header("Visual")]
        [SerializeField] Renderer starRenderer;
        [SerializeField] Transform starVisualTransform;
        [SerializeField] Camera observerCamera;

        [Header("Runtime")]
        [SerializeField] bool applyInEditMode = true;
        [SerializeField] bool updateEveryFrame = true;

        MaterialPropertyBlock propertyBlock;
        CelestialStarVisualProfile subscribedProfile;
        bool warnedAboutInvalidVisualTransform;

        public CelestialStarVisualProfile Profile => profile;
        public bool IsUsingScaledSpace { get; private set; }

        void OnEnable()
        {
            ResolveReferences();
            SyncProfileSubscription();
            ApplyVisual();
        }

        void OnValidate()
        {
            ResolveReferences();
            SyncProfileSubscription();

            if (!Application.isPlaying && applyInEditMode)
            {
                ApplyVisual();
            }
        }

        void OnDisable()
        {
            UnsubscribeFromProfile();
        }

        void LateUpdate()
        {
            if (!updateEveryFrame)
            {
                return;
            }

            if (Application.isPlaying || applyInEditMode)
            {
                UpdateVisualPlacement();
            }
        }

        [ContextMenu("Apply Star Visual")]
        public void ApplyVisual()
        {
            ResolveReferences();
            if (profile == null || starRenderer == null)
            {
                return;
            }

            if (profile.Material != null)
            {
                starRenderer.sharedMaterial = profile.Material;
            }

            starRenderer.shadowCastingMode = ShadowCastingMode.Off;
            starRenderer.receiveShadows = false;
            starRenderer.lightProbeUsage = LightProbeUsage.Off;
            starRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            starRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            propertyBlock ??= new MaterialPropertyBlock();
            starRenderer.GetPropertyBlock(propertyBlock);
            profile.ApplyMaterialProperties(propertyBlock);
            starRenderer.SetPropertyBlock(propertyBlock);
            UpdateVisualPlacement();
        }

        [ContextMenu("Log Star Visual State")]
        void LogVisualState()
        {
            ResolveReferences();
            UpdateVisualPlacement();

            if (lightSource == null || observerCamera == null || starVisualTransform == null)
            {
                Debug.LogWarning($"{name}: star visual state is incomplete.", this);
                return;
            }

            float physicalDistance =
                Vector3.Distance(observerCamera.transform.position, lightSource.Position);
            float displayDistance =
                Vector3.Distance(observerCamera.transform.position, starVisualTransform.position);
            float angularDiameter = physicalDistance > 0.001f
                ? Mathf.Rad2Deg * 2f * Mathf.Atan(lightSource.Radius / physicalDistance)
                : 0f;

            Debug.Log(
                $"{name}: scaledSpace={IsUsingScaledSpace}, " +
                $"physicalDistance={physicalDistance:F1}, " +
                $"displayDistance={displayDistance:F1}, " +
                $"angularDiameter={angularDiameter:F3} degrees.",
                this);
        }

        void UpdateVisualPlacement()
        {
            if (lightSource == null
                || starVisualTransform == null
                || observerCamera == null)
            {
                return;
            }

            if (starVisualTransform == transform)
            {
                if (!warnedAboutInvalidVisualTransform)
                {
                    Debug.LogError(
                        $"{name}: the star visual must be a child transform so scaled-space placement cannot move the physical body.",
                        this);
                    warnedAboutInvalidVisualTransform = true;
                }

                return;
            }

            warnedAboutInvalidVisualTransform = false;

            Vector3 physicalPosition = lightSource.Position;
            Vector3 observerPosition = observerCamera.transform.position;
            Vector3 observerToStar = physicalPosition - observerPosition;
            float physicalDistance = observerToStar.magnitude;

            bool shouldUseScaledSpace =
                scaledSpaceProfile != null
                && physicalDistance > 0.001f
                && scaledSpaceProfile.ShouldUseScaledSpace(
                    physicalDistance,
                    lightSource.Radius,
                    IsUsingScaledSpace);

            float visualDiameter = Mathf.Max(0.0001f, lightSource.Radius) * 2f;

            if (!shouldUseScaledSpace)
            {
                starVisualTransform.localPosition = Vector3.zero;
                starVisualTransform.localRotation = Quaternion.identity;
                starVisualTransform.localScale = Vector3.one * visualDiameter;
                IsUsingScaledSpace = false;
                return;
            }

            float displayDistance = scaledSpaceProfile.ResolveDisplayDistance(observerCamera, physicalDistance);
            float scaleRatio = displayDistance / physicalDistance;
            Vector3 displayPosition = observerPosition + observerToStar / physicalDistance * displayDistance;

            starVisualTransform.position = displayPosition;
            starVisualTransform.localRotation = Quaternion.identity;
            starVisualTransform.localScale = Vector3.one * (visualDiameter * scaleRatio);
            IsUsingScaledSpace = true;
        }

        void ResolveReferences()
        {
            if (lightSource == null)
            {
                lightSource = GetComponent<CelestialLightSource>();
            }

            if (starRenderer == null && starVisualTransform != null)
            {
                starRenderer = starVisualTransform.GetComponent<Renderer>();
            }

            if (starVisualTransform == null && starRenderer != null)
            {
                starVisualTransform = starRenderer.transform;
            }

            if (observerCamera == null)
            {
                observerCamera = Camera.main;
            }
        }

        void SyncProfileSubscription()
        {
            if (subscribedProfile == profile)
            {
                return;
            }

            UnsubscribeFromProfile();
            subscribedProfile = profile;
            if (subscribedProfile != null)
            {
                subscribedProfile.Changed += HandleProfileChanged;
            }
        }

        void UnsubscribeFromProfile()
        {
            if (subscribedProfile == null)
            {
                return;
            }

            subscribedProfile.Changed -= HandleProfileChanged;
            subscribedProfile = null;
        }

        void HandleProfileChanged()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (Application.isPlaying || applyInEditMode)
            {
                ApplyVisual();
            }
        }
    }
}
