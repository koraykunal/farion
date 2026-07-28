using System;

namespace Farion.Gameplay.Domain.Fleet
{
    public sealed class BoundedResourceState
    {
        public BoundedResourceState(double maximum, double current)
        {
            if (!IsFinite(maximum) || maximum <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(maximum));
            }

            if (!IsFinite(current))
            {
                throw new ArgumentOutOfRangeException(nameof(current));
            }

            Maximum = maximum;
            Current = Clamp(current, 0d, maximum);
        }

        public double Current { get; private set; }
        public double Maximum { get; private set; }
        public double Normalized => Maximum > 0d ? Current / Maximum : 0d;
        public bool IsEmpty => Current <= 0d;
        public bool IsFull => Current >= Maximum;

        public bool TryConsume(double amount)
        {
            if (!IsFinite(amount) || amount <= 0d || Current < amount)
            {
                return false;
            }

            Current -= amount;
            return true;
        }

        public double Restore(double amount)
        {
            if (!IsFinite(amount) || amount <= 0d)
            {
                return 0d;
            }

            double accepted = Math.Min(amount, Maximum - Current);
            Current += accepted;
            return accepted;
        }

        public double Deplete(double amount)
        {
            if (!IsFinite(amount) || amount <= 0d)
            {
                return 0d;
            }

            double consumed = Math.Min(amount, Current);
            Current -= consumed;
            return consumed;
        }

        public bool TrySetMaximum(double maximum, bool preserveNormalizedValue)
        {
            if (!IsFinite(maximum) || maximum <= 0d)
            {
                return false;
            }

            double normalized = Normalized;
            Maximum = maximum;
            Current = preserveNormalizedValue
                ? maximum * normalized
                : Math.Min(Current, maximum);
            return true;
        }

        internal BoundedResourceState Clone()
        {
            return new BoundedResourceState(Maximum, Current);
        }

        internal void ReplaceWith(BoundedResourceState replacement)
        {
            if (replacement == null)
            {
                throw new ArgumentNullException(nameof(replacement));
            }

            Maximum = replacement.Maximum;
            Current = replacement.Current;
        }

        static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        static double Clamp(double value, double minimum, double maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }
}
