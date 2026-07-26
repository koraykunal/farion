using UnityEngine;

namespace Farion.Gameplay.Flight
{
    public readonly struct SpacecraftThrusterCommand
    {
        public SpacecraftThrusterCommand(
            float forward,
            float reverse,
            float strafeRight,
            float strafeLeft,
            float ascend,
            float descend,
            float pitchUp,
            float pitchDown,
            float yawRight,
            float yawLeft,
            float rollRight,
            float rollLeft)
        {
            Forward = Mathf.Clamp01(forward);
            Reverse = Mathf.Clamp01(reverse);
            StrafeRight = Mathf.Clamp01(strafeRight);
            StrafeLeft = Mathf.Clamp01(strafeLeft);
            Ascend = Mathf.Clamp01(ascend);
            Descend = Mathf.Clamp01(descend);
            PitchUp = Mathf.Clamp01(pitchUp);
            PitchDown = Mathf.Clamp01(pitchDown);
            YawRight = Mathf.Clamp01(yawRight);
            YawLeft = Mathf.Clamp01(yawLeft);
            RollRight = Mathf.Clamp01(rollRight);
            RollLeft = Mathf.Clamp01(rollLeft);
        }

        public float Forward { get; }
        public float Reverse { get; }
        public float StrafeRight { get; }
        public float StrafeLeft { get; }
        public float Ascend { get; }
        public float Descend { get; }
        public float PitchUp { get; }
        public float PitchDown { get; }
        public float YawRight { get; }
        public float YawLeft { get; }
        public float RollRight { get; }
        public float RollLeft { get; }

        public float LinearActivity => Mathf.Max(Forward, Reverse, StrafeRight, StrafeLeft, Ascend, Descend);
        public float AngularActivity => Mathf.Max(PitchUp, PitchDown, YawRight, YawLeft, RollRight, RollLeft);
        public float Activity => Mathf.Max(LinearActivity, AngularActivity);

        public static SpacecraftThrusterCommand None => new(
            0f, 0f,
            0f, 0f,
            0f, 0f,
            0f, 0f,
            0f, 0f,
            0f, 0f);
    }
}
