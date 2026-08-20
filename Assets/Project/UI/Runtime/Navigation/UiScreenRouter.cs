using System;
using System.Collections.Generic;
using Farion.Audio;
using Farion.Gameplay.Input;
using Farion.UI.Foundation;
using UnityEngine;

namespace Farion.UI.Navigation
{
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UiFocusController))]
    public sealed class UiScreenRouter : MonoBehaviour
    {
        readonly struct HistoryEntry
        {
            public HistoryEntry(UiScreenView screen, GameObject previousSelection)
            {
                Screen = screen;
                PreviousSelection = previousSelection;
            }

            public UiScreenView Screen { get; }
            public GameObject PreviousSelection { get; }
        }

        [Header("Composition")]
        [SerializeField] List<UiScreenView> screens = new();
        [SerializeField] UiScreenId initialScreen = UiScreenId.None;
        [SerializeField] UiFocusController focusController;
        [SerializeField] PlayerControlLock controlLock;

        readonly Dictionary<UiScreenId, UiScreenView> screensById = new();
        readonly List<HistoryEntry> history = new();
        bool initialized;
        int cancelHandledFrame = -1;
        readonly List<IUiCancelConsumer> cancelConsumers = new();

        public event Action<UiScreenId> ScreenOpened;
        public event Action<UiScreenId> ScreenClosed;
        public event Action<UiScreenId> TopScreenChanged;

        public UiScreenId TopScreenId =>
            history.Count > 0 ? history[^1].Screen.ScreenId : UiScreenId.None;
        public bool HasOpenScreen => history.Count > 0;
        public bool CancelHandledThisFrame =>
            Application.isPlaying && cancelHandledFrame == Time.frameCount;

        void Reset()
        {
            ResolveReferences();
            CollectChildScreens();
        }

        void Awake()
        {
            Initialize();
        }

        void Update()
        {
            if (FarionInputActions.UiCancel.WasPressedThisFrame() &&
                TryHandleCancel())
            {
                cancelHandledFrame = Time.frameCount;
            }
        }

        void OnDisable()
        {
            if (controlLock != null)
            {
                controlLock.SetLocked(PlayerControlLockReason.UserInterface, false);
            }
        }

        void OnValidate()
        {
            ResolveReferences();
            screens ??= new List<UiScreenView>();
            screens.RemoveAll(screen => screen == null);
        }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            ResolveReferences();
            RebuildRegistry();
            history.Clear();

            foreach (UiScreenView screen in screensById.Values)
            {
                screen.SetVisible(screen.VisibleOnAwake, animated: false);
            }

            initialized = true;
            if (initialScreen != UiScreenId.None)
            {
                Open(initialScreen, animated: false);
            }
            else
            {
                UpdateControlLock();
            }
        }

        public bool Open(UiScreenId screenId)
        {
            return Open(screenId, Application.isPlaying);
        }

        public bool Open(UiScreenId screenId, bool animated)
        {
            EnsureInitialized();
            if (!screensById.TryGetValue(screenId, out UiScreenView next))
            {
                return false;
            }

            if (next.Layer == UiScreenLayer.Hud)
            {
                next.SetVisible(true, animated);
                ScreenOpened?.Invoke(next.ScreenId);
                return true;
            }

            int existingIndex = FindHistoryIndex(screenId);
            if (existingIndex >= 0)
            {
                CloseEntriesAbove(existingIndex, animated);
                next.SetVisible(true, animated);
                focusController?.Focus(next);
                UpdateControlLock();
                NotifyTopChanged();
                return true;
            }

            if (next.Layer == UiScreenLayer.Screen)
            {
                CloseLayersAbove(UiScreenLayer.Screen, animated);
                UiScreenView currentScreen = FindTopAtLayer(UiScreenLayer.Screen);
                currentScreen?.SetVisible(false, animated);
            }

            GameObject previousSelection = focusController != null
                ? focusController.CaptureCurrentSelection()
                : null;
            history.Add(new HistoryEntry(next, previousSelection));
            next.SetVisible(true, animated);
            focusController?.Focus(next);
            UpdateControlLock();
            ScreenOpened?.Invoke(next.ScreenId);
            NotifyTopChanged();
            return true;
        }

        public bool Replace(UiScreenId screenId)
        {
            EnsureInitialized();
            CloseAll(animated: Application.isPlaying);
            return Open(screenId, Application.isPlaying);
        }

        public bool Close(UiScreenId screenId)
        {
            EnsureInitialized();
            int index = FindHistoryIndex(screenId);
            if (index < 0)
            {
                return false;
            }

            CloseEntriesAbove(index - 1, Application.isPlaying);
            return true;
        }

        public bool CloseTop()
        {
            EnsureInitialized();
            if (history.Count == 0)
            {
                return false;
            }

            CloseTopInternal(Application.isPlaying);
            return true;
        }

        public void RegisterCancelConsumer(IUiCancelConsumer consumer)
        {
            if (consumer != null && !cancelConsumers.Contains(consumer))
            {
                cancelConsumers.Add(consumer);
            }
        }

        public void UnregisterCancelConsumer(IUiCancelConsumer consumer)
        {
            cancelConsumers.Remove(consumer);
        }

        public bool TryHandleCancel()
        {
            EnsureInitialized();
            for (int i = cancelConsumers.Count - 1; i >= 0; i--)
            {
                IUiCancelConsumer consumer = cancelConsumers[i];
                if (consumer == null)
                {
                    cancelConsumers.RemoveAt(i);
                    continue;
                }

                if (consumer.TryConsumeCancel())
                {
                    AudioDirector.Current?.PlayUi(UiAudioCue.Back);
                    return true;
                }
            }

            bool handled = history.Count > 0 &&
                           history[^1].Screen.CloseOnCancel &&
                           CloseTop();
            if (handled)
            {
                AudioDirector.Current?.PlayUi(UiAudioCue.Back);
            }

            return handled;
        }

        public void CloseAll(bool animated)
        {
            EnsureInitialized();
            while (history.Count > 0)
            {
                CloseTopInternal(animated);
            }
        }

        public bool IsOpen(UiScreenId screenId)
        {
            EnsureInitialized();
            return FindHistoryIndex(screenId) >= 0 ||
                   screensById.TryGetValue(screenId, out UiScreenView screen) &&
                   screen.Layer == UiScreenLayer.Hud &&
                   screen.IsVisible;
        }

        public bool TryGetScreen(UiScreenId screenId, out UiScreenView screen)
        {
            EnsureInitialized();
            return screensById.TryGetValue(screenId, out screen);
        }

        public void Register(UiScreenView screen)
        {
            if (screen == null || screens.Contains(screen))
            {
                return;
            }

            screens.Add(screen);
            initialized = false;
        }

        public void CollectChildScreens()
        {
            screens = new List<UiScreenView>(GetCompositionScreens());
            initialized = false;
        }

        void CloseTopInternal(bool animated)
        {
            int index = history.Count - 1;
            HistoryEntry closing = history[index];
            history.RemoveAt(index);
            closing.Screen.SetVisible(false, animated);

            UiScreenView fallback = null;
            if (closing.Screen.Layer == UiScreenLayer.Screen)
            {
                fallback = FindTopAtLayer(UiScreenLayer.Screen);
                fallback?.SetVisible(true, animated);
            }
            else if (history.Count > 0)
            {
                fallback = history[^1].Screen;
            }

            focusController?.Restore(closing.PreviousSelection, fallback);
            UpdateControlLock();
            ScreenClosed?.Invoke(closing.Screen.ScreenId);
            NotifyTopChanged();
        }

        void CloseEntriesAbove(int index, bool animated)
        {
            while (history.Count - 1 > index)
            {
                CloseTopInternal(animated);
            }
        }

        void CloseLayersAbove(UiScreenLayer layer, bool animated)
        {
            while (history.Count > 0 && history[^1].Screen.Layer > layer)
            {
                CloseTopInternal(animated);
            }
        }

        UiScreenView FindTopAtLayer(UiScreenLayer layer)
        {
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (history[i].Screen.Layer == layer)
                {
                    return history[i].Screen;
                }
            }

            return null;
        }

        int FindHistoryIndex(UiScreenId screenId)
        {
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (history[i].Screen.ScreenId == screenId)
                {
                    return i;
                }
            }

            return -1;
        }

        void ResolveReferences()
        {
            focusController ??= GetComponent<UiFocusController>();
            controlLock ??= GetComponent<PlayerControlLock>();
        }

        void RebuildRegistry()
        {
            screens ??= new List<UiScreenView>();
            screens.RemoveAll(screen => screen == null);
            UiScreenView[] ownedScreens = GetCompositionScreens();
            for (int i = 0; i < ownedScreens.Length; i++)
            {
                if (!screens.Contains(ownedScreens[i]))
                {
                    screens.Add(ownedScreens[i]);
                }
            }

            screensById.Clear();
            for (int i = 0; i < screens.Count; i++)
            {
                UiScreenView screen = screens[i];
                if (screen.ScreenId == UiScreenId.None)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError(
                        $"{nameof(UiScreenView)} '{screen.name}' must have a non-None screen id.",
                        screen);
#endif
                    continue;
                }

                if (screensById.ContainsKey(screen.ScreenId))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError(
                        $"Duplicate UI screen id '{screen.ScreenId}' on '{screen.name}'.",
                        screen);
#endif
                    continue;
                }

                screensById.Add(screen.ScreenId, screen);
            }
        }

        UiScreenView[] GetCompositionScreens()
        {
            return UiCompositionScope.FindAllInScope<UiScreenView>(this);
        }

        void UpdateControlLock()
        {
            if (controlLock == null)
            {
                return;
            }

            bool locked = false;
            for (int i = 0; i < history.Count; i++)
            {
                if (history[i].Screen.LocksGameplayInput)
                {
                    locked = true;
                    break;
                }
            }

            controlLock.SetLocked(PlayerControlLockReason.UserInterface, locked);
        }

        void NotifyTopChanged()
        {
            TopScreenChanged?.Invoke(TopScreenId);
        }

        void EnsureInitialized()
        {
            if (!initialized)
            {
                Initialize();
            }
        }
    }
}
