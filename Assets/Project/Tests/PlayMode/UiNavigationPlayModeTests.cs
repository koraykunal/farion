using System.Collections;
using Farion.Gameplay.Input;
using Farion.UI.Feedback;
using Farion.UI.Foundation;
using Farion.UI.Navigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Farion.Tests.PlayMode
{
    public sealed class UiNavigationPlayModeTests
    {
        GameObject composition;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (composition != null)
            {
                Object.Destroy(composition);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SiblingModalLocksInputAndRestoresFocus()
        {
            composition = CreateComposition(
                out UiSystemRoot systemRoot,
                out PlayerControlLock controlLock,
                out UiScreenView pause,
                out UiScreenView confirmation,
                out UiConfirmationDialog dialog);

            composition.SetActive(true);
            yield return null;

            UiScreenRouter router = systemRoot.ScreenRouter;
            Assert.That(
                router.Open(UiScreenId.PauseMenu, animated: false),
                Is.True);
            yield return null;
            yield return null;

            GameObject pauseSelection =
                pause.ResolveFirstSelection().gameObject;
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(pauseSelection));
            Assert.That(controlLock.IsGameplayInputLocked, Is.True);

            bool confirmed = false;
            Assert.That(
                dialog.Present(
                    "CONFIRM OPERATION",
                    "This action changes the current session.",
                    "CONFIRM",
                    () => confirmed = true),
                Is.True);
            yield return null;
            yield return null;

            Assert.That(router.TopScreenId, Is.EqualTo(UiScreenId.Confirmation));
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(confirmation.ResolveFirstSelection().gameObject));
            Assert.That(controlLock.IsGameplayInputLocked, Is.True);

            dialog.Cancel();
            yield return null;
            yield return null;

            Assert.That(confirmed, Is.False);
            Assert.That(router.TopScreenId, Is.EqualTo(UiScreenId.PauseMenu));
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(pauseSelection));
            Assert.That(controlLock.IsGameplayInputLocked, Is.True);

            Assert.That(router.CloseTop(), Is.True);
            yield return null;
            yield return null;

            Assert.That(controlLock.IsGameplayInputLocked, Is.False);
            Assert.That(router.TopScreenId, Is.EqualTo(UiScreenId.None));
        }

        static GameObject CreateComposition(
            out UiSystemRoot systemRoot,
            out PlayerControlLock controlLock,
            out UiScreenView pause,
            out UiScreenView confirmation,
            out UiConfirmationDialog dialog)
        {
            GameObject canvasObject = new(
                "TestCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.SetActive(false);

            GameObject eventSystemObject = new(
                "EventSystem",
                typeof(EventSystem));
            eventSystemObject.transform.SetParent(canvasObject.transform);

            GameObject systemObject = new(
                "UI_SystemRoot",
                typeof(RectTransform));
            systemObject.transform.SetParent(canvasObject.transform);
            controlLock = systemObject.AddComponent<PlayerControlLock>();
            systemRoot = systemObject.AddComponent<UiSystemRoot>();

            pause = CreateScreen(
                canvasObject.transform,
                "UI_PauseMenuScreen",
                UiScreenId.PauseMenu,
                UiScreenLayer.Screen);
            confirmation = CreateScreen(
                canvasObject.transform,
                "UI_ConfirmationDialog",
                UiScreenId.Confirmation,
                UiScreenLayer.Modal);
            dialog = confirmation.gameObject.AddComponent<UiConfirmationDialog>();

            return canvasObject;
        }

        static UiScreenView CreateScreen(
            Transform parent,
            string screenName,
            UiScreenId screenId,
            UiScreenLayer layer)
        {
            GameObject screenObject = new(
                screenName,
                typeof(RectTransform),
                typeof(CanvasGroup));
            screenObject.transform.SetParent(parent);

            GameObject buttonObject = new(
                "FirstSelection",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(screenObject.transform);
            Button button = buttonObject.GetComponent<Button>();

            UiScreenView screen = screenObject.AddComponent<UiScreenView>();
            screen.Configure(
                screenId,
                layer,
                button,
                canCloseOnCancel: true,
                shouldLockGameplayInput: true,
                shouldBeVisibleOnAwake: false);
            return screen;
        }
    }
}
