using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.EditorTests
{
    // The editor is a development build: the development levels of the options hold here.
    public class LogsTests
    {
        private const string _category = "Internal.EditorTests.LogsTests.Owner";

        private Lifetime _lifetime;
        private RecordingSink _sink;

        [SetUp]
        public void SetUp()
        {
            _lifetime = new Lifetime();
            _sink = new RecordingSink();
            Logs.AddSink(_lifetime, _sink);
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Terminate();
            Logs.Configure(new LogOptions());
        }

        [Test]
        public void Of_TheCategoryIsTheFullNameOfTheType()
        {
            Assert.AreEqual(_category, Logs.Of<Owner>().Category);
            Assert.AreSame(Logs.Of<Owner>(), Logs.Of<Owner>());
        }

        [Test]
        public void Of_AType_SharesTheLevelsOfTheGenericLog()
        {
            Configure(new LogRule(_category, LogLevel.Error, LogLevel.Error));

            Assert.IsFalse(Logs.Of(typeof(Owner)).IsEnabled(LogLevel.Warning));
            Assert.IsTrue(Logs.Of(typeof(Owner)).IsEnabled(LogLevel.Error));
        }

        [Test]
        public void Info_GoesToTheConsoleAndTheSinks()
        {
            Logs.Of<Owner>().Info("Joined {Code} in {Ms} ms", 7, 12);

            LogAssert.Expect(LogType.Log, "[Owner] Joined 7 in 12 ms");
            var record = _sink.Single();
            Assert.AreEqual(_category, record.Category);
            Assert.AreEqual(LogLevel.Info, record.Level);
            Assert.AreEqual("Joined {Code} in {Ms} ms", record.Template);
            Assert.AreEqual("Joined 7 in 12 ms", record.Message);
            Assert.IsTrue(record.IsShown);
            Assert.IsFalse(record.IsMark);
        }

        [Test]
        public void Warning_And_Error_GoAsTheirTypes()
        {
            var log = Logs.Of<Owner>();

            log.Warning("Slow {Ms}", 5);
            log.Error("Lost {Code}", 3);

            LogAssert.Expect(LogType.Warning, "[Owner] Slow 5");
            LogAssert.Expect(LogType.Error, "[Owner] Lost 3");
        }

        [Test]
        public void Error_WithAnException_LogsTheExceptionAfterTheLine()
        {
            var exception = new InvalidOperationException("broken");

            Logs.Of<Owner>().Error(exception, "Join {Code} failed", 7);

            LogAssert.Expect(LogType.Error, "[Owner] Join 7 failed");
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: broken");
            Assert.AreSame(exception, _sink.Single().Exception);
        }

        [Test]
        public void Exception_WithoutAText_IsTheExceptionAlone()
        {
            Logs.Of<Owner>().Exception(new InvalidOperationException("broken"));

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: broken");
            var record = _sink.Single();
            Assert.IsNull(record.Template);
            Assert.IsNull(record.Message);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void BelowTheLevel_NothingIsWrittenNorAllocated()
        {
            Configure(new LogRule(_category, LogLevel.Error, LogLevel.Error));
            var log = Logs.Of<Owner>();

            FrameAllocations.AssertNone("a log below its level", 1000, () =>
            {
                log.Info("Joined {Code} as {Role} in {Ms} ms at {Time}", 7, "host", 12.5f, 3L);
                log.Warning("Slow {Ms}", 5d);
                log.Debug("Tick {Frame}", 1);
            });

            Assert.AreEqual(0, _sink.Records.Count);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AtTheMarkLevel_BelowTheShownOne_GoesToTheSinksOnly()
        {
            Configure(new LogRule(_category, LogLevel.Error, LogLevel.Error, LogLevel.Info));

            Logs.Of<Owner>().Info("Room {Code}", 4);

            var record = _sink.Single();
            Assert.IsFalse(record.IsShown);
            Assert.IsTrue(record.IsMark);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Configure_ChangesTheLevelsOfTheCategoriesMadeBefore()
        {
            var log = Logs.Of<Owner>();

            Configure(new LogRule("Internal.EditorTests", LogLevel.Warning, LogLevel.Warning));

            Assert.IsFalse(log.IsEnabled(LogLevel.Info));
            Assert.IsTrue(log.IsEnabled(LogLevel.Warning));
        }

        [Test]
        public void IsWriting_OnlyInsideTheLineOfALog()
        {
            var seen = new List<bool>();
            void OnLog(string message, string stack, LogType type) => seen.Add(Logs.IsWriting);

            Application.logMessageReceived += OnLog;

            try
            {
                Logs.Of<Owner>().Info("Line");
                Debug.Log("Plain");
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }

            LogAssert.Expect(LogType.Log, "[Owner] Line");
            LogAssert.Expect(LogType.Log, "Plain");
            CollectionAssert.AreEqual(new[] { true, false }, seen);
            Assert.IsFalse(Logs.IsWriting);
        }

        [Test]
        public void TheEndOfTheLifetime_RemovesTheSink()
        {
            _lifetime.Terminate();

            Logs.Of<Owner>().Info("Line");

            LogAssert.Expect(LogType.Log, "[Owner] Line");
            Assert.AreEqual(0, _sink.Records.Count);
        }

        [Test]
        public void ASinkThatThrows_DoesNotStopTheOthers()
        {
            var throwing = new Lifetime();
            Logs.AddSink(throwing, new ThrowingSink());
            var after = new RecordingSink();
            Logs.AddSink(throwing, after);

            try
            {
                Logs.Of<Owner>().Info("Line");
            }
            finally
            {
                throwing.Terminate();
            }

            LogAssert.Expect(LogType.Log, "[Owner] Line");
            LogAssert.Expect(LogType.Exception, new Regex("sink broke"));
            Assert.AreEqual(1, _sink.Records.Count);
            Assert.AreEqual(1, after.Records.Count);
        }

        private static void Configure(params LogRule[] rules)
        {
            Logs.Configure(new LogOptions(LogLevel.Info, LogLevel.Warning, LogLevel.None, rules));
        }

        private class Owner
        {
        }

        private class RecordingSink : ILogSink
        {
            public List<LogRecord> Records { get; } = new();

            public void Write(in LogRecord record)
            {
                Records.Add(record);
            }

            public LogRecord Single()
            {
                Assert.AreEqual(1, Records.Count);
                return Records[0];
            }
        }

        private class ThrowingSink : ILogSink
        {
            public void Write(in LogRecord record)
            {
                throw new InvalidOperationException("sink broke");
            }
        }
    }
}
