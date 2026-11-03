using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Telemetry.Tests.Transport
{
    // The module put together as AddSocketClientTelemetry does, on the real clock, the real capture of errors, the real transport and
    // the real connection of the module network. In the hands of a test: the numbers of the collector, the page, the device and the
    // source of the startup trace. The container itself: TelemetryTransportEndToEndTests.Container.
    public class TelemetryTransportClient : ITelemetrySocket
    {
        public TelemetryTransportClient(
            TelemetryDevice device = null,
            ITelemetryDeviceSource deviceSource = null,
            bool strictNames = true,
            float sendIntervalSeconds = 0.1f,
            float sampleIntervalSeconds = 0.04f)
        {
            var options = new ClientTelemetryOptions(sendIntervalSeconds, sampleIntervalSeconds, 300, true, double.MaxValue);
            var guard = new TelemetryNameGuard(strictNames);
            var names = new TelemetryNameTable(guard);
            var buffer = new TelemetryBuffer(options);
            var writer = new TelemetryBatchWriter(names, buffer);
            var visibility = new TelemetryVisibility(Page);
            var clock = new TelemetryClock();
            var sampler = new TelemetrySampler(new ITelemetryCollector[] { Collector }, names, buffer, clock, visibility);
            var gate = new TelemetryRequestGate();
            var source = deviceSource ?? new TelemetryFixedDevice(device ?? new TelemetryDevice(platform: "editor"));

            Errors = new TelemetryErrors(buffer, clock);
            ErrorQueue = new TelemetryErrorQueue();
            Transport = new RecordingSocketTransport(new TelemetrySocketTransport(this, new TelemetryTicks(), new TelemetryRoundTrip()));

            var collection = new TelemetryCollection(
                buffer,
                names,
                sampler,
                writer,
                clock,
                visibility,
                new ITelemetryLiveCollector[]
                {
                    new TelemetryLogErrors(ErrorQueue),
                    new TelemetryErrorPump(ErrorQueue, new TelemetryTextCleaner(), new TelemetryErrorGroups(Errors, clock), options)
                });

            var startup = new TelemetryStartupReport(
                options,
                StartupSource,
                Transport,
                gate,
                source,
                new TelemetryStartupPage(new NoTelemetryPageTimeline(), StartupSource));

            startup.OnSetup(_lifetime);
            Telemetry = new ClientTelemetry(options, guard, buffer, clock, collection, writer, Transport, source, gate, startup, new TelemetryAddedCollectors());
        }

        private readonly Lifetime _lifetime = new Lifetime();

        private NetworkConnection _connection;
        private Lifetime _run;
        private string _url;

        public INetworkConnection Connection => _connection;

        public TelemetryScriptedGauges Collector { get; } = new TelemetryScriptedGauges();
        public TelemetryTestPage Page { get; } = new TelemetryTestPage();
        public TelemetryScriptedStartup StartupSource { get; } = new TelemetryScriptedStartup();
        public RecordingSocketTransport Transport { get; }
        public TelemetryErrors Errors { get; }
        public TelemetryErrorQueue ErrorQueue { get; }
        public ClientTelemetry Telemetry { get; }

        // A new connection: what a game gets from a new meta scope.
        public async UniTask Connect(string route = TelemetryTransportHost.MetaRoute)
        {
            _run?.Terminate();
            _connection = new NetworkConnection(new NetworkCommandsCollection(), new TelemetryNoOutdatedNotice(), new TelemetryEditorSockets(), new NetworkProtocolHash("e2e"));
            _run = new Lifetime();
            _url = TelemetryTransportHost.Socket(route);

            await _connection.Run(_run, _url);
        }

        // The same connection run again: what the meta socket does after a loss.
        public async UniTask Reconnect()
        {
            _run.Terminate();
            _run = new Lifetime();

            await _connection.Run(_run, _url);
        }

        public void Dispose()
        {
            _run?.Terminate();
            _lifetime.Terminate();
        }
    }
}
