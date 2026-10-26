using Infrastructure;
using Infrastructure.State;
using Internal;
using Newtonsoft.Json;

namespace Framework.Tests.Fixtures;

/// <summary>
/// A runtime channel client without Orleans: delivers to the listeners present at the moment of the delivery, as the
/// channel grain does, and reports a gap on demand.
/// </summary>
public class InMemoryChannelClient : IRuntimeChannelClient
{
    private readonly List<(Func<object, Task> Listener, Action? OnGap)> _listeners = [];

    public List<object> Published { get; } = [];

    public Task Start(IReadOnlyLifetime lifetime) => Task.CompletedTask;

    public Task Listen<T>(IReadOnlyLifetime lifetime, IRuntimeChannelId id, Func<T, Task> listener,
        Action? onGapDetected = null)
    {
        _listeners.Add((message => listener((T)message), onGapDetected));
        return Task.CompletedTask;
    }

    public Task Publish(IRuntimeChannelId id, object message)
    {
        Published.Add(message);
        Deliver(message);
        return Task.CompletedTask;
    }

    public void Deliver(object message)
    {
        foreach (var (listener, _) in _listeners.ToArray())
            listener(message).GetAwaiter().GetResult();
    }

    public void ReportGap()
    {
        foreach (var (_, onGap) in _listeners.ToArray())
            onGap?.Invoke();
    }
}

/// <summary>
/// Plain Json.NET in place of StateSerializer, which needs the grain reference activator of a silo.
/// </summary>
public class JsonTestSerializer : IStateSerializer
{
    public string Serialize(object value) => JsonConvert.SerializeObject(value);
    public string Serialize<T>(T value) => JsonConvert.SerializeObject(value);
    public T Deserialize<T>(string value) => JsonConvert.DeserializeObject<T>(value)!;
    public T? TryDeserialize<T>(string value) => JsonConvert.DeserializeObject<T>(value);
    public object? Deserialize(string value, Type type) => JsonConvert.DeserializeObject(value, type);
}
