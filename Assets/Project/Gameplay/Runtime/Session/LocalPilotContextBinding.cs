using UnityEngine;

namespace Farion.Gameplay.Session
{
    public static class LocalPilotContextBinding
    {
        public static void Apply(Transform actorRoot, ILocalPilotContext context)
        {
            if (actorRoot == null)
            {
                return;
            }

            MonoBehaviour[] behaviours =
                actorRoot.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ILocalPilotContextReceiver receiver)
                {
                    receiver.SetPilotContext(context);
                }
            }
        }
    }
}
