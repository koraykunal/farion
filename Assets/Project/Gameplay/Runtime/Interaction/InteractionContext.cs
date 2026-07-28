using Farion.Gameplay.Commands;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    public readonly struct InteractionContext
    {
        public InteractionContext(
            GameObject actor,
            Transform view,
            RaycastHit hit,
            IGameplayCommandGateway commands = null)
        {
            Actor = actor;
            View = view;
            Hit = hit;
            Commands = commands;
        }

        public GameObject Actor { get; }
        public Transform View { get; }
        public RaycastHit Hit { get; }
        public IGameplayCommandGateway Commands { get; }
        public Vector3 Point => Hit.point;
        public Vector3 Normal => Hit.normal;
    }
}
