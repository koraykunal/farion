using UnityEngine;

namespace Farion.Gameplay.Flight
{
    sealed class SpacecraftBoostController
    {
        float authority;
        float charge = 1f;
        float rechargeDelayRemaining;
        bool lockedUntilReleased;

        public float Authority => authority;
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
            charge = Mathf.Clamp01(initialCharge);
            rechargeDelayRemaining = 0f;
            lockedUntilReleased = false;
        }
    }
}
