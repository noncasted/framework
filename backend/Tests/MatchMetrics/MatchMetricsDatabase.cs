using Common.Extensions;
using Framework.Tests.Fixtures;
using Framework.Tests.Session;
using Game.MatchMetrics;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Framework.Tests.MatchMetrics;

// A database of its own with the tables of the collector, the storage over it, the test admission and a manual clock.
// Every test takes new match ids, so the tests share it.
public sealed class MatchMetricsDatabase : IAsyncLifetime, IDbSource
{
    private readonly DatabaseFixture _database = new();

    public NpgsqlDataSource Value => _database.DataSource;

    public ManualTimeProvider Time { get; } = new();
    public TestMatchAdmission Admission { get; } = new();
    public MatchMetricsOptions Options { get; } = new() { AdmissionRecheck = TimeSpan.FromSeconds(60), CloseGrace = TimeSpan.FromMinutes(2) };

    public MatchMetricStreamStore Streams { get; private set; } = null!;
    public MatchMetricBatchWriter Writer { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _database.InitializeAsync();

        await using var connection = await Value.OpenConnectionAsync();
        await MatchMetricsSchema.Create(connection);

        Streams = new MatchMetricStreamStore(this);
        Writer = new MatchMetricBatchWriter(this, new MatchMetricSeriesDictionary(this));
    }

    public MatchMetricsConnection Connect(Guid userId, ILifetime? lifetime = null)
    {
        var connections = new MatchMetricsConnections(Admission, Streams, Writer, Options, Time, NullLogger<MatchMetricsConnection>.Instance);
        return connections.Of(lifetime ?? new Lifetime(), userId);
    }

    public IMatchMetricsWriter ServerWriter(MatchMetricsOptions options)
    {
        return new MatchMetricsWriter(Streams, Writer, options, TimeProvider.System, NullLogger<MatchMetricsServerStream>.Instance);
    }

    public async Task<T> Scalar<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await Value.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);

        return (T)(await command.ExecuteScalarAsync())!;
    }

    public async ValueTask DisposeAsync()
    {
        await _database.DisposeAsync();
    }
}
