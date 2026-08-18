#ifndef FARION_OCEAN_WAVES_INCLUDED
#define FARION_OCEAN_WAVES_INCLUDED

// Mirror of Farion.Simulation.Planetary.OceanWaveField. Buoyancy samples the C#
// version, so any change here has to be made there as well.
#define FARION_OCEAN_WAVE_COUNT 3
#define FARION_OCEAN_FULL_TURN 6.28318530718

static const float3 FarionWaveDirections[FARION_OCEAN_WAVE_COUNT] =
{
    float3(0.9438601, 0.0, 0.3303510),
    float3(-0.3980149, 0.8955335, 0.1990074),
    float3(0.3030458, -0.5050763, 0.8081221)
};

static const float FarionWaveFrequencies[FARION_OCEAN_WAVE_COUNT] = { 1.0, 2.13, 4.31 };
static const float FarionWaveAmplitudes[FARION_OCEAN_WAVE_COUNT] = { 1.0, 0.55, 0.3 };
#define FARION_OCEAN_WAVE_AMPLITUDE_SUM 1.85

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
        float phase = dot(relativePosition, direction) * frequency + phases[i];
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
