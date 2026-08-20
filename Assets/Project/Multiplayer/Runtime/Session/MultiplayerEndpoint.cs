using System;

namespace Farion.Multiplayer.Session
{
    public readonly struct MultiplayerEndpoint
    {
        public const ushort DefaultPort = 7770;
        public const string LoopbackAddress = "127.0.0.1";

        MultiplayerEndpoint(string address, ushort port)
        {
            Address = address;
            Port = port;
        }

        public string Address { get; }
        public ushort Port { get; }

        public static MultiplayerEndpoint Loopback =>
            new(LoopbackAddress, DefaultPort);

        public override string ToString() => Port == DefaultPort
            ? Address
            : $"{Address}:{Port}";

        public static bool TryParse(string value, out MultiplayerEndpoint endpoint)
        {
            endpoint = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            if (trimmed[0] == '[')
            {
                return TryParseBracketedAddress(trimmed, out endpoint);
            }

            int separator = trimmed.LastIndexOf(':');
            if (separator < 0)
            {
                return TryCreate(trimmed, DefaultPort, out endpoint);
            }

            if (trimmed.IndexOf(':') != separator)
            {
                return TryCreate(trimmed, DefaultPort, out endpoint);
            }

            return TryParsePort(trimmed[(separator + 1)..], out ushort port) &&
                TryCreate(trimmed[..separator], port, out endpoint);
        }

        static bool TryParseBracketedAddress(
            string value,
            out MultiplayerEndpoint endpoint)
        {
            endpoint = default;
            int closing = value.IndexOf(']');
            if (closing <= 1)
            {
                return false;
            }

            string address = value[1..closing];
            string remainder = value[(closing + 1)..];
            if (remainder.Length == 0)
            {
                return TryCreate(address, DefaultPort, out endpoint);
            }

            return remainder[0] == ':' &&
                TryParsePort(remainder[1..], out ushort port) &&
                TryCreate(address, port, out endpoint);
        }

        static bool TryParsePort(string value, out ushort port)
        {
            return ushort.TryParse(value, out port) && port != 0;
        }

        static bool TryCreate(
            string address,
            ushort port,
            out MultiplayerEndpoint endpoint)
        {
            endpoint = default;
            string trimmed = address.Trim();
            if (trimmed.Length == 0 ||
                trimmed.IndexOf(' ') >= 0 ||
                trimmed.IndexOf('/') >= 0)
            {
                return false;
            }

            if (trimmed.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = LoopbackAddress;
            }

            endpoint = new MultiplayerEndpoint(trimmed, port);
            return true;
        }
    }
}
