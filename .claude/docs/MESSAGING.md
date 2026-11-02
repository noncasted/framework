# Backend Messaging: Delivery Semantics

**TL;DR:** `DurableQueue` is "at least one observer got it, at least once"; `RuntimeChannel` is a best-effort ordered broadcast with catch-up; `RuntimePipe` is request/response to one observer. None of them guarantees delivery to every subscriber.

Code: `backend/Infrastructure/Messaging/` (`Queues/`, `Channels/`, `Pipes/`). API usage: `COMMON_ORLEANS.md` (Messaging).

---

## Common model

Each transport is a grain keyed by the id string (`IDurableQueueId` / `IRuntimeChannelId` / `IRuntimePipeId`). A process subscribes by registering a grain observer (`AddObserver` / `BindObserver`). What an observer stands for differs by transport:

- `DurableQueue`: **one observer per id per process**, fanning the message out to the local handlers.
- `RuntimeChannel`: **one observer per `Listen` call**, so a slow handler delays only itself (several per id per process are normal).
- `RuntimePipe`: **one handler per id per process**: a new one replaces the previous one once it has bound; if it fails to bind, the previous one stays.

- Observers are refreshed by the client resubscribe loop (`AdaptiveInterval`, 10-60 s; faster after failures; for channels `ResubscribeMinIntervalMs`, `ResubscribeMaxIntervalMs`). Every listener refreshes on its own: one that hangs does not hold the others, it counts as failed after the timeout (30 s, for channels `ResubscribeTimeoutMs`) and is not called again until its call ends.
- A grain removes an observer whose call failed or timed out. The observer is back after the next resubscribe of its process.
- Grain state (observer list, channel sequence buffer) lives only in the activation. A new activation starts empty; observers re-attach on resubscribe.

### Local handlers of one observer

A channel observer has one handler; a queue observer has as many as the `Listen` calls of the process.

`Listener.Deliver` calls the handlers one by one. The observer call succeeds if **at least one** handler succeeded; a failed handler is logged and does not get the message again. The call fails only when all handlers failed.

---

## DurableQueue

Push path: `PushTransactional` (inside a transaction) / `PushDirect` writes a `DurableQueueSideEffect` into the side-effects tables. The side-effects worker executes it: `IDurableQueue.Push` sends the message to every current observer, **sequentially**.

| Outcome of `Push` | Side effect |
|-------------------|-------------|
| Every current observer accepted | completed, removed from the queue |
| No observers, or one of them failed | `Push` throws, side effect goes to the retry queue (`IncrementalRetryDelay`), after `MaxRetryCount` - dead letter table. The retry goes only to the observers that have not got the message (remembered in the activation for an hour) |

Consequences:
- **Delivered = received by the observers attached at the time.** A failed observer is removed and gets the message on a retry after it resubscribes; an observer attached after the push completed never gets it. Do not use a durable queue as a guaranteed broadcast to several processes.
- **At least once.** A worker crash after `Push` and before completion re-queues the side effect (`RequeueStuck` on coordinator start), and a reactivation between retries delivers to everyone again: handlers must be idempotent.
- **No ordering** between messages: side effects run concurrently (`ConcurrentExecutions`) and retries reorder them.
- Observers of one message are called in parallel; a slow observer holds the push and its side-effect execution slot.

## RuntimeChannel

`Publish` assigns a sequence number, stores the message in a ring buffer (`CatchUpBufferSize`) and puts it into the delivery queue of every observer. `Publish` returns before delivery.

- **Per-observer queues.** Each observer has its own ordered queue and delivery loop: a slow observer delays only itself.
- An observer is removed when a send fails, exceeds `DeliveryTimeoutSeconds`, or its queue overflows (`CatchUpBufferSize` pending messages).
- **Catch-up.** The first bind tells the client where the channel stands (`AddObserver` returns `ChannelPosition`): the history before it is not replayed, a message after it that failed or was lost is. On resubscribe the client calls `CatchUp(lastSeenSequence)` and replays missed messages from the buffer; duplicates are dropped by sequence. If the buffer no longer has them (or the activation was recreated) the client gets `GapDetected` and `onGapDetected` is invoked - the subscriber must reload its state from the source of truth.
- Order is preserved per observer; there is no ordering between different observers.
- Keep-alive: `AddObserver` keeps the activation for `ObserverKeepAliveMinutes`, `Publish` for `DeliveryTimeoutSeconds`. `DelayDeactivation` inside `OnDeactivateAsync` has no effect (deactivation already started), so keep-alive is requested on these calls instead. On deactivation the already queued messages are delivered first.

Use for state broadcasts where the receiver can resync on a gap (`DeploymentState<T>`, live data, configs).

## RuntimePipe

Request/response to a **single** bound observer (the last `BindObserver` wins). `Send` fails if no observer is bound and the sender repeats only that failure; a timeout (`SendTimeoutSeconds`) leaves the observer bound, a transport failure unbinds it; there is no queueing. `HasObserver` pings the observer (500 ms); `Exists` passes a failure of the pipe on to the caller.
Keep-alive: `BindObserver` keeps the activation for `ObserverKeepAliveMinutes` (same for `DurableQueue.AddObserver`), see CLAUDE_MISTAKES F29.

---

## Choosing

| Need | Use |
|------|-----|
| Fan-out of state changes, receiver can resync | RuntimeChannel + `onGapDetected` |
| Command that must survive restarts, processed by any one instance | DurableQueue (idempotent handler) |
| Ask one well-known service and wait for an answer | RuntimePipe |
| Guaranteed delivery to every subscriber | not provided - store the data and let subscribers read it |
