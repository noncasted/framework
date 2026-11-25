using System.Net.Sockets;
using Cluster.Discovery;
using Cluster.Monolith;
using Common.Extensions;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Framework.Tests.Cluster.Monolith;

// Two hosts of a monolith on their own ports and content roots; the first host that fails or stops by itself stops
// the other and the process with its failure; the cancellation of the caller stops the process normally. The runner replaces the process-wide NoAwait handler, so the tests run alone.
[Collection(nameof(NoAwaitCollection))]
public class MonolithRunnerTests : IDisposable
{
    private static readonly ServiceTag First = new("First");
    private static readonly ServiceTag Second = new("Second");

    private readonly string _root = Directory.CreateTempSubdirectory("monolith-runner-").FullName;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public void Dispose()
    {
        _http.Dispose();
        Directory.Delete(_root, true);
    }

    [Fact]
    public async Task TwoHosts_ServeOnTheirPortsWithTheirRolesAndContentRoots()
    {
        var ports = (First: FreePort(), Second: FreePort());
        var handlers = new List<Action<Exception>?>();
        var runner = Runner(Configuration(ports.First, ports.Second), host => {
            // Hosted services start after the runner installed its handler.
            host.Services.AddHostedService(_ => new StartProbe(() => {
                lock (handlers)
                    handlers.Add(NoAwaitExceptions.Handler);
            }));
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var previous = NoAwaitExceptions.Handler;

        var run = runner.Run(cancellation.Token);

        var first = await GetWhenUp(ports.First);
        var second = await GetWhenUp(ports.Second);
        cancellation.Cancel();
        await run;

        // MonolithHostLifetime: the runner owns the signals, no host stops itself on SIGTERM.
        first.Should().Be($"first|{Path.Combine(_root, "First")}|True|MonolithHostLifetime");
        second.Should().Be($"second|{Path.Combine(_root, "Second")}|True|MonolithHostLifetime");
        handlers.Should().HaveCount(2).And.OnlyContain(handler => handler != null && handler != previous);
        NoAwaitExceptions.Handler.Should().Be(previous, "the runner puts the previous handler back when it ends");
    }

    [Fact]
    public async Task OneHostFails_StopsTheOtherAndThrowsItsFailure()
    {
        var ports = (First: FreePort(), Second: FreePort());
        var firstStopped = new TaskCompletionSource();
        var runner = Runner(Configuration(ports.First, ports.Second), host => {
            if (host.Environment.ApplicationName == "Second")
                host.Services.AddHostedService(_ => new FailingStart());
        }, app => {
            if (app.Environment.ApplicationName == "First")
                app.Lifetime.ApplicationStopped.Register(() => firstStopped.TrySetResult());
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var run = () => runner.Run(cancellation.Token);

        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("second failed");
        firstStopped.Task.IsCompleted.Should().BeTrue("the failure of one host stops the other");
        cancellation.IsCancellationRequested.Should().BeFalse("the runner stopped by itself, not by the timeout");
    }

    [Fact]
    public async Task OneHostStopsByItself_StopsTheOtherAndThrows()
    {
        var ports = (First: FreePort(), Second: FreePort());
        var firstStopped = new TaskCompletionSource();
        var runner = Runner(Configuration(ports.First, ports.Second), host => {
            if (host.Environment.ApplicationName == "Second")
                host.Services.AddHostedService(services => new StopAfterStart(
                    services.GetRequiredService<IHostApplicationLifetime>()));
        }, app => {
            if (app.Environment.ApplicationName == "First")
                app.Lifetime.ApplicationStopped.Register(() => firstStopped.TrySetResult());
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var run = () => runner.Run(cancellation.Token);

        // A stop nobody asked for (a host giving up its deploy) is a failure of the process, not a normal exit.
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("host Second stopped by itself");
        firstStopped.Task.IsCompleted.Should().BeTrue("a host that stops by itself stops the other");
        cancellation.IsCancellationRequested.Should().BeFalse("the runner stopped by itself, not by the timeout");
    }

    [Fact]
    public async Task PortMissing_Throws()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Monolith:Hosts:First:ContentRoot"] = Path.Combine(_root, "First")
        }).Build();
        Directory.CreateDirectory(Path.Combine(_root, "First"));
        var runner = new MonolithRunner(configuration, [Host(First, _ => { }, _ => { })]);

        var run = () => runner.Run(CancellationToken.None);

        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Monolith:Hosts:First:Port*");
    }

    private IConfiguration Configuration(int firstPort, int secondPort)
    {
        Directory.CreateDirectory(Path.Combine(_root, "First"));
        Directory.CreateDirectory(Path.Combine(_root, "Second"));

        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SERVICE_NAME"] = "test-monolith",
            ["Monolith:Hosts:First:Port"] = firstPort.ToString(),
            ["Monolith:Hosts:First:ContentRoot"] = Path.Combine(_root, "First"),
            ["Monolith:Hosts:Second:Port"] = secondPort.ToString(),
            ["Monolith:Hosts:Second:ContentRoot"] = Path.Combine(_root, "Second")
        }).Build();
    }

    private static MonolithRunner Runner(
        IConfiguration configuration,
        Action<WebApplicationBuilder> configure,
        Action<WebApplication>? configureApp = null)
    {
        return new MonolithRunner(configuration, [
            Host(First, configure, configureApp ?? (_ => { })),
            Host(Second, configure, configureApp ?? (_ => { }))
        ]);
    }

    private static MonolithHost Host(
        ServiceTag tag,
        Action<WebApplicationBuilder> configure,
        Action<WebApplication> configureApp)
    {
        return new MonolithHost
        {
            Tag = tag,
            ApplicationName = tag.Value,
            Build = options => {
                var builder = WebApplication.CreateBuilder(options);
                configure(builder);
                var app = builder.Build();
                configureApp(app);

                app.MapGet("/probe", (IConfiguration configuration, IWebHostEnvironment environment,
                        IHostLifetime lifetime) =>
                    $"{configuration["SERVICE_NAME"]}|{environment.ContentRootPath.TrimEnd('/')}|" +
                    $"{configuration.GetValue<bool>("Telemetry:ProcessProviders")}|{lifetime.GetType().Name}");

                return app;
            }
        };
    }

    private async Task<string> GetWhenUp(int port)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                return await _http.GetStringAsync($"http://127.0.0.1:{port}/probe");
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
                await Task.Delay(100);
            }
        }

        throw new TimeoutException($"Nothing answers on port {port}");
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }

    private class StartProbe(Action onStart) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            onStart();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private class StopAfterStart(IHostApplicationLifetime lifetime) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            // The other host is starting meanwhile.
            lifetime.ApplicationStarted.Register(() => Task.Delay(500).ContinueWith(_ => lifetime.StopApplication()));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private class FailingStart : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // The other host is starting meanwhile.
            await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None);
            throw new InvalidOperationException("second failed");
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
