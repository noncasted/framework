using System.Collections.Concurrent;
using Common.Extensions;
using Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IRuntimePipeClient
{
    Task Start(IReadOnlyLifetime lifetime);

    Task<TResponse> Send<TResponse>(IRuntimePipeId id, object message);

    Task<bool> Exists(IRuntimePipeId id);

    Task AddHandler<TRequest, TResponse>(
        IReadOnlyLifetime lifetime,
        IRuntimePipeId id,
        Func<TRequest, Task<TResponse>> listener);
}

public class RuntimePipeClient : IRuntimePipeClient
{
    public RuntimePipeClient(
        IOrleans orleans,
        IServiceProvider services,
        ILogger<RuntimePipeClient> logger)
    {
        _orleans = orleans;
        _sender = new RuntimePipeSender(
            new Lazy<IRuntimePipeConfig>(() => services.GetRequiredService<IRuntimePipeConfig>()),
            logger);
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly RuntimePipeSender _sender;
    private readonly ILogger<RuntimePipeClient> _logger;

    private readonly ConcurrentDictionary<Guid, Listener> _listeners = new();

    public Task Start(IReadOnlyLifetime lifetime)
    {
        ResubscribeLoop.Run(lifetime,
            () => _listeners.Values.ToArray(),
            () => TimeSpan.FromSeconds(10),
            () => ResubscribeLoop.DefaultTimeout).NoAwait();
        return Task.CompletedTask;
    }

    public async Task<TResponse> Send<TResponse>(IRuntimePipeId id, object message)
    {
        return await _sender.Send<TResponse>(id, GetPipe(id), message);
    }

    public Task<bool> Exists(IRuntimePipeId id)
    {
        return GetPipe(id).HasObserver();
    }

    public async Task AddHandler<TRequest, TResponse>(
        IReadOnlyLifetime lifetime,
        IRuntimePipeId id,
        Func<TRequest, Task<TResponse>> listener)
    {
        if (lifetime.IsTerminated)
            return;

        var observer = new RuntimePipeObserver(_logger);
        observer.BindResponseHandler(async message => {
            if (message is not TRequest castedMessage)
                throw new InvalidCastException($"Expected {typeof(TRequest)}, but got {message.GetType()}");

            var response = await listener(castedMessage);

            if (response is null)
                throw new InvalidCastException($"Expected {typeof(TResponse)}, but got null");

            return response;
        });

        await RegisterObserver(lifetime, id, observer);
    }

    private async Task RegisterObserver(IReadOnlyLifetime lifetime, IRuntimePipeId id, RuntimePipeObserver observer)
    {
        var observerId = Guid.NewGuid();
        var observerReference = _orleans.Client.CreateObjectReference<IRuntimePipeObserver>(observer);

        // Taken before the new one is known: of two registrations together the later one stays.
        var replaced = _listeners.ToArray().Where(pair => pair.Value.Id.ToRaw() == id.ToRaw()).ToArray();

        var transportListener = new Listener(
            id,
            observerId,
            observer,
            observerReference,
            GetPipe(id),
            _orleans,
            _logger);

        _listeners[observerId] = transportListener;
        lifetime.Listen(() => RemoveListener(observerId, transportListener));

        try
        {
            await transportListener.Resubscribe();
            transportListener.RecordSuccess();
        }
        catch (Exception e)
        {
            transportListener.RecordFailure(e);
            RemoveListener(observerId, transportListener);
            throw;
        }

        // Only now: a new observer that failed to bind leaves the pipe with the old one.
        foreach (var (replacedId, replacedListener) in replaced)
        {
            _logger.LogWarning("[Messaging] [RuntimePipe] Removing duplicate observer {ObserverId} for pipe {PipeId}",
                replacedListener.ObserverId, id.ToRaw());
            RemoveListener(replacedId, replacedListener);
        }
    }

    private void RemoveListener(Guid listenerId, Listener listener)
    {
        var listeners = (ICollection<KeyValuePair<Guid, Listener>>)_listeners;

        if (listeners.Remove(new KeyValuePair<Guid, Listener>(listenerId, listener)))
            listener.Cleanup();
    }

    private IRuntimePipe GetPipe(IRuntimePipeId id)
    {
        var rawId = id.ToRaw();
        return _orleans.GetGrain<IRuntimePipe>(rawId);
    }

    public class Listener : IResubscribingListener
    {
        public Listener(
            IRuntimePipeId id,
            Guid observerId,
            RuntimePipeObserver observerSource,
            IRuntimePipeObserver observer,
            IRuntimePipe pipe,
            IOrleans orleans,
            ILogger logger)
        {
            Id = id;
            ObserverId = observerId;
            ObserverSource = observerSource;
            Observer = observer;
            Pipe = pipe;
            Orleans = orleans;
            Logger = logger;
            _failures = new ResubscribeFailures(
                new AdaptiveInterval(minInterval: TimeSpan.FromSeconds(10),
                    maxInterval: TimeSpan.FromSeconds(60),
                    failureBaseInterval: TimeSpan.FromSeconds(1)),
                LogResubscribeFailure);
        }

        private readonly ResubscribeFailures _failures;

        private int _cleanupStarted;

        public IRuntimePipeId Id { get; }
        public Guid ObserverId { get; }
        public RuntimePipeObserver ObserverSource { get; }
        public IRuntimePipeObserver Observer { get; }
        public IRuntimePipe Pipe { get; }
        public IOrleans Orleans { get; }
        public ILogger Logger { get; }

        public AdaptiveInterval Interval => _failures.Interval;

        public async Task Resubscribe()
        {
            await Pipe.BindObserver(ObserverId, Observer);
        }

        public void RecordSuccess()
        {
            _failures.RecordSuccess();
        }

        public void RecordFailure(Exception e)
        {
            _failures.RecordFailure(e);
        }

        public void Cleanup()
        {
            if (Interlocked.Exchange(ref _cleanupStarted, 1) == 1)
                return;

            CleanupTransport().NoAwait();
        }

        private void LogResubscribeFailure(Exception e, int attempt)
        {
            Logger.LogError(e,
                "[Messaging] [RuntimePipe] Failed to rebind observer {ObserverId} (attempt {Count}) to pipe {PipeId}",
                ObserverId, attempt, Id.ToRaw());
        }

        private async Task CleanupTransport()
        {
            ObserverSource.ClearResponseHandler();

            try
            {
                await Pipe.UnbindObserver(ObserverId);
            }
            catch (Exception e)
            {
                Logger.LogWarning(e,
                    "[Messaging] [RuntimePipe] Failed to unbind observer {ObserverId} from pipe {PipeId}",
                    ObserverId, Id.ToRaw());
            }

            try
            {
                Orleans.Client.DeleteObjectReference<IRuntimePipeObserver>(Observer);
            }
            catch (Exception e)
            {
                Logger.LogWarning(e,
                    "[Messaging] [RuntimePipe] Failed to delete object reference for observer {ObserverId} on pipe {PipeId}",
                    ObserverId, Id.ToRaw());
            }
        }
    }
}
