using Farion.Core.Physics;
using Farion.Rendering.PostProcessing;
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
    [RequireComponent(typeof(CelestialBodyVisual))]
    public sealed class EarthLikePlanetVisual :
        MonoBehaviour,
        ICelestialOceanLevelProvider,
        ICelestialEnvironmentProvider,
        ICelestialOceanEffectSource,
        ICelestialAtmosphereEffectSource
    {
        [Header("Profile")]
        [SerializeField] EarthLikePlanetVisualProfile profile;

        [Header("Components")]
        [SerializeField] CelestialBodyVisual terrainVisual;

        [Header("Authoring")]
        [SerializeField] bool applyInEditMode = true;
        [SerializeField] bool applyOnEnable = true;

        EarthLikePlanetVisualProfile subscribedProfile;
#if UNITY_EDITOR
        bool editorApplyQueued;
#endif

        public EarthLikePlanetVisualProfile Profile => profile;

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
            if (profile == null || !profile.HasOcean)
            {
                oceanLevel = 0f;
                return false;
            }

            oceanLevel = profile.OceanLevel;
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

            bool hasOcean = profile.HasOcean;
            float oceanRadius = hasOcean
                ? profile.OceanProfile.GetOceanRadius(bodyRadius, terrainRadiusRange, profile.OceanLevel)
                : 0f;

            bool hasAtmosphere = profile.AtmosphereProfile != null;
            float atmosphereRadius = hasAtmosphere
                ? profile.AtmosphereProfile.GetAtmosphereRadius(GetAtmosphereBaseRadius(bodyRadius, terrainRadiusRange))
                : 0f;

            sample = new CelestialEnvironmentSample(body, hasOcean, oceanRadius, hasAtmosphere, atmosphereRadius);
            return hasOcean || hasAtmosphere;
        }

        public bool TryGetOceanEffectData(out CelestialOceanEffectData data)
        {
            data = default;

            if (profile == null || !profile.HasOcean)
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

            float oceanRadius = profile.OceanProfile.GetOceanRadius(bodyRadius, terrainRadiusRange, profile.OceanLevel);
            data = new CelestialOceanEffectData(
                sourceBody.transform.position,
                bodyRadius,
                terrainRadiusRange,
                oceanRadius,
                profile.OceanProfile);
            return true;
        }

        public bool TryGetAtmosphereEffectData(out CelestialAtmosphereEffectData data)
        {
            data = default;

            if (profile == null || profile.AtmosphereProfile == null)
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

            data = new CelestialAtmosphereEffectData(
                sourceBody.transform.position,
                bodyRadius,
                atmosphereBaseRadius,
                atmosphereRadius,
                profile.AtmosphereProfile);
            return true;
        }

        float GetAtmosphereBaseRadius(float bodyRadius, Vector2 terrainRadiusRange)
        {
            if (profile != null && profile.HasOcean)
            {
                return profile.OceanProfile.GetOceanRadius(bodyRadius, terrainRadiusRange, profile.OceanLevel);
            }

            return Mathf.Max(0.01f, bodyRadius);
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
