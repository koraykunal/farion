using Farion.Gameplay.Input;
using Farion.UI.Feedback;
using Farion.UI.Foundation;
using Farion.UI.Gameplay;
using Farion.UI.Input;
using Farion.UI.Navigation;
using Farion.UI.Styling;
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
        public void DefaultThemeProvidesDistinctInterfaceAndInstrumentFonts()
        {
            const string path =
                "Assets/Project/Design/UI/Themes/SO_UiTheme_Default.asset";
            UiTheme theme = AssetDatabase.LoadAssetAtPath<UiTheme>(path);

            Assert.That(theme, Is.Not.Null);
            SerializedObject serializedTheme = new(theme);
            Object interfaceFont = serializedTheme
                .FindProperty("interfaceFont").objectReferenceValue;
            Object interfaceMediumFont = serializedTheme
                .FindProperty("interfaceMediumFont").objectReferenceValue;
            Object instrumentFont = serializedTheme
                .FindProperty("instrumentFont").objectReferenceValue;

            Assert.That(interfaceFont, Is.Not.Null);
            Assert.That(interfaceMediumFont, Is.Not.Null);
            Assert.That(instrumentFont, Is.Not.Null);
            Assert.That(interfaceFont, Is.Not.SameAs(instrumentFont));
        }

        [Test]
        public void AuthoredFlightHudProvidesTelemetryGraphics()
        {
            const string path =
                "Assets/Project/Prefabs/UI/Gameplay/HUD/UI_SpacecraftFlightHud.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(
                prefab.GetComponent<SpacecraftFlightHudPresenter>(),
                Is.Not.Null);
            Assert.That(
                prefab.GetComponentInChildren<SpacecraftFlightHudGraphics>(true),
                Is.Not.Null);
            Assert.That(
                prefab.transform.Find(
                    "Graphics/FlightCluster/BoostCapacitor/BoostVisual"),
                Is.Not.Null);
            Assert.That(
                prefab.transform.Find(
                    "NavigationTargetMarker/TargetReadout/DirectionArrow"),
                Is.Not.Null);

            SerializedObject graphics = new(
                prefab.GetComponentInChildren<SpacecraftFlightHudGraphics>(true));
            Assert.That(
                graphics.FindProperty("boostFrameImage").objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                graphics.FindProperty("navigationArrow").objectReferenceValue,
                Is.Not.Null);
        }

        [Test]
        public void AuthoredPauseMenuProvidesRuntimeInitialSelection()
        {
            const string path =
                "Assets/Project/Prefabs/UI/Screens/UI_PauseMenuScreen.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);

            root = Object.Instantiate(prefab);
            GameplayMenuListPresenter presenter =
                root.GetComponentInChildren<GameplayMenuListPresenter>(true);
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

        [Test]
        public void SystemRootPrefabRemainsServiceOnly()
        {
            const string path =
                "Assets/Project/Prefabs/UI/Foundation/UI_SystemRoot.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<UiSystemRoot>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<UiScreenRouter>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<UiFocusController>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<UiInputDeviceService>(), Is.Not.Null);
            Assert.That(prefab.transform.childCount, Is.Zero);
            Assert.That(
                prefab.GetComponentInChildren<UiScreenView>(true),
                Is.Null);
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
