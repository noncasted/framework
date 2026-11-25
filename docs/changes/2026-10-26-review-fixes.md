# Fixes of the framework review, 2026-10-26

Every fix was made through a test that first failed for the expected reason. Result: backend 783/783,
common 92/92, ContainerGenerator.Verify 62/62, Unity EditMode 277/281 (4 skipped benchmarks), PlayMode 9/9.
**BREAKING** marks a change of an API or a behaviour a game may rely on.

## common (reactive primitives)

- A listener of a lifetime removed by `RemoveListener` during `Terminate` is no longer called in that pass.
- `LifetimedValue`: a `Set` inside a handler no longer delivers the stale value to the remaining handlers after the
  new one.
- `ViewTrue`: `Notify` with the value true ends the previous true lifetime.
- **BREAKING** `ViewableList` and `ViewableDictionary` no longer derive from `List` / `Dictionary`: the base mutators
  bypassed the lifetimes. Kept: Count, the indexer, Add, Insert, Remove, RemoveAt, AddRange, RemoveRange, Clear,
  Contains, IndexOf, ContainsKey, TryGetValue, TryGetSourceValue, Keys, Values, GetEnumerable, foreach.
- `ViewableList.Add` / `Insert` of a duplicate throws before changing the list (before, the item got in without a lifetime).
- The indexer of `ViewableDictionary` with a new key calls `OnModified` once.
- The termination of a parent does not look for its children in its list of listeners (that was quadratic).
- **BREAKING** the dead `TerminatedLifetime.CreateChild` and `RemoveTerminationListener` are removed.
- `ReactiveExceptions.Report` is public (the updater of the client uses it).

## shared, backend/Session, backend/Hosting

- A connection closed before `Start()` leaves the session; the session closes, `SessionGate.Join` does not hang. An error
  of `Connection.Run` is logged, the disconnect is always queued.
- The wait for `ExpectedUsers` counts seats, not connections: a reconnect before the start does not start the match early.
- The match of a lobby gets its players without bots and without duplicate `BackendId`s; so does `PlayersCount` of the listings.
- `CloseIfNotJoined` closes the admission first, then ends the session (`IsClosed` is right).
- The lobby code is released when the lobby factory throws.
- **BREAKING** the owner of a session is never a bot.
- A refused `TryReserve` no longer breaks a live connection.
- The timers of a session and a lobby (`Task.Delay`) are cancelled with the lifetime.
- The lifetime of a connection is intersected with the lifetime of the user: listeners do not pile up on the lifetime of the
  session over reconnects; `SessionEntities` forgets ended seats.
- The registration of a service in `ServiceFactory` is atomic on an id collision.
- A returning connection does not get a `RemoteUpdate` about itself.
- `SessionsCollection` sends the snapshots of the live data in order.
- **BREAKING** `ISession.AllUsersConnected` is a `Task` now (remembers that everyone came; cancelled with the session).
- **BREAKING** `ServiceFactory.OnSessionCreated` runs the setup in the queue of the session and completes after it.
- **BREAKING** `IUserFactory.Create(lifetime, id, connection)`, `ISessionAdmission.Close()` / `IsHeldOnlyBy()`.
- The comment of the ranges of the protocol tags: `SessionCreateSolo = 184` and the unused 52 (the tags are not renumbered).

## backend/Infrastructure, Common, Cluster

- A transactional state is registered before the read: a failed read no longer blocks the grain forever;
  `EventState.Append` is registered too.
- Taking over the lock of a "stuck" transaction does not let it commit its old snapshot later (`Seal` before the commit,
  `idle_in_transaction_session_timeout`).
- An unknown outcome of a commit is checked with `pg_xact_status`; when it stays unclear,
  `TransactionOutcomeUnknownException` is thrown instead of a "failure" with a retry.
- **BREAKING** `AddressableState.OnOrleansStarted` and `ConfigsInitializer.Init` no longer swallow errors: a failed load
  stops the start instead of overwriting the configs with the seed values.
- `DurableQueue` delivers to every subscriber; a retry with the same id goes only to those that did not get it.
- A pipe retries only "no observer bound"; a timeout does not run the handler again and does not reset the observer.
- Channels: an activation epoch number, so messages are not silently lost after a reactivation of the grain; a resubscription
  of one listener is serialized.
- `DeploymentState` / `AddressableState`: subscribe before the read, reload on a break, records ordered by a Lamport stamp
  instead of the clock of the host; the value in memory changes only after the write to the database.
- `OpenLobbiesWatcher`: a break of the channel rereads the registry.
- Non-transactional writes of `State` are serialized; a failed write resets the cache.
- `EventState.Load` outside a transaction does not lose pending events.
- **BREAKING** `NoAwait`: an error goes straight to `NoAwaitExceptions.Handler` (the cluster sets `LogCritical`), otherwise to stderr.
- `TaskBalancer`: the drain waits for all tasks, the penalty for an error is kept.
- Side effects: a long effect is not restarted in parallel (`TouchProcessing`), a dead letter keeps the original error,
  the operations of the console do not swallow errors; at-least-once is described in `ISideEffect`.
- The waits of `ReactiveTaskExtensions` unsubscribe after completion and continue asynchronously.
- Discovery: a stopped service is not registered again, the time is set by the grain, the balancer skips a server
  lagging more than 10 s; the cluster readiness warning does not spam.
- **BREAKING** `DeployCleanup` refuses to work when its deploy id differs from the answer of `DeployIdPipe`
  (the constructor takes `IMessaging`).
- `SideEffectsWorker` ends on stop; `Connection.Run` cleans up in finally, `ForceDisconnect` sends a close frame.
- Fail fast: no `?? new TransactionOptions()`, `EventStorage` does not skip unreadable records, event types through
  `MovedTypesBinder`, `StateIdentity` with value equality, the query cache per table.

## client/core: DI, scopes, generator

- **BREAKING** the values of `WithParameter` / `RegisterInstance` / `RegisterComponent` are taken by the registration, not by
  the type (before, the last registration of the type won).
- **BREAKING** `[Inject]` of a base class is called.
- New diagnostics: CINGR010 (several, generic or private `[Inject]`), CINGR011 (abstract, struct, private nested,
  two constructors of the same length), CINGR012 (a transient listens to several phases), CINGR009 warning (a parameter takes
  one of several registrations). An exception of the emitter becomes CINGR001 instead of losing every container of the assembly.
- `if (x) builder.Register<T>()` without else no longer fails the build of the scope when nobody needs T.
- **BREAKING** a runtime check: a registration the generator does not see (through an interface, in a loop, a repeated call of an
  installer) throws at the build of the scope; `ScopeContainer.CreateEntity` without a variant for the view type throws.
- A failed load of a scope is rolled back (lifetime, container, scenes, after-dispose).
- `LoadedScope.Dispose` is idempotent, runs every step and disposes the child scopes first.
- Generated `Dispose`: the lifetime first, then the services in reverse order, errors collected; `Resolve` after
  `Dispose` throws `ObjectDisposedException`.
- Entity scopes run the loaded, dispose and after-dispose phases.
- `GameLoopScopeLoader` runs the loads one after another and does not await one UniTask twice.
- The comments of the generator are in English, the fixtures of Verify have no game names, the stubs of Verify match the runtime API.

## client/core: runtime

- `UpdaterProxy` applies the speed of its parent once and its own; skips ended updatables.
- **BREAKING** `Pause` / `Continue` are counted (Continue without Pause throws), a `SetSpeed` during a pause is applied
  after the last `Continue`.
- An updatable that throws does not stop the others and the next phases.
- **BREAKING** `RunDelay`, `RunUpdateAction`, `RunFixedAction` end with `OperationCanceledException` when the lifetime ends
  (like Progression and Tween); `RunUpdateAction(lifetime, time, …)` counts the time of the updater, not `Time.time`.
- The waits `WaitInvoke` / `WaitTrue` / `WaitFalse` unsubscribe after completion.
- `BehaviourSequence` resets the running first node.
- **BREAKING** `GameObjectLifetime`: an object hidden before `Start` gets a live lifetime (before, an ended one, and
  `ListenClick` silently did nothing); the lifetime ends in `OnDestroy`.
- `AssetGroup`: a release during a load and a failed load do not leak; **BREAKING** `Release` without `Retain` throws.
- `StateMachine`: the new state is set before the old one ends (reentrancy), `Initialize` ends the current one.
- **BREAKING** `Direction8` has 8 directions, `ToDirection4` goes by the dominant axis, a zero vector throws;
  `RotateClamped` takes the shortest way, `ToAngle` is in [0, 360).
- **BREAKING** `FindOrLoadScene` in a mock no longer takes an object from another scene; `LoadTyped` throws
  `InvalidOperationException`.
- A zero time of Progression / Curve completes at once; a negative one throws.
- **BREAKING** fail fast: `CollectionsExtensions.Random` on an empty list, `ProfilerTraceStorage.Save` / `Load`.
- `BackendOptions.SocketUrl` changes only the scheme; `LocalStorageKeys.Get` rejects names with an environment suffix.

## client/core: editor tools

- Hierarchy bindings: a nested class named after its parent, child `Register` / `Create` / members of `ObjectBindings`,
  the fields `HPBar` / `HpBar` no longer give compile errors.
- Two bindings types in one file or a foreign namespace are rejected; objects are found by the indices of their siblings, not by names.
- A batch is reset after an exception, "Cancel Prefab Batch" is available while something is left; a failed write of a file
  throws; a batch refuses while a prefab stage is open; the "foreign layer" check is not confused after Ctrl+D.
- One identifier service `CodeIdentifiers` for every catalogue generator: keywords, class members, the class name,
  duplicates.
- The scene catalogue: duplicate names, a leading digit, ordinal sorting, the scene postprocessor, an empty catalogue.
- **BREAKING** the catalogues throw on duplicate names, a missing root component, an unreadable json of the registry or userData,
  types from Assembly-CSharp.
- The prefab and audio postprocessors react to an exception of a catalogue.
- **BREAKING** `ProjectSettings/Framework.json` is required, no game paths by default; reread on change.
- The asmdef lookup takes `.asmref` into account; `SerializeReferencePicker` understands nested types; `PlayerBuildSteps` skips
  open generic types; the scanner of the container graph does not swallow errors.

## client/modules and platforms

- Network: `ConnectionWriter` does not mix the old and the new socket after a repeated `Run`; `ForceSendAll` does not send
  in parallel with the loop; a request that cannot be serialized fails at once.
- Network: `DefaultWebSocket` reads on the thread pool and answers a ping while the main thread is busy; the socket of a failed
  connect is closed; `JsWebSocket` delivers in the player loop.
- Network: the property sync and the music fades run on real time (a pause of the game does not stop them);
  `NetworkEntityFactory` throws on a foreign id; `NetworkSession.Start` does not hang on a break.
- **BREAKING** `INetworkProperty.OnWriteSent()`: the local version grows on a write (convergence on simultaneous writes).
- A test union of the protocol in the network tests (9 tests of the framework failed without a game).
- Matchmaking: removed a 10 s timeout shorter than the timeout of the writer (20 s), no duplicate lobbies on a retry.
- UI: the top of the stack on an out-of-order close, the visibility under a dialog, `Exit` in finally (the input does not stay off).
- Animations: the chain of `PlayAsync`, the remainder of the loop time, the first frame at once, an animation without frames throws.
- **BREAKING** cameras: `SetCamera(lifetime, camera)`, the camera is reset with the lifetime; `ScreenToWorld` without a camera
  throws; the screen resolution is set only in `SetOutputResolution`.
- **BREAKING** backend HTTP: the timeout from `BackendOptions.TimeoutSeconds`, `IBackendPost.Post` returns the response body,
  `GetImage` sends the headers.
- Itch: a broken save is moved to `save_v2_corrupt`; `Settings.Apply` keeps a copy.
- **BREAKING** localization: the default language and the mapping of the system language in `LocalizationOptions`.
- **BREAKING** telegram: the path to the token of the debug API in `TelegramEditorOptions`; web: a timeout of
  `WebLoadingScreen.Show`, clear jslib errors without `window.webShell`.

## Not fixed

- Owner checks of the session commands: decided not needed for now.
- Authentication of the console (code of the game host), the license of FluentAssertions.
- Not confirmed: a missing player does not kill the match of a lobby (`ExpectedUsers = 0`), the cancellation of `WaitInvoke`
  is already Canceled, the error of `SetFlag` is unreachable.
- Limitation: the property versions converge for a pair of simultaneous writes; an exact solution needs the server to
  send the writer its new version.
- Left as is: the `ActionBlock` of a session does not complete; a flush of the itch saves on every save; the owner of the shared
  RenderTexture of the camera; the parameters of the `switch` branches in the generator by type; the special cases of `GraphWalker`
  for `AddSnapshotHandler` / `AddCardActionSyncResolver` (game names).
