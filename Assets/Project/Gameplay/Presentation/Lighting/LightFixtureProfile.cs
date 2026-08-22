using UnityEngine;

namespace Farion.Gameplay.Presentation.Lighting
{
    [CreateAssetMenu(
        menuName = "Farion/Gameplay/Lighting/Light Fixture Profile",
        fileName = "SO_LightFixtureProfile")]
    public sealed class LightFixtureProfile : ScriptableObject
    {
        [Header("Emission")]
        [Tooltip("Kelvin drives the tint instead of a hand-picked RGB. A real lamp is defined " +
            "by its colour temperature, and 4300K reads as a xenon work light rather than the " +
            "flat white a raw colour picker lands on.")]
        [SerializeField] bool useColorTemperature = true;
        [Min(1000f)]
        [SerializeField] float colorTemperature = 4300f;
        [SerializeField] Color color = Color.white;
        [Min(0f)]
        [SerializeField] float intensity = 40f;
        [Min(0f)]
        [SerializeField] float range = 150f;
        [Range(1f, 179f)]
        [SerializeField] float spotAngle = 36f;
        [Range(1f, 179f)]
        [SerializeField] float innerSpotAngle = 20f;

        [Header("Spill")]
        [Tooltip("Second wide, dim lobe standing in for light scattered off the housing. " +
            "Without it the cone ends on a hard rim that immediately reads as a game spotlight.")]
        [Range(0f, 1f)]
        [SerializeField] float spillIntensityRatio = 0.14f;
        [Range(1f, 179f)]
        [SerializeField] float spillAngle = 105f;
        [Range(0f, 1f)]
        [SerializeField] float spillRangeRatio = 0.2f;

        [Header("Shadows")]
        [SerializeField] LightShadows shadows = LightShadows.Soft;
        [Range(0f, 1f)]
        [SerializeField] float shadowStrength = 0.85f;

        [Header("Cookie")]
        [Tooltip("Breaks the perfectly uniform circle. A flawless cone is the clearest tell " +
            "that a light is untouched engine default.")]
        [SerializeField] Texture cookie;

        [Header("Lens")]
        [Tooltip("Bloom in VP_SpaceLighting thresholds at 1.0, so the emissive has to clear " +
            "that in HDR or the bulb itself stays dull while the beam is blinding.")]
        [ColorUsage(false, true)]
        [SerializeField] Color lensEmissionColor = Color.white;
        [Min(0f)]
        [SerializeField] float lensEmissionIntensity = 6f;

        [Header("Ignition")]
        [Tooltip("Lamps do not reach full output instantly, and the ramp is what sells the " +
            "fixture as hardware rather than a boolean.")]
        [Min(0f)]
        [SerializeField] float turnOnSeconds = 0.25f;
        [Min(0f)]
        [SerializeField] float turnOffSeconds = 0.09f;

        [Header("Pulse")]
        [Tooltip("Zero means a steady lamp. Above zero turns the fixture into a beacon.")]
        [Min(0f)]
        [SerializeField] float pulseHz;
        [Range(0f, 1f)]
        [SerializeField] float pulseFloor = 0.08f;
        [Tooltip("Above 1 narrows the bright part of the cycle, which is what makes a beacon " +
            "read as sweeping past rather than fading in and out.")]
        [Min(0.01f)]
        [SerializeField] float pulseSharpness = 2.5f;

        [Header("Fault")]
        [Min(0f)]
        [SerializeField] float faultDecay = 6f;
        [Min(0f)]
        [SerializeField] float faultFrequency = 22f;
        [Range(0f, 1f)]
        [SerializeField] float faultDepth = 0.85f;
        [Tooltip("Fault level held while the fixture is alarming, so breached wiring keeps " +
            "stuttering instead of settling once the impact envelope has decayed.")]
        [Range(0f, 1f)]
        [SerializeField] float alarmFaultLevel = 0.45f;

        public bool UseColorTemperature => useColorTemperature;
        public float ColorTemperature => colorTemperature;
        public Color Color => color;
        public float Intensity => intensity;
        public float Range => range;
        public float SpotAngle => spotAngle;
        public float InnerSpotAngle => innerSpotAngle;
        public float SpillIntensityRatio => spillIntensityRatio;
        public float SpillAngle => spillAngle;
        public float SpillRangeRatio => spillRangeRatio;
        public LightShadows Shadows => shadows;
        public float ShadowStrength => shadowStrength;
        public Texture Cookie => cookie;
        public Color LensEmissionColor => lensEmissionColor;
        public float LensEmissionIntensity => lensEmissionIntensity;
        public float TurnOnSeconds => turnOnSeconds;
        public float TurnOffSeconds => turnOffSeconds;
        public float PulseHz => pulseHz;
        public float PulseFloor => pulseFloor;
        public float AlarmFaultLevel => alarmFaultLevel;

        public float EvaluatePulse(float time)
        {
            if (pulseHz <= 0f)
            {
                return 1f;
            }

            float wave = Mathf.Sin(time * pulseHz * Mathf.PI * 2f) * 0.5f + 0.5f;
            return Mathf.Lerp(pulseFloor, 1f, Mathf.Pow(wave, pulseSharpness));
        }

        public float EvaluateFaultEnvelope(float amplitude, float elapsed)
        {
            return amplitude <= 0f
                ? 0f
                : amplitude * Mathf.Exp(-faultDecay * Mathf.Max(0f, elapsed));
        }

        public float EvaluateFault(float envelope, float time, float seed)
        {
            float clampedEnvelope = Mathf.Clamp01(envelope);
            if (clampedEnvelope <= 0.0001f || faultDepth <= 0f || faultFrequency <= 0f)
            {
                return 1f;
            }

            float step = Mathf.Floor(time * faultFrequency) + seed * 17.13f;
            float hash = Frac(Mathf.Sin(step * 12.9898f) * 43758.5453f);
            return 1f - hash * hash * faultDepth * clampedEnvelope;
        }

        static float Frac(float value)
        {
            return value - Mathf.Floor(value);
        }

        void OnValidate()
        {
            colorTemperature = Mathf.Max(1000f, colorTemperature);
            intensity = Mathf.Max(0f, intensity);
            range = Mathf.Max(0f, range);
            innerSpotAngle = Mathf.Min(innerSpotAngle, spotAngle);
            spillAngle = Mathf.Max(spillAngle, spotAngle);
            turnOnSeconds = Mathf.Max(0f, turnOnSeconds);
            turnOffSeconds = Mathf.Max(0f, turnOffSeconds);
            pulseHz = Mathf.Max(0f, pulseHz);
            pulseSharpness = Mathf.Max(0.01f, pulseSharpness);
            faultDecay = Mathf.Max(0f, faultDecay);
            faultFrequency = Mathf.Max(0f, faultFrequency);
        }
    }
}
