using Common;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Shared;

namespace Framework.Tests.Grains;

// --- Simple state test grain ---

[GenerateSerializer]
[GrainState(Table = "state_simple_test", State = "simple_test", Lookup = "SimpleTest", Key = GrainKeyType.Guid)]
public class SimpleTestState : IDirectStateValue
{
    [Id(0)] [JsonProperty] public int Counter { get; private set; }
    [Id(1)] [JsonProperty] public string Label { get; private set; } = string.Empty;
    public int Version => 0;

    public static SimpleTestState Of(int counter, string label = "")
    {
        return new SimpleTestState { Counter = counter, Label = label };
    }

    public void SetCounter(int value)
    {
        Counter = value;
    }

    public void SetLabel(string label)
    {
        Label = label;
    }
}

public interface ISimpleTestGrain : IGrainWithGuidKey
{
    Task SetCounter(int value);
    Task<int> GetCounter();
    Task SetLabel(string label);
    Task<string> GetLabel();
}

public class SimpleTestGrain : Grain, ISimpleTestGrain
{
    public SimpleTestGrain([State] State<SimpleTestState> state)
    {
        _state = state;
    }

    private readonly State<SimpleTestState> _state;

    public async Task SetCounter(int value)
    {
        await _state.Write(s => s.SetCounter(value));
    }

    public async Task<int> GetCounter()
    {
        return await _state.Read(s => s.Counter);
    }

    public async Task SetLabel(string label)
    {
        await _state.Write(s => s.SetLabel(label));
    }

    public async Task<string> GetLabel()
    {
        return await _state.Read(s => s.Label);
    }
}

// --- Collection test state ---

[GenerateSerializer]
[GrainState(Table = "state_test_collection", State = "collection_test", Lookup = "CollectionTest",
    Key = GrainKeyType.Guid)]
public class CollectionTestState : IDirectStateValue
{
    [Id(0)] [JsonProperty] public Guid Id { get; private set; }
    [Id(1)] [JsonProperty] public string Name { get; private set; } = string.Empty;
    public int Version => 0;

    public void Init(Guid id, string name)
    {
        Id = id;
        Name = name;
    }
}

public interface ICollectionTestGrain : IGrainWithGuidKey
{
    Task SetName(string name);
    Task<string> GetName();
}

public class CollectionTestGrain : Grain, ICollectionTestGrain
{
    public CollectionTestGrain([State] State<CollectionTestState> state)
    {
        _state = state;
    }

    private readonly State<CollectionTestState> _state;

    public async Task SetName(string name)
    {
        await _state.Write(s => s.Init(this.GetGrainId().GetGuidKey(), name));
    }

    public async Task<string> GetName()
    {
        return await _state.Read(s => s.Name);
    }
}

// --- Transaction test grain ---

[GenerateSerializer]
[GrainState(Table = "state_tx_test", State = "tx_test", Lookup = "TxTest", Key = GrainKeyType.Guid)]
public class TxTestState : IDirectStateValue
{
    [Id(0)] [JsonProperty] public int Value { get; private set; }
    public int Version => 0;

    public static TxTestState Of(int value)
    {
        return new TxTestState { Value = value };
    }

    public void SetValue(int value)
    {
        Value = value;
    }

    public void Increment()
    {
        Value += 1;
    }
}

public interface ITxTestGrain : IGrainWithGuidKey
{
    [Transaction]
    Task Increment();

    [Transaction]
    Task IncrementWithDelay(int delayMs);

    Task<int> Get();
    Task Deactivate();
}

[Reentrant]
public class TxTestGrain : Grain, ITxTestGrain
{
    public TxTestGrain([State] State<TxTestState> state)
    {
        _state = state;
    }

    private readonly State<TxTestState> _state;

    public Task Increment()
    {
        return _state.Write(s => s.Increment());
    }

    public async Task IncrementWithDelay(int delayMs)
    {
        await _state.Write(s => s.Increment());
        await Task.Delay(delayMs);
    }

    public async Task<int> Get()
    {
        return await _state.Read(s => s.Value);
    }

    public Task Deactivate()
    {
        DeactivateOnIdle();
        return Task.CompletedTask;
    }
}

// --- Side effect test grain ---

[GenerateSerializer]
public class TestSideEffect : ISideEffect
{
    [Id(0)] public Guid TargetGrainId { get; init; }

    public async Task Execute(IOrleans orleans)
    {
        var grain = orleans.GetGrain<ITxTestGrain>(TargetGrainId);
        var result = await orleans.Transactions.Run(() => grain.Increment());

        if (!result.IsSuccess)
            throw new Exception("Side effect transaction failed");
    }
}

// Transactional side effect — executes inside transaction, deletion atomic with commit
[GenerateSerializer]
public class TransactionalTestSideEffect : ITransactionalSideEffect
{
    [Id(0)] public Guid TargetGrainId { get; init; }

    public async Task Execute(IOrleans orleans)
    {
        var grain = orleans.GetGrain<ITxTestGrain>(TargetGrainId);
        await grain.Increment();
    }
}

// Side effect that fails N times before succeeding
[GenerateSerializer]
public class FailingTestSideEffect : ISideEffect
{
    [Id(0)] public Guid TargetGrainId { get; init; }
    [Id(1)] public int FailCount { get; init; }

    // Static counter shared across executions (keyed by TargetGrainId to avoid cross-test interference)
    internal static readonly object AttemptsLock = new();
    internal static readonly Dictionary<Guid, int> AttemptsDict = new();

    public async Task Execute(IOrleans orleans)
    {
        lock (AttemptsLock)
        {
            AttemptsDict.TryGetValue(TargetGrainId, out var count);
            AttemptsDict[TargetGrainId] = count + 1;

            if (count < FailCount)
                throw new Exception($"Intentional failure {count + 1}/{FailCount}");
        }

        var grain = orleans.GetGrain<ITxTestGrain>(TargetGrainId);
        var result = await orleans.Transactions.Run(() => grain.Increment());

        if (!result.IsSuccess)
            throw new Exception("Side effect transaction failed");
    }

    public static void ResetAttempts()
    {
        lock (AttemptsLock)
        {
            AttemptsDict.Clear();
        }
    }

    public static int GetAttemptCount(Guid targetId)
    {
        lock (AttemptsLock)
        {
            return AttemptsDict.GetValueOrDefault(targetId);
        }
    }
}

// Transactional side effect that fails N times before succeeding (uses shared FailingTestSideEffect counter)
[GenerateSerializer]
public class FailingTransactionalTestSideEffect : ITransactionalSideEffect
{
    [Id(0)] public Guid TargetGrainId { get; init; }
    [Id(1)] public int FailCount { get; init; }

    public async Task Execute(IOrleans orleans)
    {
        lock (FailingTestSideEffect.AttemptsLock)
        {
            FailingTestSideEffect.AttemptsDict.TryGetValue(TargetGrainId, out var count);
            FailingTestSideEffect.AttemptsDict[TargetGrainId] = count + 1;

            if (count < FailCount)
                throw new Exception($"Intentional transactional failure {count + 1}/{FailCount}");
        }

        var grain = orleans.GetGrain<ITxTestGrain>(TargetGrainId);
        await grain.Increment();
    }
}

// Side effect that always fails — for max retry tests
[GenerateSerializer]
public class AlwaysFailingSideEffect : ISideEffect
{
    [Id(0)] public Guid TrackingId { get; init; }

    public Task Execute(IOrleans orleans)
    {
        throw new Exception("Always fails");
    }
}

public interface ISideEffectTestGrain : IGrainWithGuidKey
{
    Task RegisterSideEffect(Guid targetId);
}

public class SideEffectTestGrain : Grain, ISideEffectTestGrain
{
    public SideEffectTestGrain(
        [State] State<TxTestState> state,
        ISideEffectsStorage sideEffectsStorage)
    {
        _state = state;
        _sideEffectsStorage = sideEffectsStorage;
    }

    private readonly State<TxTestState> _state;
    private readonly ISideEffectsStorage _sideEffectsStorage;

    public async Task RegisterSideEffect(Guid targetId)
    {
        await _sideEffectsStorage.Write(new TestSideEffect { TargetGrainId = targetId });
    }
}

// --- Grain that registers side effects within a transaction via AddToTransaction ---

public interface ITxSideEffectGrain : IGrainWithGuidKey
{
    [Transaction]
    Task IncrementAndRegisterSideEffect(Guid sideEffectTargetId);

    [Transaction]
    Task RegisterMultipleSideEffects(IReadOnlyList<Guid> targetIds);

    [Transaction]
    Task<int> IncrementAndReturn();

    Task<int> Get();
}

[Reentrant]
public class TxSideEffectGrain : Grain, ITxSideEffectGrain
{
    public TxSideEffectGrain([State] State<TxTestState> state)
    {
        _state = state;
    }

    private readonly State<TxTestState> _state;

    public async Task IncrementAndRegisterSideEffect(Guid sideEffectTargetId)
    {
        await _state.Write(s => s.Increment());
        new TestSideEffect { TargetGrainId = sideEffectTargetId }.AddToTransaction();
    }

    public async Task RegisterMultipleSideEffects(IReadOnlyList<Guid> targetIds)
    {
        await _state.Write(s => s.Increment());

        foreach (var targetId in targetIds)
            new TestSideEffect { TargetGrainId = targetId }.AddToTransaction();
    }

    public async Task<int> IncrementAndReturn()
    {
        var state = await _state.Update(s => s.Increment());
        return state.Value;
    }

    public async Task<int> Get()
    {
        return await _state.Read(s => s.Value);
    }
}
// --- Event-sourced test grain ---

[GenerateSerializer]
[GrainEventState(State = "event_test", Lookup = "EventTest", Key = GrainKeyType.Guid)]
public class EventTestAggregate : IEventStateValue
{
    [Id(0)] [JsonProperty] public string Id { get; private set; } = string.Empty;
    [Id(1)] [JsonProperty] public int Counter { get; private set; }
    [Id(2)] [JsonProperty] public string Label { get; private set; } = string.Empty;
    public int Version => 0;

    public void Apply(CounterIncremented e) => Counter += e.Amount;
    public void Apply(LabelChanged e) => Label = e.Label;
}

public class CounterIncremented
{
    public int Amount { get; init; }
}

public class LabelChanged
{
    public string Label { get; init; } = string.Empty;
}

public class BadEvent
{
    public string Data { get; init; } = string.Empty;
}

public class NoApplyEvent
{
    public int Value { get; init; }
}

public interface IEventTestGrain : IGrainWithGuidKey
{
    [Transaction]
    Task AppendEvents(int counterAmount, string label);

    [Transaction]
    Task AppendThenFail(int counterAmount);

    Task AppendEventsStandalone(int counterAmount, string label);


    Task AppendWithoutRead();
    Task AppendNoApplyEvent();
    Task WriteWithoutPending();
    Task AppendTwiceInTransaction();
    Task<string> GetStreamId();
    Task<int> GetCounter();
    Task<EventTestAggregate> GetAggregate();
}

[Reentrant]
public class EventTestGrain : Grain, IEventTestGrain
{
    public EventTestGrain(
        [EventState] EventState<EventTestAggregate> state)
    {
        _state = state;
    }

    private readonly EventState<EventTestAggregate> _state;

    public async Task AppendEvents(int counterAmount, string label)
    {
        await _state.Read();
        await _state.Append(
            new CounterIncremented { Amount = counterAmount },
            new LabelChanged { Label = label }
        );
        await _state.Write();
    }

    public async Task AppendThenFail(int counterAmount)
    {
        await _state.Read();
        await _state.Append(new CounterIncremented { Amount = counterAmount });
        await _state.Write();
        throw new Exception("Intentional failure after append");
    }

    public async Task AppendEventsStandalone(int counterAmount, string label)
    {
        await _state.Read();
        await _state.Append(
            new CounterIncremented { Amount = counterAmount },
            new LabelChanged { Label = label }
        );
        await _state.Write();
    }

    public async Task AppendWithoutRead()
    {
        await _state.Append(new CounterIncremented { Amount = 1 });
    }

    public async Task AppendNoApplyEvent()
    {
        await _state.Read();
        await _state.Append(new NoApplyEvent { Value = 42 });
    }

    public async Task WriteWithoutPending()
    {
        await _state.Read();
        await _state.Write();
    }

    public async Task AppendTwiceInTransaction()
    {
        await _state.Read();
        await _state.Append(new CounterIncremented { Amount = 1 });
        await _state.Write();
        await _state.Append(new CounterIncremented { Amount = 2 });
        await _state.Write();
    }

    public Task<string> GetStreamId()
    {
        return Task.FromResult(_state.StreamId);
    }

    public async Task<int> GetCounter()
    {
        await _state.Read();
        return _state.Value.Counter;
    }

    public Task<EventTestAggregate> GetAggregate()
    {
        return _state.Read();
    }
}

// --- Mixed direct + event state grain ---

public interface IMixedStateGrain : IGrainWithGuidKey
{
    [Transaction]
    Task IncrementAndAppend(int amount);

    [Transaction]
    Task IncrementAndAppendThenFail(int amount);

    Task<int> GetDirectValue();
    Task<string> GetStreamId();
}

[Reentrant]
public class MixedStateGrain : Grain, IMixedStateGrain
{
    public MixedStateGrain(
        [State] State<TxTestState> directState,
        [EventState] EventState<EventTestAggregate> eventState)
    {
        _directState = directState;
        _eventState = eventState;
    }

    private readonly State<TxTestState> _directState;
    private readonly EventState<EventTestAggregate> _eventState;

    public async Task IncrementAndAppend(int amount)
    {
        await _directState.Write(s => s.Increment());
        await _eventState.Read();
        await _eventState.Append(new CounterIncremented { Amount = amount });
        await _eventState.Write();
    }

    public async Task IncrementAndAppendThenFail(int amount)
    {
        await _directState.Write(s => s.Increment());
        await _eventState.Read();
        await _eventState.Append(new CounterIncremented { Amount = amount });
        await _eventState.Write();
        throw new Exception("Intentional mixed failure");
    }

    public async Task<int> GetDirectValue()
    {
        return await _directState.Read(s => s.Value);
    }

    public Task<string> GetStreamId()
    {
        return Task.FromResult(_eventState.StreamId);
    }
}
