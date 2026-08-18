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
        // Resolve the opaque world before transparent VFX so the full-screen
        // ocean volume cannot overwrite particles or trails.
        [SerializeField] RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        [SerializeField] RenderPassEvent underwaterRenderPassEvent = RenderPassEvent.BeforeRenderingTransparents + 3;
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
            if (renderingData.cameraData.isPreviewCamera || !CelestialEffectRegistry.HasSources)
            {
                return;
            }

            Material material = ResolveMaterial();
            if (material == null)
            {
                return;
            }

            oceanPass.renderPassEvent = CelestialEffectRegistry.IsCameraInsideOcean(renderingData.cameraData.camera)
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
#if UNITY_EDITOR
                Debug.LogWarning($"{nameof(FarionOceanRendererFeature)} skipped because {DefaultShaderName} was not found.");
#endif
                return null;
            }

            oceanMaterial = CoreUtils.CreateEngineMaterial(shader);
            oceanMaterial.name = "Farion Ocean Post Process";
            return oceanMaterial;
        }

        sealed class OceanPass : ScriptableRenderPass
        {
            static readonly int OceanSphereId = Shader.PropertyToID("_FarionOceanSphere");
            static readonly int PlanetSphereId = Shader.PropertyToID("_FarionPlanetSphere");
            static readonly int OceanDeepColorId = Shader.PropertyToID("_FarionOceanDeepColor");
            static readonly int OceanShallowColorId = Shader.PropertyToID("_FarionOceanShallowColor");
            static readonly int OceanFresnelColorId = Shader.PropertyToID("_FarionOceanFresnelColor");
            static readonly int OceanHorizonColorId = Shader.PropertyToID("_FarionOceanHorizonColor");
            static readonly int OceanSpecularColorId = Shader.PropertyToID("_FarionOceanSpecularColor");
            static readonly int OceanUnderwaterColorId = Shader.PropertyToID("_FarionOceanUnderwaterColor");
            static readonly int OceanFoamColorId = Shader.PropertyToID("_FarionOceanFoamColor");
            static readonly int OceanOpticalParamsId = Shader.PropertyToID("_FarionOceanOpticalParams");
            static readonly int OceanUnderwaterOpticsId = Shader.PropertyToID("_FarionOceanUnderwaterOptics");
            static readonly int OceanWaveParamsId = Shader.PropertyToID("_FarionOceanWaveParams");
            static readonly int OceanSwellPhasesId = Shader.PropertyToID("_FarionOceanSwellPhases");
            static readonly int OceanSwellShapeId = Shader.PropertyToID("_FarionOceanSwellShape");
            static readonly int OceanWorldToLocalId = Shader.PropertyToID("_FarionOceanWorldToLocal");
            static readonly int OceanCameraInsideId = Shader.PropertyToID("_FarionOceanCameraInside");
            static readonly int OceanLightingParamsId = Shader.PropertyToID("_FarionOceanLightingParams");
            static readonly int OceanWaveNormalAId = Shader.PropertyToID("_FarionOceanWaveNormalA");
            static readonly int OceanWaveNormalBId = Shader.PropertyToID("_FarionOceanWaveNormalB");
            static readonly int AtmosphereParamsId = Shader.PropertyToID("_FarionOceanAtmosphereParams");
            static readonly int AtmosphereRayleighId = Shader.PropertyToID("_FarionOceanAtmosphereRayleigh");
            static readonly int AtmosphereOzoneId = Shader.PropertyToID("_FarionOceanAtmosphereOzone");
            static readonly int AtmosphereOpticalDepthId = Shader.PropertyToID("_FarionOceanAtmosphereOpticalDepth");

            static readonly List<CelestialOceanEffectData> OceanEffects = new();

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
                CelestialEffectRegistry.CollectOcean(cameraData.camera, OceanEffects);
                int effectCount = Mathf.Min(OceanEffects.Count, maxRenderedBodies);
                if (effectCount == 0)
                {
                    return;
                }

                TextureHandle source = resourceData.activeColorTexture;
                Vector3 cameraPosition = cameraData.camera.transform.position;
                for (int i = 0; i < effectCount; i++)
                {
                    Material effectMaterial = GetEffectMaterial(i);
                    ApplyMaterialProperties(OceanEffects[i], cameraPosition, effectMaterial);

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

            static void ApplyMaterialProperties(
                CelestialOceanEffectData effectData,
                Vector3 cameraPosition,
                Material material)
            {
                CelestialOceanProfile profile = effectData.Profile;
                Vector3 center = effectData.Center;

                material.SetTexture(OceanWaveNormalAId, profile.WaveNormalA != null ? profile.WaveNormalA : Texture2D.normalTexture);
                material.SetTexture(OceanWaveNormalBId, profile.WaveNormalB != null ? profile.WaveNormalB : Texture2D.normalTexture);
                material.SetVector(OceanSphereId, new Vector4(center.x, center.y, center.z, effectData.OceanRadius));
                material.SetVector(PlanetSphereId, new Vector4(center.x, center.y, center.z, effectData.BodyRadius));
                material.SetColor(OceanDeepColorId, profile.DeepColor);
                material.SetColor(OceanShallowColorId, profile.ShallowColor);
                material.SetColor(OceanFresnelColorId, profile.FresnelColor);
                material.SetColor(OceanHorizonColorId, profile.HorizonColor);
                material.SetColor(OceanSpecularColorId, profile.SpecularColor);
                material.SetColor(OceanUnderwaterColorId, profile.UnderwaterColor);
                material.SetColor(OceanFoamColorId, profile.FoamColor);
                material.SetVector(
                    OceanOpticalParamsId,
                    new Vector4(
                        profile.DepthMultiplier,
                        profile.AlphaMultiplier,
                        0f,
                        profile.ReferenceLightIntensity));
                Vector3 underwaterExtinction = profile.UnderwaterExtinctionCoefficients;
                material.SetVector(
                    OceanUnderwaterOpticsId,
                    new Vector4(
                        underwaterExtinction.x,
                        underwaterExtinction.y,
                        underwaterExtinction.z,
                        profile.UnderwaterSpecularStrength));
                material.SetVector(
                    OceanWaveParamsId,
                    new Vector4(profile.DetailNormalScale, profile.WaveSpeed, profile.WaveStrength, 0f));
                Vector3 phases = effectData.WavePhases;
                material.SetVector(OceanSwellPhasesId, new Vector4(phases.x, phases.y, phases.z, 0f));
                material.SetVector(
                    OceanSwellShapeId,
                    new Vector4(
                        effectData.WaveAmplitude,
                        effectData.WaveLength,
                        profile.FoamWidth,
                        profile.FoamStrength));
                material.SetMatrix(OceanWorldToLocalId, effectData.WorldToLocalRotation);
                material.SetFloat(OceanCameraInsideId, effectData.IsPointUnderwater(cameraPosition) ? 1f : 0f);
                material.SetVector(
                    OceanLightingParamsId,
                    new Vector4(
                        profile.Smoothness,
                        profile.SpecularStrength,
                        profile.IndexOfRefraction,
                        profile.ScatterStrength));
                ApplyAtmosphereProperties(effectData, material);
            }

            static void ApplyAtmosphereProperties(CelestialOceanEffectData effectData, Material material)
            {
                CelestialAtmosphereProfile profile = effectData.AtmosphereProfile;
                if (profile == null || effectData.AtmosphereRadius <= effectData.OceanRadius)
                {
                    material.SetVector(AtmosphereParamsId, Vector4.zero);
                    material.SetVector(AtmosphereRayleighId, Vector4.zero);
                    material.SetVector(AtmosphereOzoneId, Vector4.zero);
                    material.SetTexture(AtmosphereOpticalDepthId, Texture2D.whiteTexture);
                    return;
                }

                float atmosphereScale = effectData.AtmosphereRadius / effectData.OceanRadius - 1f;
                RenderTexture opticalDepth = profile.GetOpticalDepthTexture(atmosphereScale);
                material.SetVector(
                    AtmosphereParamsId,
                    new Vector4(
                        effectData.AtmosphereRadius,
                        profile.Intensity,
                        profile.MieScatteringStrength * profile.MieExtinctionRatio,
                        opticalDepth != null ? 1f : 0f));
                material.SetVector(AtmosphereRayleighId, profile.GetScatteringCoefficients());
                material.SetVector(AtmosphereOzoneId, profile.GetOzoneCoefficients());
                material.SetTexture(
                    AtmosphereOpticalDepthId,
                    opticalDepth != null ? opticalDepth : Texture2D.whiteTexture);
            }
        }
    }
}
