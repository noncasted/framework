using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Shared;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // NetworkLatencyMonitor through its public API, on a clock of the test: the probes every 0.5 s, their 5 s timeout,
    // the rates of the traffic and the text of the report on the server, on a P2P host and on a P2P guest. In play mode:
    // the monitor creates its overlay with DontDestroyOnLoad and its loop runs on the frames.
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

            harness.Clock.Advance(0.7f);
            await Frames();

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
            await harness.FirstRound();

            Assert.IsNotNull(GameObject.Find(OverlayName));
            Assert.AreNotEqual(Waiting, harness.Monitor.Report);
        });

        // A text without a font of the game took TMP_Settings.defaultFontAsset and warned in every session when there was none.
        [UnityTest]
        public IEnumerator Overlay_TakesTheFontOfTheOptions() => UniTask.ToCoroutine(async () =>
        {
            var harness = Create();
            harness.Monitor.OnSetup(harness.Lifetime);
            harness.AddLocal(1);
            await harness.FirstRound();

            var label = GameObject.Find(OverlayName).GetComponentInChildren<TextMeshProUGUI>(true);

            Assert.AreSame(harness.Options.LatencyOverlayFont, label.font);
        });

        [Test]
        public void OptionsWithoutAFont_Throw()
        {
            var options = ScriptableObject.CreateInstance<NetworkOptions>();

            Assert.Throws<InvalidOperationException>(() =>
                new NetworkLatencyMonitor(new FakeConnection(new FakeWriter()), new FakeSession(new FakeUsers()), new PeerMatch(), new FakeClock(),
                    options, new ScreenCanvasOverlay()));
        }

        // A Screen Space - Overlay canvas loses its projection on iPadOS Safari: the overlay goes to the UI camera of the game.
        [UnityTest]
        public IEnumerator Overlay_IsDrawnByTheScreenCanvasCamera() => UniTask.ToCoroutine(async () =>
        {
            var harness = Create();
            harness.Monitor.OnSetup(harness.Lifetime);
            harness.AddLocal(1);
            await harness.FirstRound();

            var canvas = GameObject.Find(OverlayName).GetComponent<Canvas>();
            var layer = LayerMask.NameToLayer(Harness.CanvasLayer);

            Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode);
            Assert.AreSame(harness.Camera, canvas.worldCamera);
            Assert.AreEqual(Harness.PlaneDistance, canvas.planeDistance);
            Assert.That(canvas.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject.layer), Is.All.EqualTo(layer));
        });

        [UnityTest]
        public IEnumerator EveryHalfSecond_PingAndProbeGoOutWithOneRisingId() => UniTask.ToCoroutine(async () =>
        {
            var harness = await Started();

            for (var round = 0; round < 2; round++)
            {
                var before = harness.Writer.Sent.Count;

                harness.Clock.Advance(0.46875f);
                await Frames();
                Assert.AreEqual(before, harness.Writer.Sent.Count, "A round before its half a second");

                await harness.NextRound(0.03125f);
            }

            var sent = harness.Writer.Sent;

            Assert.AreEqual(6, sent.Count);

            for (var round = 0; round < 3; round++)
            {
                Assert.AreEqual(round + 1, ((SharedSessionLatency.Ping)sent[round * 2]).Id);
                Assert.AreEqual(round + 1, ((SharedSessionLatency.PeerProbe)sent[round * 2 + 1]).Id);
            }
        });

        [UnityTest]
        public IEnumerator LifetimeEnds_ProbesStopAndOverlayIsDestroyed() => UniTask.ToCoroutine(async () =>
        {
            var harness = await Started();
            harness.Lifetime.Terminate();

            await UniTask.Yield();
            await UniTask.Yield();
            var sent = harness.Writer.Sent.Count;

            harness.Clock.Advance(0.7f);
            await Frames();

            Assert.AreEqual(sent, harness.Writer.Sent.Count);
            Assert.IsNull(GameObject.Find(OverlayName));
        });

        [UnityTest]
        public IEnumerator Pong_SetsPingAndGoesToReport() => UniTask.ToCoroutine(async () =>
        {
            var harness = await Started();
            Assert.AreEqual(0f, harness.Monitor.PingMs);
            Assert.That(harness.Monitor.Report, Does.Contain("Ping to server: -, lost 0\n"));

            harness.Clock.Advance(0.02f);
            harness.Monitor.OnPong(harness.LastId);
            Assert.AreEqual(20f, harness.Monitor.PingMs, 0.01f);

            await harness.NextRound();

            AssertReport(harness, $@"Network \(you: user 1\), ms\nPing to server: {Series}, lost 0\n{NoAnswers}{Footer}");
        });

        [UnityTest]
        public IEnumerator Pong_OfUnknownIdOrAnsweredTwice_IsIgnored() => UniTask.ToCoroutine(async () =>
        {
            var harness = await Started();

            harness.Monitor.OnPong(harness.LastId + 100);
            Assert.AreEqual(0f, harness.Monitor.PingMs);

            harness.Clock.Advance(0.02f);
            harness.Monitor.OnPong(harness.LastId);
            var ping = harness.Monitor.PingMs;

            harness.Clock.Advance(0.1f);
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
            harness.Clock.Advance(0.02f);
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
            harness.Clock.Advance(0.02f);
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

            // Not a second yet: the traffic stays out of the report.
            await harness.NextRound(0.75f);
            Assert.That(harness.Monitor.Report, Does.Contain("Out: 0 msg/s, 0.0 KB/s, peak 0 msg/frame"));

            // The rates of an interval of 1.5 s; the report of this round was built before them.
            await harness.NextRound(0.75f);
            await harness.NextRound();

            Assert.That(harness.Monitor.Report, Does.Contain("750 max/s"));
            Assert.That(harness.Monitor.Report, Does.Contain(
                "Out: 67 msg/s, 66.7 KB/s, peak 3 msg/frame\n"
                + "In: 33 msg/s, 66.7 KB/s"));

            // The rates are of one interval, the peak is taken: the next interval starts from zero.
            await harness.NextRound();
            await harness.NextRound();

            Assert.That(harness.Monitor.Report, Does.Contain("500 max/s"));
            Assert.That(harness.Monitor.Report, Does.Contain("Out: 0 msg/s, 0.0 KB/s, peak 0 msg/frame"));
            Assert.That(harness.Monitor.Report, Does.Contain("In: 0 msg/s, 0.0 KB/s"));
        });

        // The 5 s timeout of a probe, shared by the three kinds of the report.
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

            var firstServerId = server.LastId;

            server.Echo(2, 7f, 33f);
            host.Echo(2, 7f, 33f);
            guest.Echo(1, 0f, 0f);
            guest.Echo(3, 5f, 11f);

            // Exactly 5 s after the first probe it is still waited for.
            for (var round = 0; round < 10; round++)
                await UniTask.WhenAll(server.NextRound(), host.NextRound(), guest.NextRound());

            Assert.That(server.Monitor.Report, Does.Contain(", lost 0\n"));
            Assert.That(server.Monitor.Report, Does.Not.Contain("NO ECHO"));
            Assert.That(host.Monitor.Report, Does.Not.Contain("NO ECHO"));
            Assert.That(guest.Monitor.Report, Does.Not.Contain("NO ECHO"));

            await UniTask.WhenAll(server.NextRound(), host.NextRound(), guest.NextRound());

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

        // A broken report is diagnostics only: a stop of the probes would look like a lost connection to the watchdog. On a
        // guest a missing route is such a break: PeerHostSocket sets it before the authorization, so before the local user.
        [UnityTest]
        public IEnumerator FailedReport_ProbesGoOnAndTheNextReportAppears() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.Expect(LogType.Exception, new Regex("KeyNotFoundException"));
            LogAssert.Expect(LogType.Exception, new Regex("KeyNotFoundException"));

            var harness = Create();
            harness.Match.SetHost(1);
            harness.AddLocal(2);
            harness.Monitor.OnSetup(harness.Lifetime);

            await harness.FirstRound();
            await harness.NextRound();

            Assert.AreEqual(4, harness.Writer.Sent.Count);
            Assert.That(harness.Monitor.Report, Does.StartWith("Network: the monitor failed: "));

            harness.Match.SetRoute(2, PeerRoute.Tunnel);
            await harness.NextRound();

            Assert.AreEqual(6, harness.Writer.Sent.Count);
            AssertReport(harness,
                @"Network \(you: user 2, P2P guest of user 1, tunnel via server\), ms\n"
                + @"Ping to host: -, lost 0\n"
                + NoAnswers
                + Footer);
        });

        // The probes are what keeps the connection alive for the watchdog: no failure of a round may end them.
        [UnityTest]
        public IEnumerator FailedSend_TheNextRoundGoesOut() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.Expect(LogType.Exception, new Regex("Send failure"));

            var harness = await Started();
            var sent = harness.Writer.Sent.Count;

            harness.Writer.FailOnce(new InvalidOperationException("Send failure"));
            harness.Clock.Advance(0.5f);
            await Frames();

            var failedReport = harness.Monitor.Report;

            Assert.AreEqual(sent, harness.Writer.Sent.Count);

            await harness.NextRound();

            Assert.That(failedReport, Does.StartWith("Network: the monitor failed: "));
            Assert.AreEqual(sent + 2, harness.Writer.Sent.Count);
            AssertReport(harness, $@"Network \(you: user 1\), ms\nPing to server: -, lost 0\n{NoAnswers}{Footer}");
        });

        [UnityTest]
        public IEnumerator FailedRates_ProbesGoOnAndRatesComeBack() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.Expect(LogType.Exception, new Regex("Traffic failure"));

            var harness = await Started();
            var sent = harness.Writer.Sent.Count;

            harness.Connection.FailTrafficOnce(new InvalidOperationException("Traffic failure"));
            harness.Clock.Advance(1f);
            await Frames();

            await harness.NextRound();

            Assert.AreEqual(sent + 4, harness.Writer.Sent.Count);
            AssertReport(harness, $@"Network \(you: user 1\), ms\nPing to server: -, lost 0\n{NoAnswers}{Footer}");
        });

        [UnityTest]
        public IEnumerator FailedRemoveOfLeftPeers_TheNextRoundCleansAndGoesOut() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.Expect(LogType.Exception, new Regex("Users failure"));

            var harness = await Started();
            harness.AddRemote(2);
            harness.Echo(2, 10f, 20f);

            var sent = harness.Writer.Sent.Count;

            harness.Users.FailEntriesOnce(new InvalidOperationException("Users failure"));
            await harness.NextRound();
            await Frames();

            var failedReport = harness.Monitor.Report;

            Assert.AreEqual(sent + 2, harness.Writer.Sent.Count);

            harness.Users.Remove(2);
            await harness.NextRound();
            await Frames();

            Assert.That(failedReport, Does.StartWith("Network: the monitor failed: "));
            AssertReport(harness, $@"Network \(you: user 1\), ms\nPing to server: -, lost 0\n{NoAnswers}{Footer}");
        });

        // A failure that does not pass: what was never sent waits for no answer and is not a lost ping.
        [UnityTest]
        public IEnumerator SendFailsForLong_NothingPilesUpAndNothingCountsAsLost() => UniTask.ToCoroutine(async () =>
        {
            var harness = await Started();

            harness.Writer.FailAll();
            await FailingRounds(harness, 30);

            Assert.LessOrEqual(Pending(harness, "_pendingPings"), 1);
            Assert.LessOrEqual(Pending(harness, "_pendingProbes"), 1);

            harness.Writer.Recover();
            await harness.NextRound();

            // Only the ping of the first round was sent, and its time ran out.
            Assert.That(harness.Monitor.Report, Does.Contain(", lost 1\n"));
        });

        // The ping goes out and its probe fails: the pings still expire.
        [UnityTest]
        public IEnumerator ProbeFailsForLong_PendingPingsAreLimitedByTheTimeout() => UniTask.ToCoroutine(async () =>
        {
            var harness = await Started();

            harness.Writer.FailProbes();
            await FailingRounds(harness, 30);

            var limit = (int)(LatencyProbes.Timeout / 0.5d) + 2;

            Assert.LessOrEqual(Pending(harness, "_pendingPings"), limit);
            Assert.LessOrEqual(Pending(harness, "_pendingProbes"), limit);
        });

        // PeerHost serves the frames of a direct guest from the open of its channel and sets the route when Connect wakes
        // up later: an echo of the guest may come first.
        [UnityTest]
        public IEnumerator HostReport_GuestWithoutRouteYet_RowSaysSoUntilTheRouteIsSet() => UniTask.ToCoroutine(async () =>
        {
            var harness = await Started(match => match.SetHost(1));
            harness.AddRemote(2);
            harness.Echo(2, 7f, 33f);

            await harness.NextRound();

            AssertReport(harness,
                @"Network \(you: user 1, P2P host\), ms\n"
                + @"User 2: you <-> user 2 7 \(avg 7, max 7\), route not known yet\n"
                + Footer);

            harness.Match.SetRoute(2, PeerRoute.Direct);
            await harness.NextRound();

            AssertReport(harness,
                @"Network \(you: user 1, P2P host\), ms\n"
                + @"User 2: you <-> user 2 7 \(avg 7, max 7\), direct\n"
                + Footer);
        });

        private static void AssertReport(Harness harness, string pattern)
        {
            var report = harness.Monitor.Report;
            Assert.IsTrue(Regex.IsMatch(report, $@"\A{pattern}\z"), $"Report:\n{report}\nPattern:\n{pattern}");
        }

        // Enough frames for the loop of a monitor to see the time given to its clock.
        private static async UniTask Frames()
        {
            for (var i = 0; i < 3; i++)
                await UniTask.Yield();
        }

        private static async UniTask FailingRounds(Harness harness, int rounds)
        {
            for (var round = 0; round < rounds; round++)
            {
                LogAssert.Expect(LogType.Exception, new Regex("Send failure"));
                harness.Clock.Advance(0.5f);
                await Frames();
            }
        }

        // The entries a monitor keeps have no public count, and the limit of their number is the point of the test.
        private static int Pending(Harness harness, string field)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

            var probes = typeof(NetworkLatencyMonitor).GetField("_probes", flags).GetValue(harness.Monitor);
            return ((IDictionary)typeof(LatencyProbes).GetField(field, flags).GetValue(probes)).Count;
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
            await harness.FirstRound();
            return harness;
        }

        private class Harness
        {
            public Harness()
            {
                Connection = new FakeConnection(Writer);
                Options = ScriptableObject.CreateInstance<NetworkOptions>();
                typeof(NetworkOptions).GetField("_latencyOverlayFont", BindingFlags.Instance | BindingFlags.NonPublic)
                                      .SetValue(Options, Font);
                Camera = new GameObject("NetworkLatencyMonitorTests.Camera").AddComponent<Camera>();
                Camera.enabled = false;
                Camera.cullingMask = 1 << LayerMask.NameToLayer(CanvasLayer);
                var camera = Camera;
                Lifetime.Listen(() => UnityEngine.Object.Destroy(camera.gameObject));

                var screenCanvas = new ScreenCanvasCamera(() => camera, CanvasLayer, PlaneDistance);
                Monitor = new NetworkLatencyMonitor(Connection, new FakeSession(Users), Match, Clock, Options, screenCanvas);
            }

            // The built-in UI layer: in every project.
            public const string CanvasLayer = "UI";
            public const float PlaneDistance = 1f;

            public readonly Camera Camera;

            public readonly Lifetime Lifetime = new();
            public readonly FakeUsers Users = new();
            public readonly FakeWriter Writer = new();
            public readonly PeerMatch Match = new();
            public readonly FakeClock Clock = new();
            public readonly FakeConnection Connection;
            public readonly NetworkOptions Options;

            // The font the forked ugui package ships: the tests run in the editor only.
            private static TMP_FontAsset Font =>
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Packages/com.unity.ugui/Package Resources/Fonts & Materials/LiberationSans SDF.asset");
#else
                throw new System.InvalidOperationException("[Tests] The editor only");
#endif
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

            // The first round goes out as soon as the local user is there, without any time passed.
            public UniTask FirstRound() => WaitRound();

            public UniTask NextRound(float seconds = 0.5f)
            {
                var round = WaitRound();
                Clock.Advance(seconds);
                return round;
            }

            // The report is built in the frame the probes go out. The timeout only ends a test that has failed.
            private async UniTask WaitRound()
            {
                var sent = Writer.Sent.Count;
                await UniTask.WaitUntil(() => Writer.Sent.Count > sent).Timeout(TimeSpan.FromSeconds(10));
            }
        }

        // The time of one monitor passes when the test says: its loop reads FrameDelta once a frame and gets all the
        // time given since the last read.
        private class FakeClock : ILatencyClock
        {
            private float _unread;

            public double Now { get; private set; }

            public float FrameDelta
            {
                get
                {
                    var delta = _unread;
                    _unread = 0f;
                    return delta;
                }
            }

            public void Advance(float seconds)
            {
                Now += seconds;
                _unread += seconds;
            }
        }

        // The failure of the next call of a fake.
        private class OnceFailure
        {
            private Exception _exception;

            public void Set(Exception exception) => _exception = exception;

            public void ThrowIfSet()
            {
                if (_exception == null)
                    return;

                var exception = _exception;
                _exception = null;
                throw exception;
            }
        }

        private class FakeConnection : INetworkConnection
        {
            public FakeConnection(IConnectionWriter writer)
            {
                Writer = writer;
            }

            private readonly NetworkTraffic _traffic = new();
            private readonly OnceFailure _failure = new();

            public IConnectionReader Reader => throw new NotSupportedException();
            public IConnectionWriter Writer { get; }
            public IViewableDelegate<SocketCloseInfo> Closed => throw new NotSupportedException();
            public NetworkTraffic Traffic
            {
                get
                {
                    _failure.ThrowIfSet();
                    return _traffic;
                }
            }

            public void FailTrafficOnce(Exception exception) => _failure.Set(exception);

            public UniTask Run(IReadOnlyLifetime lifetime, string url) => throw new NotSupportedException();
            public void Drop(string reason) => throw new NotSupportedException();
        }

        private class FakeWriter : IConnectionWriter
        {
            public readonly List<INetworkContext> Sent = new();

            private readonly OnceFailure _failure = new();
            private Func<INetworkContext, bool> _fails;

            public bool IsClosed => false;
            public object CurrentRun => this;

            public void WriteOneWay(ReadOnlySpan<byte> message) => throw new NotSupportedException();
            public void WriteOneWay(INetworkContext value)
            {
                _failure.ThrowIfSet();

                if (_fails != null && _fails(value) == true)
                    throw new InvalidOperationException("Send failure");

                Sent.Add(value);
            }

            public void FailOnce(Exception exception) => _failure.Set(exception);
            public void FailAll() => _fails = _ => true;
            public void FailProbes() => _fails = value => value is SharedSessionLatency.PeerProbe;
            public void Recover() => _fails = null;

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
            private readonly OnceFailure _failure = new();

            public IReadOnlyDictionary<int, INetworkUser> Entries
            {
                get
                {
                    _failure.ThrowIfSet();
                    return _entries;
                }
            }

            public INetworkUser Local { get; private set; }
            public INetworkUser Session => throw new NotSupportedException();

            public void Add(INetworkUser user)
            {
                _entries.Add(user.Index, user);

                if (user.IsLocal == true)
                    Local = user;
            }

            public void Remove(int index) => _entries.Remove(index);
            public void FailEntriesOnce(Exception exception) => _failure.Set(exception);

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
