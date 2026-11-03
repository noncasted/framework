using System;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests
{
    // What reaches the queue from the log of Unity, from which thread, and until when.
    public class TelemetryErrorCaptureTests
    {
        private TelemetryErrorFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _fixture = new TelemetryErrorFixture();
            _fixture.Listen();
        }

        [TearDown]
        public void TearDown()
        {
            _fixture.Running.Terminate();
        }

        [Test]
        public void Log_NotTaken_AWarningIsItsOwnLevel()
        {
            LogAssert.ignoreFailingMessages = true;
            Debug.Log("plain");

            Assert.AreEqual(0, _fixture.Queue.Count);

            Debug.LogWarning("warning");

            var drained = new TelemetryQueuedError[TelemetryErrorLimits.MaxQueued];
            Assert.AreEqual(1, _fixture.Queue.Drain(drained));
            Assert.AreEqual(TelemetryErrorLevel.Warning, drained[0].Level);
        }

        [Test]
        public void ErrorExceptionAssert_Taken_WithTheirLevels()
        {
            LogAssert.ignoreFailingMessages = true;
            Debug.LogError("first kind");
            Debug.LogException(new InvalidOperationException("second kind"));
            Debug.LogAssertion("third kind");

            _fixture.PumpAt(0);
            _fixture.PumpAt(1000);

            var entries = _fixture.Records.Entries;
            Assert.AreEqual(3, entries.Count);
            Assert.AreEqual(TelemetryErrorLevel.Error, entries.Single(entry => entry.Message.Contains("first")).Level);
            Assert.AreEqual(TelemetryErrorLevel.Exception, entries.Single(entry => entry.Message.Contains("second")).Level);
            Assert.AreEqual(TelemetryErrorLevel.Error, entries.Single(entry => entry.Message.Contains("third")).Level);
            StringAssert.StartsWith("InvalidOperationException", entries.Single(entry => entry.Message.Contains("second")).Message);
        }

        [Test]
        public void Handler_DoesNotScrubOrGroup_JustQueues()
        {
            LogAssert.ignoreFailingMessages = true;
            Debug.LogError("kind");

            Assert.AreEqual(1, _fixture.Queue.Count);
            Assert.IsEmpty(_fixture.Records.Entries);
        }

        [Test]
        public void TextOfTheError_ComesScrubbed()
        {
            LogAssert.ignoreFailingMessages = true;
            Debug.LogError("Login failed for ivan.petrov@mail.ru with id 123456789");

            _fixture.PumpAt(0);
            _fixture.PumpAt(1000);

            var entry = _fixture.Records.Entries.Single();
            StringAssert.Contains("Login failed for", entry.Message);
            StringAssert.DoesNotContain("ivan", entry.Message);
            StringAssert.DoesNotContain("123456789", entry.Message);
            StringAssert.Contains("TelemetryErrorCaptureTests", entry.Stack);
        }

        [Test]
        public void AfterTheLifetime_NoHandler()
        {
            LogAssert.ignoreFailingMessages = true;
            Debug.LogError("before");
            Assert.AreEqual(1, _fixture.Queue.Count);

            _fixture.Running.Terminate();
            Debug.LogError("after");

            Assert.AreEqual(1, _fixture.Queue.Count);
        }

        // Debug.LogException of the fork when nobody listens to UnobservedTaskException: no subscription of the module is
        // needed, and one would take the exception away from the log of the game.
        [Test]
        public void UnobservedExceptionOfUniTask_ComesThroughTheLog()
        {
            LogAssert.ignoreFailingMessages = true;
            Throwing().Forget();

            Assert.AreEqual(1, _fixture.Queue.Count);
            _fixture.PumpAt(0);
            _fixture.PumpAt(1000);
            StringAssert.Contains("of a task", _fixture.Records.Entries.Single().Message);
            Assert.AreEqual(TelemetryErrorLevel.Exception, _fixture.Records.Entries.Single().Level);
        }

        [Test]
        public void HandlerThatThrows_ComesThroughReactiveLogging()
        {
            LogAssert.ignoreFailingMessages = true;
            var source = new EventSource();
            source.Advise(_fixture.Running, () => throw new InvalidOperationException("of a handler"));

            source.Invoke();

            _fixture.PumpAt(0);
            _fixture.PumpAt(1000);
            StringAssert.Contains("of a handler", _fixture.Records.Entries.Single().Message);
        }

        [Test]
        public void OtherThreads_NothingLost_NothingBroken()
        {
            LogAssert.ignoreFailingMessages = true;
            const int threads = 4;
            const int perThread = 500;

            var tasks = Enumerable.Range(0, threads)
                .Select(_ => Task.Run(() =>
                {
                    for (var i = 0; i < perThread; i++)
                        Debug.LogError("from a thread");
                }))
                .ToArray();

            var time = 0L;

            while (tasks.All(task => task.IsCompleted) == false)
                _fixture.PumpAt(time);

            Task.WaitAll(tasks);
            _fixture.PumpAt(time);
            _fixture.Pump.Pump();
            _fixture.PumpAt(time + 1000);

            // Same text, whatever the thread: all of them on one record of one group. The stacks differ between the
            // threads only if the frames of the task do, so the sum is what counts.
            Assert.AreEqual(threads * perThread, _fixture.Records.Entries.Sum(entry => entry.Repeat));
            Assert.AreEqual(0, _fixture.Queue.Count);
        }

        [Test]
        public void OtherThreads_DifferentTexts_QueueStaysBounded()
        {
            LogAssert.ignoreFailingMessages = true;
            var tasks = Enumerable.Range(0, 4)
                .Select(thread => Task.Run(() =>
                {
                    for (var i = 0; i < 1000; i++)
                        Debug.LogError("from a thread " + (char)('a' + i % 26) + thread);
                }))
                .ToArray();

            Task.WaitAll(tasks);

            Assert.AreEqual(TelemetryErrorLimits.MaxQueued, _fixture.Queue.Count);
        }

        private static async UniTask Throwing()
        {
            await UniTask.CompletedTask;
            throw new InvalidOperationException("of a task");
        }
    }
}
