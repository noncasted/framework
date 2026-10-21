using Cluster.Discovery;
using Common.Extensions;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cluster.Deploy;

// Deployment state assembled from per-service parts: every service owns the slice under its service id
// and writes only it, so two gateways never overwrite each other's data.
public interface IServiceSlices<TSlice> where TSlice : class
{
    Dictionary<Guid, TSlice> Slices { get; }
}

public interface IServiceSlicedState<TData, TSlice> : IDeploymentState<TData>
    where TData : class, IServiceSlices<TSlice>, new()
    where TSlice : class
{
    // Replaces the slice of this service. Calls that overlap are coalesced: the latest slice wins.
    Task SetSlice(TSlice slice);
}

public class ServiceSlicedState<TData, TSlice> : DeploymentState<TData>, IServiceSlicedState<TData, TSlice>
    where TData : class, IServiceSlices<TSlice>, new()
    where TSlice : class
{
    public ServiceSlicedState(
        IOrleans orleans,
        IMessaging messaging,
        ILoggerFactory loggerFactory,
        IServiceEnvironment environment,
        TData baseValue) : base(orleans, messaging, loggerFactory, baseValue)
    {
        _serviceId = environment.ServiceId;
    }

    private readonly Guid _serviceId;
    private readonly SemaphoreSlim _sliceLock = new(1, 1);
    private readonly Lock _pendingLock = new();
    private TSlice? _pending;

    // A whole-value write from one service would drop the slices of the others.
    public override Task SetValue(TData value)
    {
        throw new NotSupportedException($"{typeof(TData).Name} is changed only through SetSlice");
    }

    public async Task SetSlice(TSlice slice)
    {
        lock (_pendingLock)
            _pending = slice;

        await _sliceLock.WaitAsync();

        try
        {
            TSlice? next;

            lock (_pendingLock)
            {
                next = _pending;
                _pending = null;
            }

            // A call that came later took this slice (or a newer one) while this call waited for the lock.
            if (next == null)
                return;

            if (IsInitialized == false)
                throw new InvalidOperationException($"{typeof(TData).Name} is not attached to a deploy yet");

            var deployId = _deployId;
            var writer = _orleans.GetGrain<IServiceSlicesWriter<TData, TSlice>>(deployId);
            var state = await writer.SetSlice(_serviceId, next);

            // The epoch changed while the write was in flight: the result belongs to the old deploy row.
            if (_deployId != deployId)
                return;

            // The same state also comes back through the channel; whichever arrives first is applied.
            TryApplyUpdate(state);
        }
        finally
        {
            _sliceLock.Release();
        }
    }
}

public interface IServiceSlicesWriter<TData, TSlice> : IGrainWithGuidKey
    where TData : class, IServiceSlices<TSlice>, new()
    where TSlice : class
{
    Task<AddressableStateValue> SetSlice(Guid serviceId, TSlice slice);
}

// Keyed by deploy id. A non-reentrant grain serializes read-modify-write of the deploy row across services,
// like ClusterFeaturesWriter.
public class ServiceSlicesWriter<TData, TSlice> : Grain, IServiceSlicesWriter<TData, TSlice>
    where TData : class, IServiceSlices<TSlice>, new()
    where TSlice : class
{
    public ServiceSlicesWriter(IOrleans orleans, IMessaging messaging)
    {
        _orleans = orleans;
        _messaging = messaging;
    }

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;

    public async Task<AddressableStateValue> SetSlice(Guid serviceId, TSlice slice)
    {
        var deployId = this.GetPrimaryKey();
        var identity = DeploymentState<TData>.CreateDefaultStateIdentity(deployId);

        // Read the row on every call: the activation may have moved to another silo since the last write.
        var stored = await _orleans.StateStorage.Read<AddressableStateValue>(identity);

        var value = stored.IsInitialized
            ? _orleans.Serializer.Deserialize<TData>(stored.Value)
            : new TData();

        // A service that stopped without clearing its slice (crash) would stay in the data for the whole deploy:
        // drop slices of services that are no longer alive in discovery.
        var members = await _orleans.GetGrain<IServiceDiscoveryStorage>(deployId).Get();
        var aliveSince = DateTime.UtcNow - ServiceDiscoveryStorage.StaleThreshold;

        foreach (var id in value.Slices.Keys.ToList())
        {
            if (id == serviceId)
                continue;

            if (members.TryGetValue(id, out var member) == false || member.UpdateTime < aliveSince)
                value.Slices.Remove(id);
        }

        value.Slices[serviceId] = slice;

        // Readers drop updates not newer than the last applied one: keep UpdateDate strictly growing
        // even if this silo's clock is behind the one that wrote the previous value.
        var now = DateTime.UtcNow;

        var next = new AddressableStateValue
        {
            IsInitialized = true,
            UpdateDate = now > stored.UpdateDate ? now : stored.UpdateDate.AddTicks(1),
            Value = _orleans.Serializer.Serialize(value)
        };

        await _orleans.StateStorage.Write(identity, next);
        await _messaging.PublishChannel(new DeploymentStateChannelId<TData>(deployId), next);

        return next;
    }
}

public static class ServiceSlicedStateExtensions
{
    public static IHostApplicationBuilder AddServiceSlicedState<TData, TSlice>(this IHostApplicationBuilder builder)
        where TData : class, IServiceSlices<TSlice>, new()
        where TSlice : class
    {
        builder.Services.AddSingleton(new TData());

        builder.Add<ServiceSlicedState<TData, TSlice>>()
               .As<IServiceSlicedState<TData, TSlice>>()
               .As<IDeploymentState<TData>>()
               .As<IDeployAware>();

        return builder;
    }
}
