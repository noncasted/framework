using Cysharp.Threading.Tasks;
using Shared;
using UnityEngine;

namespace Internal
{
    /// <summary>
    /// Measures a session: the round trip to the backend (ping), the loop to every other client through the backend
    /// split into the local leg and the leg of the other client (see SharedSessionLatency), the frame time (the
    /// writer sends once a frame and a received message is applied in the frame it arrives) and the traffic.
    /// Shown by NetworkLatencyOverlay (N) and logged every few seconds. In a P2P match (IPeerMatch) the session is on
    /// the client of the host: the probes go through the host, so the host shows the legs to its guests, a guest its
    /// leg to the host and the loops to the other guests through the host.
    /// </summary>
    public class NetworkLatencyMonitor : INetworkLatencyMonitor, IScopeSetup
    {
        public NetworkLatencyMonitor(
            INetworkConnection connection,
            INetworkSession session,
            IPeerMatch peerMatch,
            ILatencyClock clock)
        {
            _session = session;
            _clock = clock;
            _probes = new LatencyProbes(connection, clock);
            _stats = new LatencyStats(connection, session, clock);
            _report = new LatencyReport(session, peerMatch, _probes, _stats);
        }

        private const float ProbeInterval = 0.5f;
        private const float RateInterval = 1f;
        private const float LogInterval = 5f;

        private readonly INetworkSession _session;
        private readonly ILatencyClock _clock;
        private readonly LatencyProbes _probes;
        private readonly LatencyStats _stats;
        private readonly LatencyReport _report;

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

            NetworkLatencyOverlay.Create(lifetime, this);

            var probeTimer = ProbeInterval;
            var rateTimer = 0f;
            var logTimer = 0f;

            while (lifetime.IsTerminated == false)
            {
                var delta = _clock.FrameDelta;

                _stats.OnFrame(delta * 1000f);

                probeTimer += delta;
                rateTimer += delta;
                logTimer += delta;

                if (probeTimer >= ProbeInterval)
                {
                    probeTimer = 0f;
                    _probes.Send();
                    _probes.Expire();
                    _stats.RemoveLeftPeers();
                    Report = _report.Build();
                }

                if (rateTimer >= RateInterval)
                {
                    _stats.UpdateRates(rateTimer);
                    rateTimer = 0f;
                }

                if (logTimer >= LogInterval)
                {
                    logTimer = 0f;
                    Debug.Log($"[Network] [Latency] {Report.Replace("\n", " | ")}");
                }

                await UniTask.Yield();
            }
        }
    }
}
