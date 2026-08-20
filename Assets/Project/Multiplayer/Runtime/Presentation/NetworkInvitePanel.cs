using System.Net;
using System.Net.Sockets;
using Farion.Multiplayer.Session;
using Farion.UI.Common;
using Farion.UI.Localization;
using Farion.UI.Styling;
using TMPro;
using UnityEngine;

namespace Farion.Multiplayer.Presentation
{
    [DisallowMultipleComponent]
    public sealed class NetworkInvitePanel : MonoBehaviour
    {
        [SerializeField] MenuButtonView inviteButton;
        [SerializeField] MenuButtonView copyAddressButton;
        [SerializeField] TMP_Text addressText;
        [SerializeField] TMP_Text hintText;
        [SerializeField] UiTheme theme;
        [SerializeField, Min(0.1f)] float refreshInterval = 1f;

        const float CopiedNoticeSeconds = 3f;

        static string cachedLocalAddress;

        float nextRefreshTime;

        UiTheme Theme => theme = UiTheme.Resolve(theme);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCachedAddress()
        {
            cachedLocalAddress = null;
        }

        void OnEnable()
        {
            if (inviteButton != null)
            {
                inviteButton.Clicked -= OpenInviteOverlay;
                inviteButton.Clicked += OpenInviteOverlay;
            }

            if (copyAddressButton != null)
            {
                copyAddressButton.Clicked -= CopyAddress;
                copyAddressButton.Clicked += CopyAddress;
            }

            nextRefreshTime = 0f;
            Refresh();
        }

        void Update()
        {
            if (Time.unscaledTime >= nextRefreshTime)
            {
                Refresh();
            }
        }

        void OnDisable()
        {
            if (inviteButton != null)
            {
                inviteButton.Clicked -= OpenInviteOverlay;
            }

            if (copyAddressButton != null)
            {
                copyAddressButton.Clicked -= CopyAddress;
            }
        }

        void Refresh()
        {
            nextRefreshTime = Time.unscaledTime + refreshInterval;
            bool hosting = MultiplayerSessionController.Active != null &&
                MultiplayerSessionController.Active.IsHost;
            bool steamReady = MultiplayerLobbyGateway.IsAvailable;

            if (inviteButton != null)
            {
                inviteButton.gameObject.SetActive(hosting && steamReady);
                inviteButton.SetTitle(UiLocalization.Get(UiTextKeys.CoopInvite));
            }

            string address = hosting ? ResolveHostAddress() : string.Empty;
            bool showAddress = hosting && !steamReady && address.Length > 0;

            if (copyAddressButton != null)
            {
                copyAddressButton.gameObject.SetActive(showAddress);
                copyAddressButton.SetTitle(
                    UiLocalization.Get(UiTextKeys.CoopCopyAddress));
            }

            if (addressText != null)
            {
                addressText.gameObject.SetActive(showAddress);
                addressText.SetText(address);
                addressText.color = Theme.PrimaryText;
            }

            if (hintText != null)
            {
                hintText.gameObject.SetActive(showAddress);
                hintText.SetText(UiLocalization.Get(UiTextKeys.CoopAddressHint));
                hintText.color = Theme.SupportingText;
            }
        }

        void OpenInviteOverlay()
        {
            MultiplayerLobbyGateway.Service?.OpenInviteOverlay();
        }

        void CopyAddress()
        {
            string address = ResolveHostAddress();
            if (address.Length == 0)
            {
                return;
            }

            GUIUtility.systemCopyBuffer = address;
            if (hintText != null)
            {
                hintText.SetText(UiLocalization.Get(UiTextKeys.CoopAddressCopied));
                nextRefreshTime = Time.unscaledTime + CopiedNoticeSeconds;
            }
        }

        static string ResolveHostAddress()
        {
            cachedLocalAddress ??= ResolveLocalIPv4();
            if (cachedLocalAddress.Length == 0)
            {
                return string.Empty;
            }

            return $"{cachedLocalAddress}:{MultiplayerEndpoint.DefaultPort}";
        }

        static string ResolveLocalIPv4()
        {
            try
            {
                using Socket probe = new(
                    AddressFamily.InterNetwork,
                    SocketType.Dgram,
                    ProtocolType.Udp);
                probe.Connect("8.8.8.8", 65530);
                return probe.LocalEndPoint is IPEndPoint endPoint
                    ? endPoint.Address.ToString()
                    : string.Empty;
            }
            catch (SocketException)
            {
                return string.Empty;
            }
        }
    }
}
