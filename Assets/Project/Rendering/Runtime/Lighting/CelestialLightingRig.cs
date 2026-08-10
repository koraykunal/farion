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

        [Header("Camera")]
        [SerializeField] Camera sceneCamera;

        [Header("Runtime")]
        [SerializeField] bool updateInEditMode = true;
        [SerializeField] bool updateEveryFrame = true;

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
                ApplyRenderSettings(directionalLight);
                ApplyCameraDefaults();
                return;
            }

            if (directionalLight != null)
            {
                ApplyDirectionalLight(state, directionalLight);
            }

            CelestialLightingGlobals.Apply(profile, state);
            ApplyRenderSettings(directionalLight);
            ApplyCameraDefaults();
        }

        void ApplyDirectionalLight(CelestialLightingState state, Light directionalLight)
        {
            directionalLight.type = LightType.Directional;
            if (syncLightPositionToSource)
            {
                directionalLight.transform.position = state.StarPosition;
            }

            Vector3 lightForward = profile.InvertLightDirection
                ? -state.LightTravelDirection
                : state.LightTravelDirection;
            directionalLight.transform.rotation = Quaternion.LookRotation(lightForward, ResolveStableUp(lightForward));
            directionalLight.color = profile.LightColor;
            directionalLight.useColorTemperature = profile.UseColorTemperature;
            directionalLight.colorTemperature = state.ColorTemperature;
            directionalLight.intensity = state.Intensity;
            directionalLight.shadows = profile.Shadows;
            directionalLight.shadowStrength = profile.ShadowStrength;
            directionalLight.shadowNearPlane = profile.ShadowNearPlane;
#if UNITY_EDITOR
            directionalLight.shadowAngle = profile.DirectionalShadowAngle;
#endif
            directionalLight.bounceIntensity = profile.BounceIntensity;
        }

        void ApplyRenderSettings(Light directionalLight)
        {
            if (!profile.ApplyRenderSettings)
            {
                return;
            }

            if (directionalLight != null)
            {
                RenderSettings.sun = directionalLight;
            }

            RenderSettings.ambientMode = profile.AmbientMode;
            RenderSettings.ambientLight = profile.AmbientLight;

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

        public static void Apply(
            CelestialLightingProfile profile,
            CelestialLightingState state)
        {
            if (profile == null)
            {
                return;
            }

            Color starColor = profile.UseColorTemperature
                ? profile.LightColor * Mathf.CorrelatedColorTemperatureToRGB(state.ColorTemperature)
                : profile.LightColor;
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
            Shader.SetGlobalColor(AmbientColorId, profile.AmbientLight);
        }
    }
}
