using System.Collections.Generic;
using Farion.Rendering.Celestial;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Farion.Rendering.PostProcessing
{
    public sealed class FarionAtmosphereRendererFeature : ScriptableRendererFeature
    {
        const string DefaultShaderName = "Hidden/Farion/Celestial/Atmosphere Post Process";
        const int MaxAtmosphereBodies = 8;

        [SerializeField] Shader atmosphereShader;
        [SerializeField] RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing + 1;
        [Range(1, MaxAtmosphereBodies)]
        [SerializeField] int maxRenderedBodies = MaxAtmosphereBodies;

        Material atmosphereMaterial;
        AtmospherePass atmospherePass;

        public override void Create()
        {
            atmospherePass ??= new AtmospherePass();
            atmospherePass.renderPassEvent = renderPassEvent;
            ResolveMaterial();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.isPreviewCamera || !CelestialAtmosphereEffectRegistry.HasSources)
            {
                return;
            }

            Material material = ResolveMaterial();
            if (material == null)
            {
                return;
            }

            atmospherePass.Setup(material, maxRenderedBodies);
            renderer.EnqueuePass(atmospherePass);
        }

        protected override void Dispose(bool disposing)
        {
            atmospherePass?.Dispose();
            CoreUtils.Destroy(atmosphereMaterial);
            atmosphereMaterial = null;
        }

        Material ResolveMaterial()
        {
            if (atmosphereMaterial != null)
            {
                return atmosphereMaterial;
            }

            Shader shader = atmosphereShader != null ? atmosphereShader : Shader.Find(DefaultShaderName);
            if (shader == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"{nameof(FarionAtmosphereRendererFeature)} skipped because {DefaultShaderName} was not found.");
#endif
                return null;
            }

            atmosphereMaterial = CoreUtils.CreateEngineMaterial(shader);
            atmosphereMaterial.name = "Farion Atmosphere Post Process";
            return atmosphereMaterial;
        }

        sealed class AtmospherePass : ScriptableRenderPass
        {
            static readonly int EffectCountId = Shader.PropertyToID("_FarionAtmosphereEffectCount");
            static readonly int AtmosphereSpheresId = Shader.PropertyToID("_FarionAtmosphereSpheres");
            static readonly int PlanetSpheresId = Shader.PropertyToID("_FarionAtmospherePlanetSpheres");
            static readonly int SurfaceRadiiId = Shader.PropertyToID("_FarionAtmosphereSurfaceRadii");
            static readonly int ScatteringCoefficientsId = Shader.PropertyToID("_FarionAtmosphereScatteringCoefficients");
            static readonly int OpticalParamsId = Shader.PropertyToID("_FarionAtmosphereOpticalParams");
            static readonly int SampleParamsId = Shader.PropertyToID("_FarionAtmosphereSampleParams");
            static readonly int BakedOpticalDepthId = Shader.PropertyToID("_FarionAtmosphereBakedOpticalDepth");
            static readonly int BlueNoiseId = Shader.PropertyToID("_FarionAtmosphereBlueNoise");

            static readonly List<CelestialAtmosphereEffectData> AtmosphereEffects = new();
            static readonly Vector4[] AtmosphereSpheres = new Vector4[MaxAtmosphereBodies];
            static readonly Vector4[] PlanetSpheres = new Vector4[MaxAtmosphereBodies];
            static readonly Vector4[] SurfaceRadii = new Vector4[MaxAtmosphereBodies];
            static readonly Vector4[] ScatteringCoefficients = new Vector4[MaxAtmosphereBodies];
            static readonly Vector4[] OpticalParams = new Vector4[MaxAtmosphereBodies];
            static readonly Vector4[] SampleParams = new Vector4[MaxAtmosphereBodies];

            readonly List<Material> materialPool = new();
            Material templateMaterial;
            int maxRenderedBodies = MaxAtmosphereBodies;

            public AtmospherePass()
            {
                ConfigureInput(ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public void Setup(Material atmosphereMaterial, int bodyLimit)
            {
                templateMaterial = atmosphereMaterial;
                maxRenderedBodies = Mathf.Clamp(bodyLimit, 1, MaxAtmosphereBodies);
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
                CelestialAtmosphereEffectRegistry.Collect(cameraData.camera, AtmosphereEffects);
                int effectCount = Mathf.Min(AtmosphereEffects.Count, maxRenderedBodies);
                if (effectCount == 0)
                {
                    return;
                }

                TextureHandle source = resourceData.activeColorTexture;
                for (int i = 0; i < effectCount; i++)
                {
                    Material effectMaterial = GetEffectMaterial(i);
                    ApplyMaterialProperties(AtmosphereEffects[i], effectMaterial);

                    TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
                    destinationDesc.name = $"Farion Atmosphere Post Process {i}";
                    destinationDesc.clearBuffer = false;
                    TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                    RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, effectMaterial, 0);
                    renderGraph.AddBlitPass(parameters, passName: $"Farion Atmosphere Post Process {i}");
                    source = destination;
                }

                resourceData.cameraColor = source;
            }

            Material GetEffectMaterial(int index)
            {
                while (materialPool.Count <= index)
                {
                    Material material = CoreUtils.CreateEngineMaterial(templateMaterial.shader);
                    material.name = $"Farion Atmosphere Post Process {materialPool.Count}";
                    materialPool.Add(material);
                }

                return materialPool[index];
            }

            void ApplyMaterialProperties(CelestialAtmosphereEffectData effectData, Material material)
            {
                CelestialAtmosphereProfile profile = effectData.Profile;
                Vector3 center = effectData.Center;
                Vector3 scattering = profile.GetScatteringCoefficients();

                AtmosphereSpheres[0] = new Vector4(center.x, center.y, center.z, effectData.AtmosphereRadius);
                PlanetSpheres[0] = new Vector4(center.x, center.y, center.z, effectData.SurfaceRadius);
                SurfaceRadii[0] = new Vector4(effectData.SurfaceRadius, 0f, 0f, 0f);
                ScatteringCoefficients[0] = new Vector4(scattering.x, scattering.y, scattering.z, 0f);
                OpticalParams[0] = new Vector4(
                    profile.DensityFalloff,
                    profile.Intensity,
                    profile.DitherStrength,
                    profile.DitherScale);
                SampleParams[0] = new Vector4(
                    profile.InScatteringSteps,
                    profile.OpticalDepthSteps,
                    profile.ReferenceLightIntensity,
                    0f);

                float atmosphereScale = effectData.SurfaceRadius > 0.0001f
                    ? Mathf.Max(0f, effectData.AtmosphereRadius / effectData.SurfaceRadius - 1f)
                    : 0f;
                RenderTexture opticalDepthTexture = profile.GetOpticalDepthTexture(atmosphereScale);
                material.SetTexture(BakedOpticalDepthId, opticalDepthTexture != null ? opticalDepthTexture : Texture2D.whiteTexture);
                material.SetTexture(BlueNoiseId, profile.BlueNoise != null ? profile.BlueNoise : Texture2D.whiteTexture);

                material.SetInt(EffectCountId, 1);
                material.SetVectorArray(AtmosphereSpheresId, AtmosphereSpheres);
                material.SetVectorArray(PlanetSpheresId, PlanetSpheres);
                material.SetVectorArray(SurfaceRadiiId, SurfaceRadii);
                material.SetVectorArray(ScatteringCoefficientsId, ScatteringCoefficients);
                material.SetVectorArray(OpticalParamsId, OpticalParams);
                material.SetVectorArray(SampleParamsId, SampleParams);
            }
        }
    }
}
