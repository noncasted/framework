using System;
using MemoryPack;
using Shared;

namespace Internal
{
    // One socket of a local server: reads the messages of the client and writes the messages of the server in the
    // envelopes of the wire (IMessageFromClient / IMessageFromServer), as the backend dispatchers do. Every request is
    // answered: a failed one with ErrorResponse (LocalServerRequestException - its code, anything else - Internal).
    public abstract class LocalServerConnection : ILocalServerConnection
    {
        protected LocalServerConnection(IReadOnlyLifetime lifetime, ILocalClient client)
        {
            Lifetime = lifetime;
            _client = client;
        }

        private static readonly ILog _log = Logs.Of<LocalServerConnection>();

        private readonly ILocalClient _client;

        public IReadOnlyLifetime Lifetime { get; }

        public void OnReceived(byte[] bytes)
        {
            var message = MemoryPackSerializer.Deserialize<IMessageFromClient>(bytes);

            switch (message)
            {
                case OneWayMessageFromClient oneWay:
                    OnOneWay(oneWay.Context);
                    break;

                case RequestMessageFromClient request:
                    HandleRequest(request);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"[Network] [Local] Unexpected message from the client: {message?.GetType().FullName ?? "null"}");
            }
        }

        public void Send(INetworkContext context)
        {
            Write(new OneWayMessageFromServer { Context = context });
        }

        protected void Respond(int requestId, INetworkContext context)
        {
            Write(new ResponseMessageFromServer { Context = context, RequestId = requestId });
        }

        protected void Close(SocketCloseInfo info)
        {
            _client.Close(info);
        }

        protected abstract void OnOneWay(INetworkContext context);

        // Answers with Respond, before or after the one-way messages the request causes.
        protected abstract void OnRequest(int requestId, INetworkContext context);

        private void HandleRequest(RequestMessageFromClient request)
        {
            var name = request.Context?.GetType().Name ?? "null";

            try
            {
                OnRequest(request.RequestId, request.Context);
            }
            catch (LocalServerRequestException exception)
            {
                _log.Warning("Request {Name} is refused: {Message}", name, exception.Message);
                Respond(request.RequestId, new ErrorResponse { Code = exception.Code, Message = $"{name}: {exception.Message}" });
            }
            catch (Exception exception)
            {
                _log.Exception(exception);

                Respond(request.RequestId, new ErrorResponse
                {
                    Code = ErrorCode.Internal,
                    Message = $"{name}: {exception.GetType().Name} {exception.Message}"
                });
            }
        }

        private void Write(IMessageFromServer message)
        {
            if (Lifetime.IsTerminated == true)
                return;

            _client.Send(MemoryPackSerializer.Serialize(message));
        }
    }

    public class LocalServerRequestException : Exception
    {
        public LocalServerRequestException(ErrorCode code, string message) : base(message)
        {
            Code = code;
        }

        public ErrorCode Code { get; }
    }
}
