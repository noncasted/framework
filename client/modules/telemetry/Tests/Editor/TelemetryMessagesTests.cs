using System;
using System.Linq;
using MemoryPack;
using NUnit.Framework;
using Shared;

namespace Global.Telemetry.Tests
{
    // The structures of the module against the messages of the contract, field by field.
    public class TelemetryMessagesTests
    {
        private static readonly Guid _id = new Guid(7, 0, 0, new byte[8]);

        private static TelemetryBatch FullBatch()
        {
            return new TelemetryBatch(
                new[] { new TelemetryBatchName("frame.dt_avg", "ms"), new TelemetryBatchName("swarm.count", null) },
                new[]
                {
                    new TelemetryBatchSample(1000, new[] { 16.7, 240d }, false),
                    new TelemetryBatchSample(2000, new[] { double.NaN, 241d }, true)
                },
                new[] { new TelemetryBatchMark(1200, "stage.boss_start"), new TelemetryBatchMark(1300, "load.end", "Group: MusicRunAudio", 250) },
                new[]
                {
                    new TelemetryBatchError(1500, TelemetryErrorLevel.Exception, "Broken", "Game.Swarm:Update ()", 3),
                    new TelemetryBatchError(1600, TelemetryErrorLevel.Error, "Lost", null, 1)
                });
        }

        private static void AssertFull(SharedClientTelemetry.Batch message)
        {
            Assert.AreEqual(_id, message.SessionId);

            Assert.AreEqual(2, message.Names.Length);
            Assert.AreEqual("frame.dt_avg", message.Names[0].Name);
            Assert.AreEqual("ms", message.Names[0].Unit);
            Assert.AreEqual("swarm.count", message.Names[1].Name);
            Assert.IsNull(message.Names[1].Unit);

            Assert.AreEqual(2, message.Samples.Length);
            Assert.AreEqual(1000, message.Samples[0].Time);
            CollectionAssert.AreEqual(new[] { 16.7, 240d }, message.Samples[0].Values);
            Assert.IsFalse(message.Samples[0].Hidden);
            Assert.AreEqual(2000, message.Samples[1].Time);
            Assert.IsTrue(double.IsNaN(message.Samples[1].Values[0]));
            Assert.AreEqual(241d, message.Samples[1].Values[1]);
            Assert.IsTrue(message.Samples[1].Hidden);

            Assert.AreEqual(2, message.Marks.Length);
            Assert.AreEqual(1200, message.Marks[0].Time);
            Assert.AreEqual("stage.boss_start", message.Marks[0].Name);
            Assert.AreEqual(1300, message.Marks[1].Time);
            Assert.AreEqual("load.end", message.Marks[1].Name);
            Assert.AreEqual("Group: MusicRunAudio", message.Marks[1].Detail);
            Assert.AreEqual(250L, message.Marks[1].DurationMs);
            Assert.IsNull(message.Marks[0].Detail);
            Assert.IsNull(message.Marks[0].DurationMs);

            Assert.AreEqual(2, message.Errors.Length);
            Assert.AreEqual(1500, message.Errors[0].Time);
            Assert.AreEqual(SharedClientTelemetry.ErrorLevel.Exception, message.Errors[0].Level);
            Assert.AreEqual("Broken", message.Errors[0].Message);
            Assert.AreEqual("Game.Swarm:Update ()", message.Errors[0].Stack);
            Assert.AreEqual(3, message.Errors[0].Repeat);
            Assert.AreEqual(1600, message.Errors[1].Time);
            Assert.AreEqual(SharedClientTelemetry.ErrorLevel.Error, message.Errors[1].Level);
            Assert.AreEqual("Lost", message.Errors[1].Message);
            Assert.IsNull(message.Errors[1].Stack);
            Assert.AreEqual(1, message.Errors[1].Repeat);
        }

        [Test]
        public void Batch_EveryField()
        {
            AssertFull(TelemetryMessages.ToBatch(_id, FullBatch()));
        }

        // What the server reads: the bytes of the message, with a NaN and a null on the way.
        [Test]
        public void Batch_EveryField_AfterMemoryPack()
        {
            var bytes = MemoryPackSerializer.Serialize(TelemetryMessages.ToBatch(_id, FullBatch()));

            AssertFull(MemoryPackSerializer.Deserialize<SharedClientTelemetry.Batch>(bytes));
        }

        [Test]
        public void Batch_NothingInIt_EmptyLists()
        {
            var empty = new TelemetryBatch(
                Array.Empty<TelemetryBatchName>(),
                Array.Empty<TelemetryBatchSample>(),
                Array.Empty<TelemetryBatchMark>(),
                Array.Empty<TelemetryBatchError>());

            var message = TelemetryMessages.ToBatch(_id, empty);

            Assert.IsEmpty(message.Names);
            Assert.IsEmpty(message.Samples);
            Assert.IsEmpty(message.Marks);
            Assert.IsEmpty(message.Errors);
        }

        // The values are a copy: a message may wait for its frame in the queue of the writer.
        [Test]
        public void Batch_ValuesAreCopied()
        {
            var values = new[] { 1d };
            var batch = new TelemetryBatch(
                Array.Empty<TelemetryBatchName>(),
                new[] { new TelemetryBatchSample(0, values, false) },
                Array.Empty<TelemetryBatchMark>(),
                Array.Empty<TelemetryBatchError>());

            var message = TelemetryMessages.ToBatch(_id, batch);
            values[0] = 2;

            Assert.AreEqual(1d, message.Samples[0].Values[0]);
        }

        [Test]
        public void Start_EveryField()
        {
            var device = new TelemetryDevice(
                platform: "WebGL",
                buildVersion: "1.2.3",
                browser: "Chrome",
                browserMajor: 120,
                os: "Windows",
                osMajor: 11,
                gpu: "ANGLE",
                cores: 8,
                memoryClass: 16,
                screenWidth: 1920,
                screenHeight: 1080,
                dpr: 1.5);

            var message = TelemetryMessages.ToSessionStart(new TelemetrySessionStart(new[] { "menu", "second" }, device, _id));

            CollectionAssert.AreEqual(new[] { "menu", "second" }, message.Tags);
            Assert.AreEqual(_id, message.StartupId);
            Assert.AreEqual("WebGL", message.Device.Platform);
            Assert.AreEqual("1.2.3", message.Device.BuildVersion);
            Assert.AreEqual("Chrome", message.Device.Browser);
            Assert.AreEqual(120, message.Device.BrowserMajor);
            Assert.AreEqual("Windows", message.Device.Os);
            Assert.AreEqual(11, message.Device.OsMajor);
            Assert.AreEqual("ANGLE", message.Device.Gpu);
            Assert.AreEqual(8, message.Device.Cores);
            Assert.AreEqual(16d, message.Device.MemoryClass);
            Assert.AreEqual(1920, message.Device.ScreenWidth);
            Assert.AreEqual(1080, message.Device.ScreenHeight);
            Assert.AreEqual(1.5, message.Device.Dpr);
        }

        // The contract reads 0 and null as unknown.
        [Test]
        public void Start_UnknownDevice_ZerosAndNulls()
        {
            var message = TelemetryMessages.ToSessionStart(new TelemetrySessionStart(new string[0], new TelemetryDevice(), Guid.Empty));

            Assert.IsEmpty(message.Tags);
            Assert.IsNull(message.Device.Platform);
            Assert.IsNull(message.Device.BuildVersion);
            Assert.IsNull(message.Device.Browser);
            Assert.AreEqual(0, message.Device.BrowserMajor);
            Assert.IsNull(message.Device.Os);
            Assert.AreEqual(0, message.Device.OsMajor);
            Assert.IsNull(message.Device.Gpu);
            Assert.AreEqual(0, message.Device.Cores);
            Assert.AreEqual(0d, message.Device.MemoryClass);
            Assert.AreEqual(0, message.Device.ScreenWidth);
            Assert.AreEqual(0, message.Device.ScreenHeight);
            Assert.AreEqual(0d, message.Device.Dpr);
        }

        [Test]
        public void Start_ThePreviousSession_AfterMemoryPack()
        {
            var previous = new TelemetryPreviousSession(_id, new[] { "menu" }, 31000, 4000, new[] { new TelemetryBatchMark(30500, "page.pagehide") });
            var message = TelemetryMessages.ToSessionStart(new TelemetrySessionStart(new string[0], new TelemetryDevice(), Guid.Empty, previous));

            var read = MemoryPackSerializer.Deserialize<SharedClientTelemetry.SessionStart>(MemoryPackSerializer.Serialize(message)).Previous;

            Assert.AreEqual(_id, read.SessionId);
            CollectionAssert.AreEqual(new[] { "menu" }, read.Tags);
            Assert.AreEqual(31000, read.LastSampleTime);
            Assert.AreEqual(4000, read.SinceMs);
            Assert.AreEqual("page.pagehide", read.Marks.Single().Name);
            Assert.AreEqual(30500, read.Marks.Single().Time);
        }

        [Test]
        public void Start_NoPreviousSession_Null()
        {
            Assert.IsNull(TelemetryMessages.ToSessionStart(new TelemetrySessionStart(new string[0], new TelemetryDevice(), Guid.Empty)).Previous);
        }

        // The numbers differ on purpose: the enum of the module starts at 0, the one of the wire at 1.
        [Test]
        public void Level_EveryValue_ByName()
        {
            var levels = Enum.GetValues(typeof(TelemetryErrorLevel)).Cast<TelemetryErrorLevel>().ToArray();

            Assert.AreEqual(Enum.GetValues(typeof(SharedClientTelemetry.ErrorLevel)).Length, levels.Length);

            foreach (var level in levels)
                Assert.AreEqual(level.ToString(), TelemetryMessages.ToLevel(level).ToString());
        }

        [Test]
        public void Level_Unknown_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TelemetryMessages.ToLevel((TelemetryErrorLevel)99));
        }
    }
}
