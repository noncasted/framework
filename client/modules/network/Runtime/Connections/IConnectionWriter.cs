using Cysharp.Threading.Tasks;
using Shared;

namespace Internal
{
    public interface IConnectionWriter
    {
        // No request is answered any more: the connection is lost (reported once, by INetworkConnection.Closed) or
        // the lifetime it was run with has ended (its owner is unloaded). Requests fail with OperationCanceledException.
        // NOT closed: a live connection after ForceSendAll; a request made there is a mistake of the caller.
        bool IsClosed { get; }

        void WriteOneWay(INetworkContext value);

        // Never returns null. The response T, or:
        // NetworkRequestException - the server answered with ErrorResponse (its command threw or is unknown);
        // TimeoutException - no response in RequestTimeoutSeconds;
        // OperationCanceledException - the connection is closed (IsClosed) before the response;
        // InvalidOperationException - a response of another type, or a request after ForceSendAll.
        UniTask<T> WriteRequest<T>(INetworkContext commandContext);
        void WriteResponse(INetworkContext commandContext, int requestId);
        void OnRequestHandled(INetworkContext commandContext, int requestId);
        UniTask ForceSendAll();
    }
}
