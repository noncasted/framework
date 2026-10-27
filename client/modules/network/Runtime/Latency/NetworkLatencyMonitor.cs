using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using Shared;
using UnityEngine;

namespace Internal
{
    public interface INetworkLatencyMonitor
    {
        // Text of the overlay, rebuilt twice a second.
        string Report { get; }

        // The last round trip to the backend, 0 before the first pong.
        float PingMs { get; }

        void OnPong(int id);
        void OnPeerEcho(SharedSessionLatency.PeerEcho echo);
    }

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
        public NetworkLatencyMonitor(INetworkConnection connection, INetworkSession session, IPeerMatch peerMatch)
        {
            _connection = connection;
            _session = session;
            _peerMatch = peerMatch;
        }

        private const float ProbeInterval = 0.5f;
        private const float RateInterval = 1f;
        private const float LogInterval = 5f;

        // A probe without an answer for this long is counted as lost.
        private const double ProbeTimeout = 5d;

        // 10 seconds of probes.
        private const int SeriesCapacity = 20;
        private const int FrameCapacity = 60;

        private readonly INetworkConnection _connection;
        private readonly INetworkSession _session;
        private readonly IPeerMatch _peerMatch;

        private readonly Dictionary<int, double> _pendingPings = new();
        private readonly Dictionary<int, double> _pendingProbes = new();
        private readonly Dictionary<int, PeerStats> _peers = new();
        private readonly List<int> _expired = new();

        private readonly LatencySeries _ping = new(SeriesCapacity);
        private readonly LatencySeries _frame = new(FrameCapacity);
        private readonly StringBuilder _builder = new();

        private int _nextId;
        private int _lostPings;

        private float _frameMaxInSecond;
        private float _frameMaxLastSecond;
        private float _sentPerSecond;
        private float _receivedPerSecond;
        private float _sentBytesPerSecond;
        private float _receivedBytesPerSecond;
        private int _peakBatch;

        private long _lastSentMessages;
        private long _lastReceivedMessages;
        private long _lastSentBytes;
        private long _lastReceivedBytes;

        public string Report { get; private set; } = "Network: waiting for the session";
        public float PingMs => _ping.Last;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            Run(lifetime).Forget();
        }

        public void OnPong(int id)
        {
            // Came after its timeout: already counted as lost.
            if (_pendingPings.Remove(id, out var sentAt) == false)
                return;

            _ping.Add(ElapsedMs(sentAt));
        }

        public void OnPeerEcho(SharedSessionLatency.PeerEcho echo)
        {
            // A probe stays pending until its timeout: every other client answers it.
            if (_pendingProbes.TryGetValue(echo.Id, out var sentAt) == false)
                return;

            // The echo of a user that has left since (it answered before its disconnect reached us).
            if (_session.Users.Entries.ContainsKey(echo.ResponderIndex) == false)
                return;

            if (_peers.TryGetValue(echo.ResponderIndex, out var peer) == false)
            {
                peer = new PeerStats();
                _peers.Add(echo.ResponderIndex, peer);
            }

            var loop = ElapsedMs(sentAt);

            peer.Loop.Add(loop);
            peer.TheirLeg.Add(echo.ResponderLegMs);
            peer.OurLeg.Add(loop - echo.ResponderLegMs);
            peer.TheirPing = echo.ResponderPingMs;
            peer.LastEchoAt = Time.realtimeSinceStartupAsDouble;
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
                var delta = Time.unscaledDeltaTime;
                var frameMs = delta * 1000f;

                _frame.Add(frameMs);

                if (frameMs > _frameMaxInSecond)
                    _frameMaxInSecond = frameMs;

                probeTimer += delta;
                rateTimer += delta;
                logTimer += delta;

                if (probeTimer >= ProbeInterval)
                {
                    probeTimer = 0f;
                    SendProbes();
                    ExpireProbes();
                    BuildReport();
                }

                if (rateTimer >= RateInterval)
                {
                    UpdateRates(rateTimer);
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

        private void SendProbes()
        {
            var now = Time.realtimeSinceStartupAsDouble;

            _nextId++;
            _pendingPings.Add(_nextId, now);
            _connection.OneWay(new SharedSessionLatency.Ping() { Id = _nextId });

            _pendingProbes.Add(_nextId, now);
            _connection.OneWay(new SharedSessionLatency.PeerProbe() { Id = _nextId });
        }

        private void ExpireProbes()
        {
            var now = Time.realtimeSinceStartupAsDouble;

            _expired.Clear();

            foreach (var (id, sentAt) in _pendingPings)
            {
                if (now - sentAt > ProbeTimeout)
                    _expired.Add(id);
            }

            foreach (var id in _expired)
                _pendingPings.Remove(id);

            _lostPings += _expired.Count;

            _expired.Clear();

            foreach (var (id, sentAt) in _pendingProbes)
            {
                if (now - sentAt > ProbeTimeout)
                    _expired.Add(id);
            }

            foreach (var id in _expired)
                _pendingProbes.Remove(id);

            // A user that has left the session: its row would stay "NO ECHO" until the session ends.
            _expired.Clear();

            foreach (var (index, _) in _peers)
            {
                if (_session.Users.Entries.ContainsKey(index) == false)
                    _expired.Add(index);
            }

            foreach (var index in _expired)
                _peers.Remove(index);
        }

        private void UpdateRates(float interval)
        {
            var traffic = _connection.Traffic;

            _sentPerSecond = (traffic.SentMessages - _lastSentMessages) / interval;
            _receivedPerSecond = (traffic.ReceivedMessages - _lastReceivedMessages) / interval;
            _sentBytesPerSecond = (traffic.SentBytes - _lastSentBytes) / interval;
            _receivedBytesPerSecond = (traffic.ReceivedBytes - _lastReceivedBytes) / interval;
            _peakBatch = traffic.TakePeakBatch();

            _lastSentMessages = traffic.SentMessages;
            _lastReceivedMessages = traffic.ReceivedMessages;
            _lastSentBytes = traffic.SentBytes;
            _lastReceivedBytes = traffic.ReceivedBytes;

            _frameMaxLastSecond = _frameMaxInSecond;
            _frameMaxInSecond = 0f;
        }

        private void BuildReport()
        {
            var localIndex = _session.LocalIndex();
            var hostIndex = _peerMatch.HostIndex;

            _builder.Clear();

            if (hostIndex == 0)
                AppendServerReport(localIndex);
            else if (hostIndex == localIndex)
                AppendHostReport(localIndex);
            else
                AppendGuestReport(localIndex, hostIndex);

            if (_peers.Count == 0)
                _builder.Append("No other users answered yet\n");

            _builder.Append($"Frame: {_frame.Average:0.0} avg, {_frameMaxLastSecond:0} max/s ({1000f / _frame.Average:0} fps)\n");
            _builder.Append($"Out: {_sentPerSecond:0} msg/s, {_sentBytesPerSecond / 1024f:0.0} KB/s, peak {_peakBatch} msg/frame\n");
            _builder.Append($"In: {_receivedPerSecond:0} msg/s, {_receivedBytesPerSecond / 1024f:0.0} KB/s");

            Report = _builder.ToString();
        }

        private void AppendServerReport(int localIndex)
        {
            _builder.Append($"Network (you: user {localIndex}), ms\n");
            _builder.Append($"Ping to server: {_ping.Format()}, lost {_lostPings}\n");

            // Not one pong yet: a backend without the latency commands drops the probes as unknown messages.
            if (_ping.Count == 0 && _lostPings > 0)
                _builder.Append("Server does not answer: is the backend deployed with the latency commands?\n");

            foreach (var (index, peer) in _peers)
            {
                _builder.Append($"User {index}: loop {peer.Loop.Format()}{FormatStale(peer)}\n");
                _builder.Append($"  you <-> server {peer.OurLeg.Format()}\n");
                _builder.Append($"  server <-> user {index} {peer.TheirLeg.Format()}, their ping {peer.TheirPing:0}\n");
            }
        }

        // The session is in this process: the ping and the own leg of a probe are the loopback, the leg of a guest is
        // the connection to it.
        private void AppendHostReport(int localIndex)
        {
            _builder.Append($"Network (you: user {localIndex}, P2P host), ms\n");

            foreach (var (index, peer) in _peers)
            {
                var route = FormatRoute(_peerMatch.Routes[index]);
                _builder.Append($"User {index}: you <-> user {index} {peer.TheirLeg.Format()}, {route}{FormatStale(peer)}\n");
            }
        }

        // The session is on the host: the own leg of a probe is the connection to the host, the leg of the host is its
        // loopback, the leg of another guest is the connection of the host to it.
        private void AppendGuestReport(int localIndex, int hostIndex)
        {
            var route = FormatRoute(_peerMatch.Routes[localIndex]);

            _builder.Append($"Network (you: user {localIndex}, P2P guest of user {hostIndex}, {route}), ms\n");
            _builder.Append($"Ping to host: {_ping.Format()}, lost {_lostPings}\n");

            foreach (var (index, peer) in _peers)
            {
                if (index == hostIndex)
                {
                    _builder.Append($"User {index} (host): you <-> user {index} {peer.OurLeg.Format()}{FormatStale(peer)}\n");
                    continue;
                }

                _builder.Append($"User {index}: loop {peer.Loop.Format()}{FormatStale(peer)}\n");
                _builder.Append($"  you <-> host {peer.OurLeg.Format()}\n");
                _builder.Append($"  host <-> user {index} {peer.TheirLeg.Format()}\n");
            }
        }

        private static string FormatRoute(PeerRoute route)
        {
            return route switch
            {
                PeerRoute.Direct => "direct",
                PeerRoute.Tunnel => "tunnel via server",
                _ => throw new ArgumentOutOfRangeException(nameof(route), route, "[Network] [Latency] No route")
            };
        }

        private static string FormatStale(PeerStats peer)
        {
            var silence = Time.realtimeSinceStartupAsDouble - peer.LastEchoAt;
            return silence > ProbeTimeout ? $" NO ECHO {silence:0}s" : string.Empty;
        }

        private static float ElapsedMs(double sentAt)
        {
            return (float)((Time.realtimeSinceStartupAsDouble - sentAt) * 1000d);
        }

        private class PeerStats
        {
            public readonly LatencySeries Loop = new(SeriesCapacity);
            public readonly LatencySeries OurLeg = new(SeriesCapacity);
            public readonly LatencySeries TheirLeg = new(SeriesCapacity);

            public float TheirPing;
            public double LastEchoAt;
        }
    }
}
