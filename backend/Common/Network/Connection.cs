using System.Net.WebSockets;
using Common.Extensions;
using Internal;
using Microsoft.Extensions.Logging;

namespace Common.Network;

public interface IConnection
{
    IReadOnlyLifetime Lifetime { get; }
    IConnectionReader Reader { get; }
    IConnectionWriter Writer { get; }

    Task Run();
    void ForceDisconnect();
}

public class Connection : IConnection
{
    public Connection(WebSocket webSocket, IReadOnlyLifetime parentLifetime, ILogger logger)
    {
        _webSocket = webSocket;
        _lifetime = parentLifetime.Child();
        _receiveLifetime = parentLifetime.Child();
        _reader = new ConnectionReader(webSocket, logger);
        _writer = new ConnectionWriter(webSocket, logger);
    }

    private readonly WebSocket _webSocket;
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(2);

    private readonly ILifetime _lifetime;

    // The pending ReceiveAsync is cancelled only after the close frame went out: cancelling it aborts the socket
    // (Kestrel), and a forced disconnect reached the client as 1006 instead of a close.
    private readonly ILifetime _receiveLifetime;

    private readonly ConnectionReader _reader;
    private readonly ConnectionWriter _writer;

    private int _isClosing;
    private Task _read = Task.CompletedTask;

    public IReadOnlyLifetime Lifetime => _lifetime;
    public IConnectionReader Reader => _reader;
    public IConnectionWriter Writer => _writer;

    public async Task Run()
    {
        if (_lifetime.IsTerminated == true)
            throw new InvalidOperationException("Connection is terminated");

        _writer.Run(_lifetime).NoAwait();

        // A failure of the read loop (a handler of a message that throws) ends the connection too: its lifetime and
        // the socket are not left open behind the exception.
        try
        {
            _read = _reader.Run(_receiveLifetime);
            await _read;
        }
        finally
        {
            if (_lifetime.IsTerminated == false)
                _lifetime.Terminate();

            OnDisconnected();
        }
    }

    public void ForceDisconnect()
    {
        _lifetime.Terminate();
        OnDisconnected();
    }

    private void OnDisconnected()
    {
        if (Interlocked.Exchange(ref _isClosing, 1) == 1)
            return;

        Close().NoAwait();
    }

    private async Task Close()
    {
        try
        {
            if (_webSocket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
                return;

            // The close frame goes out next to the pending receive; the receive gets the answer of the client and the
            // read loop ends. A client that does not answer is cut after the timeout.
            using var timeout = new CancellationTokenSource(CloseTimeout);
            await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "User disconnected", timeout.Token);
            await Task.WhenAny(_read, Task.Delay(CloseTimeout));
        }
        catch (Exception e) when (e is WebSocketException or ObjectDisposedException or IOException
                                      or OperationCanceledException)
        {
            // Close is best-effort: the client may already be gone without completing the handshake.
        }
        finally
        {
            _receiveLifetime.Terminate();
        }
    }
}
