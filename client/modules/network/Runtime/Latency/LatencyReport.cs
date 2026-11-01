using System.Text;

namespace Internal
{
    // The text of the overlay: the part of the kind of the session (on the server, a P2P host, a P2P guest) and the
    // common lines of the frame and the traffic.
    public class LatencyReport
    {
        public LatencyReport(INetworkSession session, IPeerMatch peerMatch, LatencyProbes probes, LatencyStats stats)
        {
            _session = session;
            _peerMatch = peerMatch;
            _probes = probes;
            _stats = stats;
        }

        private readonly INetworkSession _session;
        private readonly IPeerMatch _peerMatch;
        private readonly LatencyProbes _probes;
        private readonly LatencyStats _stats;
        private readonly StringBuilder _builder = new();

        public string Build()
        {
            var localIndex = _session.LocalIndex();
            var hostIndex = _peerMatch.HostIndex;

            _builder.Clear();

            if (hostIndex == 0)
                ServerLatencyReport.Append(_builder, localIndex, _stats, _probes.LostPings);
            else if (hostIndex == localIndex)
                HostLatencyReport.Append(_builder, localIndex, _stats, _peerMatch.Routes);
            else
                GuestLatencyReport.Append(_builder, localIndex, hostIndex, _stats, _probes.LostPings, _peerMatch.Routes);

            if (_stats.Peers.Count == 0)
                _builder.Append("No other users answered yet\n");

            _builder.Append($"Frame: {_stats.Frame.Average:0.0} avg, {_stats.FrameMaxLastSecond:0} max/s ({1000f / _stats.Frame.Average:0} fps)\n");
            _builder.Append($"Out: {_stats.SentPerSecond:0} msg/s, {_stats.SentBytesPerSecond / 1024f:0.0} KB/s, peak {_stats.PeakBatch} msg/frame\n");
            _builder.Append($"In: {_stats.ReceivedPerSecond:0} msg/s, {_stats.ReceivedBytesPerSecond / 1024f:0.0} KB/s");

            return _builder.ToString();
        }
    }
}
