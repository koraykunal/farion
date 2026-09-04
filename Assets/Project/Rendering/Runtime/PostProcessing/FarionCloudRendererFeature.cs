using Farion.Rendering.Celestial;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Farion.Rendering.PostProcessing
{
    public sealed class FarionCloudRendererFeature : ScriptableRendererFeature
    {
        const string DefaultShaderName = "Hidden/Farion/Celestial/Cloud Post Process";

        [SerializeField] Shader cloudShader;
        [SerializeField] RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents + 2;
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
            if (renderingData.cameraData.isPreviewCamera || !CelestialEffectRegistry.HasSources)
            {
                return;
            }

            Material material = ResolveMaterial();
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
            const float OrbitStepScale = 0.5f;
            const int MinimumOrbitViewSteps = 16;
            const int MinimumOrbitLightSteps = 3;
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
            static readonly int StructureParamsId = Shader.PropertyToID("_FarionCloudStructureParams");
            static readonly int ShapeWeightsId = Shader.PropertyToID("_FarionCloudShapeWeights");
            static readonly int DetailWeightsId = Shader.PropertyToID("_FarionCloudDetailWeights");
            static readonly int AbsorptionParamsId = Shader.PropertyToID("_FarionCloudAbsorptionParams");
            static readonly int PhaseParamsId = Shader.PropertyToID("_FarionCloudPhaseParams");
            static readonly int LightingParamsId = Shader.PropertyToID("_FarionCloudLightingParams");
            static readonly int AtmosphereParamsId = Shader.PropertyToID("_FarionCloudAtmosphereParams");
            static readonly int AtmosphereRayleighId = Shader.PropertyToID("_FarionCloudAtmosphereRayleigh");
            static readonly int AtmosphereOzoneId = Shader.PropertyToID("_FarionCloudAtmosphereOzone");
            static readonly int AtmosphereOpticalDepthId = Shader.PropertyToID("_FarionCloudAtmosphereOpticalDepth");
            static readonly int WindAxisId = Shader.PropertyToID("_FarionCloudWindAxis");
            static readonly int SamplingParamsId = Shader.PropertyToID("_FarionCloudSamplingParams");
            static readonly int MotionId = Shader.PropertyToID("_FarionCloudMotion");
            static readonly int WeatherMotionId = Shader.PropertyToID("_FarionCloudWeatherMotion");
            static readonly int LayerParamsId = Shader.PropertyToID("_FarionCloudLayerParams");
            static readonly int AmbientColorId = Shader.PropertyToID("_FarionCloudAmbientColor");

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
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (material == null ||
                    !CelestialEffectRegistry.TryGetClosestCloud(cameraData.camera, out CelestialCloudEffectData effectData))
                {
                    return;
                }

                TextureHandle source = resourceData.activeColorTexture;
                TextureDesc cloudDesc = renderGraph.GetTextureDesc(source);
                cloudDesc.name = "Farion Cloud Half Resolution";
                cloudDesc.width = Mathf.Max(1, cloudDesc.width / downsample);
                cloudDesc.height = Mathf.Max(1, cloudDesc.height / downsample);
                cloudDesc.msaaSamples = MSAASamples.None;
                cloudDesc.clearBuffer = false;
                TextureHandle cloudTexture = renderGraph.CreateTexture(cloudDesc);

                material.SetVector(
                    CloudTextureTexelSizeId,
                    new Vector4(1f / cloudDesc.width, 1f / cloudDesc.height, cloudDesc.width, cloudDesc.height));
                ApplyMaterialProperties(effectData, cameraData.camera.transform.position);

                AddRaymarchPass(renderGraph, source, cloudTexture, material);

                TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
                destinationDesc.name = "Farion Cloud Composite";
                destinationDesc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                AddCompositePass(renderGraph, source, cloudTexture, destination, material);
                resourceData.cameraColor = destination;
            }

            static void AddRaymarchPass(
                RenderGraph renderGraph,
                TextureHandle source,
                TextureHandle cloudTexture,
                Material material)
            {
                using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<RaymarchPassData>(
                    "Farion Cloud Raymarch",
                    out RaymarchPassData passData);
                passData.source = source;
                passData.material = material;
                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(cloudTexture, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (RaymarchPassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), data.material, 0);
                });
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

            void ApplyMaterialProperties(CelestialCloudEffectData effectData, Vector3 cameraPosition)
            {
                CelestialCloudProfile profile = effectData.Profile;
                Vector3 center = effectData.Center;
                ResolveStepCounts(effectData, cameraPosition, out int viewSteps, out int lightSteps);
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
                material.SetVector(StructureParamsId, profile.StructureParameters);
                Vector4 shapeWeights = profile.ShapeWeights;
                float shapeWeight = shapeWeights.x + shapeWeights.y + shapeWeights.z + shapeWeights.w;
                material.SetVector(
                    ShapeWeightsId,
                    shapeWeight > 0.0001f ? shapeWeights / shapeWeight : new Vector4(1f, 0f, 0f, 0f));
                Vector3 detailWeights = profile.DetailWeights;
                float detailWeight = detailWeights.x + detailWeights.y + detailWeights.z;
                material.SetVector(
                    DetailWeightsId,
                    detailWeight > 0.0001f ? detailWeights / detailWeight : Vector3.right);
                material.SetVector(
                    AbsorptionParamsId,
                    new Vector4(
                        profile.LightAbsorptionThroughCloud,
                        profile.LightAbsorptionTowardStar,
                        profile.DarknessThreshold,
                        profile.DetailErosion));
                material.SetVector(PhaseParamsId, profile.PhaseParameters);
                material.SetVector(LightingParamsId, profile.LightingParameters);
                ApplyAtmosphereProperties(effectData);
                material.SetVector(
                    WindAxisId,
                    new Vector4(
                        profile.LocalWindAxis.x,
                        profile.LocalWindAxis.y,
                        profile.LocalWindAxis.z,
                        0f));
                material.SetVector(
                    SamplingParamsId,
                    new Vector4(viewSteps, lightSteps, profile.DitherStrength, 0f));
                material.SetVector(LayerParamsId, profile.LayerParameters);
                material.SetColor(AmbientColorId, profile.AmbientLight);
                double time = Time.timeAsDouble;
                float baseAngle = GetMotionAngle(time, profile.BaseAngularSpeed);
                float detailAngle = GetMotionAngle(time, profile.DetailAngularSpeed);
                float weatherAngle = GetMotionAngle(time, profile.BaseAngularSpeed * 0.35f);
                material.SetVector(
                    MotionId,
                    new Vector4(
                        Mathf.Sin(baseAngle),
                        Mathf.Cos(baseAngle),
                        Mathf.Sin(detailAngle),
                        Mathf.Cos(detailAngle)));
                material.SetVector(
                    WeatherMotionId,
                    new Vector4(Mathf.Sin(weatherAngle), Mathf.Cos(weatherAngle), 0f, 0f));
            }

            static void ResolveStepCounts(
                CelestialCloudEffectData effectData,
                Vector3 cameraPosition,
                out int viewSteps,
                out int lightSteps)
            {
                CelestialCloudProfile profile = effectData.Profile;
                float altitude = Vector3.Distance(cameraPosition, effectData.Center) - effectData.OuterRadius;
                float orbitBlend = effectData.OuterRadius > 0f
                    ? Mathf.Clamp01(altitude / effectData.OuterRadius)
                    : 0f;
                viewSteps = Mathf.Max(
                    MinimumOrbitViewSteps,
                    Mathf.RoundToInt(Mathf.Lerp(profile.ViewSteps, profile.ViewSteps * OrbitStepScale, orbitBlend)));
                lightSteps = Mathf.Max(
                    MinimumOrbitLightSteps,
                    Mathf.RoundToInt(Mathf.Lerp(profile.LightSteps, profile.LightSteps * OrbitStepScale, orbitBlend)));
            }

            void ApplyAtmosphereProperties(CelestialCloudEffectData effectData)
            {
                CelestialAtmosphereProfile profile = effectData.AtmosphereProfile;
                float atmosphereScale = effectData.SurfaceRadius > 0.0001f
                    ? Mathf.Max(0f, effectData.AtmosphereRadius / effectData.SurfaceRadius - 1f)
                    : 0f;
                RenderTexture opticalDepth = profile.GetOpticalDepthTexture(atmosphereScale);
                Vector3 rayleigh = profile.GetScatteringCoefficients();
                Vector3 ozone = profile.GetOzoneCoefficients();
                material.SetVector(
                    AtmosphereParamsId,
                    new Vector4(
                        effectData.AtmosphereRadius,
                        profile.Intensity,
                        profile.MieScatteringStrength * profile.MieExtinctionRatio,
                        opticalDepth != null ? 1f : 0f));
                material.SetVector(AtmosphereRayleighId, rayleigh);
                material.SetVector(AtmosphereOzoneId, ozone);
                material.SetTexture(
                    AtmosphereOpticalDepthId,
                    opticalDepth != null ? opticalDepth : Texture2D.whiteTexture);
            }

            static float GetMotionAngle(double time, float degreesPerSecond)
            {
                const double fullTurn = System.Math.PI * 2.0;
                double angle = time * degreesPerSecond * Mathf.Deg2Rad % fullTurn;
                return (float)(angle < 0d ? angle + fullTurn : angle);
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

            sealed class RaymarchPassData
            {
                public TextureHandle source;
                public Material material;
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
