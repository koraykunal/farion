using UnityEngine;

namespace Farion.Gameplay.Flight
{
    public readonly struct SpacecraftPilotCommand
    {
        public SpacecraftPilotCommand(
            Vector3 translation,
            Vector3 rotation,
            bool boost,
            bool brake,
            bool toggleFlightAssist)
        {
            Translation = ClampAxes(translation);
            Rotation = ClampAxes(rotation);
            Boost = boost;
            Brake = brake;
            ToggleFlightAssist = toggleFlightAssist;
        }

        public Vector3 Translation { get; }
        public Vector3 Rotation { get; }
        public bool Boost { get; }
        public bool Brake { get; }
        public bool ToggleFlightAssist { get; }

        public static SpacecraftPilotCommand None => new(
            Vector3.zero,
            Vector3.zero,
            boost: false,
            brake: false,
            toggleFlightAssist: false);

        static Vector3 ClampAxes(Vector3 value)
        {
            return new Vector3(
                Mathf.Clamp(value.x, -1f, 1f),
                Mathf.Clamp(value.y, -1f, 1f),
                Mathf.Clamp(value.z, -1f, 1f));
        }
    }
}
