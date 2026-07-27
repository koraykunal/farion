using Farion.UI.Common;
using UnityEngine;

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MenuButtonView))]
    public sealed class GameplayMenuButton : MonoBehaviour
    {
        [Header("Action")]
        [SerializeField] GameplayMenuAction action;
        [SerializeField] GameplayUiController controller;
        [SerializeField] MenuButtonView view;

        public GameplayMenuAction Action => action;

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

        void ResolveReferences()
        {
            if (view == null)
            {
                view = GetComponent<MenuButtonView>();
            }

            if (controller == null)
            {
                controller = GetComponentInParent<GameplayUiController>();
            }

            if (controller == null)
            {
#if UNITY_EDITOR
                Debug.LogError($"{nameof(GameplayMenuButton)} on {name} requires a parent or explicit {nameof(GameplayUiController)} reference.", this);
#endif
            }
        }

        void InvokeAction()
        {
            controller?.Handle(action);
        }
    }
}
