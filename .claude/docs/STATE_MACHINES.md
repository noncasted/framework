# Gameplay State Machines

**TL;DR:** `Internal.StateMachine` (`client/core/Runtime/Common/StateMachines/`) holds only `Current`. States are not registered: a state enters itself and gets an `IStateHandle`. **Rule: every subscription of a state lives on `handle.Lifetime`** (use `property.View(handle, ...)`). Not related to the UI state machine (`IUIStateMachine`).

## API
- `IState`: `Definition`, `Priority`, `IsAvailable()`, `UniTask Enter()`.
- `IStateDefinition` / `ObjectStateDefinition(Guid, name)`: static, e.g. `public static readonly IStateDefinition Idle = new ObjectStateDefinition(Guid.NewGuid(), "Idle");` in `PlayerStateDefinitions`.
- `IStateMachine`: `Initialize(lifetime)`, `IsAvailable(state)` (not current, transitable, `CanInterrupt`), `CreateHandle(state)` (direct entry, no availability check), `TryCreateHandleFromPriorityQueue(state)` (all candidates of one frame, max `Priority` wins), `Exit(state)` (throws `InvalidOperationException` for a non-current state), `Exited` (fires after every exit).
- Entering a new state terminates the previous state's lifetime. `handle.Exit()` is a no-op once the lifetime is terminated.
- Override `protected virtual bool CanInterrupt(IState current, IState next)` to restrict interruptions.

## Entry via priority queue
```csharp
public async UniTask Enter()
{
    var (isSelected, handle) = await _stateMachine.TryCreateHandleFromPriorityQueue(this);
    if (isSelected == false)
        return;

    _input.Direction.View(handle, direction => { if (direction == Vector2.zero) handle.Exit(); });
}
```

## Selector on `Exited`
```csharp
_stateMachine.Initialize(lifetime);
_stateMachine.Exited.Advise(lifetime, SelectNext);
_input.Direction.Advise(lifetime, _ => SelectNext());

void SelectNext()
{
    foreach (var state in _states.OrderByDescending(s => s.Priority))
        if (state.IsAvailable()) { state.Enter().NoAwait(); return; }
}
```

The machine runs where its input is: player - local copy only (writes the network property from `Enter`); weapon - all copies (input is the replicated holder state).

## Remote copies: `RemoteStateMachine`
- A remote copy does not choose states: it enters the one requested by the owner's local state (via a network property).
- `IRemoteState`: `Definition`, `UniTask Enter(IReadOnlyLifetime lifetime, IStatePayload payload)`. No `Priority`, `IsAvailable()`, handles or priority queue.
- `IRemoteStateMachine`: `Initialize(lifetime)`, `Enter(definition, payload)` (finds the state by `Definition` among injected `IReadOnlyList<IRemoteState>`, throws if missing; terminates the previous state's lifetime), `Current`.
- `Shared.IStatePayload` - MemoryPack union (framework `shared/Session/`) for data sent with the request; every implementation declares its union tag next to it (`[MemoryPackUnion(tag, typeof(T))] partial class StatePayloadUnion`, see NETWORK.md). `new EmptyStatePayload()` when there is none.
- Remote states subscribe on the `lifetime` passed to `Enter` and never exit themselves.
- Player: a local state calls `_context.SyncRemote(PlayerStateType.X, payload)` from `Enter` (writes `PlayerStateSync { State, Payload }`); `PlayerRemoteStateSelector` views `State` property -> `_stateMachine.Enter(PlayerStateDefinitions.Get(state.State), state.Payload)`.
- Folder layout: both variants of a state live in its folder (`States/Run/PlayerLocalRun.cs`, `States/Run/PlayerRemoteRun.cs`).
