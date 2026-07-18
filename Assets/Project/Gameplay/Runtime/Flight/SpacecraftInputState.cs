using UnityEngine;

namespace Farion.Gameplay.Flight
{
    public readonly struct SpacecraftInputState
    {
        public SpacecraftInputState(Vector3 translation, Vector2 look, float roll, bool boost)
        {
            Translation = Vector3.ClampMagnitude(translation, 1f);
            Look = look;
            Roll = Mathf.Clamp(roll, -1f, 1f);
            Boost = boost;
        }

        public Vector3 Translation { get; }
        public Vector2 Look { get; }
        public float Roll { get; }
        public bool Boost { get; }

        public static SpacecraftInputState None => new(Vector3.zero, Vector2.zero, 0f, false);
    }
}
