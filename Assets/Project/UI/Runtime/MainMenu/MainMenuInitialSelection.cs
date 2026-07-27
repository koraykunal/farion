using UnityEngine;
using UnityEngine.EventSystems;

namespace Farion.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class MainMenuInitialSelection : MonoBehaviour
    {
        [SerializeField] MainMenuController controller;
        [SerializeField] GameObject continueButton;
        [SerializeField] GameObject newGameButton;

        void Reset()
        {
            ResolveReferences();
        }

        void Start()
        {
            ResolveReferences();
            SelectInitialButton();
        }

        void ResolveReferences()
        {
            if (controller == null)
            {
                controller = GetComponent<MainMenuController>();
            }

            if (controller == null)
            {
                controller = GetComponentInParent<MainMenuController>();
            }

            if (controller == null)
            {
#if UNITY_EDITOR
                Debug.LogError($"{nameof(MainMenuInitialSelection)} on {name} requires a local, parent, or explicit {nameof(MainMenuController)} reference.", this);
#endif
            }
        }

        void SelectInitialButton()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            GameObject target = controller != null && controller.HasSaveGame
                ? continueButton
                : newGameButton;

            if (target == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target);
        }
    }
}
