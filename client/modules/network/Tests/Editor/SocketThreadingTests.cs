using System;
using System.Collections;
using System.Collections.Generic;
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
    // DefaultWebSocket keeps receiving while the main thread is busy (a scene load): the protocol pings of the server
    // are answered by the receive of ClientWebSocket only, so a receive loop that waits for the main thread between
    // two receives lets the server time the client out. The frames still reach the listeners on the main thread, in
    // order.
    // NetworkConnection: a connect that times out is aborted, its socket does not go on connecting (and opening) for
    // a connection that has failed.
    public class SocketThreadingTests
    {
        [UnityTest]
        public IEnumerator DefaultWebSocket_MainThreadBlocked_PingIsAnswered() => UniTask.ToCoroutine(async () =>
        {
            using var server = new RawServer(answerUpgrade: true);
            var lifetime = new Lifetime();

            try
            {
                var socket = new DefaultWebSocket(server.Url, lifetime);
                var received = new List<byte[]>();
                socket.Received.Advise(lifetime, received.Add);

                await socket.Connect();

                // The frames go out once the main thread is blocked: none of them can be handled on it until it is free.
                server.Go.Set();
                var isPonged = server.Pong.Wait(TimeSpan.FromSeconds(3));

                Assert.IsTrue(isPonged, "The ping of the server was not answered while the main thread was busy");

                await UniTask.Delay(200, DelayType.Realtime);

                Assert.AreEqual(1, received.Count);
                Assert.AreEqual(7, received[0][0]);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        [UnityTest]
        public IEnumerator NetworkConnection_ConnectTimesOut_SocketIsAborted() => UniTask.ToCoroutine(async () =>
        {
            using var server = new RawServer(answerUpgrade: false);
            var lifetime = new Lifetime();

            try
            {
                var connection = new NetworkConnection(new NetworkCommandsCollection(),
                    new PlatformOptions(),
                    new SilentNotice(),
                    new LocalServers(),
                    new PeerSockets(),
                    new NetworkProtocolHash("0123456789abcdef"));

                typeof(NetworkConnection)
                    .GetField("_connectTimeout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(connection, TimeSpan.FromMilliseconds(300));

                Exception exception = null;

                try
                {
                    await connection.Run(lifetime, server.Url);
                }
                catch (Exception caught)
                {
                    exception = caught;
                }

                Assert.IsInstanceOf<ConnectionFailedException>(exception, exception?.ToString());

                // The connection lifetime is alive: only the end of the failed connect closes the TCP socket.
                var isClosed = await UniTask.RunOnThreadPool(() => server.ClientClosed.Wait(TimeSpan.FromSeconds(3)));
                Assert.IsTrue(isClosed, "The socket of the timed out connect is still connecting");
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        private class SilentNotice : IClientOutdatedNotice
        {
            public bool IsShown => false;

            public void Show()
            {
            }
        }

        // A websocket server on a raw TCP socket. With the upgrade answered it waits for Go, sends a binary frame and
        // a ping and sets Pong when the pong of the client arrives. Without it, it reads the upgrade request and sets
        // ClientClosed when the client closes the TCP connection.
        private class RawServer : IDisposable
        {
            public RawServer(bool answerUpgrade)
            {
                _answerUpgrade = answerUpgrade;
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                Url = $"ws://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/";
                _thread = new Thread(Serve) { IsBackground = true };
                _thread.Start();
            }

            private readonly bool _answerUpgrade;
            private readonly TcpListener _listener;
            private readonly Thread _thread;
            private Socket _client;

            public readonly ManualResetEventSlim Go = new();
            public readonly ManualResetEventSlim Pong = new();
            public readonly ManualResetEventSlim ClientClosed = new();

            public string Url { get; }

            private void Serve()
            {
                try
                {
                    _client = _listener.AcceptSocket();
                    var request = ReadRequest();

                    if (_answerUpgrade == false)
                    {
                        WaitClientClosed();
                        return;
                    }

                    Upgrade(request);

                    Go.Wait(TimeSpan.FromSeconds(10));

                    // A binary frame of one byte, then a ping without payload (server frames are not masked).
                    _client.Send(new byte[] { 0x82, 0x01, 0x07 });
                    _client.Send(new byte[] { 0x89, 0x00 });

                    ReadPong();
                }
                catch (Exception) when (_listener.Server.IsBound == false)
                {
                    // Disposed by the test.
                }
                catch (SocketException)
                {
                    // The client has gone.
                }
                catch (ObjectDisposedException)
                {
                    // Disposed by the test.
                }
            }

            private string ReadRequest()
            {
                var request = new StringBuilder();
                var buffer = new byte[1024];

                while (request.ToString().Contains("\r\n\r\n") == false)
                {
                    var read = _client.Receive(buffer);

                    if (read == 0)
                        throw new InvalidOperationException("The client closed before the upgrade");

                    request.Append(Encoding.ASCII.GetString(buffer, 0, read));
                }

                return request.ToString();
            }

            private void Upgrade(string request)
            {
                var key = ReadHeader(request, "Sec-WebSocket-Key");

                using var sha1 = SHA1.Create();
                var accept = Convert.ToBase64String(
                    sha1.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));

                var response = "HTTP/1.1 101 Switching Protocols\r\n" +
                               "Upgrade: websocket\r\n" +
                               "Connection: Upgrade\r\n" +
                               $"Sec-WebSocket-Accept: {accept}\r\n\r\n";

                _client.Send(Encoding.ASCII.GetBytes(response));
            }

            // Client frames: opcode, masked length, 4 bytes of the mask, the payload.
            private void ReadPong()
            {
                var header = new byte[2];

                while (true)
                {
                    ReadExactly(header);

                    var opcode = header[0] & 0x0F;
                    var length = header[1] & 0x7F;
                    var rest = new byte[4 + length];
                    ReadExactly(rest);

                    if (opcode == 0x0A)
                    {
                        Pong.Set();
                        return;
                    }
                }
            }

            private void ReadExactly(byte[] buffer)
            {
                var offset = 0;

                while (offset < buffer.Length)
                {
                    var read = _client.Receive(buffer, offset, buffer.Length - offset, SocketFlags.None);

                    if (read == 0)
                        throw new SocketException();

                    offset += read;
                }
            }

            private void WaitClientClosed()
            {
                var buffer = new byte[1024];

                try
                {
                    while (_client.Receive(buffer) > 0)
                    {
                    }
                }
                catch (SocketException)
                {
                    // A reset is a close too.
                }

                ClientClosed.Set();
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
                Go.Set();
                _client?.Dispose();
                _listener.Stop();
            }
        }
    }
}
