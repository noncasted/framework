using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Internal;
using NUnit.Framework;
using Shared;
using UnityEngine.TestTools;

namespace Global.Lobbies.Tests
{
    // Matchmaking: a result is awaited as long as the writer awaits its request. A shorter wait gave up on a lobby
    // the meta was still creating: the retry created a second one.
    public class MatchmakingTests
    {
        // The meta answers after 11 s, inside the 20 s of the writer: the lobby is taken, not timed out.
        [UnityTest]
        public IEnumerator CreateLobby_SlowAnswerWithinWriterTimeout_IsTaken() => UniTask.ToCoroutine(async () =>
        {
            var writer = new SlowWriter(TimeSpan.FromSeconds(11));
            var matchmaking = new Matchmaking(new FakeMetaSocket(writer));
            var lifetime = new Lifetime();

            try
            {
                var result = await matchmaking.CreateLobby(lifetime);

                Assert.AreEqual(SharedSessionRequests.EntryStatus.Ok, result.Status);
                Assert.AreEqual("ABCD", result.Code);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        private class SlowWriter : IConnectionWriter
        {
            public SlowWriter(TimeSpan delay)
            {
                _delay = delay;
            }

            private readonly TimeSpan _delay;

            public bool IsClosed => false;

            public void WriteOneWay(INetworkContext value) => throw new NotSupportedException();

            public async UniTask<T> WriteRequest<T>(INetworkContext commandContext)
            {
                await UniTask.Delay(_delay, DelayType.Realtime);

                INetworkContext result = new SharedSessionRequests.Result
                {
                    Status = SharedSessionRequests.EntryStatus.Ok,
                    Code = "ABCD",
                    ServerUrl = "ws://test",
                    Ticket = "ticket"
                };

                return (T)result;
            }

            public void WriteResponse(INetworkContext commandContext, int requestId) => throw new NotSupportedException();
            public void OnRequestHandled(INetworkContext commandContext, int requestId) => throw new NotSupportedException();
            public UniTask ForceSendAll() => throw new NotSupportedException();
        }

        private class FakeMetaSocket : IMetaSocket
        {
            public FakeMetaSocket(IConnectionWriter writer)
            {
                Connection = new FakeConnection(writer);
            }

            public INetworkConnection Connection { get; }
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
