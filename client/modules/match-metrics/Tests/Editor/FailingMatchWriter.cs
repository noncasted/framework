using System;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Global.MatchMetrics.Tests
{
    // A live connection whose every request fails with what the test gives, as IConnectionWriter does.
    public class FailingMatchWriter : IConnectionWriter
    {
        public Exception Failure { get; private set; } = new TimeoutException("No response");
        public bool IsClosed => false;
        public object CurrentRun { get; private set; } = new object();

        public void FailWith(Exception failure)
        {
            Failure = failure;
        }

        // The same connection run again after a loss.
        public void RunAgain()
        {
            CurrentRun = new object();
        }

        public UniTask<T> WriteRequest<T>(INetworkContext commandContext)
        {
            return UniTask.FromException<T>(Failure);
        }

        public void WriteOneWay(ReadOnlySpan<byte> message) => throw new NotSupportedException();
        public void WriteOneWay(INetworkContext value) => throw new NotSupportedException();
        public void WriteResponse(INetworkContext commandContext, int requestId) => throw new NotSupportedException();
        public void OnRequestHandled(INetworkContext commandContext, int requestId) => throw new NotSupportedException();
        public UniTask ForceSendAll() => throw new NotSupportedException();
    }
}
