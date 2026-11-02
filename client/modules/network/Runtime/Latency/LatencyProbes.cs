using System.Collections.Generic;
using Shared;

namespace Internal
{
    // The probes of a session on their way: a ping to the backend and a probe to every other client go out with one id
    // and wait for their answers until the timeout.
    public class LatencyProbes
    {
        public LatencyProbes(INetworkConnection connection, ILatencyClock clock)
        {
            _connection = connection;
            _clock = clock;
        }

        // A probe without an answer for this long is counted as lost.
        public const double Timeout = 5d;

        private readonly INetworkConnection _connection;
        private readonly ILatencyClock _clock;

        private readonly Dictionary<int, double> _pendingPings = new();
        private readonly Dictionary<int, double> _pendingProbes = new();
        private readonly List<int> _expired = new();

        private int _nextId;

        public int LostPings { get; private set; }

        public void Send()
        {
            var now = _clock.Now;

            _nextId++;

            // After the write: what failed to go out waits for no answer and is not lost. The write only queues the message,
            // so the answer cannot come in between.
            _connection.OneWay(new SharedSessionLatency.Ping() { Id = _nextId });
            _pendingPings.Add(_nextId, now);

            _connection.OneWay(new SharedSessionLatency.PeerProbe() { Id = _nextId });
            _pendingProbes.Add(_nextId, now);
        }

        public void Expire()
        {
            LostPings += ExpireIn(_pendingPings);
            ExpireIn(_pendingProbes);
        }

        // False for a pong that came after its timeout: already counted as lost.
        public bool TryCompletePing(int id, out float elapsedMs)
        {
            if (_pendingPings.Remove(id, out var sentAt) == false)
            {
                elapsedMs = 0f;
                return false;
            }

            elapsedMs = ElapsedMs(sentAt);
            return true;
        }

        // A probe stays pending until its timeout: every other client answers it.
        public bool TryGetProbeElapsed(int id, out float elapsedMs)
        {
            if (_pendingProbes.TryGetValue(id, out var sentAt) == false)
            {
                elapsedMs = 0f;
                return false;
            }

            elapsedMs = ElapsedMs(sentAt);
            return true;
        }

        private int ExpireIn(Dictionary<int, double> pending)
        {
            var now = _clock.Now;

            _expired.Clear();

            foreach (var (id, sentAt) in pending)
            {
                if (now - sentAt > Timeout)
                    _expired.Add(id);
            }

            foreach (var id in _expired)
                pending.Remove(id);

            return _expired.Count;
        }

        private float ElapsedMs(double sentAt)
        {
            return (float)((_clock.Now - sentAt) * 1000d);
        }
    }
}
