using Internal;

namespace Global.Telemetry
{
    // net.meta.rtt (see TelemetryRoundTrip) and net.meta.bytes_in, net.meta.bytes_out: the traffic of the meta socket over the
    // sample, the batches of the telemetry included. A new connection counts from zero.
    public class MetaNetworkTelemetryCollector : ITelemetryCollector, ITelemetryLiveCollector
    {
        public MetaNetworkTelemetryCollector(ITelemetrySocket socket, TelemetryRoundTrip roundTrip)
        {
            _socket = socket;
            _roundTrip = roundTrip;
        }

        private readonly ITelemetrySocket _socket;
        private readonly TelemetryRoundTrip _roundTrip;

        private NetworkTraffic _traffic;
        private long _received;
        private long _sent;

        public void Start(IReadOnlyLifetime running)
        {
            _roundTrip.TakeMs();
            _traffic = _socket.Connection.Traffic;
            _received = _traffic.ReceivedBytes;
            _sent = _traffic.SentBytes;
        }

        public void Collect(ITelemetrySample sample)
        {
            var rtt = _roundTrip.TakeMs();

            if (rtt >= 0d)
                sample.Gauge("net.meta.rtt", rtt, TelemetryUnits.Milliseconds);

            var traffic = _socket.Connection.Traffic;

            if (traffic != _traffic || traffic.ReceivedBytes < _received || traffic.SentBytes < _sent)
            {
                _traffic = traffic;
                _received = 0;
                _sent = 0;
            }

            sample.Gauge("net.meta.bytes_in", traffic.ReceivedBytes - _received, TelemetryUnits.Bytes);
            sample.Gauge("net.meta.bytes_out", traffic.SentBytes - _sent, TelemetryUnits.Bytes);
            _received = traffic.ReceivedBytes;
            _sent = traffic.SentBytes;
        }
    }
}
