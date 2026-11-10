using System;
using Internal;
using NUnit.Framework;
using Shared;

namespace Global.MatchMetrics.Tests
{
    public class MatchMetricsWritingTests
    {
        private ManualMatchClock _clock;
        private MatchMetricsStream _stream;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualMatchClock();
            _stream = new MatchMetricsStream(_clock, _clock);
        }

        [Test]
        public void Counter_SumsEachSecond()
        {
            var counter = _stream.Counter("damage.dealt", MatchTags.Of("source", "orb"));

            counter.Add(2f);
            _clock.Run(0.5f);
            counter.Add(3f);
            _clock.Run(0.6f);
            counter.Add(4f);
            _clock.Run(2f);

            var batch = _stream.Cut(false);

            Assert.AreEqual(1, batch.Series.Count);
            Assert.AreEqual("source", batch.Series[0].Tags.GetKey(0));
            Assert.AreEqual(1, batch.Chunks.Count);
            Assert.AreEqual(0, batch.Chunks[0].Start);
            CollectionAssert.AreEqual(new[] { 5f, 4f }, batch.Chunks[0].Values);
        }

        [Test]
        public void Gauge_LastValueOfSecond_NaNUnchanged()
        {
            var gauge = _stream.Gauge("hearts");

            gauge.Set(3f);
            gauge.Set(2f);
            _clock.Run(2f);
            gauge.Set(1f);
            _clock.Run(1f);

            var chunk = _stream.Cut(false).Chunks[0];

            Assert.AreEqual(3, chunk.Values.Length);
            Assert.AreEqual(2f, chunk.Values[0]);
            Assert.IsTrue(float.IsNaN(chunk.Values[1]));
            Assert.AreEqual(1f, chunk.Values[2]);
        }

        [Test]
        public void LastCut_TakesCurrentBucket_ThenEnds()
        {
            var counter = _stream.Counter("kills");

            _clock.Run(0.3f);
            counter.Add(1f);

            var batch = _stream.Cut(true);

            CollectionAssert.AreEqual(new[] { 1f }, batch.Chunks[0].Values);
            Assert.IsTrue(_stream.IsEnded);
            Assert.Throws<InvalidOperationException>(() => counter.Add(1f));
        }

        [Test]
        public void Pause_BreaksChunk()
        {
            var counter = _stream.Counter("kills");

            counter.Add(1f);
            _clock.Run(1f);
            counter.Add(1f);
            _clock.Run(0.5f);
            _clock.Wait(5f);
            _stream.Sync();
            _clock.Run(1f);
            counter.Add(2f);
            _clock.Run(1f);

            var chunks = _stream.Cut(false).Chunks;

            Assert.AreEqual(2, chunks.Count);
            Assert.AreEqual(0, chunks[0].Start);
            CollectionAssert.AreEqual(new[] { 1f }, chunks[0].Values);
            Assert.AreEqual(1, chunks[1].Start);
            CollectionAssert.AreEqual(new[] { 1f, 2f }, chunks[1].Values);
            Assert.Greater(chunks[1].StartMs - chunks[0].StartMs, 5000d);
        }

        [Test]
        public void Cut_GoesOnOnSameRealTime()
        {
            var counter = _stream.Counter("kills");

            counter.Add(1f);
            _clock.Run(1f);
            var first = _stream.Cut(false).Chunks[0];
            counter.Add(1f);
            _clock.Run(1f);
            var second = _stream.Cut(false).Chunks[0];

            Assert.AreEqual(1, second.Start);
            Assert.AreEqual(1000d, second.StartMs - first.StartMs, 0.01d);
        }

        [Test]
        public void Events_KeepTimeAndFields()
        {
            _clock.Run(12.5f);
            _stream.Event("upgrade.pick", MatchEventField.Of("card", 14), MatchEventField.Of("kind", "tree"));

            var record = _stream.Cut(false).Events[0];

            Assert.AreEqual(12.5f, record.Time);
            Assert.AreEqual(14d, record.Fields[0].Number);
            Assert.AreEqual("tree", record.Fields[1].Text);
        }

        [Test]
        public void SameSeries_SameHandle_OrderOfTagsIgnored()
        {
            var a = _stream.Counter("damage.dealt", MatchTags.Of("source", "orb").With("card", "3"));
            var b = _stream.Counter("damage.dealt", MatchTags.Of("card", "3").With("source", "orb"));

            Assert.AreSame(a, b);
            Assert.Throws<ArgumentException>(() => _stream.Gauge("damage.dealt", MatchTags.Of("card", "3").With("source", "orb")));
        }

        [Test]
        public void Names_Limited()
        {
            Assert.Throws<ArgumentException>(() => _stream.Counter("Damage"));
            Assert.Throws<ArgumentException>(() => _stream.Counter(new string('a', MatchMetricsLimits.MaxNameLength + 1)));
            Assert.Throws<ArgumentException>(() => MatchTags.Of("source", "Orb"));
            Assert.Throws<ArgumentException>(() => MatchTags.Of("so-urce", "orb"));
            Assert.Throws<ArgumentException>(() => MatchTags.Of("a", "1").With("a", "2"));
            Assert.Throws<ArgumentException>(() => MatchTags.Of("a", "1").With("b", "1").With("c", "1").With("d", "1").With("e", "1"));
            Assert.Throws<ArgumentException>(() => MatchEventField.Of("x", double.NaN));
            Assert.Throws<ArgumentException>(() => _stream.Event("e", new MatchEventField[MatchMetricsLimits.MaxFields + 1]));
            Assert.DoesNotThrow(() => MatchTags.Of("enemy", "elite-3"));
        }

        [Test]
        public void Series_Limited()
        {
            for (var i = 0; i < MatchMetricsLimits.MaxSeries; i++)
                _stream.Counter("s" + i);

            Assert.Throws<InvalidOperationException>(() => _stream.Counter("over"));
        }

        [Test]
        public void Write_AllocatesNothing()
        {
            var counter = _stream.Counter("kills");
            var gauge = _stream.Gauge("hearts");
            var value = 0f;

            FrameAllocations.AssertNone("A write of a counter and a gauge", 1000, () =>
            {
                counter.Add(1f);
                gauge.Set(value++);
                _clock.Run(0.01f);
            }, 300);
        }
    }
}
