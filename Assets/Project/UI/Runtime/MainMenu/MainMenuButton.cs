using Farion.UI.Common;
using UnityEngine;

namespace Farion.UI.MainMenu
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MenuButtonView))]
    public sealed class MainMenuButton : MonoBehaviour
    {
        [Header("Action")]
        [SerializeField] MainMenuAction action;
        [SerializeField] MainMenuController controller;
        [SerializeField] MenuButtonView view;

        public MainMenuAction Action => action;

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            ResolveReferences();
            if (view != null)
            {
                view.Clicked -= InvokeAction;
                view.Clicked += InvokeAction;
            }
        }

        void OnDisable()
        {
            if (view != null)
            {
                view.Clicked -= InvokeAction;
            }
        }

        public void SetAvailable(bool available)
        {
            ResolveReferences();
            view?.SetAvailable(available);
        }

        public void ConfigureContent(string title, string subtitle, Sprite icon)
        {
            ResolveReferences();
            view?.ConfigureContent(title, subtitle, icon);
        }

        void ResolveReferences()
        {
            if (view == null)
            {
                view = GetComponent<MenuButtonView>();
            }

            if (controller == null)
            {
                controller = GetComponentInParent<MainMenuController>();
            }

            if (controller == null)
            {
                Debug.LogError($"{nameof(MainMenuButton)} on {name} requires a parent or explicit {nameof(MainMenuController)} reference.", this);
            }
        }

        void InvokeAction()
        {
            controller?.Handle(action);
        }
    }
}
