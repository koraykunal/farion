using Farion.Audio.Direction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Farion.UI.Feedback
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public sealed class UiAudioFeedback :
        MonoBehaviour,
        IPointerEnterHandler,
        ISelectHandler,
        IPointerClickHandler,
        ISubmitHandler
    {
        [SerializeField] UiAudioCue activationCue = UiAudioCue.Confirm;
        [SerializeField] Selectable selectable;

        void Awake()
        {
            selectable ??= GetComponent<Selectable>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (IsInteractable())
            {
                AudioDirector.Current?.PlayUi(UiAudioCue.Focus);
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (IsInteractable())
            {
                AudioDirector.Current?.PlayUi(UiAudioCue.Focus);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            PlayActivation();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            PlayActivation();
        }

        void PlayActivation()
        {
            AudioDirector.Current?.PlayUi(
                IsInteractable() ? activationCue : UiAudioCue.Unavailable);
        }

        bool IsInteractable()
        {
            selectable ??= GetComponent<Selectable>();
            return selectable != null && selectable.IsActive() && selectable.IsInteractable();
        }
    }
}
