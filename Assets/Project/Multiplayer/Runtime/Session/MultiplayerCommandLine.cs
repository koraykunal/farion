using System;

namespace Farion.Multiplayer.Session
{
    public enum MultiplayerLaunchMode
    {
        None = 0,
        Host = 1,
        Client = 2
    }

    public static class MultiplayerCommandLine
    {
        const string ModePrefix = "-farion-net=";
        const string AddressPrefix = "-farion-address=";
        const string SteamLobbyArgument = "+connect_lobby";

        public static bool TryParse(
            string[] arguments,
            out MultiplayerLaunchMode mode,
            out MultiplayerEndpoint endpoint)
        {
            mode = MultiplayerLaunchMode.None;
            endpoint = MultiplayerEndpoint.Loopback;

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

            return mode != MultiplayerLaunchMode.None;
        }

        public static bool TryParseSteamLobby(string[] arguments, out ulong lobbyId)
        {
            lobbyId = 0UL;
            for (int i = 0; arguments != null && i + 1 < arguments.Length; i++)
            {
                if (string.Equals(
                        arguments[i],
                        SteamLobbyArgument,
                        StringComparison.OrdinalIgnoreCase) &&
                    ulong.TryParse(arguments[i + 1], out lobbyId) &&
                    lobbyId != 0UL)
                {
                    return true;
                }
            }

            return false;
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
