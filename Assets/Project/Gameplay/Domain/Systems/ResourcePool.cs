using System;

namespace Farion.Gameplay.Domain.Systems
{
    [Serializable]
    public struct ResourcePool
    {
        public float Capacity;
        public float Current;

        public ResourcePool(float capacity, float current)
        {
            Capacity = Math.Max(0f, capacity);
            Current = Math.Clamp(current, 0f, Capacity);
        }

        public static ResourcePool Full(float capacity) =>
            new(capacity, capacity);

        public static ResourcePool Drained(float capacity) =>
            new(capacity, 0f);

        public readonly float Normalized =>
            Capacity > 0f ? Current / Capacity : 0f;
        public readonly float Missing => Capacity - Current;
        public readonly bool IsEmpty => Current <= 0f;
        public readonly bool IsFull => Current >= Capacity;

        public float Drain(float amount)
        {
            if (amount <= 0f)
            {
                return 0f;
            }

            float drained = Math.Min(amount, Current);
            Current -= drained;
            return drained;
        }

        public float Fill(float amount)
        {
            if (amount <= 0f)
            {
                return 0f;
            }

            float filled = Math.Min(amount, Missing);
            Current += filled;
            return filled;
        }

        public void SetCapacity(float capacity)
        {
            Capacity = Math.Max(0f, capacity);
            Current = Math.Clamp(Current, 0f, Capacity);
        }

        public void Refill()
        {
            Current = Capacity;
        }
    }
}
