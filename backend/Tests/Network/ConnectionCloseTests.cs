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

    // A forced disconnect that meets the start of the Run, under a handler that ends with the Run as a gateway does:
    // whichever goes first, the client gets the close frame, not a reset of the socket.
    [Theory]
    [InlineData(ForceDisconnectMoment.BeforeRun)]
    [InlineData(ForceDisconnectMoment.WithRun)]
    public async Task ForceDisconnect_AtTheStartOfRun_ClientGetsCloseFrame(ForceDisconnectMoment moment)
    {
        await using var server = await ConnectionServer.Start(forceDisconnect: moment);

        for (var attempt = 0; attempt < 300; attempt++)
        {
            using var client = await server.Connect();

            var result = await client.ReceiveAsync(new byte[64], TestContext.Current.CancellationToken)
                                     .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            result.MessageType.Should().Be(WebSocketMessageType.Close, $"attempt {attempt}");
            result.CloseStatus.Should().Be(WebSocketCloseStatus.NormalClosure);
        }
    }

    // The order the race above rarely hits: the disconnect ends right before the first receive of the Run, the read loop
    // is past its checks. A cancelled token aborts the socket in ReceiveAsync, the close frame may die with it.
    [Fact]
    public async Task ForceDisconnect_RightBeforeTheFirstReceive_TheReceiveIsNotCancelled()
    {
        await using var server = await ConnectionServer.Start(disconnectBeforeFirstReceive: true);
        using var client = await server.Connect();

        var result = await client.ReceiveAsync(new byte[64], TestContext.Current.CancellationToken)
                                 .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        result.MessageType.Should().Be(WebSocketMessageType.Close);
        (await server.FirstReceiveCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))
            .Should().BeFalse("the receive of a connection in its close waits for the answer of the client");
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

    public enum ForceDisconnectMoment
    {
        BeforeRun,
        WithRun
    }

    private sealed class ConnectionServer : IAsyncDisposable
    {
        private ConnectionServer(TestWebHost host)
        {
            _host = host;
        }

        private readonly TestWebHost _host;

        public TaskCompletionSource<IConnection> Accepted { get; } = new();

        public TaskCompletionSource<bool> FirstReceiveCancelled { get; } = new();

        // Completes with the exception of Connection.Run, null when it returned.
        public TaskCompletionSource<Exception?> Finished { get; } = new();

        private TaskCompletionSource TestEnded { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static async Task<ConnectionServer> Start(Action<IConnection>? setup = null, ForceDisconnectMoment? forceDisconnect = null, bool disconnectBeforeFirstReceive = false)
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
                var hooked = new HookedSocket(socket);
                var connection = new Connection(hooked, new Lifetime(), NullLogger.Instance);

                if (disconnectBeforeFirstReceive == true)
                {
                    hooked.RunBeforeFirstReceive(cancellation => {
                        connection.ForceDisconnect();

                        // The end of the close (it cancels the receive), or its wait for the client.
                        SpinWait.SpinUntil(() => cancellation.IsCancellationRequested, TimeSpan.FromMilliseconds(300));
                        server!.FirstReceiveCancelled.SetResult(cancellation.IsCancellationRequested);
                    });
                }

                setup?.Invoke(connection);

                if (forceDisconnect != null)
                {
                    await RunWithForceDisconnect(connection, forceDisconnect.Value);
                    return;
                }

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

        private static async Task RunWithForceDisconnect(IConnection connection, ForceDisconnectMoment moment)
        {
            if (moment == ForceDisconnectMoment.BeforeRun)
                connection.ForceDisconnect();

            var start = new Barrier(2);

            var disconnect = moment == ForceDisconnectMoment.WithRun
                ? Task.Run(() => {
                    start.SignalAndWait();
                    connection.ForceDisconnect();
                })
                : Task.CompletedTask;

            if (moment == ForceDisconnectMoment.WithRun)
                start.SignalAndWait();

            try
            {
                await connection.Run();
            }
            catch (InvalidOperationException)
            {
                // A Run after the disconnect: the connection is terminated, what the client got is the point here.
            }

            await disconnect;
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

    // A real socket with a moment to step in right before the first receive.
    private sealed class HookedSocket(WebSocket socket) : WebSocket
    {
        private Action<CancellationToken>? _beforeFirstReceive;

        public void RunBeforeFirstReceive(Action<CancellationToken> hook)
        {
            _beforeFirstReceive = hook;
        }

        public override WebSocketCloseStatus? CloseStatus => socket.CloseStatus;
        public override string? CloseStatusDescription => socket.CloseStatusDescription;
        public override WebSocketState State => socket.State;
        public override string? SubProtocol => socket.SubProtocol;

        public override ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            var hook = _beforeFirstReceive;
            _beforeFirstReceive = null;
            hook?.Invoke(cancellationToken);

            return socket.ReceiveAsync(buffer, cancellationToken);
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The connection reads through the Memory overload");
        }

        public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            return socket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            return socket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            return socket.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            return socket.CloseAsync(closeStatus, statusDescription, cancellationToken);
        }

        public override void Abort()
        {
            socket.Abort();
        }

        public override void Dispose()
        {
            socket.Dispose();
        }
    }
}
