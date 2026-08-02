using Farion.Rendering.Celestial;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Farion.Rendering.PostProcessing
{
    public sealed class FarionCloudRendererFeature : ScriptableRendererFeature
    {
        const string DefaultShaderName = "Hidden/Farion/Celestial/Cloud Post Process";

        [SerializeField] Shader cloudShader;
        [SerializeField] RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents + 1;
        [Range(1, 4)] [SerializeField] int downsample = 2;

        Material cloudMaterial;
        CloudPass cloudPass;

        public override void Create()
        {
            cloudPass ??= new CloudPass();
            cloudPass.renderPassEvent = renderPassEvent;
            ResolveMaterial();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.isPreviewCamera || !CelestialCloudEffectRegistry.HasSources)
            {
                return;
            }

            Material material = ResolveMaterial();
            if (material == null)
            {
                return;
            }

            cloudPass.Setup(material, downsample);
            renderer.EnqueuePass(cloudPass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(cloudMaterial);
            cloudMaterial = null;
        }

        Material ResolveMaterial()
        {
            if (cloudMaterial != null)
            {
                return cloudMaterial;
            }

            Shader shader = cloudShader != null ? cloudShader : Shader.Find(DefaultShaderName);
            if (shader == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"{nameof(FarionCloudRendererFeature)} skipped because {DefaultShaderName} was not found.");
#endif
                return null;
            }

            cloudMaterial = CoreUtils.CreateEngineMaterial(shader);
            cloudMaterial.name = "Farion Cloud Post Process";
            return cloudMaterial;
        }

        sealed class CloudPass : ScriptableRenderPass
        {
            static readonly int CloudTextureId = Shader.PropertyToID("_FarionCloudTexture");
            static readonly int CloudTextureTexelSizeId = Shader.PropertyToID("_FarionCloudTexture_TexelSize");
            static readonly int CloudSphereId = Shader.PropertyToID("_FarionCloudSphere");
            static readonly int CloudRadiiId = Shader.PropertyToID("_FarionCloudRadii");
            static readonly int WorldToLocalId = Shader.PropertyToID("_FarionCloudWorldToLocal");
            static readonly int SeedOffsetId = Shader.PropertyToID("_FarionCloudSeedOffset");
            static readonly int ShapeNoiseId = Shader.PropertyToID("_FarionCloudShapeNoise");
            static readonly int DetailNoiseId = Shader.PropertyToID("_FarionCloudDetailNoise");
            static readonly int BlueNoiseId = Shader.PropertyToID("_FarionCloudBlueNoise");
            static readonly int ShapeParamsId = Shader.PropertyToID("_FarionCloudShapeParams");
            static readonly int ShapeWeightsId = Shader.PropertyToID("_FarionCloudShapeWeights");
            static readonly int DetailWeightsId = Shader.PropertyToID("_FarionCloudDetailWeights");
            static readonly int AbsorptionParamsId = Shader.PropertyToID("_FarionCloudAbsorptionParams");
            static readonly int PhaseParamsId = Shader.PropertyToID("_FarionCloudPhaseParams");
            static readonly int WindAxisId = Shader.PropertyToID("_FarionCloudWindAxis");
            static readonly int SamplingParamsId = Shader.PropertyToID("_FarionCloudSamplingParams");

            Material material;
            int downsample = 2;

            public CloudPass()
            {
                ConfigureInput(ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public void Setup(Material cloudMaterial, int targetDownsample)
            {
                material = cloudMaterial;
                downsample = Mathf.Clamp(targetDownsample, 1, 4);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null)
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (!CelestialCloudEffectRegistry.TryGetClosest(cameraData.camera, out CelestialCloudEffectData effectData))
                {
                    return;
                }

                ApplyMaterialProperties(effectData);
                TextureHandle source = resourceData.activeColorTexture;
                TextureDesc cloudDesc = renderGraph.GetTextureDesc(source);
                cloudDesc.name = "Farion Cloud Half Resolution";
                cloudDesc.width = Mathf.Max(1, cloudDesc.width / downsample);
                cloudDesc.height = Mathf.Max(1, cloudDesc.height / downsample);
                cloudDesc.msaaSamples = MSAASamples.None;
                cloudDesc.clearBuffer = false;
                TextureHandle cloudTexture = renderGraph.CreateTexture(cloudDesc);

                RenderGraphUtils.BlitMaterialParameters raymarch = new(source, cloudTexture, material, 0);
                renderGraph.AddBlitPass(raymarch, passName: "Farion Cloud Raymarch");

                TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
                destinationDesc.name = "Farion Cloud Composite";
                destinationDesc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                material.SetVector(
                    CloudTextureTexelSizeId,
                    new Vector4(1f / cloudDesc.width, 1f / cloudDesc.height, cloudDesc.width, cloudDesc.height));
                AddCompositePass(renderGraph, source, cloudTexture, destination, material);
                resourceData.cameraColor = destination;
            }

            static void AddCompositePass(
                RenderGraph renderGraph,
                TextureHandle source,
                TextureHandle cloudTexture,
                TextureHandle destination,
                Material material)
            {
                using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<CompositePassData>(
                    "Farion Cloud Composite",
                    out CompositePassData passData);
                passData.source = source;
                passData.cloudTexture = cloudTexture;
                passData.material = material;
                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(cloudTexture, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (CompositePassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture(CloudTextureId, data.cloudTexture);
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), data.material, 1);
                });
            }

            void ApplyMaterialProperties(CelestialCloudEffectData effectData)
            {
                CelestialCloudProfile profile = effectData.Profile;
                Vector3 center = effectData.Center;
                material.SetVector(CloudSphereId, new Vector4(center.x, center.y, center.z, effectData.OuterRadius));
                material.SetVector(CloudRadiiId, new Vector4(effectData.SurfaceRadius, effectData.InnerRadius, effectData.OuterRadius, 0f));
                material.SetMatrix(WorldToLocalId, effectData.WorldToLocalRotation);
                material.SetVector(SeedOffsetId, GetSeedOffset(effectData.Seed));
                material.SetTexture(ShapeNoiseId, profile.ShapeNoise);
                material.SetTexture(DetailNoiseId, profile.DetailNoise);
                material.SetTexture(BlueNoiseId, profile.BlueNoise != null ? profile.BlueNoise : Texture2D.whiteTexture);
                material.SetVector(
                    ShapeParamsId,
                    new Vector4(profile.ShapeScale, profile.DetailScale, profile.Coverage, profile.DensityMultiplier));
                material.SetVector(ShapeWeightsId, profile.ShapeWeights);
                material.SetVector(DetailWeightsId, profile.DetailWeights);
                material.SetVector(
                    AbsorptionParamsId,
                    new Vector4(
                        profile.LightAbsorptionThroughCloud,
                        profile.LightAbsorptionTowardStar,
                        profile.DarknessThreshold,
                        profile.DetailErosion));
                material.SetVector(PhaseParamsId, profile.PhaseParameters);
                material.SetVector(
                    WindAxisId,
                    new Vector4(
                        profile.LocalWindAxis.x,
                        profile.LocalWindAxis.y,
                        profile.LocalWindAxis.z,
                        profile.BaseAngularSpeed));
                material.SetVector(
                    SamplingParamsId,
                    new Vector4(profile.ViewSteps, profile.LightSteps, profile.DetailAngularSpeed, profile.DitherStrength));
            }

            static Vector4 GetSeedOffset(int seed)
            {
                uint value = unchecked((uint)seed);
                float x = Hash01(value ^ 0x9e3779b9u);
                float y = Hash01(value ^ 0x85ebca6bu);
                float z = Hash01(value ^ 0xc2b2ae35u);
                return new Vector4(x, y, z, 0f);
            }

            static float Hash01(uint value)
            {
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                value ^= value >> 16;
                return (value & 0x00ffffffu) / 16777216f;
            }

            sealed class CompositePassData
            {
                public TextureHandle source;
                public TextureHandle cloudTexture;
                public Material material;
            }
        }
    }
}
