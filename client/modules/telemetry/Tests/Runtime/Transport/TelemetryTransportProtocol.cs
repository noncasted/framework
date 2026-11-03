using System;
using MemoryPack;
using MemoryPack.Formatters;
using NUnit.Framework;
using Shared;

namespace Global.Telemetry.Tests.Transport
{
    // The union over INetworkContext is declared by a game, and the framework project has none. A generated union here would register
    // itself on load, in every PlayMode run of whatever project compiles these tests, next to the union of that project. So it is a
    // dynamic one: nothing generated, nothing on load; it exists only for the tests of this namespace, and only with the host (no
    // host, no registration). What was registered before comes back at the end. With nothing before it stays until the next domain
    // reload, which in a batchmode run is the end of the process.
    [SetUpFixture]
    public class TelemetryTransportProtocol
    {
        private IMemoryPackFormatter<INetworkContext> _previous;
        private bool _isRegistered;

        [OneTimeSetUp]
        public void Register()
        {
            if (TelemetryTransportHost.IsPresent == false)
                return;

            if (MemoryPackFormatterProvider.IsRegistered<INetworkContext>())
                _previous = Registered();

            MemoryPackFormatterProvider.Register(new DynamicUnionFormatter<INetworkContext>(
                (NetworkContextTags.EmptyResponse, typeof(EmptyResponse)),
                (NetworkContextTags.ErrorResponse, typeof(ErrorResponse)),
                (NetworkContextTags.TelemetrySessionStart, typeof(SharedClientTelemetry.SessionStart)),
                (NetworkContextTags.TelemetrySessionStarted, typeof(SharedClientTelemetry.SessionStarted)),
                (NetworkContextTags.TelemetryBatch, typeof(SharedClientTelemetry.Batch)),
                (NetworkContextTags.TelemetryBatchResult, typeof(SharedClientTelemetry.BatchResult)),
                (NetworkContextTags.TelemetrySessionEnd, typeof(SharedClientTelemetry.SessionEnd)),
                (NetworkContextTags.TelemetryStartupTrace, typeof(SharedClientTelemetry.StartupTrace)),
                (NetworkContextTags.TelemetryStartupTraceResult, typeof(SharedClientTelemetry.StartupTraceResult))));

            _isRegistered = true;
        }

        // The provider gives a formatter out only through a reader.
        private static IMemoryPackFormatter<INetworkContext> Registered()
        {
            using var state = MemoryPackReaderOptionalStatePool.Rent(null);
            var reader = new MemoryPackReader(ReadOnlySpan<byte>.Empty, state);

            return reader.GetFormatter<INetworkContext>();
        }

        [OneTimeTearDown]
        public void Restore()
        {
            if (_isRegistered && _previous != null)
                MemoryPackFormatterProvider.Register((MemoryPackFormatter<INetworkContext>)_previous);
        }
    }
}
