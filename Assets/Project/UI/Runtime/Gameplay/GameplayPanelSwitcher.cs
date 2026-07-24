using UnityEngine;
using Farion.UI.Common;

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayPanelSwitcher : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] GameObject hudRoot;
        [SerializeField] GameObject pauseMenuPanel;
        [SerializeField] GameObject inventoryPanel;

        public void Show(GameplayScreenState screen)
        {
            Show(screen, Application.isPlaying);
        }

        void Awake()
        {
            Show(GameplayScreenState.None, animated: false);
        }

        void Show(GameplayScreenState screen, bool animated)
        {
            SetActive(hudRoot, screen == GameplayScreenState.None, animated);
            SetActive(pauseMenuPanel, screen == GameplayScreenState.PauseMenu, animated);
            SetActive(inventoryPanel, screen == GameplayScreenState.Inventory, animated);
        }

        static void SetActive(GameObject target, bool active, bool animated)
        {
            if (target == null)
            {
                return;
            }

            if (target.TryGetComponent(out FarionPanelFader fader))
            {
                fader.SetVisible(active, animated);
                return;
            }

            target.SetActive(active);
        }
    }
}
