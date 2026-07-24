using UnityEngine;
using Farion.UI.Common;

namespace Farion.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class MainMenuPanelSwitcher : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] GameObject mainPanel;
        [SerializeField] GameObject settingsPanel;
        [SerializeField] GameObject creditsPanel;
        [SerializeField] GameObject confirmDialog;
        [SerializeField] GameObject loadingOverlay;

        public void ShowMain()
        {
            ShowOnly(mainPanel, Application.isPlaying);
        }

        public void ShowSettings()
        {
            ShowOnly(settingsPanel, Application.isPlaying);
        }

        public void ShowCredits()
        {
            ShowOnly(creditsPanel, Application.isPlaying);
        }

        public void ShowConfirmDialog()
        {
            ShowOnly(confirmDialog, Application.isPlaying);
        }

        public void ShowLoading()
        {
            ShowOnly(loadingOverlay, Application.isPlaying);
        }

        void Awake()
        {
            ShowOnly(mainPanel, animated: false);
        }

        void ShowOnly(GameObject activePanel, bool animated)
        {
            SetActive(mainPanel, activePanel == mainPanel, animated);
            SetActive(settingsPanel, activePanel == settingsPanel, animated);
            SetActive(creditsPanel, activePanel == creditsPanel, animated);
            SetActive(confirmDialog, activePanel == confirmDialog, animated);
            SetActive(loadingOverlay, activePanel == loadingOverlay, animated);
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
