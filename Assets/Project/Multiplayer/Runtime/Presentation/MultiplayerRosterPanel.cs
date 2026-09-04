using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Farion.Multiplayer.Player;
using Farion.Multiplayer.Session;
using Farion.UI.Localization;
using Farion.UI.Styling;
using TMPro;
using UnityEngine;

namespace Farion.Multiplayer.Presentation
{
    [DisallowMultipleComponent]
    public sealed class MultiplayerRosterPanel : MonoBehaviour
    {
        [SerializeField] GameObject content;
        [SerializeField] TMP_Text headerText;
        [SerializeField] TMP_Text rosterText;
        [SerializeField] UiTheme theme;
        [SerializeField, Min(0.1f)] float refreshInterval = 0.5f;

        readonly StringBuilder builder = new();
        float nextRefreshTime;

        UiTheme Theme => theme = UiTheme.Resolve(theme);

        void Awake()
        {
            if (rosterText != null && Theme.InterfaceFont != null)
            {
                rosterText.font = Theme.InterfaceFont;
                rosterText.color = Theme.SecondaryText;
            }

            if (headerText != null)
            {
                headerText.color = Theme.SupportingText;
            }
        }

        void OnEnable()
        {
            nextRefreshTime = 0f;
            Refresh();
        }

        void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            Refresh();
        }

        void Refresh()
        {
            nextRefreshTime = Time.unscaledTime + refreshInterval;
            IReadOnlyList<NetworkSessionPlayer> players =
                NetworkSessionPlayer.ActivePlayers;
            MultiplayerSessionController session = MultiplayerSessionController.Active;
            bool inSession = players.Count > 0 && session != null && !session.IsPrivate;
            if (content != null)
            {
                content.SetActive(inSession);
            }

            if (!inSession || rosterText == null)
            {
                return;
            }

            if (headerText != null)
            {
                headerText.SetText(
                    UiLocalization.ToDisplayUpper(
                        UiLocalization.Get(UiTextKeys.CoopSessionTitle)));
            }

            builder.Clear();
            int listed = 0;
            for (int i = 0; i < players.Count; i++)
            {
                NetworkSessionPlayer player = players[i];
                if (player == null || string.IsNullOrWhiteSpace(player.DisplayName))
                {
                    continue;
                }

                if (listed > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(UiLocalization.ToDisplayUpper(player.DisplayName));
                AppendDistance(player);
                listed++;
            }

            rosterText.SetText(
                listed > 0
                    ? builder.ToString()
                    : UiLocalization.Get(UiTextKeys.CoopRosterEmpty));
        }

        void AppendDistance(NetworkSessionPlayer player)
        {
            if (player == NetworkSessionPlayer.Local)
            {
                builder.Append("  ").Append(
                    UiLocalization.Get(UiTextKeys.CoopRosterSelf));
                return;
            }

            if (!TryResolveExplorer(
                    NetworkSessionPlayer.Local,
                    out NetworkExplorerController localExplorer) ||
                !TryResolveExplorer(
                    player,
                    out NetworkExplorerController remoteExplorer) ||
                localExplorer.gameObject.scene != remoteExplorer.gameObject.scene)
            {
                return;
            }

            float distance = Vector3.Distance(
                localExplorer.transform.position,
                remoteExplorer.transform.position);
            builder.Append("  ");
            if (distance >= 1000f)
            {
                builder
                    .Append((distance / 1000f).ToString("0.0", CultureInfo.InvariantCulture))
                    .Append(' ')
                    .Append(UiLocalization.Get(UiTextKeys.UnitKilometersShort));
                return;
            }

            builder
                .Append(distance.ToString("0", CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(UiLocalization.Get(UiTextKeys.UnitMetersShort));
        }

        static bool TryResolveExplorer(
            NetworkSessionPlayer player,
            out NetworkExplorerController explorer)
        {
            explorer = null;
            if (player == null || player.SessionPlayerId == 0UL)
            {
                return false;
            }

            IReadOnlyList<NetworkExplorerController> explorers =
                NetworkExplorerController.ActiveExplorers;
            for (int i = 0; i < explorers.Count; i++)
            {
                NetworkExplorerController candidate = explorers[i];
                if (candidate != null &&
                    candidate.IsSpawned &&
                    candidate.gameObject.activeInHierarchy &&
                    candidate.SessionPlayerId == player.SessionPlayerId)
                {
                    explorer = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
