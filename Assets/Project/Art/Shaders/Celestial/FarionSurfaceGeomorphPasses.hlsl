#ifndef FARION_SURFACE_GEOMORPH_PASSES_INCLUDED
#define FARION_SURFACE_GEOMORPH_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"
#include "FarionSurfaceGeomorph.hlsl"

#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct FarionGeomorphDepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 morphOffsetOS : TEXCOORD1;
    float4 morphNormalOS : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct FarionGeomorphDepthVaryings
{
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

FarionGeomorphDepthVaryings FarionGeomorphDepthOnlyVertex(FarionGeomorphDepthAttributes input)
{
    FarionGeomorphDepthVaryings output = (FarionGeomorphDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float morphWeight = FarionResolveMorphWeight(input.positionOS.xyz);
    float3 positionOS = input.positionOS.xyz + input.morphOffsetOS.xyz * morphWeight;
    output.positionCS = TransformObjectToHClip(positionOS);
    return output;
}

half4 FarionGeomorphDepthOnlyFragment(FarionGeomorphDepthVaryings input) : SV_TARGET
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return 0;
}

struct FarionGeomorphDepthNormalsVaryings
{
    float4 positionCS : SV_POSITION;
    float3 normalWS : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

FarionGeomorphDepthNormalsVaryings FarionGeomorphDepthNormalsVertex(
    FarionGeomorphDepthAttributes input)
{
    FarionGeomorphDepthNormalsVaryings output = (FarionGeomorphDepthNormalsVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float morphWeight = FarionResolveMorphWeight(input.positionOS.xyz);
    float3 positionOS = input.positionOS.xyz + input.morphOffsetOS.xyz * morphWeight;
    float3 normalOS = FarionApplyGeomorphNormal(
        input.normalOS,
        input.morphNormalOS.xyz,
        morphWeight);
    output.positionCS = TransformObjectToHClip(positionOS);
    output.normalWS = TransformObjectToWorldNormal(normalOS);
    return output;
}

void FarionGeomorphDepthNormalsFragment(
    FarionGeomorphDepthNormalsVaryings input,
    out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    outNormalWS = half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
#ifdef _WRITE_RENDERING_LAYERS
    uint renderingLayers = GetMeshRenderingLayer();
    outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
#endif
}

float3 _LightDirection;
float3 _LightPosition;

FarionGeomorphDepthVaryings FarionGeomorphShadowVertex(FarionGeomorphDepthAttributes input)
{
    FarionGeomorphDepthVaryings output = (FarionGeomorphDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float morphWeight = FarionResolveMorphWeight(input.positionOS.xyz);
    float3 positionOS = input.positionOS.xyz + input.morphOffsetOS.xyz * morphWeight;
    float3 positionWS = TransformObjectToWorld(positionOS);
    float3 normalOS = FarionApplyGeomorphNormal(
        input.normalOS,
        input.morphNormalOS.xyz,
        morphWeight);
    float3 normalWS = TransformObjectToWorldNormal(normalOS);

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    float4 positionCS = TransformWorldToHClip(
        ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif

    output.positionCS = positionCS;
    return output;
}

half4 FarionGeomorphShadowFragment(FarionGeomorphDepthVaryings input) : SV_TARGET
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return 0;
}

struct FarionGeomorphMotionVaryings
{
    float4 positionCS : SV_POSITION;
    float4 positionCSNoJitter : POSITION_CS_NO_JITTER;
    float4 previousPositionCSNoJitter : PREV_POSITION_CS_NO_JITTER;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

FarionGeomorphMotionVaryings FarionGeomorphMotionVertex(
    FarionGeomorphDepthAttributes input)
{
    FarionGeomorphMotionVaryings output = (FarionGeomorphMotionVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float currentWeight = FarionResolveMorphWeight(input.positionOS.xyz);
    float3 currentPositionOS = input.positionOS.xyz +
        input.morphOffsetOS.xyz * currentWeight;
    float4 currentPositionWS = mul(UNITY_MATRIX_M, float4(currentPositionOS, 1.0));
    output.positionCS = TransformWorldToHClip(currentPositionWS.xyz);
    output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, currentPositionWS);

    float4 previousBasePositionWS = mul(
        UNITY_PREV_MATRIX_M,
        float4(input.positionOS.xyz, 1.0));
    float previousWeight = FarionResolveMorphWeightWS(
        previousBasePositionWS.xyz,
        _FarionPreviousSurfaceObserverWS.xyz);
    float3 previousPositionOS = input.positionOS.xyz +
        input.morphOffsetOS.xyz * previousWeight;
    output.previousPositionCSNoJitter = mul(
        _PrevViewProjMatrix,
        mul(UNITY_PREV_MATRIX_M, float4(previousPositionOS, 1.0)));
    return output;
}

float4 FarionGeomorphMotionFragment(FarionGeomorphMotionVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    return float4(CalcNdcMotionVectorFromCsPositions(
        input.positionCSNoJitter,
        input.previousPositionCSNoJitter), 0, 0);
}

#endif
