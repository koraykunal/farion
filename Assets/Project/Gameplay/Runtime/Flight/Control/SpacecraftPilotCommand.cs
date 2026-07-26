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
            Translation = Vector3.ClampMagnitude(translation, 1f);
            Rotation = Vector3.ClampMagnitude(rotation, 1f);
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
    }
}
