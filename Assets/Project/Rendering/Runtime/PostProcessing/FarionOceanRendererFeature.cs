using System.Collections.Generic;
using Farion.Rendering.Celestial;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Farion.Rendering.PostProcessing
{
    public sealed class FarionOceanRendererFeature : ScriptableRendererFeature
    {
        const string DefaultShaderName = "Hidden/Farion/Celestial/Ocean Post Process";
        const int MaxOceanBodies = 8;

        [SerializeField] Shader oceanShader;
        [SerializeField] RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        [SerializeField] RenderPassEvent underwaterRenderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing + 2;
        [Range(1, MaxOceanBodies)]
        [SerializeField] int maxRenderedBodies = MaxOceanBodies;

        Material oceanMaterial;
        OceanPass oceanPass;

        public override void Create()
        {
            oceanPass ??= new OceanPass();
            oceanPass.renderPassEvent = renderPassEvent;
            ResolveMaterial();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.isPreviewCamera || !CelestialOceanEffectRegistry.HasSources)
            {
                return;
            }

            Material material = ResolveMaterial();
            if (material == null)
            {
                return;
            }

            oceanPass.renderPassEvent = CelestialOceanEffectRegistry.IsCameraInsideOcean(renderingData.cameraData.camera)
                ? underwaterRenderPassEvent
                : renderPassEvent;
            oceanPass.Setup(material, maxRenderedBodies);
            renderer.EnqueuePass(oceanPass);
        }

        protected override void Dispose(bool disposing)
        {
            oceanPass?.Dispose();
            CoreUtils.Destroy(oceanMaterial);
            oceanMaterial = null;
        }

        Material ResolveMaterial()
        {
            if (oceanMaterial != null)
            {
                return oceanMaterial;
            }

            Shader shader = oceanShader != null ? oceanShader : Shader.Find(DefaultShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"{nameof(FarionOceanRendererFeature)} skipped because {DefaultShaderName} was not found.");
                return null;
            }

            oceanMaterial = CoreUtils.CreateEngineMaterial(shader);
            oceanMaterial.name = "Farion Ocean Post Process";
            return oceanMaterial;
        }

        sealed class OceanPass : ScriptableRenderPass
        {
            static readonly int EffectCountId = Shader.PropertyToID("_FarionOceanEffectCount");
            static readonly int OceanSpheresId = Shader.PropertyToID("_FarionOceanSpheres");
            static readonly int PlanetSpheresId = Shader.PropertyToID("_FarionPlanetSpheres");
            static readonly int OceanDeepColorsId = Shader.PropertyToID("_FarionOceanDeepColors");
            static readonly int OceanShallowColorsId = Shader.PropertyToID("_FarionOceanShallowColors");
            static readonly int OceanFresnelColorsId = Shader.PropertyToID("_FarionOceanFresnelColors");
            static readonly int OceanSpecularColorsId = Shader.PropertyToID("_FarionOceanSpecularColors");
            static readonly int OceanUnderwaterColorsId = Shader.PropertyToID("_FarionOceanUnderwaterColors");
            static readonly int OceanOpticalParamsId = Shader.PropertyToID("_FarionOceanOpticalParams");
            static readonly int OceanUnderwaterParamsId = Shader.PropertyToID("_FarionOceanUnderwaterParams");
            static readonly int OceanWaveParamsId = Shader.PropertyToID("_FarionOceanWaveParams");
            static readonly int OceanLightingParamsId = Shader.PropertyToID("_FarionOceanLightingParams");
            static readonly int OceanExposureParamsId = Shader.PropertyToID("_FarionOceanExposureParams");
            static readonly int OceanWaveNormalAId = Shader.PropertyToID("_FarionOceanWaveNormalA");
            static readonly int OceanWaveNormalBId = Shader.PropertyToID("_FarionOceanWaveNormalB");

            static readonly List<CelestialOceanEffectData> OceanEffects = new();
            static readonly Vector4[] OceanSpheres = new Vector4[MaxOceanBodies];
            static readonly Vector4[] PlanetSpheres = new Vector4[MaxOceanBodies];
            static readonly Vector4[] DeepColors = new Vector4[MaxOceanBodies];
            static readonly Vector4[] ShallowColors = new Vector4[MaxOceanBodies];
            static readonly Vector4[] FresnelColors = new Vector4[MaxOceanBodies];
            static readonly Vector4[] SpecularColors = new Vector4[MaxOceanBodies];
            static readonly Vector4[] UnderwaterColors = new Vector4[MaxOceanBodies];
            static readonly Vector4[] OpticalParams = new Vector4[MaxOceanBodies];
            static readonly Vector4[] UnderwaterParams = new Vector4[MaxOceanBodies];
            static readonly Vector4[] WaveParams = new Vector4[MaxOceanBodies];
            static readonly Vector4[] LightingParams = new Vector4[MaxOceanBodies];
            static readonly Vector4[] ExposureParams = new Vector4[MaxOceanBodies];

            readonly List<Material> materialPool = new();
            Material templateMaterial;
            int maxRenderedBodies = MaxOceanBodies;

            public OceanPass()
            {
                ConfigureInput(ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public void Setup(Material oceanMaterial, int bodyLimit)
            {
                templateMaterial = oceanMaterial;
                maxRenderedBodies = Mathf.Clamp(bodyLimit, 1, MaxOceanBodies);
                requiresIntermediateTexture = true;
            }

            public void Dispose()
            {
                for (int i = 0; i < materialPool.Count; i++)
                {
                    CoreUtils.Destroy(materialPool[i]);
                }

                materialPool.Clear();
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (templateMaterial == null)
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                CelestialOceanEffectRegistry.Collect(cameraData.camera, OceanEffects);
                int effectCount = Mathf.Min(OceanEffects.Count, maxRenderedBodies);
                if (effectCount == 0)
                {
                    return;
                }

                TextureHandle source = resourceData.activeColorTexture;
                for (int i = 0; i < effectCount; i++)
                {
                    Material effectMaterial = GetEffectMaterial(i);
                    ApplyMaterialProperties(OceanEffects[i], effectMaterial);

                    TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
                    destinationDesc.name = $"Farion Ocean Post Process {i}";
                    destinationDesc.clearBuffer = false;
                    TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                    RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, effectMaterial, 0);
                    renderGraph.AddBlitPass(parameters, passName: $"Farion Ocean Post Process {i}");
                    source = destination;
                }

                resourceData.cameraColor = source;
            }

            Material GetEffectMaterial(int index)
            {
                while (materialPool.Count <= index)
                {
                    Material material = CoreUtils.CreateEngineMaterial(templateMaterial.shader);
                    material.name = $"Farion Ocean Post Process {materialPool.Count}";
                    materialPool.Add(material);
                }

                return materialPool[index];
            }

            void ApplyMaterialProperties(CelestialOceanEffectData effectData, Material material)
            {
                CelestialOceanProfile profile = effectData.Profile;
                Vector3 center = effectData.Center;

                OceanSpheres[0] = new Vector4(center.x, center.y, center.z, effectData.OceanRadius);
                PlanetSpheres[0] = new Vector4(center.x, center.y, center.z, effectData.BodyRadius);
                DeepColors[0] = profile.DeepColor;
                ShallowColors[0] = profile.ShallowColor;
                FresnelColors[0] = profile.FresnelColor;
                SpecularColors[0] = profile.SpecularColor;
                UnderwaterColors[0] = profile.UnderwaterColor;
                OpticalParams[0] = new Vector4(profile.DepthMultiplier, profile.AlphaMultiplier, 0f, 0f);
                UnderwaterParams[0] = new Vector4(
                    profile.UnderwaterDensity,
                    profile.UnderwaterSurfaceStrength,
                    profile.UnderwaterSpecularStrength,
                    0f);
                WaveParams[0] = new Vector4(profile.WaveNormalScale, profile.WaveSpeed, profile.WaveStrength, 0f);
                LightingParams[0] = new Vector4(
                    profile.Smoothness,
                    profile.SpecularStrength,
                    profile.FresnelStrength,
                    0f);
                ExposureParams[0] = new Vector4(profile.ReferenceLightIntensity, 0f, 0f, 0f);

                material.SetTexture(OceanWaveNormalAId, profile.WaveNormalA != null ? profile.WaveNormalA : Texture2D.normalTexture);
                material.SetTexture(OceanWaveNormalBId, profile.WaveNormalB != null ? profile.WaveNormalB : Texture2D.normalTexture);
                material.SetInt(EffectCountId, 1);
                material.SetVectorArray(OceanSpheresId, OceanSpheres);
                material.SetVectorArray(PlanetSpheresId, PlanetSpheres);
                material.SetVectorArray(OceanDeepColorsId, DeepColors);
                material.SetVectorArray(OceanShallowColorsId, ShallowColors);
                material.SetVectorArray(OceanFresnelColorsId, FresnelColors);
                material.SetVectorArray(OceanSpecularColorsId, SpecularColors);
                material.SetVectorArray(OceanUnderwaterColorsId, UnderwaterColors);
                material.SetVectorArray(OceanOpticalParamsId, OpticalParams);
                material.SetVectorArray(OceanUnderwaterParamsId, UnderwaterParams);
                material.SetVectorArray(OceanWaveParamsId, WaveParams);
                material.SetVectorArray(OceanLightingParamsId, LightingParams);
                material.SetVectorArray(OceanExposureParamsId, ExposureParams);
            }
        }
    }
}
