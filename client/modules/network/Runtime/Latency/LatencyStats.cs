using System.Collections.Generic;
using Shared;

namespace Internal
{
    // What is measured in a session: the ping, the other clients, the frame time and the rates of the traffic.
    public class LatencyStats
    {
        public LatencyStats(INetworkConnection connection, INetworkSession session, ILatencyClock clock)
        {
            _connection = connection;
            _session = session;
            _clock = clock;
        }

        private const int FrameCapacity = 60;

        private readonly INetworkConnection _connection;
        private readonly INetworkSession _session;
        private readonly ILatencyClock _clock;

        private readonly Dictionary<int, LatencyPeerStats> _peers = new();
        private readonly List<int> _left = new();

        private float _frameMaxInSecond;

        private long _lastSentMessages;
        private long _lastReceivedMessages;
        private long _lastSentBytes;
        private long _lastReceivedBytes;

        public LatencySeries Ping { get; } = new(LatencyPeerStats.SeriesCapacity);
        public LatencySeries Frame { get; } = new(FrameCapacity);
        public IReadOnlyDictionary<int, LatencyPeerStats> Peers => _peers;

        public float FrameMaxLastSecond { get; private set; }
        public float SentPerSecond { get; private set; }
        public float ReceivedPerSecond { get; private set; }
        public float SentBytesPerSecond { get; private set; }
        public float ReceivedBytesPerSecond { get; private set; }
        public int PeakBatch { get; private set; }

        public void OnFrame(float frameMs)
        {
            Frame.Add(frameMs);

            if (frameMs > _frameMaxInSecond)
                _frameMaxInSecond = frameMs;
        }

        public void OnEcho(float loopMs, SharedSessionLatency.PeerEcho echo)
        {
            // The echo of a user that has left since (it answered before its disconnect reached us).
            if (_session.Users.Entries.ContainsKey(echo.ResponderIndex) == false)
                return;

            if (_peers.TryGetValue(echo.ResponderIndex, out var peer) == false)
            {
                peer = new LatencyPeerStats(_clock);
                _peers.Add(echo.ResponderIndex, peer);
            }

            peer.OnEcho(loopMs, echo);
        }

        // A user that has left the session: its row would stay "NO ECHO" until the session ends.
        public void RemoveLeftPeers()
        {
            _left.Clear();

            foreach (var (index, _) in _peers)
            {
                if (_session.Users.Entries.ContainsKey(index) == false)
                    _left.Add(index);
            }

            foreach (var index in _left)
                _peers.Remove(index);
        }

        public void UpdateRates(float interval)
        {
            var traffic = _connection.Traffic;

            SentPerSecond = (traffic.SentMessages - _lastSentMessages) / interval;
            ReceivedPerSecond = (traffic.ReceivedMessages - _lastReceivedMessages) / interval;
            SentBytesPerSecond = (traffic.SentBytes - _lastSentBytes) / interval;
            ReceivedBytesPerSecond = (traffic.ReceivedBytes - _lastReceivedBytes) / interval;
            PeakBatch = traffic.TakePeakBatch();

            _lastSentMessages = traffic.SentMessages;
            _lastReceivedMessages = traffic.ReceivedMessages;
            _lastSentBytes = traffic.SentBytes;
            _lastReceivedBytes = traffic.ReceivedBytes;

            FrameMaxLastSecond = _frameMaxInSecond;
            _frameMaxInSecond = 0f;
        }
    }
}
