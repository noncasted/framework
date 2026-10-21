using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    public class DefaultWebSocketTests
    {
        private const int ServerCloseCode = 4001;

        // A server that accepts the socket and closes it at once (an outdated client, ProtocolVersion.ClientOutdatedCloseCode),
        // and a client that sends right after Connect (the session authorization): the send meets a closed socket
        // before the receive loop has reported the Close frame. The loss must carry the code of the server, not 1006.
        [UnityTest]
        public IEnumerator Send_AfterServerClose_ClosedCarriesServerCode() => UniTask.ToCoroutine(async () =>
        {
            var port = GetFreePort();
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();

            var serverTask = Serve(listener);

            var lifetime = new Lifetime();

            try
            {
                var socket = new DefaultWebSocket($"ws://127.0.0.1:{port}/", lifetime);
                var closed = new UniTaskCompletionSource<SocketCloseInfo>();
                socket.Closed.Advise(lifetime, info => closed.TrySetResult(info));

                await socket.Connect();

                // Blocks the main thread until the receive loop (thread pool) has taken the Close frame: its report
                // waits for the main thread, so the send below runs first, as it did in the real transition to a match.
                WaitForClose(socket);

                await socket.Send(new byte[] { 1, 2, 3 });

                var info = await closed.Task.Timeout(TimeSpan.FromSeconds(10));

                Assert.AreEqual(ServerCloseCode, info.Code, info.ToString());
            }
            finally
            {
                lifetime.Terminate();
                listener.Stop();
            }

            await serverTask;
        });

        private static void WaitForClose(DefaultWebSocket socket)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

            while (socket.State == WebSocketState.Open)
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("The server Close frame did not arrive");

                Thread.Sleep(5);
            }
        }

        private static async UniTask Serve(HttpListener listener)
        {
            var context = await listener.GetContextAsync();
            var webSocketContext = await context.AcceptWebSocketAsync(null);
            var webSocket = webSocketContext.WebSocket;

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await webSocket.CloseOutputAsync((WebSocketCloseStatus)ServerCloseCode, "client outdated", timeout.Token);
        }

        private static int GetFreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }
    }
}
