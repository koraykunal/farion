using Farion.Audio.Direction;
using Farion.Gameplay.Input;
using Farion.UI.Common;
using Farion.UI.Feedback;
using Farion.UI.Foundation;
using Farion.UI.Gameplay;
using Farion.UI.Input;
using Farion.UI.Navigation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Farion.Tests.EditMode
{
    public sealed class UiFoundationTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AudioSceneMixKeepsMenuAndGameplayMusicExclusive()
        {
            AudioDirector.ResolveSceneMix(
                AudioSceneContextId.MainMenu,
                1f,
                out float menu,
                out float gameplay);
            Assert.That(menu, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(gameplay, Is.EqualTo(0f).Within(0.0001f));

            AudioDirector.ResolveSceneMix(
                AudioSceneContextId.Gameplay,
                1f,
                out menu,
                out gameplay);
            Assert.That(menu, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(gameplay, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void BindingDisplayProvidesKeyboardAndGamepadPrompts()
        {
            string keyboard = UiBindingDisplay.GetDisplayString(
                FarionInputActions.OnFootInteract,
                UiInputDeviceKind.KeyboardMouse);
            string gamepad = UiBindingDisplay.GetDisplayString(
                FarionInputActions.OnFootInteract,
                UiInputDeviceKind.Gamepad);

            Assert.That(keyboard, Is.Not.Empty);
            Assert.That(gamepad, Is.Not.Empty);
            Assert.That(gamepad, Is.Not.EqualTo(keyboard));
        }

        [Test]
        public void ClosingTopScreenRestoresPreviousScreen()
        {
            root = new GameObject("UI_SystemRoot");
            root.AddComponent<UiFocusController>();
            UiScreenRouter router = root.AddComponent<UiScreenRouter>();

            UiScreenView pause = CreateScreen(
                "Pause",
                UiScreenId.PauseMenu,
                UiScreenLayer.Screen);
            UiScreenView inventory = CreateScreen(
                "Inventory",
                UiScreenId.Inventory,
                UiScreenLayer.Screen);

            router.Register(pause);
            router.Register(inventory);
            router.Initialize();

            Assert.That(router.Open(UiScreenId.PauseMenu, animated: false), Is.True);
            Assert.That(pause.IsVisible, Is.True);

            Assert.That(router.Open(UiScreenId.Inventory, animated: false), Is.True);
            Assert.That(pause.IsVisible, Is.False);
            Assert.That(inventory.IsVisible, Is.True);
            Assert.That(router.TopScreenId, Is.EqualTo(UiScreenId.Inventory));

            Assert.That(router.CloseTop(), Is.True);
            Assert.That(inventory.IsVisible, Is.False);
            Assert.That(pause.IsVisible, Is.True);
            Assert.That(router.TopScreenId, Is.EqualTo(UiScreenId.PauseMenu));
        }

        [Test]
        public void HudVisibilityDoesNotEnterNavigationHistory()
        {
            root = new GameObject("UI_SystemRoot");
            root.AddComponent<UiFocusController>();
            UiScreenRouter router = root.AddComponent<UiScreenRouter>();
            UiScreenView hud = CreateScreen(
                "Hud",
                UiScreenId.GameplayHud,
                UiScreenLayer.Hud);

            router.Register(hud);
            router.Initialize();

            Assert.That(router.Open(UiScreenId.GameplayHud, animated: false), Is.True);
            Assert.That(hud.IsVisible, Is.True);
            Assert.That(router.HasOpenScreen, Is.False);
            Assert.That(router.TopScreenId, Is.EqualTo(UiScreenId.None));
        }

        [Test]
        public void CancelClosesOnlyTheTopModal()
        {
            root = new GameObject("UI_SystemRoot");
            root.AddComponent<UiFocusController>();
            UiScreenRouter router = root.AddComponent<UiScreenRouter>();
            UiScreenView pause = CreateScreen(
                "Pause",
                UiScreenId.PauseMenu,
                UiScreenLayer.Screen);
            UiScreenView confirmation = CreateScreen(
                "Confirmation",
                UiScreenId.Confirmation,
                UiScreenLayer.Modal);

            router.Register(pause);
            router.Register(confirmation);
            router.Initialize();
            router.Open(UiScreenId.PauseMenu, animated: false);
            router.Open(UiScreenId.Confirmation, animated: false);

            Assert.That(router.TryHandleCancel(), Is.True);
            Assert.That(confirmation.IsVisible, Is.False);
            Assert.That(pause.IsVisible, Is.True);
            Assert.That(router.TopScreenId, Is.EqualTo(UiScreenId.PauseMenu));
        }

        [Test]
        public void RouterAutomaticallyRegistersScreensOwnedBySystemRoot()
        {
            root = new GameObject("UI_SystemRoot");
            root.AddComponent<UiFocusController>();
            UiScreenRouter router = root.AddComponent<UiScreenRouter>();
            UiScreenView confirmation = CreateScreen(
                "Confirmation",
                UiScreenId.Confirmation,
                UiScreenLayer.Modal);

            router.Initialize();

            Assert.That(
                router.Open(UiScreenId.Confirmation, animated: false),
                Is.True);
            Assert.That(confirmation.IsVisible, Is.True);
            Assert.That(router.TopScreenId, Is.EqualTo(UiScreenId.Confirmation));
        }

        [Test]
        public void RouterAutomaticallyRegistersCanvasSiblingScreens()
        {
            root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            GameObject systemObject = new("UI_SystemRoot");
            systemObject.transform.SetParent(root.transform);
            systemObject.AddComponent<UiFocusController>();
            UiScreenRouter router = systemObject.AddComponent<UiScreenRouter>();

            UiScreenView confirmation = CreateScreen(
                "UI_ConfirmationDialog",
                UiScreenId.Confirmation,
                UiScreenLayer.Modal);

            router.Initialize();

            Assert.That(
                router.Open(UiScreenId.Confirmation, animated: false),
                Is.True);
            Assert.That(confirmation.IsVisible, Is.True);
            Assert.That(router.TopScreenId, Is.EqualTo(UiScreenId.Confirmation));
        }

        [Test]
        public void ConfirmationDialogResolvesSiblingSystemRoot()
        {
            root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            root.SetActive(false);

            GameObject systemObject = new("UI_SystemRoot");
            systemObject.transform.SetParent(root.transform);
            UiSystemRoot systemRoot = systemObject.AddComponent<UiSystemRoot>();

            UiScreenView confirmation = CreateScreen(
                "UI_ConfirmationDialog",
                UiScreenId.Confirmation,
                UiScreenLayer.Modal);
            UiConfirmationDialog dialog =
                confirmation.gameObject.AddComponent<UiConfirmationDialog>();

            root.SetActive(true);
            systemRoot.ScreenRouter.CollectChildScreens();
            systemRoot.ScreenRouter.Initialize();

            bool confirmed = false;
            Assert.That(
                dialog.Present(
                    "CONFIRM",
                    "Proceed with the operation?",
                    "PROCEED",
                    () => confirmed = true),
                Is.True);

            dialog.Confirm();

            Assert.That(confirmed, Is.True);
            Assert.That(
                systemRoot.ScreenRouter.IsOpen(UiScreenId.Confirmation),
                Is.False);
        }

        [Test]
        public void ClosingModalRestoresActualEventSystemSelection()
        {
            root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            EventSystem eventSystem =
                new GameObject("EventSystem", typeof(EventSystem))
                    .GetComponent<EventSystem>();
            eventSystem.transform.SetParent(root.transform);

            GameObject systemObject = new("UI_SystemRoot");
            systemObject.transform.SetParent(root.transform);
            systemObject.AddComponent<UiFocusController>();
            UiScreenRouter router = systemObject.AddComponent<UiScreenRouter>();
            UiScreenView pause = CreateScreen(
                "Pause",
                UiScreenId.PauseMenu,
                UiScreenLayer.Screen);
            UiScreenView confirmation = CreateScreen(
                "Confirmation",
                UiScreenId.Confirmation,
                UiScreenLayer.Modal);

            router.Initialize();
            router.Open(UiScreenId.PauseMenu, animated: false);
            GameObject pauseSelection =
                pause.ResolveFirstSelection().gameObject;
            Assert.That(
                eventSystem.currentSelectedGameObject,
                Is.EqualTo(pauseSelection));

            router.Open(UiScreenId.Confirmation, animated: false);
            Assert.That(
                eventSystem.currentSelectedGameObject,
                Is.EqualTo(confirmation.ResolveFirstSelection().gameObject));

            router.CloseTop();

            Assert.That(
                eventSystem.currentSelectedGameObject,
                Is.EqualTo(pauseSelection));
        }

        [TestCase(UiFeedbackSeverity.Information, "INFO")]
        [TestCase(UiFeedbackSeverity.Success, "SUCCESS")]
        [TestCase(UiFeedbackSeverity.Caution, "CAUTION")]
        [TestCase(UiFeedbackSeverity.Error, "ERROR")]
        public void FeedbackIncludesNonColorSeverityLabel(
            UiFeedbackSeverity severity,
            string expectedLabel)
        {
            string formatted = UiFeedbackService.FormatMessage(
                "Operation status.",
                severity);

            Assert.That(formatted, Does.StartWith(expectedLabel));
            Assert.That(formatted, Does.Contain("Operation status."));
        }

        [Test]
        public void FeedbackFormatsItemAcquisition()
        {
            Assert.That(
                UiFeedbackService.FormatItemMessage("Iron Ore", 2),
                Is.EqualTo("Iron Ore ×2"));
        }

        [Test]
        public void SystemRootExposesRuntimeReducedMotionPreference()
        {
            root = new GameObject("UI_SystemRoot");
            UiSystemRoot systemRoot = root.AddComponent<UiSystemRoot>();
            bool? changedValue = null;
            systemRoot.ReducedMotionChanged +=
                value => changedValue = value;

            systemRoot.SetReducedMotion(true);

            Assert.That(systemRoot.ReducedMotion, Is.True);
            Assert.That(changedValue, Is.True);
        }

        [Test]
        public void PointerOwnedSelectionStopsHoveringAfterPointerExit()
        {
            UiPointerFocusState state = new();
            state.Select();
            state.PointerEnter();
            state.PointerExit();
            Assert.That(state.IsFocused, Is.True);

            state.PointerEnter();
            state.PointerClick();
            state.PointerExit();
            Assert.That(state.IsFocused, Is.False);
        }

        [Test]
        public void InventorySlotRequestsQuickViewFromRightClickAndSubmit()
        {
            root = new GameObject("InventorySlotTestRoot");
            EventSystem eventSystem = root.AddComponent<EventSystem>();
            GameObject slotObject = new(
                "InventorySlot",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(UiInventorySlotView));
            slotObject.transform.SetParent(root.transform);
            UiInventorySlotView slot = slotObject.GetComponent<UiInventorySlotView>();
            int requestCount = 0;
            slot.ContextRequested += _ => requestCount++;

            slot.OnPointerClick(
                new PointerEventData(eventSystem)
                {
                    button = PointerEventData.InputButton.Right
                });
            slot.OnSubmit(new BaseEventData(eventSystem));

            Assert.That(requestCount, Is.EqualTo(2));
        }

        [Test]
        [Category("Content")]
        public void AuthoredPauseMenuProvidesRuntimeInitialSelection()
        {
            const string path =
                "Assets/Project/Prefabs/UI/Screens/PF_UI_PauseMenuScreen.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);

            root = Object.Instantiate(prefab);
            UiGameplayMenuListPresenter presenter =
                root.GetComponentInChildren<UiGameplayMenuListPresenter>(true);
            UiScreenView screen = root.GetComponent<UiScreenView>();

            Assert.That(presenter, Is.Not.Null);
            Assert.That(screen, Is.Not.Null);

            presenter.Rebuild();

            Assert.That(presenter.FirstAvailableButton, Is.Not.Null);
            Assert.That(screen.HasExplicitFirstSelection, Is.True);
            Assert.That(
                screen.ResolveFirstSelection(),
                Is.EqualTo(presenter.FirstAvailableButton.Button));
        }

        UiScreenView CreateScreen(
            string screenName,
            UiScreenId screenId,
            UiScreenLayer layer)
        {
            GameObject screenObject = new(screenName);
            screenObject.transform.SetParent(root.transform);
            screenObject.AddComponent<CanvasGroup>();
            UiScreenView screen = screenObject.AddComponent<UiScreenView>();
            Button button = null;
            if (layer != UiScreenLayer.Hud)
            {
                button = new GameObject("FirstSelection").AddComponent<Button>();
                button.transform.SetParent(screenObject.transform);
            }

            screen.Configure(
                screenId,
                layer,
                button,
                canCloseOnCancel: layer != UiScreenLayer.Hud,
                shouldLockGameplayInput: layer != UiScreenLayer.Hud,
                shouldBeVisibleOnAwake: false);
            return screen;
        }
    }
}
