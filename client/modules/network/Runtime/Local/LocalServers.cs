using System;
using System.Collections.Generic;

namespace Internal
{
    public interface ILocalServers
    {
        // The server answers the urls LocalServerUrl.Create(name) while the lifetime lasts.
        void Add(IReadOnlyLifetime lifetime, string name, ILocalServer server);

        ILocalServer Get(string url);
    }

    public class LocalServers : ILocalServers
    {
        private readonly Dictionary<string, ILocalServer> _servers = new();

        public void Add(IReadOnlyLifetime lifetime, string name, ILocalServer server)
        {
            if (_servers.ContainsKey(name) == true)
                throw new InvalidOperationException($"[Network] [Local] Server '{name}' is already added");

            _servers.Add(name, server);
            lifetime.Listen(() => _servers.Remove(name));
        }

        public ILocalServer Get(string url)
        {
            var name = LocalServerUrl.GetName(url);

            if (_servers.TryGetValue(name, out var server) == false)
                throw new InvalidOperationException($"[Network] [Local] No local server '{name}' for {url}");

            return server;
        }
    }
}
