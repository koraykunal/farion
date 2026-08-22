using UnityEngine;

namespace Farion.Gameplay.Presentation.Lighting
{
    [DisallowMultipleComponent]
    public sealed class LightFixture : MonoBehaviour
    {
        const float StutterWrapSeconds = 10f;
        const float FaultEnvelopeMaxSeconds = 100f;

        [Header("Profiles")]
        [SerializeField] LightFixtureProfile profile;
        [Tooltip("Optional. When an alarm is raised the fixture swaps to this look, which is " +
            "how the same front floodlight can turn red on a breach instead of needing a " +
            "second lamp bolted next to it.")]
        [SerializeField] LightFixtureProfile alarmProfile;

        [Header("Emitters")]
        [SerializeField] Light coreLight;
        [SerializeField] Light spillLight;
        [SerializeField] Renderer lensRenderer;

        [Header("Variation")]
        [Tooltip("Offsets this fixture's pulse phase and stutter pattern. Left and right lamps " +
            "sharing a seed flicker in lockstep, which instantly reads as scripted.")]
        [Range(0f, 1f)]
        [SerializeField] float seed;

        [Header("Startup")]
        [SerializeField] bool startOn;

        [Header("Runtime Debug")]
        [SerializeField, Range(0f, 1f)] float debugOutput;

        bool commandedOn;
        bool alarmActive;
        float level;
        float faultAmplitude;
        float faultTime;
        float pulseTime;
        float stutterTime;
        MaterialPropertyBlock lensProperties;

        public bool IsOn => commandedOn;
        public bool IsAlarming => alarmActive;
        public float Output => debugOutput;

        public LightFixtureProfile ActiveProfile => alarmActive && alarmProfile != null
            ? alarmProfile
            : profile;

        public void SetOn(bool on)
        {
            commandedOn = on;
        }

        public void Toggle()
        {
            commandedOn = !commandedOn;
        }

        public void SetAlarm(bool alarming)
        {
            if (alarmActive == alarming)
            {
                return;
            }

            alarmActive = alarming;
            pulseTime = 0f;
        }

        public void PlayFaultFlicker(float severity)
        {
            LightFixtureProfile active = ActiveProfile;
            float standing = active != null
                ? active.EvaluateFaultEnvelope(faultAmplitude, faultTime)
                : 0f;
            faultAmplitude = Mathf.Max(standing, Mathf.Clamp01(severity));
            faultTime = 0f;
        }

        void Reset()
        {
            AutoAssignReferences();
            seed = Mathf.Repeat(transform.GetSiblingIndex() * 0.37f, 1f);
        }

        void OnValidate()
        {
            AutoAssignReferences();
        }

        void Awake()
        {
            AutoAssignReferences();
            commandedOn = startOn;
            level = startOn ? 1f : 0f;
            pulseTime = 0f;
            stutterTime = seed * StutterWrapSeconds;
        }

        void OnDisable()
        {
            faultAmplitude = 0f;
            Apply(ActiveProfile, 0f);
        }

        void Update()
        {
            LightFixtureProfile active = ActiveProfile;
            if (active == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            bool shouldBeLit = commandedOn || alarmActive;
            float rampSeconds = shouldBeLit ? active.TurnOnSeconds : active.TurnOffSeconds;
            float target = shouldBeLit ? 1f : 0f;
            level = rampSeconds <= 0.0001f
                ? target
                : Mathf.MoveTowards(level, target, deltaTime / rampSeconds);

            Apply(active, level * EvaluatePulse(active, deltaTime) * EvaluateFault(active, deltaTime));
        }

        float EvaluatePulse(LightFixtureProfile active, float deltaTime)
        {
            if (active.PulseHz <= 0f)
            {
                pulseTime = 0f;
                return 1f;
            }

            float period = 1f / active.PulseHz;
            pulseTime = Mathf.Repeat(pulseTime + deltaTime, period);
            return active.EvaluatePulse(pulseTime + seed * period);
        }

        float EvaluateFault(LightFixtureProfile active, float deltaTime)
        {
            faultTime = Mathf.Min(faultTime + deltaTime, FaultEnvelopeMaxSeconds);
            stutterTime = Mathf.Repeat(stutterTime + deltaTime, StutterWrapSeconds);

            float envelope = active.EvaluateFaultEnvelope(faultAmplitude, faultTime);
            if (alarmActive)
            {
                envelope = Mathf.Max(envelope, active.AlarmFaultLevel);
            }

            return active.EvaluateFault(envelope, stutterTime, seed);
        }

        void Apply(LightFixtureProfile active, float output)
        {
            debugOutput = Mathf.Clamp01(output);
            bool lit = active != null && output > 0.0005f;

            if (coreLight != null)
            {
                if (coreLight.enabled != lit)
                {
                    coreLight.enabled = lit;
                }

                if (lit)
                {
                    coreLight.type = LightType.Spot;
                    coreLight.intensity = active.Intensity * output;
                    coreLight.range = active.Range;
                    coreLight.spotAngle = active.SpotAngle;
                    coreLight.innerSpotAngle = active.InnerSpotAngle;
                    coreLight.shadows = active.Shadows;
                    coreLight.shadowStrength = active.ShadowStrength;
                    coreLight.cookie = active.Cookie;
                    ApplyTint(coreLight, active);
                }
            }

            bool spillLit = lit && active.SpillIntensityRatio > 0f;
            if (spillLight != null)
            {
                if (spillLight.enabled != spillLit)
                {
                    spillLight.enabled = spillLit;
                }

                if (spillLit)
                {
                    spillLight.type = LightType.Spot;
                    spillLight.intensity = active.Intensity * active.SpillIntensityRatio * output;
                    spillLight.range = active.Range * active.SpillRangeRatio;
                    spillLight.spotAngle = active.SpillAngle;
                    spillLight.innerSpotAngle = active.SpillAngle * 0.4f;
                    spillLight.shadows = LightShadows.None;
                    spillLight.cookie = null;
                    ApplyTint(spillLight, active);
                }
            }

            ApplyLensEmission(active, output);
        }

        static void ApplyTint(Light target, LightFixtureProfile active)
        {
            target.color = active.Color;
            target.useColorTemperature = active.UseColorTemperature;
            if (active.UseColorTemperature)
            {
                target.colorTemperature = active.ColorTemperature;
            }
        }

        void ApplyLensEmission(LightFixtureProfile active, float output)
        {
            if (lensRenderer == null)
            {
                return;
            }

            Color emission = active == null
                ? Color.black
                : active.LensEmissionColor * (active.LensEmissionIntensity * Mathf.Max(0f, output));

            lensProperties ??= new MaterialPropertyBlock();
            lensRenderer.GetPropertyBlock(lensProperties);
            lensProperties.SetColor(ShaderIds.EmissionColor, emission);
            lensRenderer.SetPropertyBlock(lensProperties);
        }

        void AutoAssignReferences()
        {
            if (coreLight != null && spillLight != null)
            {
                return;
            }

            Light[] lights = GetComponentsInChildren<Light>(true);
            if (coreLight == null && lights.Length > 0)
            {
                coreLight = lights[0];
            }

            if (spillLight == null && lights.Length > 1)
            {
                spillLight = lights[1];
            }
        }

        static class ShaderIds
        {
            internal static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        }
    }
}
