using System.Net.WebSockets;
using Common.Network;
using FluentAssertions;
using Framework.Tests.Security;
using Internal;
using MemoryPack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Xunit;

namespace Framework.Tests.Network;

/// <summary>
/// A Connection over a real Kestrel socket: a forced disconnect sends a close frame, and a failure of the read loop
/// still ends the connection.
/// </summary>
public class ConnectionCloseTests
{
    [Fact]
    public async Task ForceDisconnect_ClientGetsCloseFrame()
    {
        await using var server = await ConnectionServer.Start();
        using var client = await server.Connect();
        var connection = await server.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        connection.ForceDisconnect();

        var result = await client.ReceiveAsync(new byte[64], TestContext.Current.CancellationToken)
                                 .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        result.MessageType.Should().Be(WebSocketMessageType.Close);
        result.CloseStatus.Should().Be(WebSocketCloseStatus.NormalClosure);
    }

    [Fact]
    public async Task Run_HandlerThrows_ConnectionEnds()
    {
        await using var server = await ConnectionServer.Start(connection => {
            connection.Reader.OneWay.Advise(connection.Lifetime, _ => throw new InvalidOperationException("handler bug"));
        });

        using var client = await server.Connect();
        var connection = await server.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var message = MemoryPackSerializer.Serialize<IMessageFromClient>(
            new OneWayMessageFromClient { Context = new SharedSessionLatency.Ping { Id = 1 } });

        await client.SendAsync(message, WebSocketMessageType.Binary, true, TestContext.Current.CancellationToken);

        var run = await server.Finished.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        run.Should().BeOfType<InvalidOperationException>();
        connection.Lifetime.IsTerminated.Should().BeTrue("a connection whose read loop failed is over");

        var result = await client.ReceiveAsync(new byte[64], TestContext.Current.CancellationToken)
                                 .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        result.MessageType.Should().Be(WebSocketMessageType.Close);
    }

    private sealed class ConnectionServer : IAsyncDisposable
    {
        private ConnectionServer(TestWebHost host)
        {
            _host = host;
        }

        private readonly TestWebHost _host;

        public TaskCompletionSource<IConnection> Accepted { get; } = new();

        // Completes with the exception of Connection.Run, null when it returned.
        public TaskCompletionSource<Exception?> Finished { get; } = new();

        private TaskCompletionSource TestEnded { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static async Task<ConnectionServer> Start(Action<IConnection>? setup = null)
        {
            var builder = TestWebHost.CreateBuilder("Development");
            var app = builder.Build();
            app.UseWebSockets();

            ConnectionServer? server = null;

            app.Use(async (HttpContext context, RequestDelegate next) => {
                if (context.WebSockets.IsWebSocketRequest == false)
                {
                    await next(context);
                    return;
                }

                using var socket = await context.WebSockets.AcceptWebSocketAsync();
                var connection = new Connection(socket, new Lifetime(), NullLogger.Instance);
                setup?.Invoke(connection);

                try
                {
                    // The read loop is running when the test gets the connection: a ForceDisconnect before it would
                    // end the receive at once, and the abort of that receive may reset the socket under the close frame.
                    var run = connection.Run();
                    server!.Accepted.SetResult(connection);
                    await run;
                    server!.Finished.SetResult(null);
                }
                catch (Exception e)
                {
                    server!.Finished.SetResult(e);
                }

                // The socket stays open until the test is over: the close of the connection is not awaited by Run, and
                // a handler that ended before it would reset the socket.
                await server.TestEnded.Task;
            });

            server = new ConnectionServer(await TestWebHost.Start(app));
            return server;
        }

        public async Task<ClientWebSocket> Connect()
        {
            var client = new ClientWebSocket();
            var uri = new UriBuilder(_host.Address) { Scheme = "ws" }.Uri;
            await client.ConnectAsync(uri, TestContext.Current.CancellationToken);
            return client;
        }

        public ValueTask DisposeAsync()
        {
            TestEnded.SetResult();
            return _host.DisposeAsync();
        }
    }
}
