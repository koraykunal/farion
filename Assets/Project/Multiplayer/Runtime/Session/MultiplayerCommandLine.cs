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
            string address)
        {
            Mode = mode;
            Address = address;
        }

        public MultiplayerLaunchMode Mode { get; }
        public string Address { get; }
    }

    public static class MultiplayerCommandLine
    {
        const string ModePrefix = "-farion-net=";
        const string AddressPrefix = "-farion-address=";
        const string DefaultAddress = "127.0.0.1";

        public static bool TryParse(
            string[] arguments,
            out MultiplayerLaunchRequest request)
        {
            MultiplayerLaunchMode mode = MultiplayerLaunchMode.None;
            string address = DefaultAddress;

            if (arguments != null)
            {
                for (int i = 0; i < arguments.Length; i++)
                {
                    string argument = arguments[i] ?? string.Empty;
                    if (argument.StartsWith(
                            ModePrefix,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        string value = argument[ModePrefix.Length..];
                        mode = value.Equals("host", StringComparison.OrdinalIgnoreCase)
                            ? MultiplayerLaunchMode.Host
                            : value.Equals("client", StringComparison.OrdinalIgnoreCase)
                                ? MultiplayerLaunchMode.Client
                                : MultiplayerLaunchMode.None;
                    }
                    else if (argument.StartsWith(
                                 AddressPrefix,
                                 StringComparison.OrdinalIgnoreCase))
                    {
                        string value = argument[AddressPrefix.Length..].Trim();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            address = value;
                        }
                    }
                }
            }

            request = new MultiplayerLaunchRequest(mode, address);
            return mode != MultiplayerLaunchMode.None;
        }
    }
}
