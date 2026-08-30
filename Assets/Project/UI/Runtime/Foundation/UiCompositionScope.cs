using System;
using System.Collections.Generic;
using UnityEngine;

namespace Farion.UI.Foundation
{
    /// <summary>
    /// Resolves UI services and views that belong to the same authored Canvas.
    /// System prefabs are siblings under that Canvas, never nested inside
    /// UI_SystemRoot.
    /// </summary>
    public static class UiCompositionScope
    {
        public static Canvas FindOwningCanvas(Component context)
        {
            return context != null
                ? context.GetComponentInParent<Canvas>(includeInactive: true)
                : null;
        }

        public static UiSystemRoot FindSystemRoot(Component context)
        {
            return FindFirstInScope<UiSystemRoot>(context);
        }

        public static T FindFirstInScope<T>(Component context)
            where T : Component
        {
            T[] candidates = FindAllInScope<T>(context);
            return candidates.Length > 0 ? candidates[0] : null;
        }

        public static T[] FindAllInScope<T>(Component context)
            where T : Component
        {
            if (context == null)
            {
                return Array.Empty<T>();
            }

            Canvas owningCanvas = FindOwningCanvas(context);
            Transform searchRoot = owningCanvas != null
                ? owningCanvas.transform
                : context.transform;
            T[] candidates = searchRoot.GetComponentsInChildren<T>(
                includeInactive: true);
            if (owningCanvas == null)
            {
                return candidates;
            }

            List<T> owned = new(candidates.Length);
            for (int i = 0; i < candidates.Length; i++)
            {
                T candidate = candidates[i];
                if (candidate != null &&
                    FindOwningCanvas(candidate) == owningCanvas)
                {
                    owned.Add(candidate);
                }
            }

            return owned.ToArray();
        }
    }
}
