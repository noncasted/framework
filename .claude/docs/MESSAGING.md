# Backend Messaging: Delivery Semantics

**TL;DR:** `DurableQueue` is "at least one observer got it, at least once"; `RuntimeChannel` is a best-effort ordered broadcast with catch-up; `RuntimePipe` is request/response to one observer. None of them guarantees delivery to every subscriber.

Code: `backend/Infrastructure/Messaging/` (`Queues/`, `Channels/`, `Pipes/`). API usage: `COMMON_ORLEANS.md` (Messaging).

---

## Common model

Each transport is a grain keyed by the id string (`IDurableQueueId` / `IRuntimeChannelId` / `IRuntimePipeId`). A process subscribes by registering a grain observer (`AddObserver` / `BindObserver`). The client side (`*Client.Listener`) keeps **one observer per id per process** and fans the message out to the local handlers.

- Observers are refreshed by the client resubscribe loop (`AdaptiveInterval`, 10-60 s; faster after failures; for channels `ResubscribeMinIntervalMs`, `ResubscribeMaxIntervalMs`).
- A grain removes an observer whose call failed or timed out. The observer is back after the next resubscribe of its process.
- Grain state (observer list, channel sequence buffer) lives only in the activation. A new activation starts empty; observers re-attach on resubscribe.

### Local handlers of one observer

`Listener.Deliver` calls the handlers one by one. The observer call succeeds if **at least one** handler succeeded; a failed handler is logged and does not get the message again. The call fails only when all handlers failed.

---

## DurableQueue

Push path: `PushTransactional` (inside a transaction) / `PushDirect` writes a `DurableQueueSideEffect` into the side-effects tables. The side-effects worker executes it: `IDurableQueue.Push` sends the message to every current observer, **sequentially**.

| Outcome of `Push` | Side effect |
|-------------------|-------------|
| At least one observer accepted | completed, removed from the queue |
| No observers, or all failed | `Push` throws, side effect goes to the retry queue (`IncrementalRetryDelay`), after `MaxRetryCount` - dead letter table |

Consequences:
- **Delivered = received by at least one observer.** Observers that failed (or were not attached at that moment) are removed and never get this message. Do not use a durable queue as a guaranteed broadcast to several processes.
- **At least once.** A worker crash after `Push` and before completion re-queues the side effect (`RequeueStuck` on coordinator start): handlers must be idempotent.
- **No ordering** between messages: side effects run concurrently (`ConcurrentExecutions`) and retries reorder them.
- A slow observer delays the remaining observers of the same message (sequential sends) and holds a side-effect execution slot.

## RuntimeChannel

`Publish` assigns a sequence number, stores the message in a ring buffer (`CatchUpBufferSize`) and puts it into the delivery queue of every observer. `Publish` returns before delivery.

- **Per-observer queues.** Each observer has its own ordered queue and delivery loop: a slow observer delays only itself.
- An observer is removed when a send fails, exceeds `DeliveryTimeoutSeconds`, or its queue overflows (`CatchUpBufferSize` pending messages).
- **Catch-up.** On resubscribe the client calls `CatchUp(lastSeenSequence)` and replays missed messages from the buffer; duplicates are dropped by sequence. If the buffer no longer has them (or the activation was recreated) the client gets `GapDetected` and `onGapDetected` is invoked - the subscriber must reload its state from the source of truth.
- Order is preserved per observer; there is no ordering between different observers.
- Keep-alive: `AddObserver` keeps the activation for `ObserverKeepAliveMinutes`, `Publish` for `DeliveryTimeoutSeconds`. `DelayDeactivation` inside `OnDeactivateAsync` has no effect (deactivation already started), so keep-alive is requested on these calls instead. On deactivation the already queued messages are delivered first.

Use for state broadcasts where the receiver can resync on a gap (`DeploymentState<T>`, live data, configs).

## RuntimePipe

Request/response to a **single** bound observer (the last `BindObserver` wins). `Send` fails if no observer is bound; a timeout (`SendTimeoutSeconds`) unbinds it. Callers retry; there is no queueing. `HasObserver` pings the observer (500 ms).
Keep-alive: `BindObserver` keeps the activation for `ObserverKeepAliveMinutes` (same for `DurableQueue.AddObserver`), see CLAUDE_MISTAKES F29.

---

## Choosing

| Need | Use |
|------|-----|
| Fan-out of state changes, receiver can resync | RuntimeChannel + `onGapDetected` |
| Command that must survive restarts, processed by any one instance | DurableQueue (idempotent handler) |
| Ask one well-known service and wait for an answer | RuntimePipe |
| Guaranteed delivery to every subscriber | not provided - store the data and let subscribers read it |
