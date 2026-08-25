using System.Collections.Generic;
using Farion.Rendering.Celestial;
using Farion.Rendering.Lighting;
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
        const int MaxUnderwaterSpotLights = 4;

        [SerializeField] Shader oceanShader;
        // Opaque and alpha-clipped underwater geometry participates through depth.
        // Transparent VFX remain an explicit water-aware material contract.
        [SerializeField] RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        [SerializeField] RenderPassEvent underwaterRenderPassEvent = RenderPassEvent.BeforeRenderingTransparents + 3;
        [Range(1, MaxOceanBodies)]
        [SerializeField] int maxRenderedBodies = MaxOceanBodies;

        Material oceanMaterial;
        OceanPass exteriorOceanPass;
        OceanPass underwaterOceanPass;

        public override void Create()
        {
            exteriorOceanPass ??= new OceanPass(false, "Exterior");
            underwaterOceanPass ??= new OceanPass(true, "Underwater");
            exteriorOceanPass.renderPassEvent = renderPassEvent;
            underwaterOceanPass.renderPassEvent = underwaterRenderPassEvent;
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

            exteriorOceanPass.Setup(material, maxRenderedBodies);
            renderer.EnqueuePass(exteriorOceanPass);
            if (CelestialEffectRegistry.IsCameraInsideOcean(renderingData.cameraData.camera))
            {
                underwaterOceanPass.Setup(material, maxRenderedBodies);
                renderer.EnqueuePass(underwaterOceanPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            exteriorOceanPass?.Dispose();
            underwaterOceanPass?.Dispose();
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
            static readonly int OceanDepthMultiplierId = Shader.PropertyToID("_FarionOceanDepthMultiplier");
            static readonly int OceanReferenceLightIntensityId = Shader.PropertyToID("_FarionOceanReferenceLightIntensity");
            static readonly int OceanUnderwaterOpticsId = Shader.PropertyToID("_FarionOceanUnderwaterOptics");
            static readonly int OceanWaveParamsId = Shader.PropertyToID("_FarionOceanWaveParams");
            static readonly int OceanSwellPhasesId = Shader.PropertyToID("_FarionOceanSwellPhases");
            static readonly int OceanSwellShapeId = Shader.PropertyToID("_FarionOceanSwellShape");
            static readonly int OceanWorldToLocalId = Shader.PropertyToID("_FarionOceanWorldToLocal");
            static readonly int OceanCameraSurfaceParamsId = Shader.PropertyToID("_FarionOceanCameraSurfaceParams");
            static readonly int OceanLightingParamsId = Shader.PropertyToID("_FarionOceanLightingParams");
            static readonly int OceanWaveNormalAId = Shader.PropertyToID("_FarionOceanWaveNormalA");
            static readonly int OceanWaveNormalBId = Shader.PropertyToID("_FarionOceanWaveNormalB");
            static readonly int AtmosphereParamsId = Shader.PropertyToID("_FarionOceanAtmosphereParams");
            static readonly int AtmosphereRayleighId = Shader.PropertyToID("_FarionOceanAtmosphereRayleigh");
            static readonly int AtmosphereOzoneId = Shader.PropertyToID("_FarionOceanAtmosphereOzone");
            static readonly int AtmosphereOpticalDepthId = Shader.PropertyToID("_FarionOceanAtmosphereOpticalDepth");
            static readonly int UnderwaterLightCountId = Shader.PropertyToID("_FarionUnderwaterLightCount");
            static readonly int UnderwaterLightPositionRangeId = Shader.PropertyToID("_FarionUnderwaterLightPositionRange");
            static readonly int UnderwaterLightDirectionOuterCosId = Shader.PropertyToID("_FarionUnderwaterLightDirectionOuterCos");
            static readonly int UnderwaterLightColorStrengthId = Shader.PropertyToID("_FarionUnderwaterLightColorStrength");
            static readonly int UnderwaterLightInnerConeCosId = Shader.PropertyToID("_FarionUnderwaterLightInnerConeCos");

            static readonly List<CelestialOceanEffectData> OceanEffects = new();

            readonly List<Material> materialPool = new();
            readonly List<UnderwaterSpotLightShaderData> underwaterLights = new(MaxUnderwaterSpotLights);
            readonly Vector4[] underwaterLightPositionRange = new Vector4[MaxUnderwaterSpotLights];
            readonly Vector4[] underwaterLightDirectionOuterCos = new Vector4[MaxUnderwaterSpotLights];
            readonly Vector4[] underwaterLightColorStrength = new Vector4[MaxUnderwaterSpotLights];
            readonly float[] underwaterLightInnerConeCos = new float[MaxUnderwaterSpotLights];
            readonly bool renderUnderwater;
            readonly string passLabel;
            Material templateMaterial;
            int maxRenderedBodies = MaxOceanBodies;

            public OceanPass(bool renderUnderwater, string passLabel)
            {
                this.renderUnderwater = renderUnderwater;
                this.passLabel = passLabel;
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
                if (OceanEffects.Count == 0)
                {
                    return;
                }

                TextureHandle source = resourceData.activeColorTexture;
                Camera camera = cameraData.camera;
                Vector3 cameraPosition = camera.transform.position;
                int renderedCount = 0;
                for (int i = 0; i < OceanEffects.Count && renderedCount < maxRenderedBodies; i++)
                {
                    CelestialOceanEffectData effectData = OceanEffects[i];
                    float signedSurfaceDistance = effectData.GetSignedSurfaceDistance(cameraPosition);
                    if ((signedSurfaceDistance < 0f) != renderUnderwater)
                    {
                        continue;
                    }

                    Material effectMaterial = GetEffectMaterial(renderedCount);
                    ApplyMaterialProperties(
                        effectData,
                        signedSurfaceDistance,
                        effectMaterial,
                        cameraPosition);

                    string passName = $"Farion Ocean {passLabel} {renderedCount}";
                    TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
                    destinationDesc.name = passName;
                    destinationDesc.clearBuffer = false;
                    TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                    RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, effectMaterial, 0);
                    renderGraph.AddBlitPass(parameters, passName: passName);
                    source = destination;
                    renderedCount++;
                }

                if (renderedCount > 0)
                {
                    resourceData.cameraColor = source;
                }
            }

            Material GetEffectMaterial(int index)
            {
                while (materialPool.Count <= index)
                {
                    Material material = CoreUtils.CreateEngineMaterial(templateMaterial.shader);
                    material.name = $"Farion Ocean {passLabel} {materialPool.Count}";
                    materialPool.Add(material);
                }

                return materialPool[index];
            }

            void ApplyMaterialProperties(
                CelestialOceanEffectData effectData,
                float signedSurfaceDistance,
                Material material,
                Vector3 cameraPosition)
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
                material.SetFloat(OceanDepthMultiplierId, profile.DepthMultiplier);
                material.SetFloat(OceanReferenceLightIntensityId, profile.ReferenceLightIntensity);
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
                material.SetVector(
                    OceanLightingParamsId,
                    new Vector4(
                        profile.Smoothness,
                        profile.SpecularStrength,
                        profile.IndexOfRefraction,
                        profile.ScatterStrength));
                ApplyAtmosphereProperties(effectData, material);
                float longestLightRange = ApplyUnderwaterLights(effectData, cameraPosition, material);
                material.SetVector(
                    OceanCameraSurfaceParamsId,
                    new Vector4(
                        signedSurfaceDistance,
                        longestLightRange,
                        profile.UnderwaterVisibilityDistance,
                        0f));
            }

            float ApplyUnderwaterLights(
                CelestialOceanEffectData effectData,
                Vector3 cameraPosition,
                Material material)
            {
                if (!renderUnderwater)
                {
                    material.SetInt(UnderwaterLightCountId, 0);
                    return 0f;
                }

                UnderwaterSpotLight.Collect(effectData, cameraPosition, underwaterLights);
                int count = Mathf.Min(underwaterLights.Count, MaxUnderwaterSpotLights);
                float longestRange = 0f;
                for (int i = 0; i < count; i++)
                {
                    UnderwaterSpotLightShaderData light = underwaterLights[i];
                    underwaterLightPositionRange[i] = light.PositionRange;
                    underwaterLightDirectionOuterCos[i] = light.DirectionOuterCos;
                    underwaterLightColorStrength[i] = light.ColorStrength;
                    underwaterLightInnerConeCos[i] = light.InnerConeCos;
                    longestRange = Mathf.Max(longestRange, light.PositionRange.w);
                }

                material.SetInt(UnderwaterLightCountId, count);
                if (count == 0)
                {
                    return 0f;
                }

                material.SetVectorArray(UnderwaterLightPositionRangeId, underwaterLightPositionRange);
                material.SetVectorArray(UnderwaterLightDirectionOuterCosId, underwaterLightDirectionOuterCos);
                material.SetVectorArray(UnderwaterLightColorStrengthId, underwaterLightColorStrength);
                material.SetFloatArray(UnderwaterLightInnerConeCosId, underwaterLightInnerConeCos);
                return longestRange;
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
