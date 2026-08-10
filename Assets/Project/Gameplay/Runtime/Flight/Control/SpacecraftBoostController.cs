using UnityEngine;

namespace Farion.Gameplay.Flight
{
    public struct SpacecraftBoostState
    {
        public float Authority;
        public float Surge;
        public float Charge;
        public float RechargeDelayRemaining;
        public bool LockedUntilReleased;
    }

    sealed class SpacecraftBoostController
    {
        const float SurgeDecaySeconds = 0.35f;

        float authority;
        float surge;
        bool boostingLastStep;
        float charge = 1f;
        float rechargeDelayRemaining;
        bool lockedUntilReleased;

        public float Authority => authority;
        public float Surge => surge;
        public float Charge => charge;
        public bool IsActive => authority > 0.001f;
        public bool IsLocked => lockedUntilReleased;

        public void Step(
            bool requested,
            bool hasForwardThrottle,
            float deltaTime,
            float spoolRate,
            float drainPerSecond,
            float rechargePerSecond,
            float rechargeDelay)
        {
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            if (!requested)
            {
                lockedUntilReleased = false;
            }

            bool canBoost = requested &&
                hasForwardThrottle &&
                !lockedUntilReleased &&
                charge > 0f;
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

            if (canBoost)
            {
                charge = Mathf.Max(0f, charge - Mathf.Max(0f, drainPerSecond) * safeDeltaTime);
                rechargeDelayRemaining = Mathf.Max(0f, rechargeDelay);
                if (charge <= 0f)
                {
                    lockedUntilReleased = true;
                }

                return;
            }

            rechargeDelayRemaining = Mathf.Max(0f, rechargeDelayRemaining - safeDeltaTime);
            if (rechargeDelayRemaining <= 0f)
            {
                charge = Mathf.Min(1f, charge + Mathf.Max(0f, rechargePerSecond) * safeDeltaTime);
            }
        }

        public void Reset(float initialCharge = 1f)
        {
            authority = 0f;
            surge = 0f;
            boostingLastStep = false;
            charge = Mathf.Clamp01(initialCharge);
            rechargeDelayRemaining = 0f;
            lockedUntilReleased = false;
        }

        public SpacecraftBoostState CaptureState() => new()
        {
            Authority = authority,
            Surge = surge,
            Charge = charge,
            RechargeDelayRemaining = rechargeDelayRemaining,
            LockedUntilReleased = lockedUntilReleased
        };

        public void RestoreState(SpacecraftBoostState state)
        {
            authority = Mathf.Clamp01(state.Authority);
            surge = Mathf.Clamp01(state.Surge);
            charge = Mathf.Clamp01(state.Charge);
            rechargeDelayRemaining = Mathf.Max(0f, state.RechargeDelayRemaining);
            lockedUntilReleased = state.LockedUntilReleased;
        }
    }
}
