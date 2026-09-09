using UnityEngine;

namespace Farion.Gameplay.Character
{
    public readonly struct ExplorerMotorInput
    {
        public ExplorerMotorInput(
            Vector3 movement,
            Vector3 lookDirection,
            bool aim,
            bool jump,
            bool sprint,
            bool swimAscend,
            bool swimDescend = false)
        {
            SwimDescend = swimDescend;
            Movement = IsFinite(movement) ? Vector3.ClampMagnitude(movement, 1f) : Vector3.zero;
            LookDirection = IsFinite(lookDirection) && lookDirection.sqrMagnitude > 0.0001f
                ? lookDirection.normalized
                : Vector3.zero;
            Aim = aim;
            Jump = jump;
            Sprint = sprint;
            SwimAscend = swimAscend;
        }

        public Vector3 Movement { get; }
        public Vector3 LookDirection { get; }
        public bool Aim { get; }
        public bool Jump { get; }
        public bool Sprint { get; }
        public bool SwimAscend { get; }
        public bool SwimDescend { get; }

        public static ExplorerMotorInput None =>
            new(Vector3.zero, Vector3.zero, false, false, false, false);

        static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
