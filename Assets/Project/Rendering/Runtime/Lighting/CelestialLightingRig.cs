using UnityEngine;
using UnityEngine.Rendering;

namespace Farion.Rendering.Lighting
{
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
        [SerializeField] bool syncLightPositionToSource = true;

        [Header("Camera")]
        [SerializeField] Camera sceneCamera;

        [Header("Runtime")]
        [SerializeField] bool updateInEditMode = true;
        [SerializeField] bool updateEveryFrame = true;

        public CelestialLightingProfile Profile => profile;

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

            if (source != null && directionalLight != null && focus != null)
            {
                ApplyDirectionalLight(source, focus, directionalLight);
            }

            if (source != null)
            {
                CelestialLightingGlobals.Apply(profile, source, directionalLight, focus);
            }

            ApplyRenderSettings(directionalLight);
            ApplyCameraDefaults();
        }

        void ApplyDirectionalLight(CelestialLightSource source, Transform focus, Light directionalLight)
        {
            Vector3 lightTravelDirection = focus.position - source.Position;
            if (lightTravelDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float distance = lightTravelDirection.magnitude;
            directionalLight.type = LightType.Directional;
            if (syncLightPositionToSource)
            {
                directionalLight.transform.position = source.Position;
            }

            Vector3 lightForward = profile.InvertLightDirection
                ? -lightTravelDirection.normalized
                : lightTravelDirection.normalized;
            directionalLight.transform.rotation = Quaternion.LookRotation(lightForward, ResolveStableUp(lightForward));
            directionalLight.color = profile.LightColor;
            directionalLight.useColorTemperature = profile.UseColorTemperature;
            directionalLight.colorTemperature = source.HasRadiationProfile ? source.ColorTemperatureKelvin : profile.ColorTemperature;
            directionalLight.intensity = profile.EvaluateIntensity(distance);
            directionalLight.shadows = profile.Shadows;
            directionalLight.shadowStrength = profile.ShadowStrength;
            directionalLight.shadowNearPlane = profile.ShadowNearPlane;
            directionalLight.shadowAngle = profile.DirectionalShadowAngle;
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

            return transform;
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

    static class CelestialLightingGlobals
    {
        static readonly int StarPositionId = Shader.PropertyToID("_FarionStarPositionWS");
        static readonly int StarColorId = Shader.PropertyToID("_FarionStarColor");
        static readonly int StarIntensityId = Shader.PropertyToID("_FarionStarIntensity");
        static readonly int AmbientColorId = Shader.PropertyToID("_FarionAmbientColor");

        public static void Apply(
            CelestialLightingProfile profile,
            CelestialLightSource source,
            Light directionalLight,
            Transform focus)
        {
            if (profile == null || source == null)
            {
                return;
            }

            float distance = focus != null ? Vector3.Distance(source.Position, focus.position) : 0f;
            float intensity = distance > 0f
                ? profile.EvaluateIntensity(distance)
                : directionalLight != null
                    ? directionalLight.intensity
                    : 1f;

            Color starColor = directionalLight != null ? directionalLight.color : profile.LightColor;
            Shader.SetGlobalVector(StarPositionId, new Vector4(source.Position.x, source.Position.y, source.Position.z, 1f));
            Shader.SetGlobalColor(StarColorId, starColor);
            Shader.SetGlobalFloat(StarIntensityId, intensity);
            Shader.SetGlobalColor(AmbientColorId, profile.AmbientLight);
        }
    }
}
