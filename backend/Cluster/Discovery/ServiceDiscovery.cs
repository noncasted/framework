using System.Collections.Concurrent;
using Cluster.Deploy;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cluster.Discovery;

public interface IServiceDiscovery
{
    IServiceOverview Self { get; }
    IReadOnlyDictionary<Guid, IServiceOverview> Entries { get; }

    Task Start(IReadOnlyLifetime lifetime);
    Task Push();
    Task Unregister();
}

public class ServiceDiscoveryOptions
{
    // The service is a game server: it publishes its public url (GAME_SERVER_URL), matchmaking hands it to clients.
    // Set by the composition root for the game server role (PublishGameServerUrl).
    public bool PublishGameServerUrl { get; set; }
}

public class ServiceDiscovery : IServiceDiscovery
{
    public ServiceDiscovery(
        IOrleans orleans,
        IDeployContext deployContext,
        IServiceEnvironment environment,
        IOptions<ServiceDiscoveryOptions> options,
        ILogger<ServiceDiscovery> logger)
    {
        _orleans = orleans;
        _deployContext = deployContext;
        _environment = environment;
        _publishGameServerUrl = options.Value.PublishGameServerUrl;
        _logger = logger;

        _self = CreateOverview();
    }

    private readonly IOrleans _orleans;
    private readonly IDeployContext _deployContext;
    private readonly IServiceEnvironment _environment;
    private readonly bool _publishGameServerUrl;
    private readonly ILogger<ServiceDiscovery> _logger;
    private readonly ConcurrentDictionary<Guid, IServiceOverview> _entries = new();

    private volatile IServiceOverview _self;

    public IServiceOverview Self => _self;
    public IReadOnlyDictionary<Guid, IServiceOverview> Entries => _entries;

    public Task Start(IReadOnlyLifetime lifetime)
    {
        RefreshLoop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public async Task Push()
    {
        try
        {
            _self = CreateOverview();

            if (_deployContext.DeployId == Guid.Empty)
                return;

            var grain = _orleans.GetGrain<IServiceDiscoveryStorage>(_deployContext.DeployId);
            var members = await grain.Update(_self);

            ApplyMembers(members);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[ServiceDiscovery] Push failed");
        }
    }

    public async Task Unregister()
    {
        try
        {
            if (_deployContext.DeployId == Guid.Empty)
                return;

            var grain = _orleans.GetGrain<IServiceDiscoveryStorage>(_deployContext.DeployId);
            await grain.Unregister(_environment.ServiceId);
            _logger.LogInformation("[ServiceDiscovery] Unregistered service {ServiceId}", _environment.ServiceId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[ServiceDiscovery] Unregister failed");
        }
    }

    private async Task RefreshLoop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            try
            {
                await Push();
                await Task.Delay(TimeSpan.FromSeconds(2), lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                await Unregister();
                break;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[ServiceDiscovery] RefreshLoop iteration failed");
                await Task.Delay(TimeSpan.FromSeconds(5), lifetime.Token);
            }
        }
    }

    private void ApplyMembers(Dictionary<Guid, IServiceOverview> members)
    {
        foreach (var (id, overview) in members)
            _entries[id] = overview;

        foreach (var existingId in _entries.Keys.ToList())
        {
            if (members.ContainsKey(existingId) == false)
                _entries.TryRemove(existingId, out _);
        }
    }

    private IServiceOverview CreateOverview()
    {
        // Game servers publish their public url (matchmaking hands it to clients); other roles only their presence.
        if (_publishGameServerUrl == true)
        {
            return new GameServerOverview
            {
                Id = _environment.ServiceId,
                Tag = _environment.Tag,
                UpdateTime = DateTime.UtcNow,
                Url = GetGameServerUrl(),
            };
        }

        return new ServiceOverview
        {
            Id = _environment.ServiceId,
            Tag = _environment.Tag,
            UpdateTime = DateTime.UtcNow,
        };

        string GetGameServerUrl()
        {
            var url = Environment.GetEnvironmentVariable("GAME_SERVER_URL");

            if (url == null)
                throw new InvalidOperationException("GAME_SERVER_URL environment variable is not set");

            return url;
        }
    }
}

public static class ServiceDiscoveryExtensions
{
    public static IHostApplicationBuilder AddServiceDiscovery(this IHostApplicationBuilder builder)
    {
        builder.Add<ServiceDiscovery>()
               .As<IServiceDiscovery>();

        return builder;
    }

    // Marks the host as a game server (ServiceDiscoveryOptions.PublishGameServerUrl).
    public static IHostApplicationBuilder PublishGameServerUrl(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<ServiceDiscoveryOptions>(options => options.PublishGameServerUrl = true);
        return builder;
    }
}
