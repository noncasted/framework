using System;
using System.Linq;
using Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests
{
    // The errors of the logger of the framework: once each, with the category and the template, never by the log of Unity too.
    public class TelemetryLoggerErrorsTests
    {
        private const string _category = "Global.Telemetry.Tests.TelemetryLoggerErrorsTests.Owner";

        private TelemetryErrorFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _fixture = new TelemetryErrorFixture();
            _fixture.ListenWithLogger();
        }

        [TearDown]
        public void TearDown()
        {
            _fixture.Running.Terminate();
            Logs.Configure(new LogOptions());
        }

        [Test]
        public void AnError_IsItsTemplateWithTheCategory_Once()
        {
            LogAssert.ignoreFailingMessages = true;
            Logs.Of<Owner>().Error("Lost the room {Code} of {Player}", 42, "ivan.petrov@mail.ru");

            var entry = Pumped().Single();
            Assert.AreEqual(TelemetryErrorLevel.Error, entry.Level);
            Assert.AreEqual(_category, entry.Category);
            Assert.AreEqual("Lost the room {Code} of {Player}", entry.Message);
            Assert.IsNull(entry.Stack);
        }

        [Test]
        public void TheSameTemplate_WithOtherArguments_IsOneGroup()
        {
            LogAssert.ignoreFailingMessages = true;
            var log = Logs.Of<Owner>();

            log.Error("Lost the room {Code}", 1);
            log.Error("Lost the room {Code}", 2);
            log.Error("Lost the room {Code}", 3);

            var entries = Pumped();
            Assert.AreEqual(3, entries.Sum(entry => entry.Repeat));
            Assert.AreEqual(1, entries.Select(entry => entry.Message).Distinct().Count());
        }

        [Test]
        public void AWarning_IsAWarning()
        {
            LogAssert.ignoreFailingMessages = true;
            Logs.Of<Owner>().Warning("Slow {Ms}", 5);

            Assert.AreEqual(TelemetryErrorLevel.Warning, Pumped().Single().Level);
        }

        [Test]
        public void AnException_ComesWithItsTypeTextAndStack()
        {
            LogAssert.ignoreFailingMessages = true;
            var exception = Thrown();

            Logs.Of<Owner>().Error(exception, "Join {Code} failed", 7);

            var entry = Pumped().Single();
            Assert.AreEqual(TelemetryErrorLevel.Exception, entry.Level);
            Assert.AreEqual(_category, entry.Category);
            Assert.AreEqual("Join {Code} failed\nSystem.InvalidOperationException: broken", entry.Message);
            StringAssert.Contains(nameof(Thrown), entry.Stack);
        }

        [Test]
        public void AnExceptionWithoutAText_IsItsTypeAndText()
        {
            LogAssert.ignoreFailingMessages = true;
            Logs.Of<Owner>().Exception(Thrown());

            Assert.AreEqual("System.InvalidOperationException: broken", Pumped().Single().Message);
        }

        [Test]
        public void Info_IsNotAnError()
        {
            LogAssert.ignoreFailingMessages = true;
            Logs.Of<Owner>().Info("Joined {Code}", 7);

            Assert.IsEmpty(Pumped());
        }

        [Test]
        public void AnErrorTheConsoleDoesNotShow_IsNotSent()
        {
            LogAssert.ignoreFailingMessages = true;
            Logs.Configure(new LogOptions(LogLevel.None, LogLevel.None, LogLevel.Error));

            Logs.Of<Owner>().Error("Lost {Code}", 1);

            Assert.IsEmpty(Pumped());
        }

        [Test]
        public void AnErrorOfTheLogOfUnity_HasNoCategory()
        {
            LogAssert.ignoreFailingMessages = true;
            Debug.LogError("plain");

            var entry = Pumped().Single();
            Assert.IsNull(entry.Category);
            Assert.AreEqual("plain", entry.Message);
        }

        [Test]
        public void AfterTheSession_NothingIsTaken()
        {
            LogAssert.ignoreFailingMessages = true;
            _fixture.Running.Terminate();

            Logs.Of<Owner>().Error("Lost {Code}", 1);

            Assert.AreEqual(0, _fixture.Queue.Count);
        }

        private ErrorEntry[] Pumped()
        {
            _fixture.PumpAt(0);
            _fixture.PumpAt(60_000);
            return _fixture.Records.Entries.ToArray();
        }

        private static InvalidOperationException Thrown()
        {
            try
            {
                throw new InvalidOperationException("broken");
            }
            catch (InvalidOperationException exception)
            {
                return exception;
            }
        }

        private class Owner
        {
        }
    }
}
