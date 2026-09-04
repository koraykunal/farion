using System.Collections.Generic;
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
    public sealed class TerrestrialPlanetVisual : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] TerrestrialPlanetVisualProfile profile;

        [Header("Components")]
        [SerializeField] CelestialBodyVisual terrainVisual;
        [SerializeField] PlanetSurfaceModel surfaceModel;

        [Header("Authoring")]
        [SerializeField] bool applyInEditMode = true;
        [SerializeField] bool applyOnEnable = true;

        readonly List<SurfaceMaterialDefinition> materialScratch = new(SurfaceVisualProfile.MaxSurfaceSlots);
        TerrestrialPlanetVisualProfile subscribedProfile;
        PlanetSurfaceModel subscribedSurfaceModel;
        DerivedPlanetVisual derived;
        int derivedSeed;
        CelestialFrameProvider frameProvider;
        CelestialOceanEffectData oceanEffect;
        int oceanEffectFrame = -1;
        bool hasOceanEffect;
#if UNITY_EDITOR
        bool editorApplyQueued;
#endif

        public TerrestrialPlanetVisualProfile Profile => profile;
        public DerivedPlanetVisual Derived => derived;

        void OnEnable()
        {
            ResolveComponents();
            SyncSubscriptions();
            CelestialEffectRegistry.Register(this);

            if (applyOnEnable)
            {
                ApplyProfile();
            }
        }

        void OnValidate()
        {
            ResolveComponents();
            SyncSubscriptions();

            if (!Application.isPlaying && applyInEditMode)
            {
                QueueEditorApply();
            }
        }

        void OnDisable()
        {
            CelestialEffectRegistry.Unregister(this);
            Unsubscribe();
            ReleaseDerived();
        }

        public bool TryGetOceanLevel(out float oceanLevel)
        {
            PlanetHydrosphereProfile hydrosphere = ResolveHydrosphere();
            if (derived == null || derived.OceanProfile == null || hydrosphere == null || !hydrosphere.HasSurfaceOcean)
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
            if (sourceBody == null ||
                surfaceModel == null ||
                !surfaceModel.TryGetEnvironment(sourceBody, out environment))
            {
                return false;
            }

            CelestialFrameProvider provider = ResolveFrameProvider();
            double simulationTime = Application.isPlaying && provider != null && provider.Simulation != null
                ? provider.Simulation.SimulationTime
                : Time.timeAsDouble;
            environment = environment.AtSimulationTime(simulationTime);
            return true;
        }

        public bool TryGetOceanEffectData(out CelestialOceanEffectData data)
        {
            if (Application.isPlaying && oceanEffectFrame == Time.frameCount)
            {
                data = oceanEffect;
                return hasOceanEffect;
            }

            hasOceanEffect = CreateOceanEffectData(out oceanEffect);
            oceanEffectFrame = Time.frameCount;
            data = oceanEffect;
            return hasOceanEffect;
        }

        bool CreateOceanEffectData(out CelestialOceanEffectData data)
        {
            data = default;

            PlanetHydrosphereProfile hydrosphere = ResolveHydrosphere();
            if (derived == null
                || derived.OceanProfile == null
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

            ResolveRenderProjection(sourceBody, out Vector3 renderCenter, out float renderScale);
            data = BuildOceanEffectData(sourceBody, environment, renderCenter, renderScale);
            return true;
        }

        public bool TryGetAtmosphereEffectData(out CelestialAtmosphereEffectData data)
        {
            data = default;

            if (derived == null || derived.AtmosphereProfile == null || !HasSimulatedAtmosphere())
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
            CelestialOceanEffectData oceanSurface =
                TryGetOceanEffectData(out CelestialOceanEffectData ocean) ? ocean : default;

            data = new CelestialAtmosphereEffectData(
                renderCenter,
                bodyRadius * renderScale,
                environment.AtmosphereBaseRadius * renderScale,
                environment.AtmosphereRadius * renderScale,
                derived.AtmosphereProfile,
                oceanSurface);
            return true;
        }

        CelestialOceanEffectData BuildOceanEffectData(
            CelestialBody sourceBody,
            in CelestialEnvironmentSample environment,
            Vector3 renderCenter,
            float renderScale)
        {
            float bodyRadius = Mathf.Max(0.01f, sourceBody.Radius);
            return new CelestialOceanEffectData(
                renderCenter,
                bodyRadius * renderScale,
                environment.OceanRadius * renderScale,
                environment.WaveAmplitude * renderScale,
                environment.WaveLength * renderScale,
                environment.WavePhases,
                Matrix4x4.Rotate(environment.WorldToBodyRotation),
                derived.OceanProfile,
                environment.HasAtmosphere ? environment.AtmosphereRadius * renderScale : 0f,
                environment.HasAtmosphere ? derived.AtmosphereProfile : null);
        }

        public bool TryGetCloudEffectData(out CelestialCloudEffectData data)
        {
            data = default;
            ResolveComponents();

            PlanetaryGenerationProfile generation = surfaceModel != null
                ? surfaceModel.GenerationProfile
                : null;
            if (derived == null
                || derived.CloudProfile == null
                || derived.CloudProfile.ShapeNoise == null
                || derived.CloudProfile.DetailNoise == null
                || derived.AtmosphereProfile == null
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
            derived.CloudProfile.GetLayerRadii(
                surfaceRadius,
                atmosphereRadius,
                environment.TerrainRadiusMinMax.y,
                out float innerRadius,
                out float outerRadius);
            ResolveRenderProjection(sourceBody, out Vector3 renderCenter, out float renderScale);

            data = new CelestialCloudEffectData(
                renderCenter,
                surfaceRadius * renderScale,
                innerRadius * renderScale,
                outerRadius * renderScale,
                atmosphereRadius * renderScale,
                Matrix4x4.Rotate(Quaternion.Inverse(sourceBody.transform.rotation)),
                generation.PlanetSeed,
                derived.CloudProfile,
                derived.AtmosphereProfile);
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
            ResolveComponents();
            PlanetaryGenerationProfile generation = surfaceModel != null
                ? surfaceModel.GenerationProfile
                : null;
            if (profile == null || generation == null)
            {
                ReleaseDerived();
                return;
            }

            if (derived != null && derivedSeed == generation.PlanetSeed)
            {
                return;
            }

            ReleaseDerived();
            materialScratch.Clear();
            generation.SurfaceMaterialDistribution?.CollectMaterials(materialScratch);
            derived = profile.Derive(generation.PlanetSeed, materialScratch);
            derivedSeed = generation.PlanetSeed;

            if (terrainVisual != null)
            {
                terrainVisual.Configure(derived.SurfaceProfile);
            }
        }

        void ReleaseDerived()
        {
            if (derived == null)
            {
                return;
            }

            if (terrainVisual != null && terrainVisual.SurfaceProfile == derived.SurfaceProfile)
            {
                terrainVisual.Configure(null);
            }

            derived.Release();
            derived = null;
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

        CelestialFrameProvider ResolveFrameProvider()
        {
            if (frameProvider != null && frameProvider.gameObject.scene == gameObject.scene)
            {
                return frameProvider;
            }

            CelestialFrameProvider[] providers = FindObjectsByType<CelestialFrameProvider>(
                FindObjectsInactive.Exclude);
            for (int i = 0; i < providers.Length; i++)
            {
                if (providers[i].gameObject.scene == gameObject.scene)
                {
                    frameProvider = providers[i];
                    break;
                }
            }

            return frameProvider;
        }

        void SyncSubscriptions()
        {
            if (subscribedProfile != profile)
            {
                if (subscribedProfile != null)
                {
                    subscribedProfile.Changed -= HandleSourceChanged;
                }

                subscribedProfile = profile;
                if (subscribedProfile != null)
                {
                    subscribedProfile.Changed += HandleSourceChanged;
                }
            }

            if (subscribedSurfaceModel == surfaceModel)
            {
                return;
            }

            if (subscribedSurfaceModel != null)
            {
                subscribedSurfaceModel.Changed -= HandleSourceChanged;
            }

            subscribedSurfaceModel = surfaceModel;
            if (subscribedSurfaceModel != null)
            {
                subscribedSurfaceModel.Changed += HandleSourceChanged;
            }
        }

        void Unsubscribe()
        {
            if (subscribedProfile != null)
            {
                subscribedProfile.Changed -= HandleSourceChanged;
                subscribedProfile = null;
            }

            if (subscribedSurfaceModel != null)
            {
                subscribedSurfaceModel.Changed -= HandleSourceChanged;
                subscribedSurfaceModel = null;
            }
        }

        void HandleSourceChanged()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            derivedSeed = int.MinValue;
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
