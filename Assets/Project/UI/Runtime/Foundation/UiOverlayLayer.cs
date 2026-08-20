using UiNavigation = UnityEngine.UI.Navigation;
using UnityEngine;
using UnityEngine.UI;

namespace Farion.UI.Foundation
{
    public static class UiOverlayLayer
    {
        const string LayerName = "UiOverlayLayer";
        const string BlockerName = "UiOverlayBlocker";

        public static RectTransform Resolve(Component context)
        {
            Canvas canvas = context != null
                ? context.GetComponentInParent<Canvas>()
                : null;
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            if (root == null)
            {
                return null;
            }

            Transform existing = root.transform.Find(LayerName);
            RectTransform layer = existing != null
                ? existing as RectTransform
                : CreateLayer(root.transform);
            layer.SetAsLastSibling();
            return layer;
        }

        public static Button ResolveBlocker(RectTransform layer)
        {
            if (layer == null)
            {
                return null;
            }

            Transform existing = layer.Find(BlockerName);
            return existing != null
                ? existing.GetComponent<Button>()
                : CreateBlocker(layer);
        }

        static RectTransform CreateLayer(Transform parent)
        {
            GameObject layerObject = new(LayerName, typeof(RectTransform));
            layerObject.layer = parent.gameObject.layer;
            RectTransform layer = (RectTransform)layerObject.transform;
            layer.SetParent(parent, false);
            Stretch(layer);
            return layer;
        }

        static Button CreateBlocker(RectTransform layer)
        {
            GameObject blockerObject = new(
                BlockerName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            blockerObject.layer = layer.gameObject.layer;
            RectTransform blocker = (RectTransform)blockerObject.transform;
            blocker.SetParent(layer, false);
            Stretch(blocker);

            Image image = blockerObject.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;

            Button button = blockerObject.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new UiNavigation { mode = UiNavigation.Mode.None };
            blockerObject.SetActive(false);
            return button;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }
    }
}
