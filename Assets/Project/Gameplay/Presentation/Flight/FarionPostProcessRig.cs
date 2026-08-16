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
        const float ResponseVolumePriority = 100f;

        [Header("Source")]
        [SerializeField] SpacecraftMotor motor;
        [Tooltip("Optional. Lets atmospheric entry drive the same response as raw speed.")]
        [SerializeField] SpacecraftAtmosphereInteractor atmosphereInteractor;

        [Header("Speed Response")]
        [Min(1f)]
        [SerializeField] float referenceSpeed = 200f;
        [Range(0f, 1f)]
        [SerializeField] float restVignette = 0.16f;
        [Range(0f, 1f)]
        [SerializeField] float maximumVignette = 0.42f;
        [Range(0f, 1f)]
        [SerializeField] float maximumChromaticAberration = 0.35f;
        [Range(0f, 1f)]
        [SerializeField] float maximumMotionBlur = 0.3f;
        [Range(0f, 2f)]
        [SerializeField] float reentryResponse = 1.15f;
        [Min(0f)]
        [SerializeField] float responseSharpness = 5f;

        Volume volume;
        VolumeProfile profile;
        Vignette vignette;
        ChromaticAberration chromaticAberration;
        MotionBlur motionBlur;
        float responseBlend;

        public void SetMotor(SpacecraftMotor value)
        {
            motor = value;
            atmosphereInteractor = value != null
                ? value.GetComponentInChildren<SpacecraftAtmosphereInteractor>()
                : null;
        }

        void OnValidate()
        {
            motor ??= GetComponentInParent<SpacecraftMotor>();
            maximumVignette = Mathf.Max(restVignette, maximumVignette);
        }

        void OnEnable()
        {
            volume = GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = ResponseVolumePriority;
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

            if (atmosphereInteractor != null)
            {
                target = Mathf.Max(
                    target,
                    atmosphereInteractor.CurrentInteraction.AerodynamicStress * reentryResponse);
            }

            responseBlend = Mathf.Lerp(
                responseBlend,
                target,
                1f - Mathf.Exp(-Mathf.Max(0f, responseSharpness) * Time.deltaTime));

            vignette.intensity.value = Mathf.Lerp(restVignette, maximumVignette, responseBlend);
            chromaticAberration.intensity.value = maximumChromaticAberration * responseBlend;
            motionBlur.intensity.value = maximumMotionBlur * responseBlend;
        }

        void BuildProfile()
        {
            if (profile != null)
            {
                return;
            }

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Farion Flight Response Post Process";
            profile.hideFlags = HideFlags.HideAndDontSave;

            vignette = profile.Add<Vignette>(true);
            chromaticAberration = profile.Add<ChromaticAberration>(true);
            motionBlur = profile.Add<MotionBlur>(true);
            volume.sharedProfile = profile;
        }

        void ApplyStaticSettings()
        {
            vignette.active = true;
            vignette.intensity.Override(restVignette);
            vignette.smoothness.Override(0.45f);

            chromaticAberration.active = true;
            chromaticAberration.intensity.Override(0f);

            motionBlur.active = true;
            motionBlur.mode.Override(MotionBlurMode.CameraAndObjects);
            motionBlur.quality.Override(MotionBlurQuality.High);
            motionBlur.intensity.Override(0f);
            motionBlur.clamp.Override(0.05f);
        }
    }
}
