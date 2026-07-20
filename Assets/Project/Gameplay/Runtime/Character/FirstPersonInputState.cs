using UnityEngine;

namespace Farion.Gameplay.Character
{
    public readonly struct FirstPersonInputState
    {
        public FirstPersonInputState(
            Vector2 movement,
            Vector2 look,
            bool jump,
            bool sprint,
            bool interact)
        {
            Movement = Vector2.ClampMagnitude(movement, 1f);
            Look = look;
            Jump = jump;
            Sprint = sprint;
            Interact = interact;
        }

        public Vector2 Movement { get; }
        public Vector2 Look { get; }
        public bool Jump { get; }
        public bool Sprint { get; }
        public bool Interact { get; }

        public static FirstPersonInputState None => new(Vector2.zero, Vector2.zero, false, false, false);
    }
}
