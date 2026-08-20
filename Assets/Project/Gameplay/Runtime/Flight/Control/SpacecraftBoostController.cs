using UnityEngine;

namespace Farion.Gameplay.Flight
{
    public struct SpacecraftBoostState
    {
        public float Authority;
        public float Surge;
    }

    sealed class SpacecraftBoostController
    {
        const float SurgeDecaySeconds = 0.35f;

        float authority;
        float surge;
        bool boostingLastStep;

        public float Authority => authority;
        public float Surge => surge;
        public bool IsActive => authority > 0.001f;

        public void Step(
            bool requested,
            bool thrustAvailable,
            float deltaTime,
            float spoolRate)
        {
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            bool canBoost = requested && thrustAvailable;
            if (canBoost && !boostingLastStep)
            {
                surge = 1f;
            }
            else
            {
                surge = Mathf.MoveTowards(surge, 0f, safeDeltaTime / SurgeDecaySeconds);
            }

            boostingLastStep = canBoost;
            authority = Mathf.MoveTowards(
                authority,
                canBoost ? 1f : 0f,
                Mathf.Max(0f, spoolRate) * safeDeltaTime);
        }

        public void Reset()
        {
            authority = 0f;
            surge = 0f;
            boostingLastStep = false;
        }

        public SpacecraftBoostState CaptureState() => new()
        {
            Authority = authority,
            Surge = surge
        };

        public void RestoreState(SpacecraftBoostState state)
        {
            authority = Mathf.Clamp01(state.Authority);
            surge = Mathf.Clamp01(state.Surge);
            boostingLastStep = authority > 0.001f;
        }
    }
}
