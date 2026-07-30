using System;
using UnityEngine;
using UnityEngine.VFX;

namespace Farion.Gameplay.Flight
{
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
        [Range(0f, 1f)]
        [SerializeField] float minimumVisibleLoad = 0.03f;

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
        [SerializeField] Color idleLightColor = new Color(0.18f, 0.85f, 1f, 1f);
        [SerializeField] Color boostLightColor = new Color(0.76f, 0.9f, 1f, 1f);

        [Header("Runtime Debug")]
        [SerializeField, Range(0f, 1f)] float debugNozzleLoad;
        [SerializeField, Range(-1f, 1f)] float debugSideLoad;
        [SerializeField] Vector3 debugSteeringEuler;

        Quaternion initialSteeringRotation = Quaternion.identity;
        Vector3 currentSteeringEuler;
        float currentNozzleLoad;
        float currentSideLoad;
        bool initialized;

        public float NozzleLoad => currentNozzleLoad;
        public float SideLoad => currentSideLoad;

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
            initialized = true;
        }

        void OnDisable()
        {
            coreGlow?.ClearRuntimeState();
            innerPlasma?.ClearRuntimeState();
            outerPlasma?.ClearRuntimeState();
            shockDiamonds?.ClearRuntimeState();
            distortion?.ClearRuntimeState();
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

            currentNozzleLoad = Smooth(currentNozzleLoad, targetNozzleLoad, response, clampedDeltaTime);
            currentSideLoad = Smooth(currentSideLoad, targetSideLoad, response, clampedDeltaTime);
            if (currentNozzleLoad < 0.0001f)
            {
                currentNozzleLoad = 0f;
            }

            ApplySteering(frame, clampedDeltaTime);
            ApplyMeshLayers(frame, clampedDeltaTime);
            ApplyGraphs(frame, clampedDeltaTime);
            ApplyLight(frame);
            ApplyDebug();
        }

        void Validate()
        {
            sideSign = Mathf.Clamp(sideSign, -1f, 1f);
            response = Mathf.Max(0f, response);
            minimumVisibleLoad = Mathf.Clamp01(minimumVisibleLoad);
            lateralLoadResponse = Mathf.Max(0f, lateralLoadResponse);
            yawLoadResponse = Mathf.Max(0f, yawLoadResponse);
            rollLoadResponse = Mathf.Max(0f, rollLoadResponse);
            verticalLoadResponse = Mathf.Max(0f, verticalLoadResponse);
            minDirectionalMultiplier = Mathf.Max(0f, minDirectionalMultiplier);
            maxDirectionalMultiplier = Mathf.Max(minDirectionalMultiplier, maxDirectionalMultiplier);
            maxSteeringAngle = Mathf.Max(0f, maxSteeringAngle);
            steeringResponse = Mathf.Max(0f, steeringResponse);
            smokeAtmosphereThreshold = Mathf.Clamp01(smokeAtmosphereThreshold);
            sparksBoostThreshold = Mathf.Clamp01(sparksBoostThreshold);
            sparksGraphTuning?.Validate();
            smokeGraphTuning?.Validate();
            maxLightIntensity = Mathf.Max(0f, maxLightIntensity);
            maxLightRange = Mathf.Max(0f, maxLightRange);
            lightPower = Mathf.Max(0.1f, lightPower);
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
            float responseT = steeringResponse <= 0f ? 1f : 1f - Mathf.Exp(-steeringResponse * deltaTime);
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
            coreGlow?.ApplyFrame(frame, currentNozzleLoad, deltaTime);
            innerPlasma?.ApplyFrame(frame, currentNozzleLoad, deltaTime);
            outerPlasma?.ApplyFrame(frame, currentNozzleLoad, deltaTime);
            shockDiamonds?.ApplyFrame(
                frame,
                currentNozzleLoad * Mathf.Lerp(0.35f, 1f, frame.Boost),
                deltaTime);
            distortion?.ApplyFrame(frame, currentNozzleLoad, deltaTime);
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

            float smokeLoad = frame.AtmosphereDensity > smokeAtmosphereThreshold
                ? Mathf.Clamp01(currentNozzleLoad * frame.AtmosphereDensity)
                : 0f;
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
            thrusterLight.intensity = maxLightIntensity * lightLoad * Mathf.Lerp(1f, 1.35f, frame.Boost);
            thrusterLight.range = maxLightRange * Mathf.Lerp(0.35f, 1f, lightLoad);
            thrusterLight.color = Color.Lerp(idleLightColor, boostLightColor, Mathf.Clamp01(frame.Boost + lightLoad * 0.35f));
        }

        void ApplyDebug()
        {
            debugNozzleLoad = currentNozzleLoad;
            debugSideLoad = currentSideLoad;
            debugSteeringEuler = currentSteeringEuler;
        }

        static float Smooth(float current, float target, float response, float deltaTime)
        {
            float responseT = response <= 0f ? 1f : 1f - Mathf.Exp(-response * deltaTime);
            return Mathf.Lerp(current, target, responseT);
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
            [Min(0f)]
            [SerializeField] float spawnRate;
            [Min(0f)]
            [SerializeField] float burstCount;
            [Min(0f)]
            [SerializeField] float speed;
            [Min(0f)]
            [SerializeField] float lifetime;
            [Min(0f)]
            [SerializeField] float size;
            [Range(0f, 1f)]
            [SerializeField] float alpha = 1f;
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
                    size = 0.045f,
                    alpha = 0.9f,
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
                    size = 0.22f,
                    alpha = 0.38f,
                    atmosphereRateMultiplier = 0.75f
                };
            }

            public void Validate()
            {
                spawnRate = Mathf.Max(0f, spawnRate);
                burstCount = Mathf.Max(0f, burstCount);
                speed = Mathf.Max(0f, speed);
                lifetime = Mathf.Max(0f, lifetime);
                size = Mathf.Max(0f, size);
                alpha = Mathf.Clamp01(alpha);
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
                float count = burstCount * shapedLoad * rateMultiplier;
                float speedValue = speed * Mathf.Lerp(0.35f, 1.25f, shapedLoad) * Mathf.Lerp(1f, 1.4f, Mathf.Clamp01(boost));
                float sizeValue = size * Mathf.Lerp(0.65f, 1.3f, shapedLoad);

                SetFloat(graph, VfxIds.Rate, rate);
                SetFloat(graph, VfxIds.Count, count);
                SetFloat(graph, VfxIds.Speed, speedValue);
                SetFloat(graph, VfxIds.Lifetime, lifetime);
                SetFloat(graph, VfxIds.LifetimeUnderscore, lifetime);
                SetFloat(graph, VfxIds.Size, sizeValue);
                SetFloat(graph, VfxIds.SizeUnderscore, sizeValue);
                SetFloat(graph, VfxIds.AlphaPublic, alpha * shapedLoad);
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

        static class VfxIds
        {
            public static readonly int Throttle = Shader.PropertyToID("Throttle");
            public static readonly int ShipThrottle = Shader.PropertyToID("ShipThrottle");
            public static readonly int NozzleLoad = Shader.PropertyToID("NozzleLoad");
            public static readonly int SideLoad = Shader.PropertyToID("SideLoad");
            public static readonly int Boost = Shader.PropertyToID("Boost");
            public static readonly int Heat = Shader.PropertyToID("Heat");
            public static readonly int AtmosphereDensity = Shader.PropertyToID("AtmosphereDensity");
            public static readonly int RelativeSpeed = Shader.PropertyToID("RelativeSpeed");
            public static readonly int LocalTranslation = Shader.PropertyToID("LocalTranslation");
            public static readonly int LocalRotation = Shader.PropertyToID("LocalRotation");
            public static readonly int LocalLinearAcceleration = Shader.PropertyToID("LocalLinearAcceleration");
            public static readonly int LocalAngularAcceleration = Shader.PropertyToID("LocalAngularAcceleration");
            public static readonly int InAtmosphere = Shader.PropertyToID("InAtmosphere");
            public static readonly int BoostActive = Shader.PropertyToID("BoostActive");
            public static readonly int Rate = Shader.PropertyToID("Rate");
            public static readonly int Count = Shader.PropertyToID("Count");
            public static readonly int Speed = Shader.PropertyToID("Speed");
            public static readonly int Lifetime = Shader.PropertyToID("Lifetime");
            public static readonly int LifetimeUnderscore = Shader.PropertyToID("_Lifetime");
            public static readonly int Size = Shader.PropertyToID("Size");
            public static readonly int SizeUnderscore = Shader.PropertyToID("_Size");
            public static readonly int AlphaPublic = Shader.PropertyToID("Alpha");
        }
    }
}
