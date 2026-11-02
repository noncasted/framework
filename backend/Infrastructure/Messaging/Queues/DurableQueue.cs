using Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IDurableQueue : IGrainWithStringKey
{
    Task AddObserver(Guid id, IDurableQueueObserver observer);
    Task RemoveObserver(Guid id);
    // Delivers the message to every subscribed observer and fails while one of them has not got it. A retry with the
    // same messageId (DurableQueueSideEffect) goes only to the observers that have not got it yet: a failed observer
    // gets the message once it resubscribes, the others do not get it again. The ids live in the activation memory,
    // so a reactivation between the retries delivers to everyone again: at least once per observer.
    Task Push(object message, Guid correlationId = default, Guid messageId = default);
}

public class DurableQueue : Grain, IDurableQueue
{
    public DurableQueue(ILogger<DurableQueue> logger, IDurableQueueConfig config)
    {
        _logger = logger;
        _config = config;
    }

    private readonly ILogger<DurableQueue> _logger;
    private readonly IDurableQueueConfig _config;

    private static readonly TimeSpan DeliveredRetention = TimeSpan.FromHours(1);

    private readonly Dictionary<Guid, ObserverData> _observers = new();

    // messageId → the observers that got a message whose push failed for some other observer, until it is retried.
    private readonly Dictionary<Guid, DeliveredData> _delivered = new();

    public Task AddObserver(Guid id, IDurableQueueObserver observer)
    {
        if (_observers.TryGetValue(id, out var data) == false)
        {
            data = new ObserverData(id, observer);
            _observers[id] = data;
        }

        data.Refresh(observer);

        // Observers live only in this activation's memory and refresh themselves periodically (resubscribe loop):
        // keep the activation between refreshes. This must happen here, DelayDeactivation called from
        // OnDeactivateAsync does not stop a deactivation that has already started.
        DelayDeactivation(TimeSpan.FromMinutes(_config.Value.ObserverKeepAliveMinutes));

        return Task.CompletedTask;
    }

    public Task RemoveObserver(Guid id)
    {
        _observers.Remove(id);
        return Task.CompletedTask;
    }

    public async Task Push(object message, Guid correlationId = default, Guid messageId = default)
    {
        var queueName = this.GetPrimaryKeyString();
        using var activity = TraceExtensions.MessagingDurableQueue.StartActivity("DurableQueue.Push");
        activity?.SetTag("messaging.queue", queueName);
        activity?.SetTag("message.type", message.GetType().Name);
        activity?.SetTag("observer.count", _observers.Count);

        if (correlationId != Guid.Empty)
            activity?.SetTag("messaging.correlation_id", correlationId);

        BackendMetrics.DurableQueuePushed.Add(1);
        BackendMetrics.DurableQueueObserverCount.Record(_observers.Count);

        if (_observers.Count == 0)
        {
            BackendMetrics.DurableQueueNoSubscribers.Add(1);

            _logger.LogWarning(
                "[Messaging] [DurableQueue] No active subscribers for queue '{QueueName}' (correlation {CorrelationId})",
                queueName, correlationId);

            throw new InvalidOperationException(
                $"No active subscribers for durable queue '{queueName}'. Message left in processing for requeue.");
        }

        PruneDelivered();

        // A message without an id (a direct push) is not retried by a side effect: nothing to remember.
        var delivered = messageId != Guid.Empty && _delivered.TryGetValue(messageId, out var data)
            ? data.Observers
            : new HashSet<Guid>();

        var targets = _observers.Values.Where(observer => delivered.Contains(observer.Id) == false).ToArray();

        // In parallel: an observer on a dead silo holds the grain for one call timeout, not one per dead observer.
        var results = await Task.WhenAll(targets.Select(async observer => {
            try
            {
                await observer.Observer.Send(message);
                return (Observer: observer, Error: (Exception?)null);
            }
            catch (Exception e)
            {
                return (Observer: observer, Error: e);
            }
        }));

        var failedCount = 0;

        foreach (var (observer, error) in results)
        {
            if (error == null)
            {
                delivered.Add(observer.Id);
                continue;
            }

            failedCount++;
            _observers.Remove(observer.Id);
            BackendMetrics.DurableQueueDeliveryFailure.Add(1);

            _logger.LogError(error,
                "[Messaging] [DurableQueue] Delivering message from {QueueName} to observer {ObserverId} failed (correlation {CorrelationId})",
                queueName, observer.Id, correlationId);
        }

        activity?.SetTag("observer.success_count", targets.Length - failedCount);
        activity?.SetTag("observer.failure_count", failedCount);

        if (failedCount == 0)
        {
            _delivered.Remove(messageId);
            return;
        }

        if (messageId != Guid.Empty)
        {
            _delivered[messageId] = new DeliveredData
            {
                Observers = delivered,
                Until = DateTime.UtcNow + DeliveredRetention
            };
        }

        BackendMetrics.DurableQueueNoSubscribers.Add(1);

        _logger.LogWarning(
            "[Messaging] [DurableQueue] Delivered message from {QueueName} to {SuccessCount}/{TargetCount} observers; removed {FailedCount} failed observers, the retry goes to the rest (correlation {CorrelationId})",
            queueName, targets.Length - failedCount, targets.Length, failedCount, correlationId);

        throw new InvalidOperationException(
            $"{failedCount} of {targets.Length} observers of durable queue '{queueName}' failed. Message left in processing for requeue.");
    }

    private void PruneDelivered()
    {
        var now = DateTime.UtcNow;

        foreach (var (id, data) in _delivered.ToArray())
        {
            if (data.Until < now)
                _delivered.Remove(id);
        }
    }

    private class DeliveredData
    {
        public required HashSet<Guid> Observers { get; init; }
        public required DateTime Until { get; init; }
    }

    public class ObserverData
    {
        public ObserverData(Guid id, IDurableQueueObserver observer)
        {
            Id = id;
            Observer = observer;
        }

        public Guid Id { get; }
        public IDurableQueueObserver Observer { get; private set; }

        public void Refresh(IDurableQueueObserver observer)
        {
            Observer = observer;
        }
    }
}
