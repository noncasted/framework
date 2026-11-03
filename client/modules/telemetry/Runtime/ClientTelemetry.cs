using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Telemetry
{
    // The root of the module: keeps the one open session and lines the sessions up on the transport.
    public class ClientTelemetry : IClientTelemetry
    {
        public ClientTelemetry(
            ClientTelemetryOptions options,
            TelemetryNameGuard names,
            TelemetryBuffer buffer,
            ITelemetryClock clock,
            TelemetryCollection collection,
            TelemetryBatchWriter writer,
            ITelemetryTransport transport,
            ITelemetryDeviceSource device,
            TelemetryRequestGate gate,
            TelemetryStartupReport startup)
        {
            _options = options;
            _names = names;
            _buffer = buffer;
            _clock = clock;
            _collection = collection;
            _writer = writer;
            _transport = transport;
            _device = device;
            _gate = gate;
            _startup = startup;
        }

        private readonly ClientTelemetryOptions _options;
        private readonly TelemetryNameGuard _names;
        private readonly TelemetryBuffer _buffer;
        private readonly ITelemetryClock _clock;
        private readonly TelemetryCollection _collection;
        private readonly TelemetryBatchWriter _writer;
        private readonly ITelemetryTransport _transport;
        private readonly ITelemetryDeviceSource _device;
        private readonly TelemetryRequestGate _gate;
        private readonly TelemetryStartupReport _startup;

        private TelemetrySession _current;

        public void StartSession(IReadOnlyLifetime lifetime, params string[] tags)
        {
            var accepted = AcceptTags(tags);

            if (_options.IsEnabled == false)
                return;

            var previousSent = UniTask.CompletedTask;

            if (_current != null)
            {
                _current.Close();
                previousSent = _current.Sent;
            }

            _current = new TelemetrySession(_options, _collection, _writer, _transport, _device, _gate, _startup, accepted);
            _current.Open(lifetime, previousSent);
        }

        public void Mark(string name)
        {
            if (_names.AcceptName(name) == false)
                return;

            _buffer.AddMark(_clock.ElapsedMilliseconds, name);
        }

        public void SendStartupTrace()
        {
            _startup.Send();
        }

        private IReadOnlyList<string> AcceptTags(string[] tags)
        {
            var accepted = new List<string>(tags.Length);

            foreach (var tag in tags)
            {
                if (_names.AcceptName(tag) == false)
                    continue;

                if (accepted.Count == TelemetryLimits.MaxTagsPerSession)
                {
                    _names.Reject($"The tag '{tag}': over {TelemetryLimits.MaxTagsPerSession} tags in a session");
                    continue;
                }

                accepted.Add(tag);
            }

            return accepted;
        }
    }
}
