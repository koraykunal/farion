using UnityEngine;
using UnityEngine.Rendering;

namespace Farion.Rendering.Lighting
{
    [CreateAssetMenu(menuName = "Farion/Rendering/Celestial Lighting Profile", fileName = "SO_CelestialLightingProfile")]
    public sealed class CelestialLightingProfile : ScriptableObject
    {
        [Header("Main Light")]
        [SerializeField] Color lightColor = new(1f, 0.96f, 0.86f, 1f);
        [SerializeField] bool useColorTemperature = true;
        [Min(1000f)]
        [SerializeField] float colorTemperature = 5778f;
        [Min(0.001f)]
        [SerializeField] float referenceDistance = 350f;
        [Min(0f)]
        [SerializeField] float referenceIntensity = 6f;
        [SerializeField] bool useInverseSquareFalloff;
        [Tooltip("Flip only if the lit side appears opposite the authored star direction.")]
        [SerializeField] bool invertLightDirection;
        [Min(0.001f)]
        [SerializeField] float minimumFalloffDistance = 50f;
        [Min(0f)]
        [SerializeField] float minimumIntensity = 1.25f;
        [Min(0f)]
        [SerializeField] float maximumIntensity = 12f;

        [Header("Shadows")]
        [SerializeField] LightShadows shadows = LightShadows.Soft;
        [Range(0f, 1f)]
        [SerializeField] float shadowStrength = 0.9f;
        [Min(0.001f)]
        [SerializeField] float shadowNearPlane = 0.2f;
        [Range(0f, 10f)]
        [SerializeField] float directionalShadowAngle = 0.55f;
        [Min(0f)]
        [SerializeField] float bounceIntensity;

        [Header("Space Ambient")]
        [SerializeField] bool applyRenderSettings = true;
        [SerializeField] AmbientMode ambientMode = AmbientMode.Flat;
        [SerializeField] Color ambientLight = new(0.04f, 0.045f, 0.055f, 1f);
        [Range(0f, 4f)]
        [SerializeField] float ambientIntensity = 1f;
        [SerializeField] bool disableFog = true;

        [Header("Camera Defaults")]
        [SerializeField] bool applyCameraDefaults = true;
        [SerializeField] CameraClearFlags cameraClearFlags = CameraClearFlags.Skybox;
        [SerializeField] Color cameraBackground = Color.black;
        [Min(0.001f)]
        [SerializeField] float nearClipPlane = 0.05f;
        [Min(1f)]
        [SerializeField] float farClipPlane = 5000f;
        [SerializeField] bool allowHdr = true;

        public Color LightColor => lightColor;
        public bool UseColorTemperature => useColorTemperature;
        public float ColorTemperature => colorTemperature;
        public bool InvertLightDirection => invertLightDirection;
        public LightShadows Shadows => shadows;
        public float ShadowStrength => shadowStrength;
        public float ShadowNearPlane => shadowNearPlane;
        public float DirectionalShadowAngle => directionalShadowAngle;
        public float BounceIntensity => bounceIntensity;
        public bool ApplyRenderSettings => applyRenderSettings;
        public AmbientMode AmbientMode => ambientMode;
        public Color AmbientLight => ambientLight * ambientIntensity;
        public bool DisableFog => disableFog;
        public bool ApplyCameraDefaults => applyCameraDefaults;
        public CameraClearFlags CameraClearFlags => cameraClearFlags;
        public Color CameraBackground => cameraBackground;
        public float NearClipPlane => nearClipPlane;
        public float FarClipPlane => farClipPlane;
        public bool AllowHdr => allowHdr;

        public float EvaluateIntensity(float distance)
        {
            float safeDistance = Mathf.Max(distance, minimumFalloffDistance);
            float intensity = referenceIntensity;

            if (useInverseSquareFalloff)
            {
                float normalizedDistance = referenceDistance / safeDistance;
                intensity *= normalizedDistance * normalizedDistance;
            }

            return Mathf.Clamp(intensity, minimumIntensity, maximumIntensity);
        }

        void OnValidate()
        {
            colorTemperature = Mathf.Max(1000f, colorTemperature);
            referenceDistance = Mathf.Max(0.001f, referenceDistance);
            referenceIntensity = Mathf.Max(0f, referenceIntensity);
            minimumFalloffDistance = Mathf.Max(0.001f, minimumFalloffDistance);
            maximumIntensity = Mathf.Max(minimumIntensity, maximumIntensity);
            shadowNearPlane = Mathf.Max(0.001f, shadowNearPlane);
            nearClipPlane = Mathf.Max(0.001f, nearClipPlane);
            farClipPlane = Mathf.Max(nearClipPlane + 1f, farClipPlane);
        }
    }
}
