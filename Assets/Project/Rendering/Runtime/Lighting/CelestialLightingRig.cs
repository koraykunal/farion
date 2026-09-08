using Farion.Rendering.Celestial;
using Farion.Rendering.PostProcessing;
using Farion.Simulation.Celestial;
using UnityEngine;
using UnityEngine.Rendering;

namespace Farion.Rendering.Lighting
{
    [DefaultExecutionOrder(300)]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CelestialLightingRig : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] CelestialLightingProfile profile;

        [Header("Source And Focus")]
        [SerializeField] CelestialLightSource primarySource;
        [SerializeField] Transform lightingFocus;
        [SerializeField] bool useMainCameraAsFallbackFocus;

        [Header("Scene Light")]
        [SerializeField] Light mainDirectionalLight;
        [SerializeField] bool syncLightPositionToSource;

        [Header("Planetshine")]
        [SerializeField] Light planetshineLight;
        [SerializeField] CelestialFrameProvider frameProvider;

        [Header("Camera")]
        [SerializeField] Camera sceneCamera;

        [Header("Runtime")]
        [SerializeField] bool updateInEditMode = true;
        [SerializeField] bool updateEveryFrame = true;

        readonly CelestialSkyReflectionBuilder skyReflectionBuilder = new();
        Quaternion heldLightRotation;
        bool hasHeldLightRotation;

        public CelestialLightingProfile Profile => profile;

        public void SetPrimarySource(CelestialLightSource source)
        {
            primarySource = source;
            ApplyLighting();
        }

        void OnEnable()
        {
            ApplyLighting();
        }

        void OnDisable()
        {
            skyReflectionBuilder.Release();
        }

        void OnValidate()
        {
            if (!Application.isPlaying && updateInEditMode)
            {
                ApplyLighting();
            }
        }

        void LateUpdate()
        {
            if (!updateEveryFrame)
            {
                return;
            }

            if (Application.isPlaying || updateInEditMode)
            {
                ApplyLighting();
            }
        }

        [ContextMenu("Apply Lighting Now")]
        public void ApplyLighting()
        {
            if (profile == null)
            {
                return;
            }

            CelestialLightSource source = ResolvePrimarySource();
            Light directionalLight = ResolveMainDirectionalLight();
            Transform focus = ResolveFocus();

            if (!CelestialLightingState.TryCreate(profile, source, focus, out CelestialLightingState state))
            {
                ApplyRenderSettings(directionalLight, false, default, default);
                ApplyCameraDefaults();
                return;
            }

            bool hasAtmosphericAmbient = TryResolveAtmosphericAmbient(
                state,
                focus,
                out AtmosphericAmbientSample ambientSample);

            if (directionalLight != null)
            {
                ApplyDirectionalLight(
                    state,
                    directionalLight,
                    hasAtmosphericAmbient ? ambientSample.DensityFactor : 0f);
            }

            ApplyPlanetshine(state, focus);
            Color ambientGlobal = hasAtmosphericAmbient
                ? profile.AmbientLight + ambientSample.Equator
                : profile.AmbientLight;
            CelestialLightingGlobals.Apply(profile, state, ambientGlobal);
            ApplyRenderSettings(
                directionalLight,
                hasAtmosphericAmbient,
                ambientSample,
                state.DirectionToStar);
            ApplyCameraDefaults();
        }

        bool TryResolveAtmosphericAmbient(
            in CelestialLightingState state,
            Transform focus,
            out AtmosphericAmbientSample sample)
        {
            sample = default;
            if (!profile.DeriveAmbientFromAtmosphere || focus == null)
            {
                return false;
            }

            if (!CelestialEffectRegistry.TryGetAtmosphereNear(
                    focus.position,
                    out CelestialAtmosphereEffectData atmosphere))
            {
                return false;
            }

            return CelestialAtmosphericAmbient.TrySample(
                    atmosphere,
                    focus.position,
                    state.DirectionToStar,
                    CelestialLightingGlobals.ResolveStarColor(profile, state),
                    state.Intensity,
                    profile,
                    out sample)
                && sample.DensityFactor > 0.001f;
        }

        void ApplyDirectionalLight(CelestialLightingState state, Light directionalLight, float atmosphereDensity)
        {
            directionalLight.type = LightType.Directional;
            if (syncLightPositionToSource)
            {
                directionalLight.transform.position = state.StarPosition;
            }

            Vector3 lightForward = profile.InvertLightDirection
                ? -state.LightTravelDirection
                : state.LightTravelDirection;
            Quaternion targetRotation = Quaternion.LookRotation(lightForward, ResolveStableUp(lightForward));
            if (!hasHeldLightRotation
                || Quaternion.Angle(heldLightRotation, targetRotation) >= profile.ShadowDirectionStepDegrees)
            {
                heldLightRotation = targetRotation;
                hasHeldLightRotation = true;
            }

            directionalLight.transform.rotation = heldLightRotation;
            directionalLight.color = profile.LightColor;
            directionalLight.useColorTemperature = profile.UseColorTemperature;
            directionalLight.colorTemperature = state.ColorTemperature;
            directionalLight.intensity = state.Intensity;
            directionalLight.shadows = profile.Shadows;
            directionalLight.shadowStrength = Mathf.Lerp(
                profile.AirlessShadowStrength,
                profile.ShadowStrength,
                Mathf.Clamp01(atmosphereDensity));
            directionalLight.shadowNearPlane = profile.ShadowNearPlane;
#if UNITY_EDITOR
            directionalLight.shadowAngle = profile.DirectionalShadowAngle;
#endif
            directionalLight.bounceIntensity = profile.BounceIntensity;
        }

        void ApplyPlanetshine(CelestialLightingState state, Transform focus)
        {
            if (planetshineLight == null)
            {
                return;
            }

            if (!profile.EnablePlanetshine
                || frameProvider == null
                || focus == null
                || !frameProvider.TrySample(focus.position, Vector3.zero, out CelestialFrameSample sample)
                || !sample.HasBody)
            {
                planetshineLight.enabled = false;
                return;
            }

            Vector3 bodyToFocus = focus.position - sample.BodyPosition;
            float centerDistance = bodyToFocus.magnitude;
            if (centerDistance <= 0.0001f)
            {
                planetshineLight.enabled = false;
                return;
            }

            Vector3 outwardDirection = bodyToFocus / centerDistance;
            float starFacing = Vector3.Dot(outwardDirection, state.DirectionToStar);
            float intensity = profile.EvaluatePlanetshineIntensity(
                sample.BodyRadius,
                centerDistance,
                starFacing,
                state.Intensity);

            if (intensity <= 0.0001f)
            {
                planetshineLight.enabled = false;
                return;
            }

            planetshineLight.enabled = true;
            planetshineLight.type = LightType.Directional;
            planetshineLight.transform.rotation = Quaternion.LookRotation(
                outwardDirection,
                ResolveStableUp(outwardDirection));
            planetshineLight.color = profile.PlanetshineColor;
            planetshineLight.useColorTemperature = false;
            planetshineLight.intensity = intensity;
            planetshineLight.shadows = profile.PlanetshineShadows;
            planetshineLight.bounceIntensity = 0f;
        }

        void ApplyRenderSettings(
            Light directionalLight,
            bool hasAtmosphericAmbient,
            in AtmosphericAmbientSample ambientSample,
            Vector3 directionToStar)
        {
            if (!profile.ApplyRenderSettings)
            {
                return;
            }

            if (directionalLight != null)
            {
                RenderSettings.sun = directionalLight;
            }

            if (hasAtmosphericAmbient)
            {
                Color baseAmbient = profile.AmbientLight;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = baseAmbient + ambientSample.Sky;
                RenderSettings.ambientEquatorColor = baseAmbient + ambientSample.Equator;
                RenderSettings.ambientGroundColor = baseAmbient + ambientSample.Ground;
            }
            else
            {
                RenderSettings.ambientMode = profile.AmbientMode;
                RenderSettings.ambientLight = profile.AmbientLight;
            }

            bool useSkyReflection = hasAtmosphericAmbient
                && profile.DeriveReflectionFromAtmosphere
                && ambientSample.DensityFactor > 0.02f;
            if (useSkyReflection)
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = skyReflectionBuilder.Update(
                    ambientSample,
                    directionToStar);
            }
            else if (profile.ReflectionCubemap != null)
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = profile.ReflectionCubemap;
            }
            else
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
                RenderSettings.customReflectionTexture = null;
            }

            RenderSettings.reflectionIntensity = profile.ReflectionIntensity;

            if (profile.DisableFog)
            {
                RenderSettings.fog = false;
            }
        }

        void ApplyCameraDefaults()
        {
            if (!profile.ApplyCameraDefaults)
            {
                return;
            }

            Camera camera = ResolveSceneCamera();
            if (camera == null)
            {
                return;
            }

            camera.clearFlags = profile.CameraClearFlags;
            camera.backgroundColor = profile.CameraBackground;
            camera.nearClipPlane = profile.NearClipPlane;
            camera.farClipPlane = profile.FarClipPlane;
            camera.allowHDR = profile.AllowHdr;
        }

        CelestialLightSource ResolvePrimarySource()
        {
            return primarySource;
        }

        Light ResolveMainDirectionalLight()
        {
            return mainDirectionalLight;
        }

        Transform ResolveFocus()
        {
            if (lightingFocus != null)
            {
                return lightingFocus;
            }

            Camera camera = ResolveSceneCamera();
            if (useMainCameraAsFallbackFocus && camera != null)
            {
                return camera.transform;
            }

            return null;
        }

        Camera ResolveSceneCamera()
        {
            return sceneCamera;
        }

        static Vector3 ResolveStableUp(Vector3 forward)
        {
            return Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
        }

#if UNITY_EDITOR
        [ContextMenu("Resolve Missing Scene References")]
        void ResolveMissingSceneReferences()
        {
            if (primarySource == null)
            {
                primarySource = FindAnyObjectByType<CelestialLightSource>(FindObjectsInactive.Exclude);
            }

            if (mainDirectionalLight == null)
            {
                mainDirectionalLight = RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional
                    ? RenderSettings.sun
                    : FindFirstDirectionalLight();
            }

            if (sceneCamera == null)
            {
                sceneCamera = Camera.main != null
                    ? Camera.main
                    : FindAnyObjectByType<Camera>(FindObjectsInactive.Exclude);
            }

            if (frameProvider == null)
            {
                frameProvider = FindAnyObjectByType<CelestialFrameProvider>(FindObjectsInactive.Exclude);
            }
        }

        static Light FindFirstDirectionalLight()
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                {
                    return lights[i];
                }
            }

            return null;
        }
#endif
    }

    readonly struct CelestialLightingState
    {
        CelestialLightingState(
            Vector3 starPosition,
            Vector3 lightTravelDirection,
            float intensity,
            float colorTemperature)
        {
            StarPosition = starPosition;
            LightTravelDirection = lightTravelDirection;
            Intensity = intensity;
            ColorTemperature = colorTemperature;
        }

        public Vector3 StarPosition { get; }
        public Vector3 LightTravelDirection { get; }
        public Vector3 DirectionToStar => -LightTravelDirection;
        public float Intensity { get; }
        public float ColorTemperature { get; }

        public static bool TryCreate(
            CelestialLightingProfile profile,
            CelestialLightSource source,
            Transform focus,
            out CelestialLightingState state)
        {
            state = default;
            if (profile == null || source == null || focus == null)
            {
                return false;
            }

            Vector3 travel = focus.position - source.Position;
            float distance = travel.magnitude;
            if (distance <= 0.0001f)
            {
                return false;
            }

            float colorTemperature = source.HasRadiationProfile
                ? source.ColorTemperatureKelvin
                : profile.ColorTemperature;
            state = new CelestialLightingState(
                source.Position,
                travel / distance,
                profile.EvaluateIntensity(distance),
                colorTemperature);
            return true;
        }
    }

    static class CelestialLightingGlobals
    {
        static readonly int StarPositionId = Shader.PropertyToID("_FarionStarPositionWS");
        static readonly int StarDirectionId = Shader.PropertyToID("_FarionStarDirectionWS");
        static readonly int StarColorId = Shader.PropertyToID("_FarionStarColor");
        static readonly int StarIntensityId = Shader.PropertyToID("_FarionStarIntensity");
        static readonly int AmbientColorId = Shader.PropertyToID("_FarionAmbientColor");

        public static Color ResolveStarColor(
            CelestialLightingProfile profile,
            in CelestialLightingState state)
        {
            return profile.UseColorTemperature
                ? profile.LightColor * Mathf.CorrelatedColorTemperatureToRGB(state.ColorTemperature)
                : profile.LightColor;
        }

        public static void Apply(
            CelestialLightingProfile profile,
            CelestialLightingState state,
            Color ambientColor)
        {
            if (profile == null)
            {
                return;
            }

            Color starColor = ResolveStarColor(profile, state);
            Shader.SetGlobalVector(StarPositionId, new Vector4(
                state.StarPosition.x,
                state.StarPosition.y,
                state.StarPosition.z,
                1f));
            Shader.SetGlobalVector(StarDirectionId, new Vector4(
                state.DirectionToStar.x,
                state.DirectionToStar.y,
                state.DirectionToStar.z,
                0f));
            Shader.SetGlobalColor(StarColorId, starColor);
            Shader.SetGlobalFloat(StarIntensityId, state.Intensity);
            Shader.SetGlobalColor(AmbientColorId, ambientColor);
        }
    }
}
