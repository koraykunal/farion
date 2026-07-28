using UnityEngine;

namespace Farion.UI.Settings
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiSafeAreaFitter : MonoBehaviour
    {
        [SerializeField] RectTransform target;
        [SerializeField] Vector2 minimumPadding = new(24f, 24f);

        Rect lastSafeArea;
        Vector2Int lastScreenSize;

        void Reset()
        {
            target = transform as RectTransform;
        }

        void OnEnable()
        {
            Apply(force: true);
        }

        void Update()
        {
            Apply(force: false);
        }

        void Apply(bool force)
        {
            target ??= transform as RectTransform;
            if (target == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            Vector2Int screenSize = new(Screen.width, Screen.height);
            if (!force && safeArea == lastSafeArea && screenSize == lastScreenSize)
            {
                return;
            }

            lastSafeArea = safeArea;
            lastScreenSize = screenSize;

            Vector2 min = safeArea.position;
            Vector2 max = safeArea.position + safeArea.size;
            min.x = Mathf.Max(min.x, minimumPadding.x);
            min.y = Mathf.Max(min.y, minimumPadding.y);
            max.x = Mathf.Min(max.x, Screen.width - minimumPadding.x);
            max.y = Mathf.Min(max.y, Screen.height - minimumPadding.y);

            target.anchorMin = new Vector2(min.x / Screen.width, min.y / Screen.height);
            target.anchorMax = new Vector2(max.x / Screen.width, max.y / Screen.height);
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }
    }
}
