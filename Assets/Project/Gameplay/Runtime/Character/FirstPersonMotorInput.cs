using UnityEngine;

namespace Farion.Gameplay.Character
{
    public readonly struct FirstPersonMotorInput
    {
        public const float MaximumYawDegreesPerStep = 45f;

        public FirstPersonMotorInput(
            Vector2 movement,
            float yawDegrees,
            bool jump,
            bool sprint,
            bool swimAscend)
        {
            Movement = Vector2.ClampMagnitude(movement, 1f);
            YawDegrees = Mathf.Clamp(
                float.IsFinite(yawDegrees) ? yawDegrees : 0f,
                -MaximumYawDegreesPerStep,
                MaximumYawDegreesPerStep);
            Jump = jump;
            Sprint = sprint;
            SwimAscend = swimAscend;
        }

        public Vector2 Movement { get; }
        public float YawDegrees { get; }
        public bool Jump { get; }
        public bool Sprint { get; }
        public bool SwimAscend { get; }

        public static FirstPersonMotorInput None =>
            new(Vector2.zero, 0f, false, false, false);
    }
}
