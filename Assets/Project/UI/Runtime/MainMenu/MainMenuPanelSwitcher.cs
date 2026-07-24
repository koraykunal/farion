using UnityEngine;

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
            ShowOnly(mainPanel);
        }

        public void ShowSettings()
        {
            ShowOnly(settingsPanel);
        }

        public void ShowCredits()
        {
            ShowOnly(creditsPanel);
        }

        public void ShowConfirmDialog()
        {
            ShowOnly(confirmDialog);
        }

        public void ShowLoading()
        {
            ShowOnly(loadingOverlay);
        }

        void Awake()
        {
            ShowMain();
        }

        void ShowOnly(GameObject activePanel)
        {
            SetActive(mainPanel, activePanel == mainPanel);
            SetActive(settingsPanel, activePanel == settingsPanel);
            SetActive(creditsPanel, activePanel == creditsPanel);
            SetActive(confirmDialog, activePanel == confirmDialog);
            SetActive(loadingOverlay, activePanel == loadingOverlay);
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
