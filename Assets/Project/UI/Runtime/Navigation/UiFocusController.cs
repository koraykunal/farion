using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Farion.UI.Navigation
{
    [DisallowMultipleComponent]
    public sealed class UiFocusController : MonoBehaviour
    {
        Coroutine pendingFocus;

        public GameObject CaptureCurrentSelection()
        {
            return EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
        }

        public void Focus(UiScreenView screen)
        {
            Selectable selectable = screen != null
                ? screen.ResolveFirstSelection()
                : null;
            ScheduleFocus(selectable != null ? selectable.gameObject : null);
        }

        public void Restore(GameObject previousSelection, UiScreenView fallbackScreen)
        {
            if (IsValidSelection(previousSelection))
            {
                ScheduleFocus(previousSelection);
                return;
            }

            Focus(fallbackScreen);
        }

        public void Clear()
        {
            ScheduleFocus(null);
        }

        void ScheduleFocus(GameObject target)
        {
            if (pendingFocus != null)
            {
                StopCoroutine(pendingFocus);
                pendingFocus = null;
            }

            if (!Application.isPlaying)
            {
                ApplyFocus(target);
                return;
            }

            pendingFocus = StartCoroutine(FocusNextFrame(target));
        }

        IEnumerator FocusNextFrame(GameObject target)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            ApplyFocus(target);
            pendingFocus = null;
        }

        void ApplyFocus(GameObject target)
        {
            EventSystem eventSystem = ResolveEventSystem();
            if (eventSystem == null)
            {
                return;
            }

            eventSystem.SetSelectedGameObject(null);
            if (IsValidSelection(target))
            {
                eventSystem.SetSelectedGameObject(target);
            }
        }

        EventSystem ResolveEventSystem()
        {
            if (EventSystem.current != null)
            {
                return EventSystem.current;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            return canvas != null
                ? canvas.GetComponentInChildren<EventSystem>(true)
                : null;
        }

        static bool IsValidSelection(GameObject target)
        {
            return target != null &&
                   target.activeInHierarchy &&
                   target.TryGetComponent(out Selectable selectable) &&
                   selectable.IsActive() &&
                   selectable.IsInteractable();
        }
    }
}
