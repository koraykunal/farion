using System.Collections.Generic;
using Farion.Core.Physics;
using Farion.Multiplayer.Player;
using Farion.Multiplayer.Session;
using Farion.UI.Styling;
using TMPro;
using UnityEngine;

namespace Farion.Multiplayer.Presentation
{
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class NetworkPlayerNameplateLayer : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] RectTransform container;
        [SerializeField, Min(0f)] float headOffset = 2.1f;
        [SerializeField, Min(1f)] float fadeStartDistance = 40f;
        [SerializeField, Min(1f)] float maximumDistance = 320f;

        [Header("Type")]
        [SerializeField] UiTheme theme;
        [SerializeField, Min(6f)] float nearFontSize = 20f;
        [SerializeField, Min(4f)] float farFontSize = 12f;

        [Header("Occlusion")]
        [SerializeField] bool hideWhenOccluded = true;

        readonly List<TMP_Text> labels = new();
        Camera viewCamera;
        Canvas hostCanvas;

        UiTheme Theme => theme = UiTheme.Resolve(theme);

        void Awake()
        {
            container ??= transform as RectTransform;
            hostCanvas = container != null
                ? container.GetComponentInParent<Canvas>()
                : null;
        }

        void OnDisable()
        {
            for (int i = 0; i < labels.Count; i++)
            {
                labels[i].gameObject.SetActive(false);
            }
        }

        void LateUpdate()
        {
            if (container == null)
            {
                return;
            }

            Camera resolvedCamera = ResolveCamera();
            int used = resolvedCamera != null ? DrawNameplates(resolvedCamera) : 0;
            for (int i = used; i < labels.Count; i++)
            {
                labels[i].gameObject.SetActive(false);
            }
        }

        int DrawNameplates(Camera resolvedCamera)
        {
            IReadOnlyList<NetworkExplorerController> explorers =
                NetworkExplorerController.ActiveExplorers;
            Vector3 eye = resolvedCamera.transform.position;
            int used = 0;
            for (int i = 0; i < explorers.Count; i++)
            {
                NetworkExplorerController explorer = explorers[i];
                if (explorer == null ||
                    explorer.IsOwner ||
                    !explorer.gameObject.activeInHierarchy ||
                    !TryResolveDisplayName(
                        explorer.SessionPlayerId,
                        out string displayName))
                {
                    continue;
                }

                Vector3 anchor = explorer.transform.position +
                    explorer.transform.up * headOffset;
                float distance = Vector3.Distance(eye, anchor);
                if (distance > maximumDistance ||
                    !TryProjectToScreen(
                        resolvedCamera,
                        anchor,
                        out Vector2 screenPoint) ||
                    IsOccluded(eye, anchor))
                {
                    continue;
                }

                TMP_Text label = ResolveLabel(used);
                label.gameObject.SetActive(true);
                label.text = displayName;
                label.fontSize = Mathf.Lerp(
                    nearFontSize,
                    farFontSize,
                    Mathf.InverseLerp(fadeStartDistance, maximumDistance, distance));
                Color color = Theme.PrimaryText;
                color.a = Mathf.Lerp(
                    1f,
                    0.25f,
                    Mathf.InverseLerp(fadeStartDistance, maximumDistance, distance));
                label.color = color;
                PlaceLabel(label, screenPoint);
                used++;
            }

            return used;
        }

        bool TryProjectToScreen(
            Camera resolvedCamera,
            Vector3 worldPoint,
            out Vector2 screenPoint)
        {
            Vector3 projected = resolvedCamera.WorldToScreenPoint(worldPoint);
            screenPoint = projected;
            return projected.z > 0f &&
                projected.x >= 0f &&
                projected.x <= Screen.width &&
                projected.y >= 0f &&
                projected.y <= Screen.height;
        }

        void PlaceLabel(TMP_Text label, Vector2 screenPoint)
        {
            Camera canvasCamera =
                hostCanvas != null &&
                hostCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? hostCanvas.worldCamera
                    : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    container,
                    screenPoint,
                    canvasCamera,
                    out Vector2 localPoint))
            {
                label.rectTransform.anchoredPosition = localPoint;
            }
        }

        bool IsOccluded(Vector3 eye, Vector3 anchor)
        {
            return hideWhenOccluded &&
                Physics.Linecast(
                    eye,
                    anchor,
                    FarionLayers.CameraObstacleMask,
                    QueryTriggerInteraction.Ignore);
        }

        static bool TryResolveDisplayName(ulong sessionPlayerId, out string displayName)
        {
            displayName = string.Empty;
            if (sessionPlayerId == 0UL)
            {
                return false;
            }

            IReadOnlyList<NetworkSessionPlayer> players =
                NetworkSessionPlayer.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                NetworkSessionPlayer player = players[i];
                if (player != null &&
                    player.SessionPlayerId == sessionPlayerId &&
                    !string.IsNullOrWhiteSpace(player.DisplayName))
                {
                    displayName = player.DisplayName;
                    return true;
                }
            }

            return false;
        }

        Camera ResolveCamera()
        {
            if (viewCamera != null && viewCamera.isActiveAndEnabled)
            {
                return viewCamera;
            }

            viewCamera = Camera.main;
            return viewCamera;
        }

        TMP_Text ResolveLabel(int index)
        {
            while (labels.Count <= index)
            {
                labels.Add(CreateLabel());
            }

            return labels[index];
        }

        TMP_Text CreateLabel()
        {
            GameObject host = new(
                $"Nameplate_{labels.Count:00}",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            host.transform.SetParent(container, worldPositionStays: false);
            TextMeshProUGUI label = host.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.overflowMode = TextOverflowModes.Overflow;
            if (Theme.InterfaceMediumFont != null)
            {
                label.font = Theme.InterfaceMediumFont;
                label.fontWeight = FontWeight.Medium;
            }

            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320f, 32f);
            rect.pivot = new Vector2(0.5f, 0f);
            return label;
        }
    }
}
