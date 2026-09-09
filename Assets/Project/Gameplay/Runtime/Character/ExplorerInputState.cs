using UnityEngine;

namespace Farion.Gameplay.Character
{
    public readonly struct ExplorerInputState
    {
        public ExplorerInputState(
            Vector2 movement,
            Vector2 look,
            bool jump,
            bool sprint,
            bool interact,
            bool aim = false,
            bool toggleTool = false,
            bool useTool = false,
            bool dive = false)
        {
            Dive = dive;
            Movement = Vector2.ClampMagnitude(movement, 1f);
            Look = look;
            Jump = jump;
            Sprint = sprint;
            Interact = interact;
            Aim = aim;
            ToggleTool = toggleTool;
            UseTool = useTool;
        }

        public Vector2 Movement { get; }
        public Vector2 Look { get; }
        public bool Jump { get; }
        public bool Sprint { get; }
        public bool Interact { get; }
        public bool Aim { get; }
        public bool ToggleTool { get; }
        public bool UseTool { get; }
        public bool Dive { get; }

        public static ExplorerInputState None => new(Vector2.zero, Vector2.zero, false, false, false);
    }
}
