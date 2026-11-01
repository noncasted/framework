using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Shared;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // NetworkLatencyMonitor through its public API, in real time: the probes every 0.5 s, their 5 s timeout, the rates
    // of the traffic and the text of the report on the server, on a P2P host and on a P2P guest. In play mode: the
    // monitor creates its overlay with DontDestroyOnLoad.
    public class NetworkLatencyMonitorTests
    {
        private const string Waiting = "Network: waiting for the session";
        private const string OverlayName = nameof(NetworkLatencyOverlay);

        // The format of a filled LatencySeries; a negative value is the own leg of a loop shorter than the given leg.
        private const string Series = @"-?\d+ \(avg -?\d+, max \d+\)";

        private const string Footer = @"Frame: \S+ avg, \d+ max/s \(\S+ fps\)\n"
                                      + @"Out: \d+ msg/s, [\d.]+ KB/s, peak \d+ msg/frame\n"
                                      + @"In: \d+ msg/s, [\d.]+ KB/s";

        private const string NoAnswers = @"No other users answered yet\n";

        private readonly List<Harness> _harnesses = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var harness in _harnesses)
                harness.Lifetime.Terminate();

            _harnesses.Clear();
        }

        [UnityTest]
        public IEnumerator BeforeLocalUser_NothingIsSentAndReportWaits() => UniTask.ToCoroutine(async () =>
        {
            var harness = Create();
            harness.Monitor.OnSetup(harness.Lifetime);

            await Seconds(0.7f);

            Assert.AreEqual(Waiting, harness.Monitor.Report);
            Assert.AreEqual(0f, harness.Monitor.PingMs);
            Assert.IsEmpty(harness.Writer.Sent);
            Assert.IsNull(GameObject.Find(OverlayName));
        });

        [UnityTest]
        public IEnumerator LocalUserAppears_ProbesStartAndOverlayIsCreated() => UniTask.ToCoroutine(async () =>
        {
            var harness = Create();
            harness.Monitor.OnSetup(harness.Lifetime);
            await UniTask.Yield();

            harness.AddLocal(1);
            await harness.NextRound();

            Assert.IsNotNull(GameObject.Find(OverlayName));
            Assert.AreNotEqual(Waiting, harness.Monitor.Report);
        });

        [UnityTest]
        public IEnumerator EveryHalfSecond_PingAndProbeGoOutWithOneRisingId() => UniTask.ToCoroutine(async () =>
        {
            var harness = await Started();
            var start = Time.realtimeSinceStartupAsDouble;

            await harness.NextRound();
            await harness.NextRound();

            var elapsed = Time.realtimeSinceStartupAsDouble - start;
            var sent = harness.Writer.Sent;

            Assert.AreEqual(6, sent.Count);

            for (var round = 0; round < 3; round++)
            {
                Assert.AreEqual(round + 1, ((SharedSessionLatency.Ping)sent[round * 2]).Id);
                Assert.AreEqual(round + 1, ((SharedSessionLatency.PeerProbe)sent[round * 2 + 1]).Id);
            }

            Assert.GreaterOrEqual(elapsed, 0.9d);
            Assert.Less(elapsed, 2d);
        });

        [UnityTest]
        public IEnumerator LifetimeEnds_ProbesStopAndOverlayIsDestroyed() => UniTask.ToCoroutine(async () =>
        {
            var harness = await Started();
            harness.Lifetime.Terminate();

            await UniTask.Yield();
            await UniTask.Yield();
            var sent = harness.Writer.Sent.Count;

            await Seconds(0.7f);

            Assert.AreEqual(sent, harness.Writer.Sent.Count);
            Assert.IsNull(GameObject.Find(OverlayName));
        });

        [UnityTest]
        public IEnumerator Pong_SetsPingAndGoesToReport() => UniTask.ToCoroutine(async () =>
        {
            var harness = await Started();
            Assert.AreEqual(0f, harness.Monitor.PingMs);
            Assert.That(harness.Monitor.Report, Does.Contain("Ping to server: -, lost 0\n"));

            await UniTask.Yield();
            harness.Monitor.OnPong(harness.LastId);
            Assert.Greater(harness.Monitor.PingMs, 0f);

            await harness.NextRound();

            AssertReport(harness, $@"Network \(you: user 1\), ms\nPing to server: {Series}, lost 0\n{NoAnswers}{Footer}");
        });

        [UnityTest]
        public IEnumerator Pong_OfUnknownIdOrAnsweredTwice_IsIgnored() => UniTask.ToCoroutine(async () =>
        {
            var harness = await Started();

            harness.Monitor.OnPong(harness.LastId + 100);
            Assert.AreEqual(0f, harness.Monitor.PingMs);

            await UniTask.Yield();
            harness.Monitor.OnPong(harness.LastId);
            var ping = harness.Monitor.PingMs;

            await Seconds(0.1f);
            harness.Monitor.OnPong(harness.LastId);

            Assert.AreEqual(ping, harness.Monitor.PingMs);
        });

        [UnityTest]
        public IEnumerator ServerReport_NoAnswers() => UniTask.ToCoroutine(async () =>
        {
            var harness = await Started();

            AssertReport(harness, $@"Network \(you: user 1\), ms\nPing to server: -, lost 0\n{NoAnswers}{Footer}");
        });

        [UnityTest]
        public IEnumerator ServerReport_RowsOfEveryUserThatAnswered() => UniTask.ToCoroutine(async () =>
        {
            var harness = await Started();
            harness.AddRemote(2);
            harness.AddRemote(3);

            // A probe stays pending: every other client answers it, and may answer again.
            await UniTask.Yield();
            harness.Echo(2, 7f, 33f);
            harness.Echo(2, 9f, 44f);
            harness.Echo(3, 5f, 11f);

            await harness.NextRound();

            AssertReport(harness,
                @"Network \(you: user 1\), ms\nPing to server: -, lost 0\n"
                + $@"User 2: loop {Series}\n  you <-> server {Series}\n"
                + @"  server <-> user 2 9 \(avg 8, max 9\), their ping 44\n"
                + $@"User 3: loop {Series}\n  you <-> server {Series}\n"
                + @"  server <-> user 3 5 \(avg 5, max 5\), their ping 11\n"
                + Footer);
        });

        [UnityTest]
        public IEnumerator Echo_OfUnknownProbeOrOfUserNotInSession_IsIgnored() => UniTask.ToCoroutine(async () =>
        {
            var harness = await Started();
            harness.AddRemote(2);

            harness.Monitor.OnPeerEcho(new SharedSessionLatency.PeerEcho
            {
                Id = harness.LastId + 100,
                ResponderIndex = 2,
                ResponderLegMs = 1f
            });

            harness.Echo(3, 1f, 1f);

            await harness.NextRound();

            AssertReport(harness, $@"Network \(you: user 1\), ms\nPing to server: -, lost 0\n{NoAnswers}{Footer}");
        });

        [UnityTest]
        public IEnumerator UserLeaves_ItsRowGoesWithTheNextRound() => UniTask.ToCoroutine(async () =>
        {
            var harness = await Started();
            harness.AddRemote(2);

            harness.Echo(2, 7f, 33f);
            await harness.NextRound();
            Assert.That(harness.Monitor.Report, Does.Contain("User 2:"));

            harness.Users.Remove(2);
            await harness.NextRound();

            AssertReport(harness, $@"Network \(you: user 1\), ms\nPing to server: -, lost 0\n{NoAnswers}{Footer}");
        });

        [UnityTest]
        public IEnumerator HostReport_RoutesOfGuestsAndNoPing() => UniTask.ToCoroutine(async () =>
        {
            var harness = await Started(match =>
            {
                match.SetHost(1);
                match.SetRoute(2, PeerRoute.Direct);
                match.SetRoute(3, PeerRoute.Tunnel);
            });

            AssertReport(harness, $@"Network \(you: user 1, P2P host\), ms\n{NoAnswers}{Footer}");

            harness.AddRemote(2);
            harness.AddRemote(3);
            harness.Echo(2, 7f, 33f);
            harness.Echo(3, 5f, 11f);
            harness.Monitor.OnPong(harness.LastId);

            await harness.NextRound();

            AssertReport(harness,
                @"Network \(you: user 1, P2P host\), ms\n"
                + @"User 2: you <-> user 2 7 \(avg 7, max 7\), direct\n"
                + @"User 3: you <-> user 3 5 \(avg 5, max 5\), tunnel via server\n"
                + Footer);
        });

        [UnityTest]
        public IEnumerator GuestReport_HostRowAndLoopsOfOtherGuests() => UniTask.ToCoroutine(async () =>
        {
            var harness = await Started(match =>
            {
                match.SetHost(1);
                match.SetRoute(2, PeerRoute.Tunnel);
            }, 2);

            AssertReport(harness,
                @"Network \(you: user 2, P2P guest of user 1, tunnel via server\), ms\n"
                + @"Ping to host: -, lost 0\n"
                + NoAnswers
                + Footer);

            harness.AddRemote(1);
            harness.AddRemote(3);
            harness.Echo(1, 0f, 0f);
            harness.Echo(3, 5f, 11f);
            await UniTask.Yield();
            harness.Monitor.OnPong(harness.LastId);

            await harness.NextRound();

            AssertReport(harness,
                @"Network \(you: user 2, P2P guest of user 1, tunnel via server\), ms\n"
                + $@"Ping to host: {Series}, lost 0\n"
                + $@"User 1 \(host\): you <-> user 1 {Series}\n"
                + $@"User 3: loop {Series}\n"
                + $@"  you <-> host {Series}\n"
                + @"  host <-> user 3 5 \(avg 5, max 5\)\n"
                + Footer);
        });

        [UnityTest]
        public IEnumerator Rates_OfTheTrafficOfTheLastSecond() => UniTask.ToCoroutine(async () =>
        {
            var harness = await Started();

            for (var i = 0; i < 100; i++)
                harness.Connection.Traffic.OnSent(1024);

            for (var i = 0; i < 50; i++)
                harness.Connection.Traffic.OnReceived(2048);

            harness.Connection.Traffic.OnBatch(3);

            await UniTask.WaitUntil(() => harness.Monitor.Report.Contains("peak 3 msg/frame"))
                         .Timeout(TimeSpan.FromSeconds(3));

            var match = Regex.Match(harness.Monitor.Report,
                @"Out: (\d+) msg/s, ([\d.]+) KB/s, peak 3 msg/frame\nIn: (\d+) msg/s, ([\d.]+) KB/s");

            Assert.IsTrue(match.Success, harness.Monitor.Report);

            // Over an interval of a little more than a second.
            AssertBetween(match.Groups[1].Value, 50f, 100f);
            AssertBetween(match.Groups[2].Value, 50f, 100f);
            AssertBetween(match.Groups[3].Value, 25f, 50f);
            AssertBetween(match.Groups[4].Value, 50f, 100f);

            // The rates are of one second, the peak is taken: the next interval starts from zero.
            await UniTask.WaitUntil(() => harness.Monitor.Report.Contains("Out: 0 msg/s, 0.0 KB/s, peak 0 msg/frame"))
                         .Timeout(TimeSpan.FromSeconds(3));

            await UniTask.WaitUntil(() => Regex.IsMatch(harness.Monitor.Report, @"Frame: [\d.]+ avg, [1-9]\d* max/s"))
                         .Timeout(TimeSpan.FromSeconds(3));
        });

        // One real-time wait for the 5 s timeout of a probe, shared by the three kinds of the report.
        [UnityTest]
        public IEnumerator AfterTimeout_PingsAreLostProbesExpireAndSilentRowsAreMarked() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.Expect(LogType.Log, new Regex(
                @"^\[Network\] \[Latency\] Network \(you: user 1\), ms \| Ping to server: -, lost \d+ \| "));

            var server = await Started();
            server.AddRemote(2);

            var host = await Started(match =>
            {
                match.SetHost(1);
                match.SetRoute(2, PeerRoute.Direct);
            });

            host.AddRemote(2);

            var guest = await Started(match =>
            {
                match.SetHost(1);
                match.SetRoute(2, PeerRoute.Direct);
            }, 2);

            guest.AddRemote(1);
            guest.AddRemote(3);

            await UniTask.Yield();

            var firstServerId = server.LastId;

            server.Echo(2, 7f, 33f);
            host.Echo(2, 7f, 33f);
            guest.Echo(1, 0f, 0f);
            guest.Echo(3, 5f, 11f);

            await UniTask.WaitUntil(() => Regex.IsMatch(server.Monitor.Report, @"lost [1-9]")
                                          && server.Monitor.Report.Contains("NO ECHO")
                                          && host.Monitor.Report.Contains("NO ECHO")
                                          && guest.Monitor.Report.Contains("NO ECHO"))
                         .Timeout(TimeSpan.FromSeconds(8));

            const string stale = @" NO ECHO \d+s\n";

            AssertReport(server,
                @"Network \(you: user 1\), ms\nPing to server: -, lost \d+\n"
                + @"Server does not answer: is the backend deployed with the latency commands\?\n"
                + $@"User 2: loop {Series}{stale}"
                + $@"  you <-> server {Series}\n"
                + @"  server <-> user 2 7 \(avg 7, max 7\), their ping 33\n"
                + Footer);

            AssertReport(host,
                @"Network \(you: user 1, P2P host\), ms\n"
                + $@"User 2: you <-> user 2 7 \(avg 7, max 7\), direct{stale}"
                + Footer);

            AssertReport(guest,
                @"Network \(you: user 2, P2P guest of user 1, direct\), ms\n"
                + @"Ping to host: -, lost \d+\n"
                + $@"User 1 \(host\): you <-> user 1 {Series}{stale}"
                + $@"User 3: loop {Series}{stale}"
                + $@"  you <-> host {Series}\n"
                + @"  host <-> user 3 5 \(avg 5, max 5\)\n"
                + Footer);

            // A late answer is already counted as lost: neither the pong nor the echo comes back.
            server.AddRemote(3);
            server.Monitor.OnPong(firstServerId);
            server.Monitor.OnPeerEcho(new SharedSessionLatency.PeerEcho { Id = firstServerId, ResponderIndex = 3 });
            Assert.AreEqual(0f, server.Monitor.PingMs);

            // The notice of a silent server goes with the first pong.
            server.Monitor.OnPong(server.LastId);
            await server.NextRound();

            Assert.That(server.Monitor.Report, Does.Not.Contain("Server does not answer"));
            Assert.That(server.Monitor.Report, Does.Not.Contain("User 3:"));
        });

        // Fixes a known defect, not a requirement: a guest whose route is not set yet fails the report (the route is
        // read by index), nothing catches it in the loop of the monitor, so the probes stop for the rest of the session.
        [UnityTest]
        public IEnumerator KnownDefect_FailedReportStopsTheProbes() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.Expect(LogType.Exception, new Regex("KeyNotFoundException"));

            var harness = Create();
            harness.Match.SetHost(1);
            harness.AddLocal(2);
            harness.Monitor.OnSetup(harness.Lifetime);

            await Seconds(1.2f);

            Assert.AreEqual(2, harness.Writer.Sent.Count);
            Assert.AreEqual(Waiting, harness.Monitor.Report);
        });

        private static void AssertReport(Harness harness, string pattern)
        {
            var report = harness.Monitor.Report;
            Assert.IsTrue(Regex.IsMatch(report, $@"\A{pattern}\z"), $"Report:\n{report}\nPattern:\n{pattern}");
        }

        private static void AssertBetween(string text, float min, float max)
        {
            var value = float.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
            Assert.GreaterOrEqual(value, min);
            Assert.LessOrEqual(value, max);
        }

        private static UniTask Seconds(float seconds)
        {
            return UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.Realtime);
        }

        private Harness Create()
        {
            var harness = new Harness();
            _harnesses.Add(harness);
            return harness;
        }

        // Monitor of the local user, past its first round of probes.
        private async UniTask<Harness> Started(Action<PeerMatch> setup = null, int localIndex = 1)
        {
            var harness = Create();
            setup?.Invoke(harness.Match);
            harness.AddLocal(localIndex);
            harness.Monitor.OnSetup(harness.Lifetime);
            await harness.NextRound();
            return harness;
        }

        private class Harness
        {
            public Harness()
            {
                Connection = new FakeConnection(Writer);
                Monitor = new NetworkLatencyMonitor(Connection, new FakeSession(Users), Match);
            }

            public readonly Lifetime Lifetime = new();
            public readonly FakeUsers Users = new();
            public readonly FakeWriter Writer = new();
            public readonly PeerMatch Match = new();
            public readonly FakeConnection Connection;
            public readonly NetworkLatencyMonitor Monitor;

            public int LastId => Writer.Sent.OfType<SharedSessionLatency.Ping>().Last().Id;

            public void AddLocal(int index) => Users.Add(new FakeUser(index, true));
            public void AddRemote(int index) => Users.Add(new FakeUser(index, false));

            public void Echo(int responder, float legMs, float pingMs)
            {
                Monitor.OnPeerEcho(new SharedSessionLatency.PeerEcho
                {
                    Id = LastId,
                    ResponderIndex = responder,
                    ResponderLegMs = legMs,
                    ResponderPingMs = pingMs
                });
            }

            // The report is built in the frame the probes go out.
            public async UniTask NextRound()
            {
                var sent = Writer.Sent.Count;
                await UniTask.WaitUntil(() => Writer.Sent.Count > sent).Timeout(TimeSpan.FromSeconds(3));
            }
        }

        private class FakeConnection : INetworkConnection
        {
            public FakeConnection(IConnectionWriter writer)
            {
                Writer = writer;
            }

            public IConnectionReader Reader => throw new NotSupportedException();
            public IConnectionWriter Writer { get; }
            public IViewableDelegate<SocketCloseInfo> Closed => throw new NotSupportedException();
            public NetworkTraffic Traffic { get; } = new();

            public UniTask Run(IReadOnlyLifetime lifetime, string url) => throw new NotSupportedException();
            public void Drop(string reason) => throw new NotSupportedException();
        }

        private class FakeWriter : IConnectionWriter
        {
            public readonly List<INetworkContext> Sent = new();

            public bool IsClosed => false;

            public void WriteOneWay(INetworkContext value) => Sent.Add(value);

            public UniTask<T> WriteRequest<T>(INetworkContext commandContext) => throw new NotSupportedException();
            public void WriteResponse(INetworkContext commandContext, int requestId) => throw new NotSupportedException();
            public void OnRequestHandled(INetworkContext commandContext, int requestId) => throw new NotSupportedException();
            public UniTask ForceSendAll() => throw new NotSupportedException();
        }

        private class FakeSession : INetworkSession
        {
            public FakeSession(INetworkUsersCollection users)
            {
                Users = users;
            }

            public INetworkUsersCollection Users { get; }
            public IReadOnlyLifetime Lifetime => throw new NotSupportedException();

            public UniTask Start(IReadOnlyLifetime lifetime, string serverUrl, string ticket) => throw new NotSupportedException();
        }

        private class FakeUsers : INetworkUsersCollection
        {
            private readonly Dictionary<int, INetworkUser> _entries = new();

            public IReadOnlyDictionary<int, INetworkUser> Entries => _entries;
            public INetworkUser Local { get; private set; }
            public INetworkUser Session => throw new NotSupportedException();

            public void Add(INetworkUser user)
            {
                _entries.Add(user.Index, user);

                if (user.IsLocal == true)
                    Local = user;
            }

            public void Remove(int index) => _entries.Remove(index);

            public void SetSession(INetworkUser session) => throw new NotSupportedException();
            public INetworkUser GetOwner(int index) => throw new NotSupportedException();
        }

        private class FakeUser : INetworkUser
        {
            public FakeUser(int index, bool isLocal)
            {
                Index = index;
                IsLocal = isLocal;
            }

            public int Index { get; }
            public bool IsLocal { get; }
            public Guid Id => throw new NotSupportedException();
            public IReadOnlyDictionary<int, INetworkEntity> Entities => throw new NotSupportedException();
            public IReadOnlyLifetime Lifetime => throw new NotSupportedException();
            public IViewableProperty<bool> IsAway => throw new NotSupportedException();

            public void AddEntity(INetworkEntity entity) => throw new NotSupportedException();
            public void SetAway(bool isAway) => throw new NotSupportedException();
            public void DisposeRemote() => throw new NotSupportedException();
        }
    }
}
