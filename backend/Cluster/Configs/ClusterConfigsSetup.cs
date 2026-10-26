using Common.Extensions;
using Internal;
using Infrastructure;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging;

namespace Cluster.Configs;

public class ClusterConfigsSetup : ICoordinatorSetupCompleted
{
    public ClusterConfigsSetup(
        ISideEffectsConfig sideEffects,
        IDurableQueueConfig messageQueue,
        ITaskBalancerConfig taskBalancer,
        IRuntimePipeConfig runtimePipe,
        IRuntimeChannelConfig runtimeChannel,
        ITransactionConfig transactionConfig,
        ILogger<ClusterConfigsSetup> logger)
    {
        _sideEffects = sideEffects;
        _durableQueue = messageQueue;
        _taskBalancer = taskBalancer;
        _runtimePipe = runtimePipe;
        _runtimeChannel = runtimeChannel;
        _transactionConfig = transactionConfig;
        _logger = logger;
    }

    private readonly ISideEffectsConfig _sideEffects;
    private readonly IDurableQueueConfig _durableQueue;
    private readonly ITaskBalancerConfig _taskBalancer;
    private readonly IRuntimePipeConfig _runtimePipe;
    private readonly IRuntimeChannelConfig _runtimeChannel;
    private readonly ITransactionConfig _transactionConfig;
    private readonly ILogger<ClusterConfigsSetup> _logger;

    public async Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        await ConfigsInitializer.Init("config.sideEffects", _sideEffects, _logger);
        await ConfigsInitializer.Init("config.durableQueue", _durableQueue, _logger);
        await ConfigsInitializer.Init("config.taskBalancer", _taskBalancer, _logger);
        await ConfigsInitializer.Init("config.runtimePipe", _runtimePipe, _logger);
        await ConfigsInitializer.Init("config.runtimeChannel", _runtimeChannel, _logger);
        await ConfigsInitializer.Init("config.transaction", _transactionConfig, _logger);
    }
}

// First value of a config state, set once by the coordinator: <jsonPath>.json next to the service binaries when the
// file is there, the default value otherwise. A state that already has a value is left as it is.
public static class ConfigsInitializer
{
    public static Task Init<T>(string jsonPath, IAddressableState<T> storage, ILogger logger) where T : class, new()
    {
        return Init(jsonPath, storage, new T(), logger);
    }

    // No catch: a config that failed to initialize stops the coordinator setup instead of running with the default.
    public static async Task Init<T>(string jsonPath, IAddressableState<T> storage, T defaultValue, ILogger logger)
        where T : class, new()
    {
        if (storage.IsInitialized == true)
            return;

        var configPath = Path.Combine(AppContext.BaseDirectory, $"{jsonPath}.json");

        if (File.Exists(configPath) == false)
        {
            await storage.SetValue(defaultValue);
            return;
        }

        logger.LogInformation("[ConfigsInitializer] Seeding config {JsonPath} from {ConfigPath}", jsonPath, configPath);

        var json = await File.ReadAllTextAsync(configPath);
        var value = JsonUtils.Deserialize<T>(json).ThrowIfNull();
        await storage.SetValue(value);
    }
}
