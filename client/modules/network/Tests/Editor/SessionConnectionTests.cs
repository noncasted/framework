using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // The server takes the ticket and drops the socket without an answer and without a Close frame (the session
    // gateway does so when the ticket comes after its handshake timeout): SessionConnection.Connect fails at once
    // with OperationCanceledException as every request of a lost connection, not after the 20 s timeout of the
    // request. A subclass of OperationCanceledException would not carry more: UniTaskCompletionSource turns any of
    // them into a bare cancellation.
    public class SessionConnectionTests
    {
        private static readonly TimeSpan LossTimeout = TimeSpan.FromSeconds(5);

        [UnityTest]
        public IEnumerator ServerDropsAfterTicket_ConnectFailsAtOnce() => UniTask.ToCoroutine(async () =>
        {
            using var server = new DroppingServer();
            var lifetime = new Lifetime();

            try
            {
                var connection = new NetworkConnection(new NetworkCommandsCollection(),
                    new SilentOutdatedNotice(),
                    new WebSocketFactory(new PlatformOptions(), new LocalServers(), new PeerSockets()),
                    new NetworkProtocolHash("0123456789abcdef"));

                Exception exception = null;

                try
                {
                    await new SessionConnection(connection).Connect(lifetime, server.Url, "ticket").Timeout(LossTimeout);
                }
                catch (Exception caught)
                {
                    exception = caught;
                }

                Assert.IsInstanceOf<OperationCanceledException>(exception, exception?.ToString());
                Assert.IsTrue(server.TicketReceived, "the ticket was never sent");
                Assert.IsTrue(connection.Writer.IsClosed);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        private class SilentOutdatedNotice : IClientOutdatedNotice
        {
            public bool IsShown => false;

            public void Show()
            {
                throw new InvalidOperationException("The test server is of the same protocol");
            }
        }

        // A websocket server on a raw TCP socket: answers the upgrade, waits for the first frame and closes the
        // socket (FIN, no Close frame: 1006 on the client).
        private class DroppingServer : IDisposable
        {
            public DroppingServer()
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                Url = $"ws://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/";
                _thread = new Thread(Serve) { IsBackground = true };
                _thread.Start();
            }

            private readonly TcpListener _listener;
            private readonly Thread _thread;
            private Socket _client;
            private volatile bool _ticketReceived;

            public string Url { get; }
            public bool TicketReceived => _ticketReceived;

            private void Serve()
            {
                _client = _listener.AcceptSocket();

                var request = new StringBuilder();
                var buffer = new byte[4096];

                while (request.ToString().Contains("\r\n\r\n") == false)
                {
                    var read = _client.Receive(buffer);

                    if (read == 0)
                        throw new InvalidOperationException("The client closed before the upgrade");

                    request.Append(Encoding.ASCII.GetString(buffer, 0, read));
                }

                var key = ReadHeader(request.ToString(), "Sec-WebSocket-Key");

                using var sha1 = SHA1.Create();
                var accept = Convert.ToBase64String(
                    sha1.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));

                _client.Send(Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\n" +
                                                     "Upgrade: websocket\r\n" +
                                                     "Connection: Upgrade\r\n" +
                                                     $"Sec-WebSocket-Accept: {accept}\r\n\r\n"));

                if (_client.Receive(buffer) == 0)
                    throw new InvalidOperationException("The client closed before the ticket");

                _ticketReceived = true;
                _client.Shutdown(SocketShutdown.Both);
                _client.Close();
            }

            private static string ReadHeader(string request, string name)
            {
                foreach (var line in request.Split("\r\n"))
                {
                    if (line.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase) == true)
                        return line.Substring(name.Length + 1).Trim();
                }

                throw new InvalidOperationException($"No {name} in the upgrade request");
            }

            public void Dispose()
            {
                _client?.Dispose();
                _listener.Stop();
            }
        }
    }
}
