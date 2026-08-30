using System;
using Farion.Gameplay.Flight;
using Farion.Core.Numerics;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.VFX;

namespace Farion.Gameplay.Presentation.Flight
{
    [MovedFrom(true, "Farion.Gameplay.Flight", "Farion.Gameplay.Runtime")]
    [DisallowMultipleComponent]
    public sealed class SpacecraftThrusterNozzleVfx : MonoBehaviour
    {
        [Header("Nozzle")]
        [Tooltip("Use -1 for the left main nozzle and 1 for the right main nozzle.")]
        [Range(-1f, 1f)]
        [SerializeField] float sideSign;
        [SerializeField] Transform steeringRoot;
        [Min(0f)]
        [SerializeField] float response = 14f;
        [Min(0f)]
        [SerializeField] float releaseResponse = 6f;
        [Range(0f, 1f)]
        [SerializeField] float minimumVisibleLoad = 0.03f;

        [Header("Ignition Transient")]
        [Min(0f)]
        [SerializeField] float ignitionFlareGain = 0.32f;
        [Min(0.01f)]
        [SerializeField] float ignitionFlareDecay = 4.5f;

        [Header("Directional Load")]
        [Min(0f)]
        [SerializeField] float lateralLoadResponse = 0.18f;
        [Min(0f)]
        [SerializeField] float yawLoadResponse = 0.14f;
        [Min(0f)]
        [SerializeField] float rollLoadResponse = 0.1f;
        [Min(0f)]
        [SerializeField] float verticalLoadResponse = 0.05f;
        [Min(0f)]
        [SerializeField] float minDirectionalMultiplier = 0.65f;
        [Min(0f)]
        [SerializeField] float maxDirectionalMultiplier = 1.35f;

        [Header("Steering")]
        [Min(0f)]
        [SerializeField] float maxSteeringAngle = 7f;
        [Min(0f)]
        [SerializeField] float steeringResponse = 12f;
        [SerializeField] Vector3 translationSteeringEuler = new Vector3(4f, 5f, 2f);
        [SerializeField] Vector3 rotationSteeringEuler = new Vector3(3f, 4f, 4f);

        [Header("Plume Inertia")]
        [Tooltip("How quickly the exhaust column catches up with the nozzle after a hard rotation.")]
        [Min(0.01f)]
        [SerializeField] float plumeBendResponse = 5.5f;
        [Tooltip("Maximum lateral lag of the plume tail, in nozzle radii before per-layer gain.")]
        [Range(0f, 1f)]
        [SerializeField] float maxPlumeBend = 0.45f;

        [Header("Mesh Layers")]
        [SerializeField] SpacecraftThrusterMeshLayer coreGlow;
        [SerializeField] SpacecraftThrusterMeshLayer innerPlasma;
        [SerializeField] SpacecraftThrusterMeshLayer outerPlasma;
        [SerializeField] SpacecraftThrusterMeshLayer shockDiamonds;
        [SerializeField] SpacecraftThrusterMeshLayer distortion;

        [Header("VFX Graphs")]
        [SerializeField] VisualEffect sparksGraph;
        [SerializeField] VisualEffect smokeGraph;
        [Range(0f, 1f)]
        [SerializeField] float smokeAtmosphereThreshold = 0.02f;
        [Range(0f, 1f)]
        [SerializeField] float sparksBoostThreshold = 0.7f;
        [Min(0f)]
        [SerializeField] float groundDustGain = 2.4f;
        [Range(0f, 1f)]
        [SerializeField] float groundDustFloor = 0.35f;

        [Header("VFX Graph Tuning")]
        [SerializeField] ThrusterGraphTuning sparksGraphTuning = ThrusterGraphTuning.Sparks();
        [SerializeField] ThrusterGraphTuning smokeGraphTuning = ThrusterGraphTuning.Smoke();

        [Header("Light")]
        [SerializeField] Light thrusterLight;
        [Min(0f)]
        [SerializeField] float maxLightIntensity = 40f;
        [Min(0f)]
        [SerializeField] float maxLightRange = 7f;
        [Min(0.1f)]
        [SerializeField] float lightPower = 1.7f;
        [Tooltip("Extra range in vacuum, where the plume runs longer than at sea level.")]
        [Range(1f, 2f)]
        [SerializeField] float vacuumLightRangeScale = 1.25f;
        [SerializeField] Color idleLightColor = new Color(0.18f, 0.85f, 1f, 1f);
        [SerializeField] Color boostLightColor = new Color(0.76f, 0.9f, 1f, 1f);
        [Tooltip("Matches the plume overheat colour so the hull wash stays consistent with the exhaust.")]
        [SerializeField] Color overheatLightColor = new Color(1f, 0.42f, 0.1f, 1f);

        [Header("Nozzle Heat Glow")]
        [Tooltip("Optional. Point this at the engine-bell renderers to drive _EmissionColor from thermal soak.")]
        [SerializeField] Renderer[] heatGlowRenderers = Array.Empty<Renderer>();
        [ColorUsage(false, true)]
        [SerializeField] Color heatGlowColor = new Color(2.6f, 0.42f, 0.06f, 1f);
        [Range(0.1f, 6f)]
        [SerializeField] float heatGlowPower = 2.2f;
        [Min(0f)]
        [SerializeField] float heatGlowResponse = 1.6f;

        Quaternion initialSteeringRotation = Quaternion.identity;
        Quaternion laggedWorldRotation = Quaternion.identity;
        Vector3 currentSteeringEuler;
        Vector3 currentPlumeBend;
        float currentNozzleLoad;
        float currentSideLoad;
        float currentIgnitionFlare;
        float currentHeatGlow;
        MaterialPropertyBlock heatGlowProperties;
        bool initialized;
        bool plumeBendTracked;

        public float NozzleLoad => currentNozzleLoad;
        public float SideLoad => currentSideLoad;
        public float IgnitionFlare => currentIgnitionFlare;
        public Vector3 PlumeBend => currentPlumeBend;

        void Reset()
        {
            steeringRoot = transform;
            thrusterLight = GetComponentInChildren<Light>(true);
            AutoAssignMeshLayers();
        }

        void Awake()
        {
            Initialize();
        }

        void OnValidate()
        {
            Validate();
        }

        public void Initialize()
        {
            Validate();
            if (steeringRoot == null)
            {
                steeringRoot = transform;
            }

            AutoAssignMeshLayers();
            initialSteeringRotation = steeringRoot.localRotation;
            plumeBendTracked = false;
            currentPlumeBend = Vector3.zero;
            initialized = true;
        }

        void OnDisable()
        {
            coreGlow?.ClearRuntimeState();
            innerPlasma?.ClearRuntimeState();
            outerPlasma?.ClearRuntimeState();
            shockDiamonds?.ClearRuntimeState();
            distortion?.ClearRuntimeState();
            currentIgnitionFlare = 0f;
            currentHeatGlow = 0f;
            currentPlumeBend = Vector3.zero;
            plumeBendTracked = false;
            ClearHeatGlow();
        }

        void ClearHeatGlow()
        {
            if (heatGlowRenderers == null || heatGlowRenderers.Length == 0)
            {
                return;
            }

            heatGlowProperties ??= new MaterialPropertyBlock();
            for (int i = 0; i < heatGlowRenderers.Length; i++)
            {
                Renderer target = heatGlowRenderers[i];
                if (target == null)
                {
                    continue;
                }

                target.GetPropertyBlock(heatGlowProperties);
                heatGlowProperties.SetColor(HeatGlowIds.EmissionColor, Color.black);
                target.SetPropertyBlock(heatGlowProperties);
            }
        }

        public void ApplyFrame(SpacecraftThrusterVfxFrame frame, float deltaTime)
        {
            if (!initialized)
            {
                Initialize();
            }

            float clampedDeltaTime = Mathf.Max(0f, deltaTime);
            float targetSideLoad = CalculateSideLoad(frame);
            float multiplier = Mathf.Clamp(
                1f + targetSideLoad + Mathf.Abs(frame.LocalTranslation.y) * verticalLoadResponse,
                minDirectionalMultiplier,
                maxDirectionalMultiplier);
            float targetNozzleLoad = Mathf.Clamp01(frame.Throttle * multiplier);
            if (targetNozzleLoad > 0.001f)
            {
                targetNozzleLoad = Mathf.Max(targetNozzleLoad, minimumVisibleLoad);
            }

            float previousNozzleLoad = currentNozzleLoad;
            float loadResponse = targetNozzleLoad >= currentNozzleLoad ? response : releaseResponse;
            currentNozzleLoad = FarionMath.Smooth(currentNozzleLoad, targetNozzleLoad, loadResponse, clampedDeltaTime);
            currentSideLoad = FarionMath.Smooth(currentSideLoad, targetSideLoad, response, clampedDeltaTime);
            if (currentNozzleLoad < 0.0001f)
            {
                currentNozzleLoad = 0f;
            }

            UpdateIgnitionFlare(previousNozzleLoad, clampedDeltaTime);
            ApplySteering(frame, clampedDeltaTime);
            UpdatePlumeBend(clampedDeltaTime);
            ApplyMeshLayers(frame, clampedDeltaTime);
            ApplyGraphs(frame, clampedDeltaTime);
            ApplyLight(frame);
            ApplyHeatGlow(frame, clampedDeltaTime);
        }

        void UpdateIgnitionFlare(float previousNozzleLoad, float deltaTime)
        {
            currentIgnitionFlare = AdvanceIgnitionFlare(
                currentIgnitionFlare,
                currentNozzleLoad - previousNozzleLoad,
                deltaTime,
                ignitionFlareGain,
                ignitionFlareDecay);
        }

        internal static float AdvanceIgnitionFlare(
            float currentFlare,
            float loadDelta,
            float deltaTime,
            float gain,
            float decay)
        {
            float decayed = Mathf.Max(0f, currentFlare - decay * Mathf.Max(0f, deltaTime));
            if (deltaTime <= 0f || loadDelta <= 0f)
            {
                return decayed;
            }

            return Mathf.Clamp01(Mathf.Max(decayed, loadDelta / deltaTime * gain));
        }

        void UpdatePlumeBend(float deltaTime)
        {
            Transform plumeRoot = steeringRoot != null ? steeringRoot : transform;
            Quaternion currentRotation = plumeRoot.rotation;
            if (!plumeBendTracked)
            {
                laggedWorldRotation = currentRotation;
                plumeBendTracked = true;
            }

            laggedWorldRotation = Quaternion.Slerp(
                laggedWorldRotation,
                currentRotation,
                FarionMath.SmoothFactor(plumeBendResponse, deltaTime));

            Vector3 laggedAxis = plumeRoot.InverseTransformDirection(laggedWorldRotation * Vector3.forward);
            currentPlumeBend = CalculatePlumeBend(laggedAxis, currentNozzleLoad, maxPlumeBend);
        }

        internal static Vector3 CalculatePlumeBend(
            Vector3 laggedLocalAxis,
            float nozzleLoad,
            float maxBend)
        {
            return Vector3.ClampMagnitude(
                new Vector3(laggedLocalAxis.x, laggedLocalAxis.y, 0f) * Mathf.Clamp01(nozzleLoad),
                Mathf.Max(0f, maxBend));
        }

        void Validate()
        {
            sideSign = Mathf.Clamp(sideSign, -1f, 1f);
            response = Mathf.Max(0f, response);
            releaseResponse = Mathf.Max(0f, releaseResponse);
            ignitionFlareGain = Mathf.Max(0f, ignitionFlareGain);
            ignitionFlareDecay = Mathf.Max(0.01f, ignitionFlareDecay);
            minimumVisibleLoad = Mathf.Clamp01(minimumVisibleLoad);
            lateralLoadResponse = Mathf.Max(0f, lateralLoadResponse);
            yawLoadResponse = Mathf.Max(0f, yawLoadResponse);
            rollLoadResponse = Mathf.Max(0f, rollLoadResponse);
            verticalLoadResponse = Mathf.Max(0f, verticalLoadResponse);
            minDirectionalMultiplier = Mathf.Max(0f, minDirectionalMultiplier);
            maxDirectionalMultiplier = Mathf.Max(minDirectionalMultiplier, maxDirectionalMultiplier);
            maxSteeringAngle = Mathf.Max(0f, maxSteeringAngle);
            steeringResponse = Mathf.Max(0f, steeringResponse);
            plumeBendResponse = Mathf.Max(0.01f, plumeBendResponse);
            maxPlumeBend = Mathf.Clamp01(maxPlumeBend);
            smokeAtmosphereThreshold = Mathf.Clamp01(smokeAtmosphereThreshold);
            sparksBoostThreshold = Mathf.Clamp01(sparksBoostThreshold);
            groundDustGain = Mathf.Max(0f, groundDustGain);
            groundDustFloor = Mathf.Clamp01(groundDustFloor);
            heatGlowPower = Mathf.Clamp(heatGlowPower, 0.1f, 6f);
            heatGlowResponse = Mathf.Max(0f, heatGlowResponse);
            sparksGraphTuning?.Validate();
            smokeGraphTuning?.Validate();
            maxLightIntensity = Mathf.Max(0f, maxLightIntensity);
            maxLightRange = Mathf.Max(0f, maxLightRange);
            lightPower = Mathf.Max(0.1f, lightPower);
            vacuumLightRangeScale = Mathf.Clamp(vacuumLightRangeScale, 1f, 2f);
        }

        void AutoAssignMeshLayers()
        {
            SpacecraftThrusterMeshLayer[] layers =
                GetComponentsInChildren<SpacecraftThrusterMeshLayer>(true);
            for (int i = 0; i < layers.Length; i++)
            {
                SpacecraftThrusterMeshLayer layer = layers[i];
                if (layer == null)
                {
                    continue;
                }

                switch (layer.LayerKind)
                {
                    case SpacecraftThrusterMeshLayerKind.CoreGlow:
                        coreGlow ??= layer;
                        break;
                    case SpacecraftThrusterMeshLayerKind.InnerPlasma:
                        innerPlasma ??= layer;
                        break;
                    case SpacecraftThrusterMeshLayerKind.OuterPlasma:
                        outerPlasma ??= layer;
                        break;
                    case SpacecraftThrusterMeshLayerKind.ShockDiamonds:
                        shockDiamonds ??= layer;
                        break;
                    case SpacecraftThrusterMeshLayerKind.Distortion:
                        distortion ??= layer;
                        break;
                }
            }
        }

        float CalculateSideLoad(SpacecraftThrusterVfxFrame frame)
        {
            if (Mathf.Approximately(sideSign, 0f))
            {
                return 0f;
            }

            float demand =
                frame.LocalTranslation.x * lateralLoadResponse +
                frame.LocalRotation.y * yawLoadResponse +
                frame.LocalRotation.z * rollLoadResponse;
            return Mathf.Clamp(demand * sideSign, -1f, 1f);
        }

        void ApplySteering(SpacecraftThrusterVfxFrame frame, float deltaTime)
        {
            if (steeringRoot == null)
            {
                return;
            }

            Vector3 targetSteering = CalculateTargetSteering(frame) * Mathf.SmoothStep(0f, 1f, currentNozzleLoad);
            float responseT = FarionMath.SmoothFactor(steeringResponse, deltaTime);
            currentSteeringEuler = Vector3.Lerp(currentSteeringEuler, targetSteering, responseT);
            steeringRoot.localRotation = initialSteeringRotation * Quaternion.Euler(currentSteeringEuler);
        }

        Vector3 CalculateTargetSteering(SpacecraftThrusterVfxFrame frame)
        {
            if (maxSteeringAngle <= 0f)
            {
                return Vector3.zero;
            }

            Vector3 translation = frame.LocalTranslation;
            Vector3 rotation = frame.LocalRotation;
            Vector3 steering = new Vector3(
                -translation.y * translationSteeringEuler.x + rotation.x * rotationSteeringEuler.x,
                translation.x * translationSteeringEuler.y + rotation.y * rotationSteeringEuler.y,
                sideSign * translation.x * translationSteeringEuler.z + sideSign * rotation.z * rotationSteeringEuler.z);

            return Vector3.ClampMagnitude(steering, maxSteeringAngle);
        }

        void ApplyMeshLayers(SpacecraftThrusterVfxFrame frame, float deltaTime)
        {
            coreGlow?.ApplyFrame(frame, currentNozzleLoad, currentIgnitionFlare, currentPlumeBend, deltaTime);
            innerPlasma?.ApplyFrame(frame, currentNozzleLoad, currentIgnitionFlare, currentPlumeBend, deltaTime);
            outerPlasma?.ApplyFrame(frame, currentNozzleLoad, currentIgnitionFlare, currentPlumeBend, deltaTime);
            shockDiamonds?.ApplyFrame(
                frame,
                currentNozzleLoad * Mathf.Clamp01(frame.AtmosphereDensity * 1.6f),
                currentIgnitionFlare,
                currentPlumeBend,
                deltaTime);
            distortion?.ApplyFrame(frame, currentNozzleLoad, currentIgnitionFlare, currentPlumeBend, deltaTime);
        }

        void ApplyGraphs(SpacecraftThrusterVfxFrame frame, float deltaTime)
        {
            float boostSparks = Mathf.InverseLerp(sparksBoostThreshold, 1f, frame.Boost);
            float sparksLoad = Mathf.Clamp01(boostSparks);
            ApplyGraph(
                sparksGraph,
                sparksGraphTuning,
                frame,
                sparksLoad,
                currentSideLoad,
                shouldPlay: sparksLoad > 0.001f,
                deltaTime);

            float exhaustMedium = frame.AtmosphereDensity > smokeAtmosphereThreshold
                ? frame.AtmosphereDensity
                : 0f;
            float groundDust = frame.GroundProximity *
                Mathf.Lerp(groundDustFloor, 1f, frame.AtmosphereDensity) *
                groundDustGain;
            float smokeLoad = Mathf.Clamp01(
                currentNozzleLoad * (exhaustMedium + groundDust));
            ApplyGraph(
                smokeGraph,
                smokeGraphTuning,
                frame,
                smokeLoad,
                currentSideLoad,
                shouldPlay: smokeLoad > 0.001f,
                deltaTime);
        }

        void ApplyGraph(
            VisualEffect graph,
            ThrusterGraphTuning tuning,
            SpacecraftThrusterVfxFrame frame,
            float load,
            float sideLoad,
            bool shouldPlay,
            float deltaTime)
        {
            if (graph == null)
            {
                return;
            }

            SetFloat(graph, VfxIds.Throttle, load);
            SetFloat(graph, VfxIds.ShipThrottle, frame.Throttle);
            SetFloat(graph, VfxIds.NozzleLoad, load);
            SetFloat(graph, VfxIds.SideLoad, sideLoad);
            SetFloat(graph, VfxIds.Boost, frame.Boost);
            SetFloat(graph, VfxIds.Heat, frame.Heat);
            SetFloat(graph, VfxIds.AtmosphereDensity, frame.AtmosphereDensity);
            SetFloat(graph, VfxIds.GroundProximity, frame.GroundProximity);
            SetFloat(graph, VfxIds.RelativeSpeed, frame.RelativeSpeed);
            SetVector3(graph, VfxIds.LocalTranslation, frame.LocalTranslation);
            SetVector3(graph, VfxIds.LocalRotation, frame.LocalRotation);
            SetVector3(graph, VfxIds.LocalLinearAcceleration, frame.LocalLinearAcceleration);
            SetVector3(graph, VfxIds.LocalAngularAcceleration, frame.LocalAngularAcceleration);
            SetBool(graph, VfxIds.InAtmosphere, frame.InAtmosphere);
            SetBool(graph, VfxIds.BoostActive, frame.Boost > 0.001f);

            tuning?.Apply(graph, load, frame.Boost, frame.Heat, frame.AtmosphereDensity);
            tuning?.UpdatePlayback(graph, shouldPlay, deltaTime);
        }

        void ApplyLight(SpacecraftThrusterVfxFrame frame)
        {
            if (thrusterLight == null)
            {
                return;
            }

            float lightLoad = Mathf.Pow(Mathf.Clamp01(currentNozzleLoad), lightPower);
            thrusterLight.enabled = lightLoad > 0.001f;
            thrusterLight.intensity = maxLightIntensity *
                lightLoad *
                Mathf.Lerp(1f, 1.35f, frame.Boost) *
                (1f + currentIgnitionFlare * 0.9f);
            thrusterLight.range = maxLightRange *
                Mathf.Lerp(0.35f, 1f, lightLoad) *
                Mathf.Lerp(vacuumLightRangeScale, 1f, frame.AtmosphereDensity);

            Color plumeColor = Color.Lerp(
                idleLightColor,
                boostLightColor,
                Mathf.Clamp01(frame.Boost + lightLoad * 0.35f));
            thrusterLight.color = Color.Lerp(
                plumeColor,
                overheatLightColor,
                Mathf.Clamp01(frame.Heat * frame.Heat));
        }

        void ApplyHeatGlow(SpacecraftThrusterVfxFrame frame, float deltaTime)
        {
            if (heatGlowRenderers == null || heatGlowRenderers.Length == 0)
            {
                return;
            }

            float soak = Mathf.Clamp01(
                Mathf.Max(frame.Heat, currentNozzleLoad * 0.55f + frame.Boost * 0.45f));
            currentHeatGlow = FarionMath.Smooth(currentHeatGlow, soak, heatGlowResponse, deltaTime);

            heatGlowProperties ??= new MaterialPropertyBlock();
            Color emission = heatGlowColor * Mathf.Pow(currentHeatGlow, heatGlowPower);
            for (int i = 0; i < heatGlowRenderers.Length; i++)
            {
                Renderer target = heatGlowRenderers[i];
                if (target == null)
                {
                    continue;
                }

                target.GetPropertyBlock(heatGlowProperties);
                heatGlowProperties.SetColor(HeatGlowIds.EmissionColor, emission);
                target.SetPropertyBlock(heatGlowProperties);
            }
        }

        static void SetFloat(VisualEffect graph, int id, float value)
        {
            if (graph.HasFloat(id))
            {
                graph.SetFloat(id, value);
            }
        }

        static void SetVector3(VisualEffect graph, int id, Vector3 value)
        {
            if (graph.HasVector3(id))
            {
                graph.SetVector3(id, value);
            }
        }

        static void SetBool(VisualEffect graph, int id, bool value)
        {
            if (graph.HasBool(id))
            {
                graph.SetBool(id, value);
            }
        }

        [Serializable]
        sealed class ThrusterGraphTuning
        {
            [Tooltip("Drives the graph Rate property. Zero rate precedes Stop so existing particles decay.")]
            [Min(0f)]
            [SerializeField] float spawnRate;
            [Tooltip("Drives the graph Speed property.")]
            [Min(0f)]
            [SerializeField] float speed;
            [Tooltip("Must match the graph particle lifetime. Only used to hold the effect alive while its tail decays.")]
            [Min(0f)]
            [SerializeField] float lifetime;
            [Min(0f)]
            [SerializeField] float boostRateMultiplier = 0.35f;
            [Min(0f)]
            [SerializeField] float heatRateMultiplier = 0.15f;
            [Min(0f)]
            [SerializeField] float atmosphereRateMultiplier = 0.1f;

            [NonSerialized] bool playing;
            [NonSerialized] float tailRemaining;

            public static ThrusterGraphTuning Sparks()
            {
                return new ThrusterGraphTuning
                {
                    spawnRate = 34f,
                    speed = 7.5f,
                    lifetime = 0.55f,
                    boostRateMultiplier = 0.85f,
                    heatRateMultiplier = 0.4f
                };
            }

            public static ThrusterGraphTuning Smoke()
            {
                return new ThrusterGraphTuning
                {
                    spawnRate = 24f,
                    speed = 0.65f,
                    lifetime = 1.8f,
                    atmosphereRateMultiplier = 0.75f
                };
            }

            public void Validate()
            {
                spawnRate = Mathf.Max(0f, spawnRate);
                speed = Mathf.Max(0f, speed);
                lifetime = Mathf.Max(0f, lifetime);
                boostRateMultiplier = Mathf.Max(0f, boostRateMultiplier);
                heatRateMultiplier = Mathf.Max(0f, heatRateMultiplier);
                atmosphereRateMultiplier = Mathf.Max(0f, atmosphereRateMultiplier);
            }

            public void Apply(VisualEffect graph, float load, float boost, float heat, float atmosphereDensity)
            {
                float shapedLoad = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(load));
                float rateMultiplier =
                    1f +
                    Mathf.Clamp01(boost) * boostRateMultiplier +
                    Mathf.Clamp01(heat) * heatRateMultiplier +
                    Mathf.Clamp01(atmosphereDensity) * atmosphereRateMultiplier;
                float rate = spawnRate * shapedLoad * rateMultiplier;
                float speedValue = speed *
                    Mathf.Lerp(0.35f, 1.25f, shapedLoad) *
                    Mathf.Lerp(1f, 1.4f, Mathf.Clamp01(boost));

                SetFloat(graph, VfxIds.Rate, rate);
                SetFloat(graph, VfxIds.Speed, speedValue);
                graph.playRate = Mathf.Lerp(0.75f, 1.35f, Mathf.Clamp01(boost + heat * 0.35f));
            }

            public void UpdatePlayback(VisualEffect graph, bool shouldPlay, float deltaTime)
            {
                if (shouldPlay)
                {
                    tailRemaining = 0f;
                    if (!graph.enabled)
                    {
                        graph.enabled = true;
                        graph.Reinit();
                    }

                    if (!playing)
                    {
                        graph.Play();
                        playing = true;
                    }

                    return;
                }

                if (playing)
                {
                    graph.Stop();
                    playing = false;
                    tailRemaining = Mathf.Max(0.08f, lifetime + 0.08f);
                }

                if (tailRemaining > 0f)
                {
                    tailRemaining = Mathf.Max(
                        0f,
                        tailRemaining - Mathf.Max(0f, deltaTime));
                }

                if (tailRemaining <= 0f && graph.enabled)
                {
                    graph.enabled = false;
                }
            }
        }

        static class HeatGlowIds
        {
            public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        }

        static class VfxIds
        {
            public static readonly int Throttle = Shader.PropertyToID("Throttle");
            public static readonly int ShipThrottle = Shader.PropertyToID("ShipThrottle");
            public static readonly int NozzleLoad = Shader.PropertyToID("NozzleLoad");
            public static readonly int SideLoad = Shader.PropertyToID("SideLoad");
            public static readonly int Boost = Shader.PropertyToID("Boost");
            public static readonly int Heat = Shader.PropertyToID("Heat");
            public static readonly int AtmosphereDensity = Shader.PropertyToID("AtmosphereDensity");
            public static readonly int GroundProximity = Shader.PropertyToID("GroundProximity");
            public static readonly int RelativeSpeed = Shader.PropertyToID("RelativeSpeed");
            public static readonly int LocalTranslation = Shader.PropertyToID("LocalTranslation");
            public static readonly int LocalRotation = Shader.PropertyToID("LocalRotation");
            public static readonly int LocalLinearAcceleration = Shader.PropertyToID("LocalLinearAcceleration");
            public static readonly int LocalAngularAcceleration = Shader.PropertyToID("LocalAngularAcceleration");
            public static readonly int InAtmosphere = Shader.PropertyToID("InAtmosphere");
            public static readonly int BoostActive = Shader.PropertyToID("BoostActive");
            public static readonly int Rate = Shader.PropertyToID("Rate");
            public static readonly int Speed = Shader.PropertyToID("Speed");
        }
    }
}
