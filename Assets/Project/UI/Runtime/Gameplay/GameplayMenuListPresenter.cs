using System;
using System.Collections.Generic;
using Farion.UI.Common;
using UnityEngine;

namespace Farion.UI.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameplayMenuListPresenter : MonoBehaviour
    {
        [Serializable]
        sealed class MenuEntry
        {
            [SerializeField] GameplayMenuAction action;
            [SerializeField] string title;
            [SerializeField] Sprite icon;
            [SerializeField] bool available = true;

            public GameplayMenuAction Action => action;
            public string Title => title;
            public Sprite Icon => icon;
            public bool Available => available;
        }

        [Header("References")]
        [SerializeField] GameplayUiController controller;
        [SerializeField] Transform buttonContainer;
        [SerializeField] MenuButtonView buttonPrefab;

        [Header("Entries")]
        [SerializeField] List<MenuEntry> entries = new();

        readonly List<MenuButtonView> generatedButtons = new();
        bool built;

        void Awake()
        {
            ResolveReferences();
            Build();
        }

        void OnEnable()
        {
            ResolveReferences();
        }

        public void Rebuild()
        {
            built = false;
            ClearGeneratedButtons();
            Build();
        }

        void Build()
        {
            if (built || buttonPrefab == null)
            {
                return;
            }

            Transform parent = buttonContainer != null ? buttonContainer : transform;
            for (int i = 0; i < entries.Count; i++)
            {
                MenuEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                MenuButtonView button = Instantiate(buttonPrefab, parent);
                button.name = $"UI_MenuButton_{entry.Action}";
                button.ConfigureContent(entry.Title, string.Empty, entry.Icon);
                button.SetAvailable(entry.Available);

                GameplayMenuAction action = entry.Action;
                button.Clicked += () => Handle(action);
                generatedButtons.Add(button);
            }

            built = true;
        }

        void ClearGeneratedButtons()
        {
            for (int i = generatedButtons.Count - 1; i >= 0; i--)
            {
                MenuButtonView button = generatedButtons[i];
                if (button == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(button.gameObject);
                }
                else
                {
                    DestroyImmediate(button.gameObject);
                }
            }

            generatedButtons.Clear();
        }

        void Handle(GameplayMenuAction action)
        {
            ResolveReferences();
            controller?.Handle(action);
        }

        void ResolveReferences()
        {
            if (buttonContainer == null)
            {
                buttonContainer = transform;
            }

            if (controller == null)
            {
                controller = GetComponentInParent<GameplayUiController>();
            }
        }
    }
}
