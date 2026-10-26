namespace Infrastructure;

public interface ICorrelatedSideEffect
{
    Guid CorrelationId { get; }
}

[GenerateSerializer]
public class DurableQueueSideEffect : ISideEffect, ICorrelatedSideEffect
{
    [Id(0)]
    public required string QueueName { get; set; }

    [Id(1)]
    public required object Message { get; set; }

    [Id(2)]
    public Guid CorrelationId { get; set; }

    // Stored with the side effect, the same on every retry: the queue delivers a retry only to the observers that
    // have not got the message yet.
    [Id(3)]
    public Guid MessageId { get; set; } = Guid.NewGuid();

    public Task Execute(IOrleans orleans)
    {
        var queue = orleans.GetGrain<IDurableQueue>(QueueName);
        return queue.Push(Message, CorrelationId, MessageId);
    }
}