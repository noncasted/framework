using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Shared;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // NetworkSession.Start: the wait for the local user ends with the loss of the connection, not only with the user.
    // Its timeout is not covered (real time).
    public class NetworkSessionTests
    {
        [UnityTest]
        public IEnumerator Start_ConnectionLostBeforeLocalUser_FailsAtOnce() => UniTask.ToCoroutine(async () =>
        {
            var writer = new ClosableWriter();
            var connection = new FakeConnection(writer);
            var session = new NetworkSession(new NetworkSessionCallbacks(), new NetworkUsersCollection(), new OpenSession(), connection);
            var lifetime = new Lifetime();

            var start = session.Start(lifetime, "ws://test", "ticket");
            await UniTask.Yield();

            writer.IsClosed = true;

            Exception exception = null;

            try
            {
                await start.Timeout(TimeSpan.FromSeconds(2));
            }
            catch (Exception caught)
            {
                exception = caught;
            }

            Assert.IsInstanceOf<OperationCanceledException>(exception, exception?.ToString());
            Assert.IsNotInstanceOf<TimeoutException>(exception);
            lifetime.Terminate();
        });

        private class OpenSession : ISessionConnection
        {
            public UniTask Connect(IReadOnlyLifetime lifetime, string serverUrl, string ticket)
            {
                return UniTask.CompletedTask;
            }
        }

        private class ClosableWriter : IConnectionWriter
        {
            public bool IsClosed { get; set; }

            public void WriteOneWay(INetworkContext value) => throw new NotSupportedException();
            public UniTask<T> WriteRequest<T>(INetworkContext commandContext) => throw new NotSupportedException();
            public void WriteResponse(INetworkContext commandContext, int requestId) => throw new NotSupportedException();
            public void OnRequestHandled(INetworkContext commandContext, int requestId) => throw new NotSupportedException();
            public UniTask ForceSendAll() => throw new NotSupportedException();
        }

        private class FakeConnection : INetworkConnection
        {
            public FakeConnection(IConnectionWriter writer)
            {
                Writer = writer;
            }

            public IConnectionReader Reader => throw new NotSupportedException();
            public IConnectionWriter Writer { get; }
            public IViewableDelegate<SocketCloseInfo> Closed { get; } = new ViewableDelegate<SocketCloseInfo>();
            public NetworkTraffic Traffic { get; } = new();

            public UniTask Run(IReadOnlyLifetime lifetime, string url) => throw new NotSupportedException();
            public void Drop(string reason) => throw new NotSupportedException();
        }
    }
}
