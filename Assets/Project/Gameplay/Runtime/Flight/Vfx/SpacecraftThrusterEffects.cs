using System;
using UnityEngine;

namespace Farion.Gameplay.Flight
{
    [DefaultExecutionOrder(260)]
    [DisallowMultipleComponent]
    public sealed class SpacecraftThrusterEffects : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] SpacecraftMotor motor;
        [SerializeField] SpacecraftRig rig;

        [Header("Response")]
        [Min(0.01f)]
        [SerializeField] float response = 10f;
        [Range(0f, 1f)]
        [SerializeField] float idleThreshold = 0.02f;
        [Min(0.01f)]
        [SerializeField] float thrustReferenceAcceleration = 18f;
        [Min(1f)]
        [SerializeField] float boostIntensityMultiplier = 1.35f;

        [Header("Emitters")]
        [SerializeField] ThrusterEmitter[] emitters = Array.Empty<ThrusterEmitter>();

        Vector3 localTranslationInput;
        Vector3 localRotationInput;
        SpacecraftThrusterCommand thrusterCommand;
        float normalizedThrust;
        float boostBlend;

        public Vector3 LocalTranslationInput => localTranslationInput;
        public Vector3 LocalRotationInput => localRotationInput;
        public float NormalizedThrust => normalizedThrust;
        public float BoostBlend => boostBlend;

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
            InitializeEmitters();
        }

        void OnValidate()
        {
            response = Mathf.Max(0.01f, response);
            idleThreshold = Mathf.Clamp01(idleThreshold);
            thrustReferenceAcceleration = Mathf.Max(0.01f, thrustReferenceAcceleration);
            boostIntensityMultiplier = Mathf.Max(1f, boostIntensityMultiplier);
            ResolveReferences();
            InitializeEmitters();
        }

        void Update()
        {
            ResolveReferences();
            SampleSource();
            ApplyEmitters(Time.deltaTime);
        }

        void ResolveReferences()
        {
            if (motor == null)
            {
                motor = GetComponentInParent<SpacecraftMotor>();
            }

            if (rig == null)
            {
                rig = GetComponentInParent<SpacecraftRig>();
            }
        }

        void InitializeEmitters()
        {
            if (emitters == null)
            {
                return;
            }

            for (int i = 0; i < emitters.Length; i++)
            {
                emitters[i]?.Initialize();
            }
        }

        void SampleSource()
        {
            if (motor == null)
            {
                localTranslationInput = Vector3.zero;
                localRotationInput = Vector3.zero;
                thrusterCommand = SpacecraftThrusterCommand.None;
                normalizedThrust = 0f;
                boostBlend = 0f;
                return;
            }

            localTranslationInput = Vector3.ClampMagnitude(motor.LastLocalTranslationInput, 1f);
            localRotationInput = Vector3.ClampMagnitude(motor.LastLocalRotationInput, 1f);
            thrusterCommand = motor.CurrentThrusterCommand;
            normalizedThrust = Mathf.Clamp01(Mathf.Max(
                motor.LastThrustAcceleration.magnitude / thrustReferenceAcceleration,
                thrusterCommand.Activity));
            boostBlend = Mathf.Clamp01(motor.CurrentBoostMultiplier - 1f);
        }

        void ApplyEmitters(float deltaTime)
        {
            if (emitters == null)
            {
                return;
            }

            float responseT = response <= 0f ? 1f : 1f - Mathf.Exp(-response * deltaTime);
            for (int i = 0; i < emitters.Length; i++)
            {
                ThrusterEmitter emitter = emitters[i];
                if (emitter == null)
                {
                    continue;
                }

                float targetIntensity = EvaluateEmitterIntensity(emitter.Role) * emitter.IntensityScale;
                if (motor != null && motor.BoostActive && emitter.AllowBoostIntensity)
                {
                    targetIntensity *= boostIntensityMultiplier;
                }

                emitter.Apply(Mathf.Clamp01(targetIntensity), responseT, idleThreshold, boostBlend, deltaTime);
            }
        }

        float EvaluateEmitterIntensity(ThrusterRole role)
        {
            return role switch
            {
                ThrusterRole.MainForward => Mathf.Max(thrusterCommand.Forward, Positive(localTranslationInput.z)),
                ThrusterRole.Reverse => Mathf.Max(thrusterCommand.Reverse, Negative(localTranslationInput.z)),
                ThrusterRole.StrafeLeft => Mathf.Max(thrusterCommand.StrafeLeft, Negative(localTranslationInput.x)),
                ThrusterRole.StrafeRight => Mathf.Max(thrusterCommand.StrafeRight, Positive(localTranslationInput.x)),
                ThrusterRole.Ascend => Mathf.Max(thrusterCommand.Ascend, Positive(localTranslationInput.y)),
                ThrusterRole.Descend => Mathf.Max(thrusterCommand.Descend, Negative(localTranslationInput.y)),
                ThrusterRole.PitchUp => Mathf.Max(thrusterCommand.PitchUp, Positive(localRotationInput.x)),
                ThrusterRole.PitchDown => Mathf.Max(thrusterCommand.PitchDown, Negative(localRotationInput.x)),
                ThrusterRole.YawLeft => Mathf.Max(thrusterCommand.YawLeft, Negative(localRotationInput.y)),
                ThrusterRole.YawRight => Mathf.Max(thrusterCommand.YawRight, Positive(localRotationInput.y)),
                ThrusterRole.RollLeft => Mathf.Max(thrusterCommand.RollLeft, Negative(localRotationInput.z)),
                ThrusterRole.RollRight => Mathf.Max(thrusterCommand.RollRight, Positive(localRotationInput.z)),
                ThrusterRole.EngineActivity => rig != null ? rig.EngineActivity : normalizedThrust,
                _ => 0f
            };
        }

        static float Positive(float value)
        {
            return Mathf.Clamp01(value);
        }

        static float Negative(float value)
        {
            return Mathf.Clamp01(-value);
        }

        public enum ThrusterRole
        {
            MainForward,
            Reverse,
            StrafeLeft,
            StrafeRight,
            Ascend,
            Descend,
            PitchUp,
            PitchDown,
            YawLeft,
            YawRight,
            RollLeft,
            RollRight,
            EngineActivity
        }

        [Serializable]
        sealed class ThrusterEmitter
        {
            [SerializeField] string label;
            [SerializeField] ThrusterRole role;
            [Min(0f)]
            [SerializeField] float intensityScale = 1f;
            [SerializeField] bool allowBoostIntensity = true;
            [SerializeField] AnimationCurve intensityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            [Header("Payload")]
            [SerializeField] ParticleSystem[] particles = Array.Empty<ParticleSystem>();
            [SerializeField] TimedThrusterBeam[] timedBeams = Array.Empty<TimedThrusterBeam>();
            [SerializeField] Light[] lights = Array.Empty<Light>();
            [SerializeField] Renderer[] glowRenderers = Array.Empty<Renderer>();
            [SerializeField] AudioSource[] audioSources = Array.Empty<AudioSource>();
            [SerializeField] Transform[] lengthScaleTargets = Array.Empty<Transform>();

            [Header("Particle Multipliers")]
            [Min(0f)]
            [SerializeField] float maxEmissionMultiplier = 1f;
            [Min(0f)]
            [SerializeField] float maxStartSpeedMultiplier = 1f;
            [Min(0f)]
            [SerializeField] float maxStartSizeMultiplier = 1f;
            [Min(0f)]
            [SerializeField] float maxStartLifetimeMultiplier = 1f;
            [SerializeField] bool stopParticlesWhenIdle = true;

            [Header("Light")]
            [Min(0f)]
            [SerializeField] float maxLightIntensityMultiplier = 1f;

            [Header("Audio")]
            [Range(0f, 1f)]
            [SerializeField] float maxVolume = 0.45f;
            [Min(0f)]
            [SerializeField] float idlePitch = 0.85f;
            [Min(0f)]
            [SerializeField] float maxPitch = 1.25f;

            [Header("Length Scale")]
            [SerializeField] Vector3 idleScale = Vector3.one;
            [SerializeField] Vector3 activeScale = new(1f, 1f, 1.8f);

            ParticleDefaults[] particleDefaults = Array.Empty<ParticleDefaults>();
            float[] lightDefaults = Array.Empty<float>();
            MaterialPropertyBlock glowPropertyBlock;
            float currentIntensity;

            public ThrusterRole Role => role;
            public float IntensityScale => intensityScale;
            public bool AllowBoostIntensity => allowBoostIntensity;

            public void Initialize()
            {
                intensityScale = Mathf.Max(0f, intensityScale);
                if (intensityCurve == null || intensityCurve.length == 0)
                {
                    intensityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                }

                maxEmissionMultiplier = maxEmissionMultiplier <= 0f ? 1f : maxEmissionMultiplier;
                maxStartSpeedMultiplier = maxStartSpeedMultiplier <= 0f ? 1f : maxStartSpeedMultiplier;
                maxStartSizeMultiplier = maxStartSizeMultiplier <= 0f ? 1f : maxStartSizeMultiplier;
                maxStartLifetimeMultiplier = maxStartLifetimeMultiplier <= 0f ? 1f : maxStartLifetimeMultiplier;
                maxLightIntensityMultiplier = Mathf.Max(0f, maxLightIntensityMultiplier);
                maxVolume = Mathf.Clamp01(maxVolume);
                idlePitch = idlePitch <= 0f ? 0.85f : idlePitch;
                maxPitch = maxPitch <= 0f ? 1.25f : Mathf.Max(idlePitch, maxPitch);
                if (idleScale == Vector3.zero)
                {
                    idleScale = Vector3.one;
                }

                if (activeScale == Vector3.zero)
                {
                    activeScale = new Vector3(1f, 1f, 1.8f);
                }

                CacheParticleDefaults();
                CacheLightDefaults();
                InitializeTimedBeams();
            }

            public void Apply(float targetIntensity, float responseT, float idleThreshold, float sourceBoostBlend, float deltaTime)
            {
                currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, responseT);
                if (currentIntensity < 0.0001f)
                {
                    currentIntensity = 0f;
                }

                float curvedIntensity = intensityCurve != null
                    ? Mathf.Clamp01(intensityCurve.Evaluate(currentIntensity))
                    : currentIntensity;

                ApplyParticles(curvedIntensity, idleThreshold);
                ApplyTimedBeams(curvedIntensity, sourceBoostBlend, deltaTime);
                ApplyLights(curvedIntensity);
                ApplyGlowRenderers(curvedIntensity);
                ApplyAudio(curvedIntensity);
                ApplyLengthScale(curvedIntensity);
            }

            void CacheParticleDefaults()
            {
                if (particles == null)
                {
                    particleDefaults = Array.Empty<ParticleDefaults>();
                    return;
                }

                if (particleDefaults.Length == particles.Length)
                {
                    return;
                }

                particleDefaults = new ParticleDefaults[particles.Length];
                for (int i = 0; i < particles.Length; i++)
                {
                    ParticleSystem particle = particles[i];
                    if (particle == null)
                    {
                        continue;
                    }

                    ParticleSystem.MainModule main = particle.main;
                    ParticleSystem.EmissionModule emission = particle.emission;
                    particleDefaults[i] = new ParticleDefaults(
                        emission.rateOverTimeMultiplier,
                        main.startSpeedMultiplier,
                        main.startSizeMultiplier,
                        main.startLifetimeMultiplier);
                }
            }

            void CacheLightDefaults()
            {
                if (lights == null)
                {
                    lightDefaults = Array.Empty<float>();
                    return;
                }

                if (lightDefaults.Length == lights.Length)
                {
                    return;
                }

                lightDefaults = new float[lights.Length];
                for (int i = 0; i < lights.Length; i++)
                {
                    lightDefaults[i] = lights[i] != null ? lights[i].intensity : 0f;
                }
            }

            void ApplyParticles(float intensity, float idleThreshold)
            {
                if (particles == null)
                {
                    return;
                }

                CacheParticleDefaults();
                for (int i = 0; i < particles.Length; i++)
                {
                    ParticleSystem particle = particles[i];
                    if (particle == null)
                    {
                        continue;
                    }

                    ParticleDefaults defaults = particleDefaults.Length > i ? particleDefaults[i] : default;
                    ParticleSystem.EmissionModule emission = particle.emission;
                    ParticleSystem.MainModule main = particle.main;

                    emission.rateOverTimeMultiplier = defaults.EmissionRate * Mathf.Lerp(0f, maxEmissionMultiplier, intensity);
                    main.startSpeedMultiplier = defaults.StartSpeed * Mathf.Lerp(0.1f, maxStartSpeedMultiplier, intensity);
                    main.startSizeMultiplier = defaults.StartSize * Mathf.Lerp(0.4f, maxStartSizeMultiplier, intensity);
                    main.startLifetimeMultiplier = defaults.StartLifetime * Mathf.Lerp(0.3f, maxStartLifetimeMultiplier, intensity);

                    bool shouldPlay = intensity > idleThreshold;
                    if (shouldPlay && !particle.isPlaying)
                    {
                        particle.Play(withChildren: true);
                    }
                    else if (!shouldPlay && stopParticlesWhenIdle && particle.isPlaying)
                    {
                        particle.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }

            void ApplyLights(float intensity)
            {
                if (lights == null)
                {
                    return;
                }

                CacheLightDefaults();
                for (int i = 0; i < lights.Length; i++)
                {
                    Light light = lights[i];
                    if (light == null)
                    {
                        continue;
                    }

                    float baseIntensity = lightDefaults.Length > i ? lightDefaults[i] : 0f;
                    light.intensity = baseIntensity * Mathf.Lerp(0f, maxLightIntensityMultiplier, intensity);
                    light.enabled = intensity > 0.001f;
                }
            }

            void InitializeTimedBeams()
            {
                if (timedBeams == null)
                {
                    return;
                }

                for (int i = 0; i < timedBeams.Length; i++)
                {
                    timedBeams[i]?.Initialize();
                }
            }

            void ApplyTimedBeams(float intensity, float sourceBoostBlend, float deltaTime)
            {
                if (timedBeams == null)
                {
                    return;
                }

                for (int i = 0; i < timedBeams.Length; i++)
                {
                    timedBeams[i]?.Apply(intensity, sourceBoostBlend, deltaTime);
                }
            }

            void ApplyGlowRenderers(float intensity)
            {
                if (glowRenderers == null)
                {
                    return;
                }

                glowPropertyBlock ??= new MaterialPropertyBlock();
                Color glowColor = Color.Lerp(
                    new Color(1f, 0.28f, 0.04f, 0f),
                    new Color(1f, 0.82f, 0.38f, 1f),
                    intensity);

                for (int i = 0; i < glowRenderers.Length; i++)
                {
                    Renderer glowRenderer = glowRenderers[i];
                    if (glowRenderer == null)
                    {
                        continue;
                    }

                    glowRenderer.GetPropertyBlock(glowPropertyBlock);
                    glowPropertyBlock.SetColor("_BaseColor", glowColor);
                    glowPropertyBlock.SetColor("_Color", glowColor);
                    glowPropertyBlock.SetColor("_EmissionColor", glowColor * Mathf.Lerp(0f, 3.5f, intensity));
                    glowPropertyBlock.SetFloat("_Alpha", intensity);
                    glowPropertyBlock.SetFloat("_Intensity", Mathf.Lerp(0f, 2.5f, intensity));
                    glowRenderer.SetPropertyBlock(glowPropertyBlock);
                    glowRenderer.enabled = intensity > 0.001f;
                }
            }

            void ApplyAudio(float intensity)
            {
                if (audioSources == null)
                {
                    return;
                }

                for (int i = 0; i < audioSources.Length; i++)
                {
                    AudioSource audioSource = audioSources[i];
                    if (audioSource == null)
                    {
                        continue;
                    }

                    audioSource.volume = Mathf.Lerp(0f, maxVolume, intensity);
                    audioSource.pitch = Mathf.Lerp(idlePitch, maxPitch, intensity);
                    if (intensity > 0.001f && !audioSource.isPlaying)
                    {
                        audioSource.Play();
                    }
                    else if (intensity <= 0.001f && audioSource.isPlaying)
                    {
                        audioSource.Stop();
                    }
                }
            }

            void ApplyLengthScale(float intensity)
            {
                if (lengthScaleTargets == null)
                {
                    return;
                }

                Vector3 scale = Vector3.Lerp(idleScale, activeScale, intensity);
                for (int i = 0; i < lengthScaleTargets.Length; i++)
                {
                    Transform target = lengthScaleTargets[i];
                    if (target != null)
                    {
                        target.localScale = scale;
                    }
                }
            }

            readonly struct ParticleDefaults
            {
                public ParticleDefaults(float emissionRate, float startSpeed, float startSize, float startLifetime)
                {
                    EmissionRate = emissionRate;
                    StartSpeed = startSpeed;
                    StartSize = startSize;
                    StartLifetime = startLifetime;
                }

                public float EmissionRate { get; }
                public float StartSpeed { get; }
                public float StartSize { get; }
                public float StartLifetime { get; }
            }

            [Serializable]
            sealed class TimedThrusterBeam
            {
                [SerializeField] string label;
                [SerializeField] SpacecraftThrusterBeam beam;
                [Range(0f, 1f)]
                [SerializeField] float activationThreshold = 0.02f;
                [Min(0f)]
                [SerializeField] float delaySeconds;
                [Min(0.01f)]
                [SerializeField] float rampUpSeconds = 0.2f;
                [Min(0f)]
                [SerializeField] float releaseHoldSeconds;
                [Min(0.01f)]
                [SerializeField] float rampDownSeconds = 0.08f;
                [Min(0f)]
                [SerializeField] float intensityScale = 1f;
                [Range(0f, 1f)]
                [SerializeField] float minimumBoostBlend;
                [SerializeField] AnimationCurve rampCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

                float activationTimer;
                float releaseTimer;
                float currentIntensity;
                bool initialized;

                public void Initialize()
                {
                    if (initialized)
                    {
                        return;
                    }

                    Validate();
                    initialized = true;
                }

                public void Apply(float sourceIntensity, float boostBlend, float deltaTime)
                {
                    Validate();
                    float clampedDeltaTime = Mathf.Max(0f, deltaTime);
                    float targetIntensity = CalculateTargetIntensity(sourceIntensity, boostBlend, clampedDeltaTime);
                    float transitionSeconds = targetIntensity >= currentIntensity ? rampUpSeconds : rampDownSeconds;

                    currentIntensity = Mathf.MoveTowards(
                        currentIntensity,
                        targetIntensity,
                        clampedDeltaTime / Mathf.Max(0.01f, transitionSeconds));

                    if (currentIntensity < 0.0001f)
                    {
                        currentIntensity = 0f;
                    }

                    if (beam != null)
                    {
                        beam.SetIntensity(currentIntensity);
                    }
                }

                void Validate()
                {
                    activationThreshold = Mathf.Clamp01(activationThreshold);
                    delaySeconds = Mathf.Max(0f, delaySeconds);
                    rampUpSeconds = Mathf.Max(0.01f, rampUpSeconds);
                    releaseHoldSeconds = Mathf.Max(0f, releaseHoldSeconds);
                    rampDownSeconds = Mathf.Max(0.01f, rampDownSeconds);
                    intensityScale = Mathf.Max(0f, intensityScale);
                    minimumBoostBlend = Mathf.Clamp01(minimumBoostBlend);
                    if (rampCurve == null || rampCurve.length == 0)
                    {
                        rampCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                    }
                }

                float CalculateTargetIntensity(float sourceIntensity, float boostBlend, float deltaTime)
                {
                    if (sourceIntensity > activationThreshold && boostBlend >= minimumBoostBlend)
                    {
                        releaseTimer = 0f;
                        activationTimer += deltaTime;
                        float rampAmount = Mathf.Clamp01((activationTimer - delaySeconds) / rampUpSeconds);
                        float shapedRamp = Mathf.Clamp01(rampCurve.Evaluate(rampAmount));
                        return Mathf.Clamp01(sourceIntensity * intensityScale * shapedRamp);
                    }

                    releaseTimer += deltaTime;
                    if (releaseTimer >= releaseHoldSeconds)
                    {
                        activationTimer = 0f;
                    }

                    if (releaseTimer < releaseHoldSeconds)
                    {
                        return currentIntensity;
                    }

                    return 0f;
                }
            }
        }
    }
}
