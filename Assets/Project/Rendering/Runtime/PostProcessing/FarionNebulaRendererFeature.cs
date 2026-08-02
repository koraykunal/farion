using Farion.Rendering.Space;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Farion.Rendering.PostProcessing
{
    public sealed class FarionNebulaRendererFeature : ScriptableRendererFeature
    {
        const string DefaultShaderName = "Hidden/Farion/Space/Volumetric Nebula";

        [SerializeField] Shader nebulaShader;
        [SerializeField] RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        Material nebulaMaterial;
        NebulaPass nebulaPass;

        public override void Create()
        {
            nebulaPass ??= new NebulaPass();
            nebulaPass.renderPassEvent = renderPassEvent;
            ResolveMaterial();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            NebulaVolume volume = NebulaVolume.Active;
            if (renderingData.cameraData.isPreviewCamera || volume == null)
            {
                return;
            }

            Material material = ResolveMaterial();
            if (material == null)
            {
                return;
            }

            nebulaPass.Setup(material, volume);
            renderer.EnqueuePass(nebulaPass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(nebulaMaterial);
            nebulaMaterial = null;
        }

        Material ResolveMaterial()
        {
            if (nebulaMaterial != null)
            {
                return nebulaMaterial;
            }

            Shader shader = nebulaShader != null ? nebulaShader : Shader.Find(DefaultShaderName);
            if (shader == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"{nameof(FarionNebulaRendererFeature)} skipped because {DefaultShaderName} was not found.");
#endif
                return null;
            }

            nebulaMaterial = CoreUtils.CreateEngineMaterial(shader);
            nebulaMaterial.name = "Farion Volumetric Nebula";
            return nebulaMaterial;
        }

        sealed class NebulaPass : ScriptableRenderPass
        {
            static readonly int SphereId = Shader.PropertyToID("_FarionNebulaSphere");
            static readonly int DeepColorId = Shader.PropertyToID("_FarionNebulaDeepColor");
            static readonly int MidColorId = Shader.PropertyToID("_FarionNebulaMidColor");
            static readonly int HighlightColorId = Shader.PropertyToID("_FarionNebulaHighlightColor");
            static readonly int ShapeId = Shader.PropertyToID("_FarionNebulaShape");
            static readonly int DetailId = Shader.PropertyToID("_FarionNebulaDetail");
            static readonly int MotionId = Shader.PropertyToID("_FarionNebulaMotion");
            static readonly int WorldToLocalRotationId = Shader.PropertyToID("_FarionNebulaWorldToLocalRotation");

            Material material;
            NebulaVolume volume;

            public NebulaPass()
            {
                ConfigureInput(ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public void Setup(Material effectMaterial, NebulaVolume source)
            {
                material = effectMaterial;
                volume = source;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null || volume == null)
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                ApplyMaterialProperties();

                TextureHandle source = resourceData.activeColorTexture;
                TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
                destinationDesc.name = "Farion Volumetric Nebula";
                destinationDesc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, material, 0);
                renderGraph.AddBlitPass(parameters, passName: "Farion Volumetric Nebula");
                resourceData.cameraColor = destination;
            }

            void ApplyMaterialProperties()
            {
                Transform volumeTransform = volume.transform;
                Vector3 center = volumeTransform.position;
                Vector3 drift = volume.DriftDirection;

                material.SetVector(SphereId, new Vector4(center.x, center.y, center.z, volume.Radius));
                material.SetColor(DeepColorId, volume.DeepColor);
                material.SetColor(MidColorId, volume.MidColor);
                material.SetColor(HighlightColorId, volume.HighlightColor);
                material.SetVector(ShapeId, new Vector4(
                    volume.StructureScale,
                    volume.Density,
                    volume.EdgeFade,
                    volume.Brightness));
                material.SetVector(DetailId, new Vector4(
                    volume.Extinction,
                    volume.StepCount,
                    volume.Seed,
                    0f));
                material.SetVector(MotionId, new Vector4(drift.x, drift.y, drift.z, volume.DriftSpeed));
                material.SetMatrix(
                    WorldToLocalRotationId,
                    Matrix4x4.Rotate(Quaternion.Inverse(volumeTransform.rotation)));
            }
        }
    }
}
