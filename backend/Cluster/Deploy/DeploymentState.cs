using Common.Extensions;
using Internal;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Cluster.Deploy;

public interface IDeploymentState<T> : IViewableProperty<T> where T : class, new()
{
    bool IsInitialized { get; }
    Task SetValue(T value);
}

public class DeploymentState<T> : ViewableProperty<T>, IDeploymentState<T>, IDeployAware where T : class, new()
{
    public DeploymentState(
        IOrleans orleans,
        IMessaging messaging,
        ILoggerFactory loggerFactory,
        T baseValue) : base(baseValue)
    {
        _orleans = orleans;
        _messaging = messaging;
        _logger = loggerFactory.CreateLogger(GetType());
    }

    private readonly IMessaging _messaging;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // Values are applied from the loading epoch, own writes and channel deliveries, on different threads.
    // Set is not thread-safe (the listeners and value lifetimes are plain lists): concurrent Sets threw
    // "Collection was modified" and the watermark check-then-set let an older value overwrite a newer one.
    // Taken only around the synchronous apply (watermark + Set + listeners), never across an await.
    private readonly object _applyLock = new();
    private StateIdentity? _identity;
    private DeploymentStateChannelId<T>? _channelId;
    private volatile bool _isInitialized;

    // UpdateDate of the last state applied to Value (from load, SetValue, or a channel
    // update). Channel updates include our own publishes echoed back with messaging
    // latency — without this watermark an old echo rolls Value back and the next
    // read-modify-write (ClusterFeatures.Apply) publishes a state computed from the
    // stale base, clobbering newer flags cluster-wide.
    private DateTime _lastAppliedUpdate;

    protected readonly IOrleans _orleans;
    protected readonly ILogger _logger;

    protected Guid _deployId;

    public bool IsInitialized => _isInitialized;

    protected virtual StateIdentity CreateStateIdentity(Guid deployId)
    {
        return CreateDefaultStateIdentity(deployId);
    }

    public static StateIdentity CreateDefaultStateIdentity(Guid deployId)
    {
        return new StateIdentity
        {
            Key = deployId,
            Type = $"deployment_state_{typeof(T).Name}",
            TableName = "cluster",
            Extension = null
        };
    }

    public virtual async Task OnDeployChanged(Guid newDeployId, IReadOnlyLifetime deployLifetime)
    {
        // The value of the previous epoch is not valid for the new one: readers must not see
        // IsInitialized == true until the new epoch is loaded. A load failure propagates to
        // DeployContext, which ends the epoch and crashes the caller.
        _isInitialized = false;

        _deployId = newDeployId;

        _identity = CreateStateIdentity(newDeployId);
        _channelId = new DeploymentStateChannelId<T>(newDeployId);

        lock (_applyLock)
            _lastAppliedUpdate = default;

        // Listening before the read: an update published while the read runs is not lost, the watermark keeps the
        // newer of the two. A gap of the channel (missed updates) reloads the value from the database.
        var identity = _identity;

        await _messaging.ListenChannel<AddressableStateValue>(deployLifetime, _channelId, OnUpdate,
            () => Load(identity).NoAwait());

        await Load(identity);
        _isInitialized = true;
    }

    private async Task Load(StateIdentity identity)
    {
        var state = await _orleans.StateStorage.Read<AddressableStateValue>(identity);

        if (identity != _identity)
            return;

        if (state.IsInitialized == true)
        {
            TryApplyUpdate(state);
            return;
        }

        // Nothing stored for this epoch and nothing came on its channel: the value of the previous epoch goes.
        lock (_applyLock)
        {
            if (_lastAppliedUpdate == default)
                Set(new T());
        }
    }

    public virtual async Task SetValue(T value)
    {
        await _writeLock.WaitAsync();

        try
        {
            if (_deployId == Guid.Empty || _identity == null || _channelId == null)
                throw new InvalidOperationException("DeploymentState is not initialized with a valid deploy ID.");

            // Later than every value this host has seen, whatever the clock of the host that wrote it: the readers
            // order the values by this date, a write must never lose to the one it replaces.
            var updateDate = DateTime.UtcNow;

            lock (_applyLock)
            {
                if (updateDate <= _lastAppliedUpdate)
                    updateDate = _lastAppliedUpdate.AddTicks(1);
            }

            var state = new AddressableStateValue
            {
                IsInitialized = true,
                UpdateDate = updateDate,
                Value = _orleans.Serializer.Serialize(value)
            };

            // The value becomes visible only once it is in the database: a failed write leaves the old one.
            await _orleans.StateStorage.Write(_identity, state);

            lock (_applyLock)
            {
                if (state.UpdateDate > _lastAppliedUpdate)
                {
                    _lastAppliedUpdate = state.UpdateDate;
                    Set(value);
                }
            }

            await _messaging.PublishChannel(_channelId, state);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[DeploymentState] Failed to persist {Type}: {Value}", typeof(T).Name, value);
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    protected virtual void OnUpdate(AddressableStateValue state)
    {
        TryApplyUpdate(state);
    }

    protected bool TryApplyUpdate(AddressableStateValue state)
    {
        if (state.IsInitialized == false)
            return false;

        lock (_applyLock)
        {
            if (state.UpdateDate <= _lastAppliedUpdate)
            {
                _logger.LogDebug(
                    "[DeploymentState] Skipped stale update for {Type}: sent {Sent:O}, last applied {Applied:O}",
                    typeof(T).Name, state.UpdateDate, _lastAppliedUpdate);
                return false;
            }

            // A value that does not deserialize is a schema mismatch between services: fail the delivery loudly
            // instead of keeping a silently stale value.
            T value;

            try
            {
                value = _orleans.Serializer.Deserialize<T>(state.Value);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    $"[DeploymentState] Failed to deserialize update for {typeof(T).Name} (deploy {_deployId})", e);
            }

            _lastAppliedUpdate = state.UpdateDate;
            Set(value);
            return true;
        }
    }
}
