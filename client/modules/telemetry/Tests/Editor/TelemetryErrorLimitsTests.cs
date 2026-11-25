using System;
using System.Linq;
using System.Threading.Tasks;
using Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests
{
    // A stream of errors: one record with a counter, bounded in every direction, and no effect on the game.
    public class TelemetryErrorLimitsTests
    {
        private const string _stack = "Game.Player:Update ()\nGame.Loop:Run ()\n";

        private TelemetryErrorFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _fixture = new TelemetryErrorFixture();
        }

        [TearDown]
        public void TearDown()
        {
            _fixture.Running.Terminate();
        }

        private void Add(string message, string stack = _stack)
        {
            _fixture.Queue.Add(TelemetryErrorLevel.Error, message, stack);
        }

        // The pass that takes what was queued, then the one a window later that gives the record.
        private void PassWindow(TelemetryErrorFixture fixture)
        {
            fixture.PumpAt(0);
            fixture.PumpAt(TelemetryErrorLimits.FirstWindowMs);
        }

        // Frames in order, the log of Unity on the way: this is the loop of a broken Update.
        [Test]
        public void ErrorEveryFrame_OneRecordWithACounter()
        {
            LogAssert.ignoreFailingMessages = true;
            _fixture.Listen();

            for (var frame = 0; frame < 60; frame++)
            {
                Debug.LogError("Broken update");
                _fixture.PumpAt(frame * 16);
            }

            Assert.IsEmpty(_fixture.Records.Entries);

            _fixture.PumpAt(TelemetryErrorLimits.FirstWindowMs);

            var entry = _fixture.Records.Entries.Single();
            StringAssert.Contains("Broken update", entry.Message);
            Assert.AreEqual(60, entry.Repeat);
        }

        [Test]
        public void ErrorEveryFrame_InOneHour_FewRecords_ThatCountEverything()
        {
            LogAssert.ignoreFailingMessages = true;
            var frames = 0;

            for (var time = 0L; time < 3600_000; time += 100)
            {
                Add("Broken update");
                frames++;
                _fixture.PumpAt(time);
            }

            _fixture.Pump.FlushAll();

            Assert.Less(_fixture.Records.Entries.Count, 80);
            Assert.AreEqual(frames, _fixture.Records.Entries.Sum(entry => entry.Repeat));
        }

        [Test]
        public void SameMessage_DifferentId_OneGroup()
        {
            LogAssert.ignoreFailingMessages = true;
            for (var i = 0; i < 3; i++)
            {
                Add("No player with id " + (100000 + i));
                _fixture.PumpAt(i);
            }

            _fixture.PumpAt(TelemetryErrorLimits.FirstWindowMs);

            var entry = _fixture.Records.Entries.Single();
            Assert.AreEqual(3, entry.Repeat);
            StringAssert.DoesNotContain("10000", entry.Message);
        }

        [Test]
        public void SameMessage_DifferentStack_TwoGroups()
        {
            LogAssert.ignoreFailingMessages = true;
            Add("Broken", "Game.First:Run ()");
            Add("Broken", "Game.Second:Run ()");

            PassWindow(_fixture);

            Assert.AreEqual(2, _fixture.Records.Entries.Count);
        }

        [Test]
        public void DifferentTextPastTheStart_OneGroup()
        {
            LogAssert.ignoreFailingMessages = true;
            var start = string.Concat(Enumerable.Repeat("word ", TelemetryErrorLimits.KeyMessageLength / 5 + 1));

            Add(start + " first");
            Add(start + " second");
            PassWindow(_fixture);

            Assert.AreEqual(2, _fixture.Records.Entries.Single().Repeat);
        }

        // New texts cost the regular expressions, known ones do not: a message with a new id every time is cut off here.
        [Test]
        public void NewTextsInOnePass_ScrubbedUpToTheBudget()
        {
            LogAssert.ignoreFailingMessages = true;
            for (var i = 0; i < TelemetryErrorLimits.MaxQueued; i++)
                Add("No player with id " + (100000 + i));

            _fixture.PumpAt(TelemetryErrorLimits.FirstWindowMs);
            _fixture.PumpAt(TelemetryErrorLimits.FirstWindowMs * 2);

            Assert.LessOrEqual(_fixture.Records.Entries.Sum(entry => entry.Repeat), TelemetryErrorLimits.MaxScrubsPerPass);
        }

        [Test]
        public void KnownTexts_NotLimitedByTheBudget()
        {
            LogAssert.ignoreFailingMessages = true;
            for (var i = 0; i < TelemetryErrorLimits.MaxQueued; i++)
                Add(i % 2 == 0 ? "Even" : "Odd");

            _fixture.PumpAt(0);

            for (var i = 0; i < TelemetryErrorLimits.MaxQueued; i++)
                Add(i % 2 == 0 ? "Even" : "Odd");

            _fixture.PumpAt(TelemetryErrorLimits.FirstWindowMs);

            Assert.AreEqual(TelemetryErrorLimits.MaxQueued * 2, _fixture.Records.Entries.Sum(entry => entry.Repeat));
        }

        [Test]
        public void ManyGroupsAtOnce_LimitPerWindow_RestWaits()
        {
            LogAssert.ignoreFailingMessages = true;
            var kinds = TelemetryErrorLimits.MaxRecordsPerWindow * 2;

            for (var i = 0; i < kinds; i++)
            {
                Add("Kind " + (char)('a' + i));
                _fixture.PumpAt(0);
            }

            _fixture.PumpAt(TelemetryErrorLimits.FirstWindowMs);
            Assert.AreEqual(TelemetryErrorLimits.MaxRecordsPerWindow, _fixture.Records.Entries.Count);

            _fixture.PumpAt(TelemetryErrorLimits.FirstWindowMs + 1);
            Assert.AreEqual(kinds, _fixture.Records.Entries.Count);
        }

        [Test]
        public void TooManyGroups_NewOnesDropped()
        {
            LogAssert.ignoreFailingMessages = true;
            var fixture = new TelemetryErrorFixture();

            for (var i = 0; i < TelemetryErrorLimits.MaxGroups + 10; i++)
            {
                fixture.Groups.Count(TelemetryErrorLevel.Error, null, "Kind " + i, null, 1);
            }

            fixture.Pump.FlushAll();

            Assert.AreEqual(TelemetryErrorLimits.MaxGroups, fixture.Records.Entries.Count);
        }

        [Test]
        public void SessionLimit_NoMoreRecords_QueueClosed()
        {
            LogAssert.ignoreFailingMessages = true;
            _fixture.Listen();

            for (var round = 0; round < 40 && _fixture.Queue.IsClosed == false; round++)
            {
                for (var i = 0; i < TelemetryErrorLimits.MaxGroups; i++)
                    _fixture.Groups.Count(TelemetryErrorLevel.Error, null, "Kind " + i, null, 1);

                _fixture.Pump.FlushAll();
            }

            Assert.AreEqual(TelemetryErrorLimits.MaxRecordsPerSession, _fixture.Records.Entries.Count);
            Assert.IsTrue(_fixture.Queue.IsClosed);

            Debug.LogError("late");
            Assert.AreEqual(0, _fixture.Queue.Count);
        }

        [Test]
        public void Queue_Full_NewOnesDropped_OldestStay()
        {
            LogAssert.ignoreFailingMessages = true;
            for (var i = 0; i < TelemetryErrorLimits.MaxQueued * 3; i++)
                Add("Kind " + (i % 7 == 0 ? "x" : i.ToString()));

            Assert.AreEqual(TelemetryErrorLimits.MaxQueued, _fixture.Queue.Count);
        }

        [Test]
        public void Queue_SameErrorInARow_OneSlotWithACounter()
        {
            LogAssert.ignoreFailingMessages = true;
            for (var i = 0; i < 1000; i++)
                Add("Same");

            Assert.AreEqual(1, _fixture.Queue.Count);
        }

        [Test]
        public void Queue_SameErrorInARow_NoGarbage()
        {
            LogAssert.ignoreFailingMessages = true;
            var message = new string('m', 200);
            var stack = new string('s', 2000);
            _fixture.Queue.Add(TelemetryErrorLevel.Error, message, stack);

            var before = GC.GetTotalMemory(false);

            for (var i = 0; i < 100_000; i++)
                _fixture.Queue.Add(TelemetryErrorLevel.Error, message, stack);

            Assert.Less(GC.GetTotalMemory(false) - before, 4096);
        }

        [Test]
        public void Pump_Repeating_NoGarbage()
        {
            LogAssert.ignoreFailingMessages = true;
            Add("Same");
            _fixture.PumpAt(0);

            var before = GC.GetTotalMemory(false);

            for (var i = 0; i < 2000; i++)
            {
                _fixture.Queue.Add(TelemetryErrorLevel.Error, "Same", _stack);
                _fixture.Pump.Pump();
            }

            Assert.Less(GC.GetTotalMemory(false) - before, 4096);
        }

        [Test]
        public void LongTexts_CutToTheContract()
        {
            LogAssert.ignoreFailingMessages = true;
            // A digit grows into <n>: the text is longer after the scrubbing than before it.
            Add(string.Concat(Enumerable.Repeat("1 ", 10_000)), string.Concat(Enumerable.Repeat("1 ", 40_000)));
            PassWindow(_fixture);

            var entry = _fixture.Records.Entries.Single();
            Assert.LessOrEqual(entry.Message.Length, TelemetryLimits.MaxMessageLength);
            Assert.LessOrEqual(entry.Stack.Length, TelemetryLimits.MaxStackLength);
        }

        [Test]
        public void NoStack_NoFieldInTheRecord()
        {
            LogAssert.ignoreFailingMessages = true;
            Add("Without a stack", "");
            PassWindow(_fixture);

            Assert.IsNull(_fixture.Records.Entries.Single().Stack);
        }

        // Errors of the module itself.
        [Test]
        public void ErrorWhileHandling_NotInTheQueue()
        {
            LogAssert.ignoreFailingMessages = true;
            var fixture = new TelemetryErrorFixture(new LoggingErrors());
            fixture.Listen();

            Debug.LogError("first");
            PassWindow(fixture);

            // Right after the pass: the next one would take away what got in.
            Assert.AreEqual(0, fixture.Queue.Count);
            fixture.Running.Terminate();
        }

        [Test]
        public void ExceptionWhileHandling_StaysInsideThePump_NotInTheQueue()
        {
            LogAssert.ignoreFailingMessages = true;
            var fixture = new TelemetryErrorFixture(new ThrowingErrors());
            fixture.Listen();

            Debug.LogError("first");

            Assert.DoesNotThrow(() => PassWindow(fixture));
            Assert.AreEqual(0, fixture.Queue.Count);
            fixture.Running.Terminate();
        }

        [Test]
        public void ErrorOfAnotherThreadWhileHandling_Taken()
        {
            LogAssert.ignoreFailingMessages = true;
            var fixture = new TelemetryErrorFixture(new LoggingFromAThreadErrors());
            fixture.Listen();

            Debug.LogError("first");
            PassWindow(fixture);

            Assert.AreEqual(1, fixture.Queue.Count);
            fixture.Running.Terminate();
        }

        [Test]
        public void SelfLog_NotInTheQueue_ButInTheLog()
        {
            LogAssert.ignoreFailingMessages = true;
            _fixture.Listen();
            var seen = 0;
            void OnLog(string message, string stack, LogType type) => seen++;
            Application.logMessageReceived += OnLog;

            TelemetrySelfLog.Exception(new InvalidOperationException("own"));

            Application.logMessageReceived -= OnLog;
            Assert.AreEqual(1, seen);
            Assert.AreEqual(0, _fixture.Queue.Count);
        }

        [Test]
        public void CollectorThatThrows_NotInTheQueue()
        {
            LogAssert.ignoreFailingMessages = true;
            _fixture.Listen();
            var telemetry = new TelemetryFixture();
            telemetry.Collector.Script(_ => throw new InvalidOperationException("of a collector"));

            telemetry.Sampler.Sample();

            Assert.AreEqual(0, _fixture.Queue.Count);
        }

        private class LoggingErrors : ITelemetryErrors
        {
            public void Add(TelemetryErrorLevel level, string category, string message, string stack, int repeat)
            {
                Debug.LogError("inside");
            }
        }

        private class ThrowingErrors : ITelemetryErrors
        {
            public void Add(TelemetryErrorLevel level, string category, string message, string stack, int repeat)
            {
                throw new InvalidOperationException("inside");
            }
        }

        private class LoggingFromAThreadErrors : ITelemetryErrors
        {
            public void Add(TelemetryErrorLevel level, string category, string message, string stack, int repeat)
            {
                Task.Run(() => Debug.LogError("of the game, from a thread")).Wait();
            }
        }
    }
}
