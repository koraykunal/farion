Shader "Hidden/Farion/Lighting/Auto Exposure Apply"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "FarionAutoExposureApply"

            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            StructuredBuffer<float> _FarionExposureBuffer;
            float _FarionExposureStrength;

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord.xy);
                float exposure = lerp(1.0, _FarionExposureBuffer[0], saturate(_FarionExposureStrength));
                return half4(source.rgb * exposure, source.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
