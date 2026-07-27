using UnityEngine;

namespace Farion.Gameplay.Flight
{
    public readonly struct SpacecraftInputState
    {
        public SpacecraftInputState(Vector3 translation, Vector2 look, float roll, bool boost)
            : this(translation, look, roll, boost, brake: false, toggleFlightAssist: false)
        {
        }

        public SpacecraftInputState(
            Vector3 translation,
            Vector2 look,
            float roll,
            bool boost,
            bool brake,
            bool toggleFlightAssist,
            bool toggleLandingGear = false)
        {
            Translation = ClampAxes(translation);
            Look = look;
            Roll = Mathf.Clamp(roll, -1f, 1f);
            Boost = boost;
            Brake = brake;
            ToggleFlightAssist = toggleFlightAssist;
            ToggleLandingGear = toggleLandingGear;
        }

        public Vector3 Translation { get; }
        public Vector2 Look { get; }
        public float Roll { get; }
        public bool Boost { get; }
        public bool Brake { get; }
        public bool ToggleFlightAssist { get; }
        public bool ToggleLandingGear { get; }

        public static SpacecraftInputState None => new(Vector3.zero, Vector2.zero, 0f, false);

        static Vector3 ClampAxes(Vector3 value)
        {
            return new Vector3(
                Mathf.Clamp(value.x, -1f, 1f),
                Mathf.Clamp(value.y, -1f, 1f),
                Mathf.Clamp(value.z, -1f, 1f));
        }
    }
}
