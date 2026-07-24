using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    public readonly struct InteractionContext
    {
        public InteractionContext(GameObject actor, Transform view, RaycastHit hit)
        {
            Actor = actor;
            View = view;
            Hit = hit;
        }

        public GameObject Actor { get; }
        public Transform View { get; }
        public RaycastHit Hit { get; }
        public Vector3 Point => Hit.point;
        public Vector3 Normal => Hit.normal;
    }
}
