using UnityEngine;
using UnityEngine.Rendering;

namespace Farion.Rendering.Lighting
{
    [CreateAssetMenu(menuName = "Farion/Rendering/Lighting/Celestial Lighting Profile", fileName = "SO_CelestialLightingProfile")]
    public sealed class CelestialLightingProfile : ScriptableObject
    {
        [Header("Main Light")]
        [SerializeField] Color lightColor = new(1f, 0.985f, 0.95f, 1f);
        [SerializeField] bool useColorTemperature = true;
        [Min(1000f)]
        [SerializeField] float colorTemperature = 5778f;
        [Min(0.001f)]
        [SerializeField] float referenceDistance = 350f;
        [Min(0f)]
        [SerializeField] float referenceIntensity = 1.1f;
        [SerializeField] bool useInverseSquareFalloff;
        [Tooltip("Flip only if the lit side appears opposite the authored star direction.")]
        [SerializeField] bool invertLightDirection;
        [Min(0.001f)]
        [SerializeField] float minimumFalloffDistance = 50f;
        [Min(0f)]
        [SerializeField] float minimumIntensity;
        [Min(0f)]
        [SerializeField] float maximumIntensity = 4f;

        [Header("Shadows")]
        [SerializeField] LightShadows shadows = LightShadows.Soft;
        [Range(0f, 1f)]
        [SerializeField] float shadowStrength = 0.86f;
        [Min(0.001f)]
        [SerializeField] float shadowNearPlane = 0.2f;
        [Range(0f, 10f)]
        [SerializeField] float directionalShadowAngle = 0.55f;
        [Tooltip("The scene light holds its rotation until the star has moved this many degrees. Zero (default) follows the star continuously; each step lands as a visible hop, so only raise it if shadow edges shimmer even with soft shadows and proper bias.")]
        [Range(0f, 2f)]
        [SerializeField] float shadowDirectionStepDegrees;
        [Min(0f)]
        [SerializeField] float bounceIntensity;

        [Header("Planetshine")]
        [Tooltip("Second light carrying sunlight bounced off the nearby body. Without it a ship flying low over a lit planet stays as dark underneath as it is in deep space.")]
        [SerializeField] bool enablePlanetshine = true;
        [SerializeField] Color planetshineColor = new(0.62f, 0.68f, 0.78f, 1f);
        [Min(0f)]
        [SerializeField] float planetshineIntensity = 0.85f;
        [Tooltip("Fraction of the star's light the surface reflects back.")]
        [Range(0f, 1f)]
        [SerializeField] float planetshineAlbedo = 0.35f;
        [SerializeField] LightShadows planetshineShadows = LightShadows.None;

        [Header("Space Ambient")]
        [SerializeField] bool applyRenderSettings = true;
        [SerializeField] AmbientMode ambientMode = AmbientMode.Flat;
        [SerializeField] Color ambientLight = new(0.018f, 0.021f, 0.028f, 1f);
        [Range(0f, 4f)]
        [SerializeField] float ambientIntensity = 1f;
        [SerializeField] bool disableFog = true;

        [Header("Atmospheric Ambient")]
        [Tooltip("Inside an atmosphere, replace the flat space ambient with a trilight gradient derived from the body's scattering profile, so shadows fill with sky light instead of a constant colour.")]
        [SerializeField] bool deriveAmbientFromAtmosphere = true;
        [Tooltip("Fraction of the star intensity the day sky contributes as ambient.")]
        [Min(0f)]
        [SerializeField] float atmosphericAmbientIntensity = 0.32f;
        [Tooltip("Average surface albedo used for the ground half of the ambient gradient.")]
        [SerializeField] Color groundAlbedo = new(0.30f, 0.27f, 0.23f, 1f);
        [Tooltip("Horizon tint blended in while the star sits near the horizon.")]
        [ColorUsage(false, true)]
        [SerializeField] Color duskColor = new(1f, 0.5f, 0.25f, 1f);
        [Tooltip("Shadow strength on airless bodies. Atmospheric bodies blend towards the softer Shadows value with air density.")]
        [Range(0f, 1f)]
        [SerializeField] float airlessShadowStrength = 0.98f;
        [Tooltip("Inside an atmosphere, replace the static space cubemap with an analytic sky cubemap so reflections match the sky instead of deep space.")]
        [SerializeField] bool deriveReflectionFromAtmosphere = true;

        [Header("Environment Reflection")]
        [Tooltip("Specular environment for metals. The star dome skybox reflects near black, "
            + "which collapses every metallic surface to a flat unlit tone.")]
        [SerializeField] Cubemap reflectionCubemap;
        [Range(0f, 2f)]
        [SerializeField] float reflectionIntensity = 1f;

        [Header("Camera Defaults")]
        [SerializeField] bool applyCameraDefaults = true;
        [SerializeField] CameraClearFlags cameraClearFlags = CameraClearFlags.Skybox;
        [SerializeField] Color cameraBackground = Color.black;
        [Min(0.001f)]
        [SerializeField] float nearClipPlane = 0.1f;
        [Min(1f)]
        [Tooltip("Physical scene visibility. Use scaled-space visuals instead of raising this indefinitely.")]
        [SerializeField] float farClipPlane = 50000f;
        [SerializeField] bool allowHdr = true;

        public Color LightColor => lightColor;
        public bool UseColorTemperature => useColorTemperature;
        public float ColorTemperature => colorTemperature;
        public bool InvertLightDirection => invertLightDirection;
        public LightShadows Shadows => shadows;
        public float ShadowStrength => shadowStrength;
        public float ShadowNearPlane => shadowNearPlane;
        public float DirectionalShadowAngle => directionalShadowAngle;
        public float ShadowDirectionStepDegrees => shadowDirectionStepDegrees;
        public float BounceIntensity => bounceIntensity;
        public bool EnablePlanetshine => enablePlanetshine;
        public Color PlanetshineColor => planetshineColor;
        public float PlanetshineIntensity => planetshineIntensity;
        public float PlanetshineAlbedo => planetshineAlbedo;
        public LightShadows PlanetshineShadows => planetshineShadows;
        public bool ApplyRenderSettings => applyRenderSettings;

        public float EvaluatePlanetshineIntensity(
            float bodyRadius,
            float centerDistance,
            float starFacing,
            float starIntensity)
        {
            if (!enablePlanetshine || bodyRadius <= 0.0001f || centerDistance <= bodyRadius)
            {
                return 0f;
            }

            float radiusRatio = bodyRadius / centerDistance;
            float coverage = radiusRatio * radiusRatio;
            float phase = Mathf.Clamp01(starFacing * 0.5f + 0.5f);
            return planetshineIntensity * planetshineAlbedo * coverage * phase * starIntensity;
        }
        public AmbientMode AmbientMode => ambientMode;
        public Color AmbientLight => ambientLight * ambientIntensity;
        public bool DisableFog => disableFog;
        public bool DeriveAmbientFromAtmosphere => deriveAmbientFromAtmosphere;
        public float AtmosphericAmbientIntensity => atmosphericAmbientIntensity;
        public Color GroundAlbedo => groundAlbedo;
        public Color DuskColor => duskColor;
        public float AirlessShadowStrength => airlessShadowStrength;
        public bool DeriveReflectionFromAtmosphere => deriveReflectionFromAtmosphere;
        public Cubemap ReflectionCubemap => reflectionCubemap;
        public float ReflectionIntensity => Mathf.Max(0f, reflectionIntensity);
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
            planetshineIntensity = Mathf.Max(0f, planetshineIntensity);
            planetshineAlbedo = Mathf.Clamp01(planetshineAlbedo);
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
