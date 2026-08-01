using Farion.Core.Physics;
using Farion.Rendering.PostProcessing;
using Farion.Simulation.Celestial;
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
        ICelestialEnvironmentProvider,
        ICelestialOceanEffectSource,
        ICelestialAtmosphereEffectSource
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

        public bool TryGetEnvironment(CelestialBody body, out CelestialEnvironmentSample sample)
        {
            sample = default;
            if (profile == null || body == null || body != GetComponent<CelestialBody>())
            {
                return false;
            }

            ResolveComponents();

            float bodyRadius = Mathf.Max(0.01f, body.Radius);
            Vector2 terrainRadiusRange = terrainVisual != null && terrainVisual.HasRenderRadiusRange
                ? terrainVisual.RenderRadiusMinMax
                : new Vector2(bodyRadius, bodyRadius);

            PlanetHydrosphereProfile hydrosphere = ResolveHydrosphere();
            bool hasOcean = profile.OceanProfile != null && hydrosphere != null && hydrosphere.HasSurfaceOcean;
            float oceanRadius = hasOcean
                ? profile.OceanProfile.GetOceanRadius(bodyRadius, terrainRadiusRange, hydrosphere.OceanLevel)
                : 0f;

            bool hasAtmosphere = profile.AtmosphereProfile != null && HasSimulatedAtmosphere();
            float atmosphereRadius = hasAtmosphere
                ? profile.AtmosphereProfile.GetAtmosphereRadius(GetAtmosphereBaseRadius(bodyRadius, terrainRadiusRange))
                : 0f;

            sample = new CelestialEnvironmentSample(body, hasOcean, oceanRadius, hasAtmosphere, atmosphereRadius);
            return hasOcean || hasAtmosphere;
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

            ResolveComponents();

            CelestialBody sourceBody = GetComponent<CelestialBody>();
            if (sourceBody == null)
            {
                return false;
            }

            float bodyRadius = Mathf.Max(0.01f, sourceBody.Radius);
            Vector2 terrainRadiusRange = terrainVisual != null && terrainVisual.HasRenderRadiusRange
                ? terrainVisual.RenderRadiusMinMax
                : new Vector2(bodyRadius, bodyRadius);

            float oceanRadius = profile.OceanProfile.GetOceanRadius(
                bodyRadius,
                terrainRadiusRange,
                hydrosphere.OceanLevel);
            ResolveRenderProjection(sourceBody, out Vector3 renderCenter, out float renderScale);
            data = new CelestialOceanEffectData(
                renderCenter,
                bodyRadius * renderScale,
                terrainRadiusRange * renderScale,
                oceanRadius * renderScale,
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

            ResolveComponents();

            CelestialBody sourceBody = GetComponent<CelestialBody>();
            if (sourceBody == null)
            {
                return false;
            }

            float bodyRadius = Mathf.Max(0.01f, sourceBody.Radius);
            Vector2 terrainRadiusRange = terrainVisual != null && terrainVisual.HasRenderRadiusRange
                ? terrainVisual.RenderRadiusMinMax
                : new Vector2(bodyRadius, bodyRadius);
            float atmosphereBaseRadius = GetAtmosphereBaseRadius(bodyRadius, terrainRadiusRange);
            float atmosphereRadius = profile.AtmosphereProfile.GetAtmosphereRadius(atmosphereBaseRadius);
            ResolveRenderProjection(sourceBody, out Vector3 renderCenter, out float renderScale);

            data = new CelestialAtmosphereEffectData(
                renderCenter,
                bodyRadius * renderScale,
                atmosphereBaseRadius * renderScale,
                atmosphereRadius * renderScale,
                profile.AtmosphereProfile);
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

        float GetAtmosphereBaseRadius(float bodyRadius, Vector2 terrainRadiusRange)
        {
            PlanetHydrosphereProfile hydrosphere = ResolveHydrosphere();
            if (profile != null
                && profile.OceanProfile != null
                && hydrosphere != null
                && hydrosphere.HasSurfaceOcean)
            {
                return profile.OceanProfile.GetOceanRadius(
                    bodyRadius,
                    terrainRadiusRange,
                    hydrosphere.OceanLevel);
            }

            return Mathf.Max(0.01f, bodyRadius);
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
