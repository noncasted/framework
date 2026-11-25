using System;
using Cysharp.Threading.Tasks;
using Shared;

namespace Internal
{
    /// <summary>
    /// Measures a session: the round trip to the backend (ping), the loop to every other client through the backend
    /// split into the local leg and the leg of the other client (see SharedSessionLatency), the frame time (the
    /// writer sends once a frame and a received message is applied in the frame it arrives) and the traffic.
    /// Logged every few seconds; shown by NetworkLatencyOverlay of Internal.Network.Overlay while IsMeasuring. In a
    /// P2P match (IPeerMatch) the session is on the client of the host: the probes go through the host, so the host
    /// shows the legs to its guests, a guest its leg to the host and the loops to the other guests through the host.
    /// </summary>
    public class NetworkLatencyMonitor : INetworkLatencyMonitor, IScopeSetup
    {
        public NetworkLatencyMonitor(
            INetworkConnection connection,
            INetworkSession session,
            IPeerMatch peerMatch,
            ILatencyClock clock,
            ILog<NetworkLatencyMonitor> log)
        {
            _session = session;
            _clock = clock;
            _log = log;
            _probes = new LatencyProbes(connection, clock);
            _stats = new LatencyStats(connection, session, clock);
            _report = new LatencyReport(session, peerMatch, _probes, _stats);
        }

        private const float ProbeInterval = 0.5f;
        private const float RateInterval = 1f;
        private const float LogInterval = 5f;

        private readonly INetworkSession _session;
        private readonly ILatencyClock _clock;
        private readonly ILog _log;
        private readonly LatencyProbes _probes;
        private readonly LatencyStats _stats;
        private readonly LatencyReport _report;
        private readonly ViewableProperty<bool> _isMeasuring = new(false);

        private float _probeTimer = ProbeInterval;
        private float _rateTimer;
        private float _logTimer;

        public IViewableProperty<bool> IsMeasuring => _isMeasuring;
        public string Report { get; private set; } = "Network: waiting for the session";
        public float PingMs => _stats.Ping.Last;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            Run(lifetime).NoAwait();
        }

        public void OnPong(int id)
        {
            if (_probes.TryCompletePing(id, out var elapsedMs) == true)
                _stats.Ping.Add(elapsedMs);
        }

        public void OnPeerEcho(SharedSessionLatency.PeerEcho echo)
        {
            if (_probes.TryGetProbeElapsed(echo.Id, out var loopMs) == true)
                _stats.OnEcho(loopMs, echo);
        }

        private async UniTask Run(IReadOnlyLifetime lifetime)
        {
            // Probes go out once the backend knows this user: before the auth it has no session to answer from.
            await UniTask.WaitUntil(() => _session.Users.Local != null, cancellationToken: lifetime.Token);

            _isMeasuring.Set(true);
            lifetime.Listen(() => _isMeasuring.Set(false));

            while (lifetime.IsTerminated == false)
            {
                Tick();
                await UniTask.Yield();
            }
        }

        // The boundary of the diagnostics: the probes are what keeps the connection alive for the watchdog, so no failure
        // of a frame ends the loop. The failure is logged and shown instead of the last report, the timers stay as they
        // are: a failed round comes again in its interval, the next round builds the report anew.
        private void Tick()
        {
            try
            {
                Measure(_clock.FrameDelta);
            }
            catch (Exception exception)
            {
                _log.Exception(exception);
                Report = $"Network: the monitor failed: {exception.Message}";
            }
        }

        private void Measure(float delta)
        {
            _stats.OnFrame(delta * 1000f);

            _probeTimer += delta;
            _rateTimer += delta;
            _logTimer += delta;

            if (_probeTimer >= ProbeInterval)
            {
                _probeTimer = 0f;

                // Before the send: a send that keeps failing must not stop the timeout of what already went out.
                _probes.Expire();
                _probes.Send();
                _stats.RemoveLeftPeers();
                Report = _report.Build();
            }

            if (_rateTimer >= RateInterval)
            {
                _stats.UpdateRates(_rateTimer);
                _rateTimer = 0f;
            }

            if (_logTimer >= LogInterval)
            {
                _logTimer = 0f;
                _log.Info("{Report}", Report.Replace("\n", " | "));
            }
        }
    }
}
