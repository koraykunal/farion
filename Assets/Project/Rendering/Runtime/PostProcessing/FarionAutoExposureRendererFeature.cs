using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Farion.Rendering.PostProcessing
{
    public sealed class FarionAutoExposureRendererFeature : ScriptableRendererFeature
    {
        static readonly int ExposureStrengthId = Shader.PropertyToID("_FarionExposureStrength");

        const string DefaultApplyShaderName = "Hidden/Farion/Lighting/Auto Exposure Apply";
        const int HistogramBins = 256;
        const int HistogramThreads = 16;
        const int StaleStateFrames = 240;

        static readonly List<int> StaleKeys = new();

        [Header("Resources")]
        [SerializeField] ComputeShader exposureCompute;
        [SerializeField] Shader applyShader;

        [Header("Placement")]
        [SerializeField] RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing - 1;

        [Header("Metering")]
        [Tooltip("Pixels dimmer than this are excluded from metering entirely. Empty space sits near 0.002, so without this the meter reads the void and drives exposure to its ceiling.")]
        [Min(0f)]
        [SerializeField] float meteringThreshold = 0.02f;
        [Tooltip("Darkest luminance the meter considers, in log2 units.")]
        [Range(-12f, 0f)]
        [SerializeField] float minimumLogLuminance = -6f;
        [Range(0f, 16f)]
        [SerializeField] float maximumLogLuminance = 6f;
        [Range(0f, 0.9f)]
        [SerializeField] float lowerPercentile = 0.3f;
        [Tooltip("Ignores the brightest part of the frame so a star in view does not crush the whole image.")]
        [Range(0.1f, 1f)]
        [SerializeField] float upperPercentile = 0.95f;

        [Header("Adaptation")]
        [Tooltip("How far the metered exposure is allowed to pull the image away from 1.0. Lower values keep auto exposure subtle.")]
        [Range(0f, 1f)]
        [SerializeField] float exposureStrength = 0.6f;
        [Tooltip("Target middle grey. Higher values make the image brighter overall.")]
        [Range(0.01f, 1f)]
        [SerializeField] float keyValue = 0.18f;
        [Tooltip("Speed when the scene gets brighter. The eye recovers from glare quickly.")]
        [Min(0f)]
        [SerializeField] float brightenSpeed = 3.5f;
        [Tooltip("Speed when the scene gets darker. Dark adaptation is much slower in reality.")]
        [Min(0f)]
        [SerializeField] float darkenSpeed = 0.9f;
        [Tooltip("Space should stay dark. A wide range here is what makes the void read as daylight.")]
        [Min(0.0001f)]
        [SerializeField] float minimumExposure = 0.5f;
        [Min(0.0001f)]
        [SerializeField] float maximumExposure = 2.2f;

        Material applyMaterial;
        AutoExposurePass exposurePass;

        public override void Create()
        {
            exposurePass ??= new AutoExposurePass();
            exposurePass.renderPassEvent = renderPassEvent;
            ResolveMaterial();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.isPreviewCamera || exposureCompute == null)
            {
                return;
            }

            Material material = ResolveMaterial();
            if (material == null)
            {
                return;
            }

            AutoExposureSettings settings = new(
                minimumLogLuminance,
                Mathf.Max(minimumLogLuminance + 0.1f, maximumLogLuminance),
                lowerPercentile,
                Mathf.Max(lowerPercentile + 0.01f, upperPercentile),
                keyValue,
                brightenSpeed,
                darkenSpeed,
                minimumExposure,
                Mathf.Max(minimumExposure, maximumExposure),
                meteringThreshold);
            material.SetFloat(ExposureStrengthId, exposureStrength);

            exposurePass.Setup(exposureCompute, material, settings);
            renderer.EnqueuePass(exposurePass);
        }

        protected override void Dispose(bool disposing)
        {
            exposurePass?.Dispose();
            exposurePass = null;
            CoreUtils.Destroy(applyMaterial);
            applyMaterial = null;
        }

        Material ResolveMaterial()
        {
            if (applyMaterial != null)
            {
                return applyMaterial;
            }

            Shader shader = applyShader != null ? applyShader : Shader.Find(DefaultApplyShaderName);
            if (shader == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"{nameof(FarionAutoExposureRendererFeature)} skipped because {DefaultApplyShaderName} was not found.");
#endif
                return null;
            }

            applyMaterial = CoreUtils.CreateEngineMaterial(shader);
            applyMaterial.name = "Farion Auto Exposure Apply";
            return applyMaterial;
        }

        readonly struct AutoExposureSettings
        {
            public AutoExposureSettings(
                float minimumLogLuminance,
                float maximumLogLuminance,
                float lowerPercentile,
                float upperPercentile,
                float keyValue,
                float brightenSpeed,
                float darkenSpeed,
                float minimumExposure,
                float maximumExposure,
                float meteringThreshold)
            {
                MinimumLogLuminance = minimumLogLuminance;
                LogLuminanceRange = maximumLogLuminance - minimumLogLuminance;
                LowerPercentile = lowerPercentile;
                UpperPercentile = upperPercentile;
                KeyValue = keyValue;
                BrightenSpeed = brightenSpeed;
                DarkenSpeed = darkenSpeed;
                MinimumExposure = minimumExposure;
                MaximumExposure = maximumExposure;
                MeteringThreshold = meteringThreshold;
            }

            public float MinimumLogLuminance { get; }
            public float LogLuminanceRange { get; }
            public float LowerPercentile { get; }
            public float UpperPercentile { get; }
            public float KeyValue { get; }
            public float BrightenSpeed { get; }
            public float DarkenSpeed { get; }
            public float MinimumExposure { get; }
            public float MaximumExposure { get; }
            public float MeteringThreshold { get; }
        }

        sealed class AutoExposurePass : ScriptableRenderPass
        {
            static readonly int HistogramId = Shader.PropertyToID("_FarionHistogram");
            static readonly int ExposureId = Shader.PropertyToID("_FarionExposure");
            static readonly int SourceTextureId = Shader.PropertyToID("_FarionExposureSource");
            static readonly int SourceSizeId = Shader.PropertyToID("_FarionSourceSize");
            static readonly int LuminanceRangeId = Shader.PropertyToID("_FarionLuminanceRange");
            static readonly int AdaptationId = Shader.PropertyToID("_FarionAdaptation");
            static readonly int ExposureLimitsId = Shader.PropertyToID("_FarionExposureLimits");
            static readonly int ExposureBufferId = Shader.PropertyToID("_FarionExposureBuffer");

            readonly Dictionary<int, CameraExposureState> cameraStates = new();
            ComputeShader compute;
            Material applyMaterial;
            AutoExposureSettings settings;
            int clearKernel = -1;
            int buildKernel = -1;
            int resolveKernel = -1;

            public AutoExposurePass()
            {
                requiresIntermediateTexture = true;
            }

            public void Setup(ComputeShader exposureCompute, Material material, AutoExposureSettings exposureSettings)
            {
                if (compute != exposureCompute)
                {
                    compute = exposureCompute;
                    clearKernel = compute.FindKernel("ClearHistogram");
                    buildKernel = compute.FindKernel("BuildHistogram");
                    resolveKernel = compute.FindKernel("ResolveExposure");
                }

                applyMaterial = material;
                settings = exposureSettings;
                requiresIntermediateTexture = true;
            }

            public void Dispose()
            {
                foreach (CameraExposureState state in cameraStates.Values)
                {
                    state.Release();
                }

                cameraStates.Clear();
            }

            CameraExposureState ResolveCameraState(Camera camera)
            {
                int key = camera.GetHashCode();
                if (!cameraStates.TryGetValue(key, out CameraExposureState state))
                {
                    state = new CameraExposureState();
                    cameraStates.Add(key, state);
                }

                state.LastUsedFrame = Time.frameCount;
                state.EnsureBuffers();
                PruneStaleStates();
                return state;
            }

            void PruneStaleStates()
            {
                if (cameraStates.Count <= 1)
                {
                    return;
                }

                StaleKeys.Clear();
                int frame = Time.frameCount;
                foreach (KeyValuePair<int, CameraExposureState> entry in cameraStates)
                {
                    if (frame - entry.Value.LastUsedFrame > StaleStateFrames)
                    {
                        StaleKeys.Add(entry.Key);
                    }
                }

                for (int i = 0; i < StaleKeys.Count; i++)
                {
                    cameraStates[StaleKeys[i]].Release();
                    cameraStates.Remove(StaleKeys[i]);
                }
            }

            sealed class CameraExposureState
            {
                public GraphicsBuffer Histogram;
                public GraphicsBuffer Exposure;
                public int LastUsedFrame;

                public void EnsureBuffers()
                {
                    if (Histogram == null || !Histogram.IsValid())
                    {
                        Histogram = new GraphicsBuffer(
                            GraphicsBuffer.Target.Structured,
                            HistogramBins,
                            sizeof(uint))
                        {
                            name = "Farion Exposure Histogram"
                        };
                    }

                    if (Exposure == null || !Exposure.IsValid())
                    {
                        Exposure = new GraphicsBuffer(
                            GraphicsBuffer.Target.Structured,
                            1,
                            sizeof(float))
                        {
                            name = "Farion Exposure Value"
                        };
                        Exposure.SetData(new[] { 0f });
                    }
                }

                public void Release()
                {
                    Histogram?.Release();
                    Histogram = null;
                    Exposure?.Release();
                    Exposure = null;
                }
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (compute == null || applyMaterial == null || clearKernel < 0)
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                CameraExposureState cameraState = ResolveCameraState(cameraData.camera);

                TextureHandle source = resourceData.activeColorTexture;
                TextureDesc sourceDesc = renderGraph.GetTextureDesc(source);
                if (sourceDesc.width <= 0 || sourceDesc.height <= 0)
                {
                    return;
                }

                BufferHandle histogramHandle = renderGraph.ImportBuffer(cameraState.Histogram);
                BufferHandle exposureHandle = renderGraph.ImportBuffer(cameraState.Exposure);

                Vector4 luminanceRange = new(
                    settings.MinimumLogLuminance,
                    1f / Mathf.Max(settings.LogLuminanceRange, 0.0001f),
                    settings.LogLuminanceRange,
                    Time.deltaTime);
                Vector4 adaptation = new(
                    settings.BrightenSpeed,
                    settings.DarkenSpeed,
                    settings.LowerPercentile,
                    settings.UpperPercentile);
                Vector4 exposureLimits = new(
                    settings.KeyValue,
                    settings.MinimumExposure,
                    settings.MaximumExposure,
                    settings.MeteringThreshold);

                using (IComputeRenderGraphBuilder builder = renderGraph.AddComputePass(
                    "Farion Auto Exposure Histogram",
                    out MeterPassData passData))
                {
                    passData.compute = compute;
                    passData.clearKernel = clearKernel;
                    passData.buildKernel = buildKernel;
                    passData.resolveKernel = resolveKernel;
                    passData.source = source;
                    passData.histogram = histogramHandle;
                    passData.exposure = exposureHandle;
                    passData.sourceWidth = sourceDesc.width;
                    passData.sourceHeight = sourceDesc.height;
                    passData.luminanceRange = luminanceRange;
                    passData.adaptation = adaptation;
                    passData.exposureLimits = exposureLimits;

                    builder.UseTexture(source, AccessFlags.Read);
                    builder.UseBuffer(histogramHandle, AccessFlags.ReadWrite);
                    builder.UseBuffer(exposureHandle, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (MeterPassData data, ComputeGraphContext context) =>
                        ExecuteMeter(data, context));
                }

                applyMaterial.SetBuffer(ExposureBufferId, cameraState.Exposure);

                TextureDesc destinationDesc = sourceDesc;
                destinationDesc.name = "Farion Auto Exposure";
                destinationDesc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, applyMaterial, 0);
                renderGraph.AddBlitPass(parameters, passName: "Farion Auto Exposure Apply");
                resourceData.cameraColor = destination;
            }

            static void ExecuteMeter(MeterPassData data, ComputeGraphContext context)
            {
                ComputeCommandBuffer cmd = context.cmd;

                cmd.SetComputeBufferParam(data.compute, data.clearKernel, HistogramId, data.histogram);
                cmd.DispatchCompute(data.compute, data.clearKernel, 1, 1, 1);

                cmd.SetComputeVectorParam(data.compute, LuminanceRangeId, data.luminanceRange);
                cmd.SetComputeVectorParam(data.compute, AdaptationId, data.adaptation);
                cmd.SetComputeVectorParam(data.compute, ExposureLimitsId, data.exposureLimits);
                cmd.SetComputeIntParams(data.compute, SourceSizeId, data.sourceWidth, data.sourceHeight);

                cmd.SetComputeBufferParam(data.compute, data.buildKernel, HistogramId, data.histogram);
                cmd.SetComputeTextureParam(data.compute, data.buildKernel, SourceTextureId, data.source);
                cmd.DispatchCompute(
                    data.compute,
                    data.buildKernel,
                    Mathf.CeilToInt(data.sourceWidth / (float)HistogramThreads),
                    Mathf.CeilToInt(data.sourceHeight / (float)HistogramThreads),
                    1);

                cmd.SetComputeBufferParam(data.compute, data.resolveKernel, HistogramId, data.histogram);
                cmd.SetComputeBufferParam(data.compute, data.resolveKernel, ExposureId, data.exposure);
                cmd.DispatchCompute(data.compute, data.resolveKernel, 1, 1, 1);
            }

            class MeterPassData
            {
                public ComputeShader compute;
                public int clearKernel;
                public int buildKernel;
                public int resolveKernel;
                public TextureHandle source;
                public BufferHandle histogram;
                public BufferHandle exposure;
                public int sourceWidth;
                public int sourceHeight;
                public Vector4 luminanceRange;
                public Vector4 adaptation;
                public Vector4 exposureLimits;
            }
        }
    }
}
