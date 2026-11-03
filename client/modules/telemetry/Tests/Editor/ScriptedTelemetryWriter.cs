using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Global.Telemetry.Tests
{
    // The writer of a connection by the rules of IConnectionWriter, with the server in the test: the server gives an answer or
    // throws what the request fails with; a null answer leaves the request in flight until the test ends it.
    public class ScriptedTelemetryWriter : IConnectionWriter
    {
        public ScriptedTelemetryWriter(Func<INetworkContext, INetworkContext> server = null)
        {
            _server = server ?? (_ => null);
        }

        private readonly Func<INetworkContext, INetworkContext> _server;
        private readonly List<UniTaskCompletionSource<INetworkContext>> _inFlight = new List<UniTaskCompletionSource<INetworkContext>>();

        public List<INetworkContext> Requests { get; } = new List<INetworkContext>();
        public int InFlight => _inFlight.Count;
        public bool IsClosed { get; private set; }
        public object CurrentRun { get; private set; } = new object();

        // The same connection run again after a loss.
        public void RunAgain()
        {
            IsClosed = false;
            CurrentRun = new object();
        }

        // The connection is lost: what is in flight is cancelled.
        public void Lose()
        {
            IsClosed = true;

            foreach (var request in _inFlight)
                request.TrySetCanceled();

            _inFlight.Clear();
        }

        // Ends the oldest request in flight.
        public void Answer(INetworkContext answer)
        {
            var request = _inFlight[0];
            _inFlight.RemoveAt(0);
            request.TrySetResult(answer);
        }

        public void Fail(Exception exception)
        {
            var request = _inFlight[0];
            _inFlight.RemoveAt(0);
            request.TrySetException(exception);
        }

        public async UniTask<T> WriteRequest<T>(INetworkContext commandContext)
        {
            Requests.Add(commandContext);

            if (IsClosed)
                throw new OperationCanceledException("The connection is closed");

            var answer = await AnswerOf(commandContext);

            return answer switch
            {
                T typed => typed,
                ErrorResponse error => throw new NetworkRequestException(error.Code, error.Message, "test"),
                _ => throw new InvalidOperationException($"Got {answer.GetType().Name}, expected {typeof(T).Name}")
            };
        }

        public void WriteOneWay(INetworkContext value) => throw new NotSupportedException();
        public void WriteResponse(INetworkContext commandContext, int requestId) => throw new NotSupportedException();
        public void OnRequestHandled(INetworkContext commandContext, int requestId) => throw new NotSupportedException();
        public UniTask ForceSendAll() => throw new NotSupportedException();

        private UniTask<INetworkContext> AnswerOf(INetworkContext request)
        {
            var answer = _server(request);

            if (answer != null)
                return UniTask.FromResult(answer);

            var inFlight = new UniTaskCompletionSource<INetworkContext>();
            _inFlight.Add(inFlight);
            return inFlight.Task;
        }
    }
}
