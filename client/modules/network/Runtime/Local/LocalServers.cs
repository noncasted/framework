using System;
using System.Collections.Generic;

namespace Internal
{
    // A server that runs inside the client process (the offline mode): it is addressed by a url of the scheme
    // LocalServerUrl.Scheme, NetworkConnection opens a LoopbackWebSocket to it instead of a network socket. The
    // client stack above the socket (reader, writer, commands, sessions) works the same as with a remote server.
    public interface ILocalServer
    {
        // A socket of the client is opened: the returned connection gets every frame the client sends, the client
        // gets every frame the connection sends to it. The lifetime ends with the socket.
        ILocalServerConnection Accept(IReadOnlyLifetime lifetime, ILocalClient client);
    }

    public interface ILocalServerConnection
    {
        void OnReceived(byte[] bytes);
    }

    // The client side of a loopback socket, as the local server sees it.
    public interface ILocalClient
    {
        void Send(byte[] bytes);

        // The server closes the socket (a refused authorization): reported to the client as a loss with the code.
        void Close(SocketCloseInfo info);
    }

    public static class LocalServerUrl
    {
        public const string Scheme = "offline";

        public static string Create(string name)
        {
            return $"{Scheme}://{name}";
        }

        public static bool IsLocal(string url)
        {
            return url.StartsWith(Scheme + "://", StringComparison.Ordinal) == true;
        }

        public static string GetName(string url)
        {
            return new Uri(url).Host;
        }
    }

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
