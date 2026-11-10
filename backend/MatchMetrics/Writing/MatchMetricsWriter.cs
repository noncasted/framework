using Internal;
using Microsoft.Extensions.Logging;

namespace Game.MatchMetrics;

internal sealed class MatchMetricsWriter : IMatchMetricsWriter
{
    public MatchMetricsWriter(
        MatchMetricStreamStore streams,
        MatchMetricBatchWriter writer,
        MatchMetricsOptions options,
        TimeProvider time,
        ILogger<MatchMetricsServerStream> logger)
    {
        _streams = streams;
        _writer = writer;
        _options = options;
        _time = time;
        _logger = logger;
    }

    private readonly MatchMetricStreamStore _streams;
    private readonly MatchMetricBatchWriter _writer;
    private readonly MatchMetricsOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<MatchMetricsServerStream> _logger;

    public IMatchMetricsServerStream Open(IReadOnlyLifetime lifetime, Guid matchId)
    {
        var stream = new MatchMetricsServerStream(matchId, _streams, _writer, _time, _logger);
        stream.Start(lifetime, _options.ServerFlush);

        return stream;
    }
}
