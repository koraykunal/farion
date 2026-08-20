using System;

namespace Farion.Gameplay.Domain.Systems
{
    [Serializable]
    public struct ShipModuleBonuses
    {
        public float MaxSpeedBonus;
        public float BoostSpeedBonus;
        public float FuelCapacityBonus;
        public float HullCapacityBonus;

        public ShipModuleBonuses(
            float maxSpeedBonus,
            float boostSpeedBonus,
            float fuelCapacityBonus,
            float hullCapacityBonus)
        {
            MaxSpeedBonus = maxSpeedBonus;
            BoostSpeedBonus = boostSpeedBonus;
            FuelCapacityBonus = fuelCapacityBonus;
            HullCapacityBonus = hullCapacityBonus;
        }

        public static ShipModuleBonuses None => default;

        public readonly float MaxSpeedMultiplier => ToMultiplier(MaxSpeedBonus);
        public readonly float BoostSpeedMultiplier => ToMultiplier(BoostSpeedBonus);
        public readonly float FuelCapacityMultiplier => ToMultiplier(FuelCapacityBonus);
        public readonly float HullCapacityMultiplier => ToMultiplier(HullCapacityBonus);

        public readonly ShipModuleBonuses Combine(in ShipModuleBonuses other) => new(
            MaxSpeedBonus + other.MaxSpeedBonus,
            BoostSpeedBonus + other.BoostSpeedBonus,
            FuelCapacityBonus + other.FuelCapacityBonus,
            HullCapacityBonus + other.HullCapacityBonus);

        static float ToMultiplier(float bonus) => Math.Max(0f, 1f + bonus);
    }
}
