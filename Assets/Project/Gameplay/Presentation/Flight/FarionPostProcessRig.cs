using Farion.Gameplay.Flight;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Farion.Gameplay.Presentation.Flight
{
    [DefaultExecutionOrder(345)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public sealed class FarionPostProcessRig : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] SpacecraftMotor motor;

        [Header("Bloom")]
        [SerializeField] bool enableBloom = true;
        [Min(0f)]
        [SerializeField] float bloomThreshold = 1.05f;
        [Min(0f)]
        [SerializeField] float bloomIntensity = 0.55f;
        [Range(0f, 1f)]
        [SerializeField] float bloomScatter = 0.62f;

        [Header("Grade")]
        [SerializeField] bool enableColorGrade = true;
        [Range(-2f, 2f)]
        [SerializeField] float postExposure = 0.05f;
        [Range(-100f, 100f)]
        [SerializeField] float contrast = 6f;
        [Range(-100f, 100f)]
        [SerializeField] float saturation = -4f;

        [Header("Speed Response")]
        [Min(1f)]
        [SerializeField] float referenceSpeed = 200f;
        [Range(0f, 1f)]
        [SerializeField] float restVignette = 0.16f;
        [Range(0f, 1f)]
        [SerializeField] float maximumVignette = 0.42f;
        [Range(0f, 1f)]
        [SerializeField] float maximumChromaticAberration = 0.35f;
        [Min(0f)]
        [SerializeField] float responseSharpness = 5f;

        Volume volume;
        VolumeProfile profile;
        Bloom bloom;
        ColorAdjustments colorAdjustments;
        Tonemapping tonemapping;
        Vignette vignette;
        ChromaticAberration chromaticAberration;
        float responseBlend;

        void OnValidate()
        {
            motor ??= GetComponentInParent<SpacecraftMotor>();
            maximumVignette = Mathf.Max(restVignette, maximumVignette);
        }

        void OnEnable()
        {
            volume = GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            BuildProfile();
            ApplyStaticSettings();
        }

        void OnDisable()
        {
            if (profile != null)
            {
                Destroy(profile);
                profile = null;
            }
        }

        void LateUpdate()
        {
            if (vignette == null)
            {
                return;
            }

            float target = 0f;
            if (motor != null)
            {
                SpacecraftMovementTelemetry telemetry = motor.Telemetry;
                float speed01 = Mathf.Clamp01(telemetry.RelativeSpeed / referenceSpeed);
                target = Mathf.Max(speed01 * speed01, telemetry.BoostBlend * 0.7f);
            }

            responseBlend = Mathf.Lerp(
                responseBlend,
                target,
                1f - Mathf.Exp(-Mathf.Max(0f, responseSharpness) * Time.deltaTime));

            vignette.intensity.value = Mathf.Lerp(restVignette, maximumVignette, responseBlend);
            chromaticAberration.intensity.value = maximumChromaticAberration * responseBlend;
        }

        void BuildProfile()
        {
            if (profile != null)
            {
                return;
            }

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Farion Runtime Post Process";
            profile.hideFlags = HideFlags.HideAndDontSave;

            bloom = profile.Add<Bloom>(true);
            colorAdjustments = profile.Add<ColorAdjustments>(true);
            tonemapping = profile.Add<Tonemapping>(true);
            vignette = profile.Add<Vignette>(true);
            chromaticAberration = profile.Add<ChromaticAberration>(true);
            volume.sharedProfile = profile;
        }

        void ApplyStaticSettings()
        {
            bloom.active = enableBloom;
            bloom.threshold.Override(bloomThreshold);
            bloom.intensity.Override(bloomIntensity);
            bloom.scatter.Override(bloomScatter);

            colorAdjustments.active = enableColorGrade;
            colorAdjustments.postExposure.Override(postExposure);
            colorAdjustments.contrast.Override(contrast);
            colorAdjustments.saturation.Override(saturation);

            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);

            vignette.active = true;
            vignette.intensity.Override(restVignette);
            vignette.smoothness.Override(0.45f);

            chromaticAberration.active = true;
            chromaticAberration.intensity.Override(0f);
        }
    }
}
