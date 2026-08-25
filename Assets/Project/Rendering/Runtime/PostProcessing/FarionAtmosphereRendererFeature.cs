using System.Collections.Generic;
using Farion.Rendering.Celestial;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Farion.Rendering.PostProcessing
{
    public sealed class FarionAtmosphereRendererFeature : ScriptableRendererFeature
    {
        const string DefaultShaderName = "Hidden/Farion/Celestial/Atmosphere Post Process";
        const int MaxAtmosphereBodies = 8;

        [SerializeField] Shader atmosphereShader;
        [SerializeField] RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents + 1;
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
            if (renderingData.cameraData.isPreviewCamera || !CelestialEffectRegistry.HasSources)
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
            static readonly int OceanSurfaceParamsId = Shader.PropertyToID("_FarionAtmosphereOceanSurfaceParams");
            static readonly int OceanWavePhasesId = Shader.PropertyToID("_FarionAtmosphereOceanWavePhases");
            static readonly int OceanWorldToLocalId = Shader.PropertyToID("_FarionAtmosphereOceanWorldToLocal");
            static readonly int ScatteringCoefficientsId = Shader.PropertyToID("_FarionAtmosphereScatteringCoefficients");
            static readonly int OpticalParamsId = Shader.PropertyToID("_FarionAtmosphereOpticalParams");
            static readonly int SampleParamsId = Shader.PropertyToID("_FarionAtmosphereSampleParams");
            static readonly int MieParamsId = Shader.PropertyToID("_FarionAtmosphereMieParams");
            static readonly int OzoneCoefficientsId = Shader.PropertyToID("_FarionAtmosphereOzoneCoefficients");
            static readonly int OzoneShapeId = Shader.PropertyToID("_FarionAtmosphereOzoneShape");
            static readonly int BakedOpticalDepthId = Shader.PropertyToID("_FarionAtmosphereBakedOpticalDepth");
            static readonly int BlueNoiseId = Shader.PropertyToID("_FarionAtmosphereBlueNoise");
            static readonly List<CelestialAtmosphereEffectData> AtmosphereEffects = new();
            static readonly Vector4[] AtmosphereSpheres = new Vector4[MaxAtmosphereBodies];
            static readonly Vector4[] PlanetSpheres = new Vector4[MaxAtmosphereBodies];
            static readonly Vector4[] SurfaceRadii = new Vector4[MaxAtmosphereBodies];
            static readonly Vector4[] OceanSurfaceParams = new Vector4[MaxAtmosphereBodies];
            static readonly Vector4[] OceanWavePhases = new Vector4[MaxAtmosphereBodies];
            static readonly Matrix4x4[] OceanWorldToLocal = new Matrix4x4[MaxAtmosphereBodies];
            static readonly Vector4[] ScatteringCoefficients = new Vector4[MaxAtmosphereBodies];
            static readonly Vector4[] OpticalParams = new Vector4[MaxAtmosphereBodies];
            static readonly Vector4[] SampleParams = new Vector4[MaxAtmosphereBodies];
            static readonly Vector4[] MieParams = new Vector4[MaxAtmosphereBodies];
            static readonly Vector4[] OzoneCoefficients = new Vector4[MaxAtmosphereBodies];
            static readonly Vector4[] OzoneShape = new Vector4[MaxAtmosphereBodies];

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
                CelestialEffectRegistry.CollectAtmosphere(cameraData.camera, AtmosphereEffects);
                int effectCount = Mathf.Min(AtmosphereEffects.Count, maxRenderedBodies);
                if (effectCount == 0)
                {
                    return;
                }

                TextureHandle source = resourceData.activeColorTexture;
                int passIndex = 0;
                int start = 0;
                while (start < effectCount)
                {
                    int groupCount = 1;
                    while (start + groupCount < effectCount
                        && groupCount < MaxAtmosphereBodies
                        && SharesBakeKey(AtmosphereEffects[start], AtmosphereEffects[start + groupCount]))
                    {
                        groupCount++;
                    }

                    Material effectMaterial = GetEffectMaterial(passIndex);
                    ApplyMaterialProperties(
                        start,
                        groupCount,
                        cameraData.camera.transform.position,
                        effectMaterial);

                    TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
                    destinationDesc.name = $"Farion Atmosphere Post Process {passIndex}";
                    destinationDesc.clearBuffer = false;
                    TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                    AddAtmospherePass(renderGraph, source, destination, effectMaterial, passIndex);
                    source = destination;

                    start += groupCount;
                    passIndex++;
                }

                resourceData.cameraColor = source;
            }

            static void AddAtmospherePass(
                RenderGraph renderGraph,
                TextureHandle source,
                TextureHandle destination,
                Material material,
                int index)
            {
                using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<AtmospherePassData>(
                    $"Farion Atmosphere Post Process {index}",
                    out AtmospherePassData passData);
                passData.source = source;
                passData.material = material;
                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (AtmospherePassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), data.material, 0);
                });
            }

            sealed class AtmospherePassData
            {
                public TextureHandle source;
                public Material material;
            }

            static float AtmosphereScaleOf(CelestialAtmosphereEffectData effectData)
            {
                return effectData.SurfaceRadius > 0.0001f
                    ? Mathf.Max(0f, effectData.AtmosphereRadius / effectData.SurfaceRadius - 1f)
                    : 0f;
            }

            static bool SharesBakeKey(CelestialAtmosphereEffectData a, CelestialAtmosphereEffectData b)
            {
                return ReferenceEquals(a.Profile, b.Profile)
                    && CelestialAtmosphereProfile.SharesOpticalDepthScale(
                        AtmosphereScaleOf(a),
                        AtmosphereScaleOf(b));
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

            void ApplyMaterialProperties(
                int start,
                int count,
                Vector3 cameraPosition,
                Material material)
            {
                CelestialAtmosphereProfile profile = AtmosphereEffects[start].Profile;
                Vector3 scattering = profile.GetScatteringCoefficients();
                Vector3 ozone = profile.GetOzoneCoefficients();

                for (int i = 0; i < count; i++)
                {
                    CelestialAtmosphereEffectData effectData = AtmosphereEffects[start + i];
                    Vector3 center = effectData.Center;

                    AtmosphereSpheres[i] = new Vector4(center.x, center.y, center.z, effectData.AtmosphereRadius);
                    PlanetSpheres[i] = new Vector4(center.x, center.y, center.z, effectData.SurfaceRadius);
                    SurfaceRadii[i] = new Vector4(effectData.SurfaceRadius, 0f, 0f, 0f);
                    if (effectData.HasOceanSurface)
                    {
                        CelestialOceanEffectData oceanSurface = effectData.OceanSurface;
                        Vector3 wavePhases = oceanSurface.WavePhases;
                        OceanSurfaceParams[i] = new Vector4(
                            oceanSurface.OceanRadius,
                            oceanSurface.WaveAmplitude,
                            oceanSurface.WaveLength,
                            oceanSurface.GetSignedSurfaceDistance(cameraPosition));
                        OceanWavePhases[i] = new Vector4(wavePhases.x, wavePhases.y, wavePhases.z, 0f);
                        OceanWorldToLocal[i] = oceanSurface.WorldToLocalRotation;
                    }
                    else
                    {
                        OceanSurfaceParams[i] = Vector4.zero;
                        OceanWavePhases[i] = Vector4.zero;
                        OceanWorldToLocal[i] = Matrix4x4.identity;
                    }
                    ScatteringCoefficients[i] = new Vector4(scattering.x, scattering.y, scattering.z, 0f);
                    OpticalParams[i] = new Vector4(
                        profile.DensityFalloff,
                        profile.Intensity,
                        profile.DitherStrength,
                        profile.DitherScale);
                    SampleParams[i] = new Vector4(
                        profile.InScatteringSteps,
                        profile.OpticalDepthSteps,
                        profile.ReferenceLightIntensity,
                        0f);
                    MieParams[i] = new Vector4(
                        profile.MieScatteringStrength,
                        profile.MieAnisotropy,
                        profile.MieExtinctionRatio,
                        profile.MieDensityFalloff);
                    OzoneCoefficients[i] = new Vector4(ozone.x, ozone.y, ozone.z, 0f);
                    OzoneShape[i] = new Vector4(
                        profile.OzonePeakHeight,
                        profile.OzoneBandWidth,
                        0f,
                        0f);
                }

                RenderTexture opticalDepthTexture = profile.GetOpticalDepthTexture(
                    AtmosphereScaleOf(AtmosphereEffects[start]));
                material.SetTexture(BakedOpticalDepthId, opticalDepthTexture != null ? opticalDepthTexture : Texture2D.whiteTexture);
                material.SetTexture(BlueNoiseId, profile.BlueNoise != null ? profile.BlueNoise : Texture2D.whiteTexture);

                material.SetInt(EffectCountId, count);
                material.SetVectorArray(AtmosphereSpheresId, AtmosphereSpheres);
                material.SetVectorArray(PlanetSpheresId, PlanetSpheres);
                material.SetVectorArray(SurfaceRadiiId, SurfaceRadii);
                material.SetVectorArray(OceanSurfaceParamsId, OceanSurfaceParams);
                material.SetVectorArray(OceanWavePhasesId, OceanWavePhases);
                material.SetMatrixArray(OceanWorldToLocalId, OceanWorldToLocal);
                material.SetVectorArray(ScatteringCoefficientsId, ScatteringCoefficients);
                material.SetVectorArray(OpticalParamsId, OpticalParams);
                material.SetVectorArray(SampleParamsId, SampleParams);
                material.SetVectorArray(MieParamsId, MieParams);
                material.SetVectorArray(OzoneCoefficientsId, OzoneCoefficients);
                material.SetVectorArray(OzoneShapeId, OzoneShape);
            }
        }
    }
}
