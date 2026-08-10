using Farion.Rendering.PostProcessing;
using Farion.Simulation.Celestial;
using Farion.Simulation.Physics;
using Farion.Simulation.Planetary;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Farion.Rendering.Celestial
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CelestialBody))]
    [RequireComponent(typeof(CelestialBodyVisual))]
    public sealed class TerrestrialPlanetVisual :
        MonoBehaviour,
        ICelestialOceanLevelProvider,
        ICelestialOceanEffectSource,
        ICelestialAtmosphereEffectSource,
        ICelestialCloudEffectSource
    {
        [Header("Profile")]
        [SerializeField] TerrestrialPlanetVisualProfile profile;

        [Header("Components")]
        [SerializeField] CelestialBodyVisual terrainVisual;
        [SerializeField] PlanetSurfaceModel surfaceModel;

        [Header("Authoring")]
        [SerializeField] bool applyInEditMode = true;
        [SerializeField] bool applyOnEnable = true;

        TerrestrialPlanetVisualProfile subscribedProfile;
#if UNITY_EDITOR
        bool editorApplyQueued;
#endif

        public TerrestrialPlanetVisualProfile Profile => profile;

        void OnEnable()
        {
            ResolveComponents();
            SyncProfileSubscription();
            CelestialOceanEffectRegistry.Register(this);
            CelestialAtmosphereEffectRegistry.Register(this);
            CelestialCloudEffectRegistry.Register(this);

            if (applyOnEnable)
            {
                ApplyProfile();
            }
        }

        void OnValidate()
        {
            ResolveComponents();
            SyncProfileSubscription();

            if (!Application.isPlaying && applyInEditMode)
            {
                QueueEditorApply();
            }
        }

        void OnDisable()
        {
            CelestialOceanEffectRegistry.Unregister(this);
            CelestialAtmosphereEffectRegistry.Unregister(this);
            CelestialCloudEffectRegistry.Unregister(this);
            UnsubscribeFromProfile();
        }

        public bool TryGetOceanLevel(out float oceanLevel)
        {
            PlanetHydrosphereProfile hydrosphere = ResolveHydrosphere();
            if (profile == null || profile.OceanProfile == null || hydrosphere == null || !hydrosphere.HasSurfaceOcean)
            {
                oceanLevel = 0f;
                return false;
            }

            oceanLevel = hydrosphere.OceanLevel;
            return true;
        }

        bool TryResolveEnvironment(
            out CelestialBody sourceBody,
            out CelestialEnvironmentSample environment)
        {
            ResolveComponents();
            sourceBody = GetComponent<CelestialBody>();
            environment = default;
            return sourceBody != null &&
                   surfaceModel != null &&
                   surfaceModel.TryGetEnvironment(sourceBody, out environment);
        }

        public bool TryGetOceanEffectData(out CelestialOceanEffectData data)
        {
            data = default;

            PlanetHydrosphereProfile hydrosphere = ResolveHydrosphere();
            if (profile == null
                || profile.OceanProfile == null
                || hydrosphere == null
                || !hydrosphere.HasSurfaceOcean)
            {
                return false;
            }

            if (!TryResolveEnvironment(
                    out CelestialBody sourceBody,
                    out CelestialEnvironmentSample environment) ||
                !environment.HasOcean)
            {
                return false;
            }

            float bodyRadius = Mathf.Max(0.01f, sourceBody.Radius);
            ResolveRenderProjection(sourceBody, out Vector3 renderCenter, out float renderScale);
            data = new CelestialOceanEffectData(
                renderCenter,
                bodyRadius * renderScale,
                environment.TerrainRadiusMinMax * renderScale,
                environment.OceanRadius * renderScale,
                profile.OceanProfile);
            return true;
        }

        public bool TryGetAtmosphereEffectData(out CelestialAtmosphereEffectData data)
        {
            data = default;

            if (profile == null || profile.AtmosphereProfile == null || !HasSimulatedAtmosphere())
            {
                return false;
            }

            if (!TryResolveEnvironment(
                    out CelestialBody sourceBody,
                    out CelestialEnvironmentSample environment) ||
                !environment.HasAtmosphere)
            {
                return false;
            }

            float bodyRadius = Mathf.Max(0.01f, sourceBody.Radius);
            ResolveRenderProjection(sourceBody, out Vector3 renderCenter, out float renderScale);

            data = new CelestialAtmosphereEffectData(
                renderCenter,
                bodyRadius * renderScale,
                environment.AtmosphereBaseRadius * renderScale,
                environment.AtmosphereRadius * renderScale,
                profile.AtmosphereProfile);
            return true;
        }

        public bool TryGetCloudEffectData(out CelestialCloudEffectData data)
        {
            data = default;
            ResolveComponents();

            PlanetaryGenerationProfile generation = surfaceModel != null
                ? surfaceModel.GenerationProfile
                : null;
            if (profile == null
                || profile.CloudProfile == null
                || profile.CloudProfile.ShapeNoise == null
                || profile.CloudProfile.DetailNoise == null
                || profile.AtmosphereProfile == null
                || generation == null
                || !generation.SupportsSurfaceWaterClouds)
            {
                return false;
            }

            if (!TryResolveEnvironment(
                    out CelestialBody sourceBody,
                    out CelestialEnvironmentSample environment) ||
                !environment.HasAtmosphere)
            {
                return false;
            }

            float surfaceRadius = environment.AtmosphereBaseRadius;
            float atmosphereRadius = environment.AtmosphereRadius;
            profile.CloudProfile.GetLayerRadii(surfaceRadius, atmosphereRadius, out float innerRadius, out float outerRadius);
            ResolveRenderProjection(sourceBody, out Vector3 renderCenter, out float renderScale);

            data = new CelestialCloudEffectData(
                renderCenter,
                surfaceRadius * renderScale,
                innerRadius * renderScale,
                outerRadius * renderScale,
                Matrix4x4.Rotate(Quaternion.Inverse(sourceBody.transform.rotation)),
                generation.PlanetSeed,
                profile.CloudProfile);
            return true;
        }

        static void ResolveRenderProjection(
            CelestialBody sourceBody,
            out Vector3 center,
            out float scale)
        {
            if (sourceBody.TryGetComponent(out CelestialScaledSpaceVisual scaledVisual)
                && scaledVisual.IsUsingScaledSpace)
            {
                center = scaledVisual.RenderCenter;
                scale = scaledVisual.RenderScale;
                return;
            }

            center = sourceBody.transform.position;
            scale = 1f;
        }

        PlanetHydrosphereProfile ResolveHydrosphere()
        {
            ResolveComponents();
            return surfaceModel != null && surfaceModel.GenerationProfile != null
                ? surfaceModel.GenerationProfile.HydrosphereProfile
                : null;
        }

        bool HasSimulatedAtmosphere()
        {
            ResolveComponents();
            return surfaceModel != null
                && surfaceModel.GenerationProfile != null
                && surfaceModel.GenerationProfile.HasAtmosphere;
        }

        [ContextMenu("Apply Planet Visual Profile")]
        public void ApplyProfile()
        {
            if (profile == null)
            {
                return;
            }

            ResolveComponents();

            if (terrainVisual != null)
            {
                terrainVisual.Configure(profile.ShapeProfile, profile.SurfaceProfile);
            }
        }

        void ResolveComponents()
        {
            if (terrainVisual == null)
            {
                terrainVisual = GetComponent<CelestialBodyVisual>();
            }

            if (surfaceModel == null)
            {
                surfaceModel = GetComponent<PlanetSurfaceModel>();
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

            if (Application.isPlaying)
            {
                ApplyProfile();
                return;
            }

            if (applyInEditMode)
            {
                QueueEditorApply();
            }
        }

        void QueueEditorApply()
        {
#if UNITY_EDITOR
            if (editorApplyQueued)
            {
                return;
            }

            editorApplyQueued = true;
            EditorApplication.delayCall += RunQueuedEditorApply;
#endif
        }

#if UNITY_EDITOR
        void RunQueuedEditorApply()
        {
            editorApplyQueued = false;
            if (this == null || Application.isPlaying || !applyInEditMode)
            {
                return;
            }

            ApplyProfile();
        }
#endif
    }
}
