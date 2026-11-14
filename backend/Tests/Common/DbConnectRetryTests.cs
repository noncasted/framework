using System.Diagnostics;
using Common.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace Framework.Tests.Common;

// The deploy setup waits for a database that is still starting (a transient failure), but a failure no retry can fix
// (a wrong connection string, a refused password) ends at once with its own exception.
public class DbConnectRetryTests
{
    [Fact]
    public async Task GetConnection_PermanentFailure_ThrowsAtOnceWithTheCause()
    {
        var configuration = Configuration("NoSuchKeyword=1", attempts: 3, retryDelayMs: 1000);
        var stopwatch = Stopwatch.StartNew();

        await FluentActions.Awaiting(() => configuration.GetConnection()).Should().ThrowAsync<ArgumentException>();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(900));
    }

    [Fact]
    public async Task GetConnection_TransientFailure_RetriesThenThrowsTheLastCause()
    {
        // Nothing listens on port 1: a refused connection is transient.
        var configuration = Configuration("Host=127.0.0.1;Port=1;Timeout=2", attempts: 2, retryDelayMs: 10);

        var failure = await FluentActions.Awaiting(() => configuration.GetConnection()).Should().ThrowAsync<NpgsqlException>();

        failure.Which.IsTransient.Should().BeTrue();
    }

    private static ConfigurationManager Configuration(string connectionString, int attempts, int retryDelayMs)
    {
        var configuration = new ConfigurationManager();

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["postgres"] = connectionString,
            ["PostgresConnect:Attempts"] = attempts.ToString(),
            ["PostgresConnect:RetryDelayMs"] = retryDelayMs.ToString()
        });

        return configuration;
    }
}
