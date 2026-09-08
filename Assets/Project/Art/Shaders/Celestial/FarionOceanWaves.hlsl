#ifndef FARION_OCEAN_WAVES_INCLUDED
#define FARION_OCEAN_WAVES_INCLUDED

// Mirror of Farion.Simulation.Planetary.OceanWaveField. Buoyancy samples the C#
// version, so any change here has to be made there as well.
// Nine plane waves in three dispersion groups; waves 3k..3k+2 share phase k.
#define FARION_OCEAN_WAVE_COUNT 9
#define FARION_OCEAN_WAVE_GROUP_COUNT 3
#define FARION_OCEAN_FULL_TURN 6.28318530718

static const float3 FarionWaveDirections[FARION_OCEAN_WAVE_COUNT] =
{
    float3(0.9438584, 0.0000000, 0.3303504),
    float3(0.9282791, 0.3094264, 0.2062842),
    float3(0.7752025, -0.3391511, 0.5329517),
    float3(-0.3980149, 0.8955335, 0.1990074),
    float3(-0.0998752, 0.9488147, -0.2996257),
    float3(-0.5848442, 0.6823183, 0.4386332),
    float3(0.3030458, -0.5050763, 0.8081220),
    float3(0.5628090, -0.3069867, 0.7674668),
    float3(0.0504433, -0.7062066, 0.7062066)
};

static const float FarionWaveFrequencies[FARION_OCEAN_WAVE_COUNT] =
    { 1.0, 0.91, 1.12, 2.13, 1.9383, 2.3856, 4.31, 3.9221, 4.8272 };
static const float FarionWaveAmplitudes[FARION_OCEAN_WAVE_COUNT] =
    { 0.5, 0.3, 0.25, 0.28, 0.18, 0.14, 0.15, 0.1, 0.08 };
static const float FarionWavePhaseOffsets[FARION_OCEAN_WAVE_COUNT] =
    { 0.0, 1.7, 3.9, 0.6, 2.8, 5.1, 1.3, 4.4, 2.2 };
#define FARION_OCEAN_WAVE_AMPLITUDE_SUM 1.98

float FarionSampleWaveHeight(
    float3 relativePosition,
    float waveLength,
    float amplitude,
    float3 phases,
    out float3 gradient)
{
    gradient = 0.0;
    if (amplitude <= 0.0 || waveLength <= 0.0)
    {
        return 0.0;
    }

    float height = 0.0;
    [unroll]
    for (int i = 0; i < FARION_OCEAN_WAVE_COUNT; i++)
    {
        float3 direction = FarionWaveDirections[i];
        float frequency = FARION_OCEAN_FULL_TURN * FarionWaveFrequencies[i] / waveLength;
        float phase = dot(relativePosition, direction) * frequency
            + phases[i / FARION_OCEAN_WAVE_GROUP_COUNT]
            + FarionWavePhaseOffsets[i];
        float weight = FarionWaveAmplitudes[i] / FARION_OCEAN_WAVE_AMPLITUDE_SUM;
        float sine;
        float cosine;
        sincos(phase, sine, cosine);
        height += weight * sine;
        gradient += weight * cosine * frequency * direction;
    }

    gradient *= amplitude;
    return amplitude * height;
}

float3 FarionApplyWaveNormal(float3 sphereNormal, float3 gradient)
{
    float3 tangentGradient = gradient - sphereNormal * dot(gradient, sphereNormal);
    return normalize(sphereNormal - tangentGradient);
}

#endif
