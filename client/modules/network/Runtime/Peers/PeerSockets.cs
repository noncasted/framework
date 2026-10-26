using System;
using System.Collections.Generic;

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

    public interface IPeerSockets
    {
        // The socket of PeerUrl.Create(name) is created by the factory (over the lifetime of the socket) while the
        // lifetime lasts.
        void Add(IReadOnlyLifetime lifetime, string name, Func<IReadOnlyLifetime, IWebSocket> factory);

        IWebSocket Create(string url, IReadOnlyLifetime socketLifetime);
    }

    public class PeerSockets : IPeerSockets
    {
        private readonly Dictionary<string, Func<IReadOnlyLifetime, IWebSocket>> _factories = new();

        public void Add(IReadOnlyLifetime lifetime, string name, Func<IReadOnlyLifetime, IWebSocket> factory)
        {
            if (_factories.ContainsKey(name) == true)
                throw new InvalidOperationException($"[Network] [Peers] Peer '{name}' is already added");

            _factories.Add(name, factory);
            lifetime.Listen(() => _factories.Remove(name));
        }

        public IWebSocket Create(string url, IReadOnlyLifetime socketLifetime)
        {
            var name = PeerUrl.GetName(url);

            if (_factories.TryGetValue(name, out var factory) == false)
                throw new InvalidOperationException($"[Network] [Peers] No peer '{name}' for {url}");

            return factory(socketLifetime);
        }
    }
}
