using System;

namespace Farion.Multiplayer.Session
{
    public enum MultiplayerLaunchMode
    {
        None = 0,
        Host = 1,
        Client = 2
    }

    public readonly struct MultiplayerLaunchRequest
    {
        public MultiplayerLaunchRequest(
            MultiplayerLaunchMode mode,
            MultiplayerEndpoint endpoint)
        {
            Mode = mode;
            Endpoint = endpoint;
        }

        public MultiplayerLaunchMode Mode { get; }
        public MultiplayerEndpoint Endpoint { get; }
    }

    public static class MultiplayerCommandLine
    {
        const string ModePrefix = "-farion-net=";
        const string AddressPrefix = "-farion-address=";

        public static bool TryParse(
            string[] arguments,
            out MultiplayerLaunchRequest request)
        {
            MultiplayerLaunchMode mode = MultiplayerLaunchMode.None;
            MultiplayerEndpoint endpoint = MultiplayerEndpoint.Loopback;

            for (int i = 0; arguments != null && i < arguments.Length; i++)
            {
                string argument = arguments[i] ?? string.Empty;
                if (argument.StartsWith(
                        ModePrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    mode = ParseMode(argument[ModePrefix.Length..]);
                }
                else if (argument.StartsWith(
                             AddressPrefix,
                             StringComparison.OrdinalIgnoreCase) &&
                         MultiplayerEndpoint.TryParse(
                             argument[AddressPrefix.Length..],
                             out MultiplayerEndpoint parsed))
                {
                    endpoint = parsed;
                }
            }

            request = new MultiplayerLaunchRequest(mode, endpoint);
            return mode != MultiplayerLaunchMode.None;
        }

        static MultiplayerLaunchMode ParseMode(string value)
        {
            if (value.Equals("host", StringComparison.OrdinalIgnoreCase))
            {
                return MultiplayerLaunchMode.Host;
            }

            return value.Equals("client", StringComparison.OrdinalIgnoreCase)
                ? MultiplayerLaunchMode.Client
                : MultiplayerLaunchMode.None;
        }
    }
}
