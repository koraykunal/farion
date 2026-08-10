using Farion.Core.Physics;
using UnityEngine;

namespace Farion.Gameplay.Interaction
{
    static class InteractableLayerBinding
    {
        public static void Apply(Component interactable)
        {
            if (interactable == null)
            {
                return;
            }

            interactable.gameObject.layer = FarionLayers.Interactable;
        }
    }
}
