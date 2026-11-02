using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using NUnit.Framework;
using Shared;
using UnityEditor;
using UnityEngine.TestTools;

namespace Global.Lobbies.Tests
{
    // Matchmaking: a result is awaited as long as the writer awaits its request. A shorter wait gave up on a lobby
    // the meta was still creating: the retry created a second one.
    public class MatchmakingTests
    {
        // The meta answers as late as it likes inside the 20 s of the writer: the lobby is taken, not timed out. The wait
        // is not real: a timer of its own would end the request before the answer (it did at 10 s), so the test looks
        // for such a timer among the tasks of UniTask. A timer that is not a task of UniTask (CancellationTokenSource
        // with a delay, CancelAfterSlim) is not seen.
        [UnityTest]
        public IEnumerator CreateLobby_SlowAnswerWithinWriterTimeout_IsTaken() => UniTask.ToCoroutine(async () =>
        {
            var writer = new HeldWriter();
            var matchmaking = new Matchmaking(new FakeMetaSocket(writer));
            var lifetime = new Lifetime();

            try
            {
                var request = default(UniTask<LobbyJoinResult>);
                var timers = TimersStartedBy(() => request = matchmaking.CreateLobby(lifetime));

                CollectionAssert.IsEmpty(timers, "The only timeout of a request is the one of the writer");

                await UniTask.Yield();
                await UniTask.Yield();
                Assert.AreEqual(UniTaskStatus.Pending, request.Status);

                writer.Answer(new SharedSessionRequests.Result
                {
                    Status = SharedSessionRequests.EntryStatus.Ok,
                    Code = "ABCD",
                    ServerUrl = "ws://test",
                    Ticket = "ticket"
                });

                var result = await request;

                Assert.AreEqual(SharedSessionRequests.EntryStatus.Ok, result.Status);
                Assert.AreEqual("ABCD", result.Code);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // The waits of time among the tasks of UniTask created by the action. The tracker of UniTask is a setting of the
        // editor saved in its preferences, which stay as they were.
        private static List<string> TimersStartedBy(Action action)
        {
            var preference = EditorPrefs.GetBool(TaskTracker.EnableTrackingKey);
            var tracking = TaskTracker.EditorEnableState.EnableTracking;
            var known = new HashSet<int>();
            var timers = new List<string>();

            TaskTracker.EditorEnableState.EnableTracking = true;

            try
            {
                TaskTracker.ForEachActiveTask((id, _, _, _, _) => known.Add(id));

                action();

                TaskTracker.ForEachActiveTask((id, type, _, _, _) =>
                {
                    if (known.Contains(id) == false && type.StartsWith("Delay"))
                        timers.Add(type);
                });
            }
            finally
            {
                TaskTracker.EditorEnableState.EnableTracking = tracking;
                EditorPrefs.SetBool(TaskTracker.EnableTrackingKey, preference);
            }

            return timers;
        }

        // Keeps a request unanswered until the test answers it.
        private class HeldWriter : IConnectionWriter
        {
            private readonly UniTaskCompletionSource<INetworkContext> _answer = new();

            public bool IsClosed => false;

            public void WriteOneWay(INetworkContext value) => throw new NotSupportedException();

            public void Answer(INetworkContext answer) => _answer.TrySetResult(answer);

            public async UniTask<T> WriteRequest<T>(INetworkContext commandContext)
            {
                return (T)await _answer.Task;
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
