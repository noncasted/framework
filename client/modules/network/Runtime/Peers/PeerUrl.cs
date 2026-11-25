using System;

namespace Internal
{
    // A direct connection to another player (the host of a P2P match): NetworkConnection opens the socket of a
    // PeerUrl through the factory added for its name, the client stack above the socket is the same as with the server.
    public static class PeerUrl
    {
        public const string Scheme = "peer";

        public static string Create(string name)
        {
            return $"{Scheme}://{name}";
        }

        public static bool IsPeer(string url)
        {
            return url.StartsWith(Scheme + "://", StringComparison.Ordinal) == true;
        }

        public static string GetName(string url)
        {
            return new Uri(url).Host;
        }
    }
}
