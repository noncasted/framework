using System;
using Internal;
using NUnit.Framework;
using Shared;
using UnityEngine;
using UnityEngine.TestTools;

namespace Global.MatchMetrics.Tests
{
    // The streams of the module over a transport that answers when the test says: one request in flight, the repeat of a
    // batch after a reconnect with its Seq and no duplicate, the end of the sending on Closed.
    public class MatchMetricsSendingTests
    {
        private static readonly Guid _matchId = new(3, 0, 0, new byte[8]);

        private ManualMatchClock _clock;
        private FakeMatchTransport _transport;
        private MatchMetrics _metrics;
        private Lifetime _lifetime;
        private IMatchCounter _counter;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualMatchClock();
            _transport = new FakeMatchTransport();
            _metrics = new MatchMetrics(new MatchMetricsOptions(sendIntervalSeconds: 30f), _transport, _clock, Logs.Of<MatchMetrics>());
            _lifetime = new Lifetime();
            _counter = _metrics.Open(_lifetime, _matchId, _clock).Counter("kills");
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Terminate();
        }

        private void Play(float seconds)
        {
            for (var i = 0; i < seconds; i++)
            {
                _counter.Add(1f);
                _clock.Run(1f);
                _metrics.Tick();
            }
        }

        [Test]
        public void FirstBatch_OpensThenSends()
        {
            Play(30);

            Assert.AreEqual(1, _transport.Opens.Count);
            Assert.AreEqual(_matchId, _transport.Opens[0].MatchId);
            Assert.AreEqual(SharedMatchMetrics.MatchStreamKind.Client, _transport.Opens[0].Kind);

            _transport.AnswerOpen();

            Assert.AreEqual(1, _transport.Batches.Count);
            Assert.AreEqual(1, _transport.Batches[0].Seq);
            Assert.AreEqual(FakeMatchTransport.StreamId, _transport.Batches[0].StreamId);
            Assert.AreEqual("kills", _transport.Batches[0].Series[0].Name);
            Assert.AreEqual(30, _transport.Batches[0].Chunks[0].Values.Length);
        }

        [Test]
        public void OneBatchInFlight()
        {
            Play(30);
            _transport.AnswerOpen();
            Play(60);

            Assert.AreEqual(1, _transport.InFlight);
            Assert.AreEqual(1, _transport.Batches.Count);

            _transport.AnswerBatch();

            Assert.AreEqual(2, _transport.Batches.Count);
            Assert.AreEqual(2, _transport.Batches[1].Seq);
            Assert.AreEqual(0, _transport.Batches[1].Series.Length);

            _transport.AnswerBatch();

            Assert.AreEqual(3, _transport.Batches.Count);
            Assert.AreEqual(3, _transport.Batches[2].Seq);
        }

        [Test]
        public void Reconnect_RepeatsUnansweredWithSameSeq()
        {
            Play(30);
            _transport.AnswerOpen();
            _transport.Reconnect();
            _metrics.Tick();

            Assert.AreEqual(2, _transport.Opens.Count);
            Assert.AreEqual(_transport.Opens[0].WriterId, _transport.Opens[1].WriterId);

            _transport.AnswerOpen(lastSeq: 0);

            Assert.AreEqual(2, _transport.Batches.Count);
            Assert.AreEqual(1, _transport.Batches[1].Seq);
            Assert.AreEqual(1, _transport.Batches[1].Series.Length);
        }

        // The server got the batch before the loss: it is not sent again, the next one goes on from its Seq.
        [Test]
        public void Reconnect_ServerHasBatch_NoDuplicate()
        {
            Play(30);
            _transport.AnswerOpen();
            _transport.Reconnect();
            _metrics.Tick();
            _transport.AnswerOpen(lastSeq: 1);

            Assert.AreEqual(1, _transport.Batches.Count);

            Play(30);

            Assert.AreEqual(2, _transport.Batches.Count);
            Assert.AreEqual(2, _transport.Batches[1].Seq);
        }

        [Test]
        public void Closed_StopsSending()
        {
            Play(30);
            _transport.AnswerOpen();
            _transport.AnswerBatch(SharedMatchMetrics.MatchMetricsStatus.Closed);
            Play(90);

            Assert.AreEqual(1, _transport.Batches.Count);
            Assert.AreEqual(0, _transport.InFlight);
        }

        [Test]
        public void NotInMatch_StopsSending()
        {
            Play(30);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("NotInMatch"));
            _transport.AnswerOpen(status: SharedMatchMetrics.MatchMetricsStatus.NotInMatch);
            Play(60);

            Assert.AreEqual(0, _transport.Batches.Count);
            Assert.AreEqual(1, _transport.Opens.Count);
        }

        // The end of the stream takes the last batch; the entry goes once it is confirmed.
        [Test]
        public void End_SendsLastBatch_ThenEntryGoes()
        {
            Play(5);
            _lifetime.Terminate();

            Assert.AreEqual(1, _transport.Opens.Count);

            _transport.AnswerOpen();
            _transport.AnswerBatch();
            _metrics.Tick();

            Assert.AreEqual(5, _transport.Batches[0].Chunks[0].Values.Length);
            Assert.AreEqual(0, _metrics.Count);
        }

        // A request without an answer on a live link (a timeout, an error of the server): the same link is tried again after
        // the interval of the sending, at once on a new link.
        [Test]
        public void FailedLink_RetriedAfterTheInterval_AtOnceOnNewLink()
        {
            Play(30);
            _transport.AnswerOpen();
            _transport.FailBatch();
            _clock.Wait(1f);
            _metrics.Tick();

            Assert.AreEqual(1, _transport.Batches.Count);

            _transport.Reconnect();
            _metrics.Tick();

            Assert.AreEqual(2, _transport.Opens.Count);
        }

        // The end of the stream cuts its last batch once: a failure after it is tried again on the same link, nothing else
        // would send it before a reconnect.
        [Test]
        public void FailedLink_StreamEnded_SameBatchAgainAfterTheInterval()
        {
            Play(30);
            _transport.AnswerOpen();
            Play(5);
            _lifetime.Terminate();
            _transport.FailBatch();
            _clock.Wait(29f);
            _metrics.Tick();

            Assert.AreEqual(1, _transport.Batches.Count);

            _clock.Wait(1f);
            _metrics.Tick();

            Assert.AreEqual(2, _transport.Batches.Count);
            Assert.AreEqual(1, _transport.Batches[1].Seq);

            _transport.AnswerBatch();

            Assert.AreEqual(3, _transport.Batches.Count);
            Assert.AreEqual(2, _transport.Batches[2].Seq);

            _transport.AnswerBatch();
            _metrics.Tick();

            Assert.AreEqual(0, _metrics.Count);
        }
    }
}
