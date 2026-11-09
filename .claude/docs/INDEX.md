# Framework docs

Docs shared by every game built on the framework. Names below are relative to this folder: `.claude/docs` in the
framework, `tools/framework/.claude/docs` in a game. Read a doc when its keywords come up; only the principles are
always loaded.

Paths inside the docs:
- `client/core`, `client/flow`, `client/modules`, `client/platforms`, `shared`, `common` and
  `backend/{Common,Infrastructure,Cluster,Console,Session,Hosting}` are the framework (`tools/framework/...` in a game).
- `client/Assets/...` and the rest of `backend/...` (`Meta`, `Orchestration`, `Tools`) are examples from a game; another
  game has the same parts under its own names.
- A doc named here that is absent from this folder (`ENTITIES.md`, `CODEGEN.md`, `NETWORK.md`, `CLAUDE_MISTAKES.md`,
  ...) belongs to the game: `.claude/docs` of the project, if it has one.

A doc of a game wins where it states a local exception; a rule true for every game belongs here, not in the game.

@ARCHITECTURE_PRINCIPLES.md

The framework of a game is updated by a release with the `update-framework` skill, which also carries out what the release asks of the game.

The cluster and the Unity editor are tools of the agent: start, restart, rebuild and recover them with the `control` skill, without
asking the user. Agents testing in parallel take their own copy of the project with its own editor (`control copy create`).

Research media of a project: work in progress is in `docs/media/research/<topic>`, finished research in
`docs/media/research/complete/<topic>`. A session that finished its task and left files in `docs/media` runs the
`media-cleanup` skill. Before a new study look in both places.

## Keyword -> doc

| Keywords | Doc |
|----------|-----|
| planning a feature or a refactoring, "if X the plan has Y" | ARCHITECTURE_PATTERNS.md |
| container, DI, IScopeBuilder, IEntityBuilder, Register, As<>, ISceneService, IScopeSetup, OnSetup(), [Inject], ScopeLoadOptions, ContainerScopeParent, CINGR, Container Graph | COMMON_CONTAINER.md |
| content directory, ContentReference, ContentScene, Loadable, LoadableSceneId, IContentLoader, ContentRoot, AddContent, LoadContent, RequestContent, IsPreloaded, Addressables (removed in 0.7) | CONTENT.md |
| HierarchyBindings, *Bindings.g.cs, ObjectBindings, Create Object Bindings, Regenerate bindings | HIERARCHY_BINDINGS.md |
| Lifetime, Advise, View, Terminate, subscription, cleanup, Lifetime Graph, LifetimeAnalytics, lifetime leak | COMMON_LIFETIMES.md, COMMON_LIFETIMES_PATTERNS.md |
| EventSource, ViewableProperty, ViewableList, reactive, observable, event | COMMON_REACTIVE_BASICS.md, then COMMON_REACTIVE_VALUES.md, COMMON_REACTIVE_COLLECTIONS.md, COMMON_REACTIVE_PATTERNS.md |
| IState, IStateMachine, IStateHandle, ObjectStateDefinition, state machine | STATE_MACHINES.md |
| UniTask, async, IReadOnlyList, file I/O, callback wrapping | API_DESIGN_FULL.md |
| member order, _camelCase, NoAwait, Async suffix, braces, comments, public setters, file size, catch | CODE_STYLE_FULL.md |
| Grain, IGrainWithGuidKey, [Transaction], constructor injection, State<T>, IStateValue, [GenerateSerializer], [Id(N)], StatesLookup, StateCollection, jsonb, IStateStorage, DirectStorage, EventState, Marten, GrainState, PostgreSQL, IOrleans, GetGrain, messaging, ListenDurableQueue, PushTransactionalQueue | COMMON_ORLEANS.md |
| delivery semantics, DurableQueue, RuntimeChannel, RuntimePipe, catch-up, GapDetected, observer keep-alive, dead letter | MESSAGING.md |
| Blazor, razor, @inject, UiComponent, early return, console UI | BLAZOR.md |
| which pattern to use, decision | DECISION_TREES.md |
| WebGL, iOS, iPadOS, Safari, WebKit, Android, Mali, ANGLE, Metal, shader, nointerpolation, flat varying, ImmCB, GPU hang, frame every 5 s, black screen, context lost, INVALID_OPERATION, ?glcheck, DPR, fullscreen, silent mode, JSPI, emscripten, wasm, stripping, Bee, Brotli, CRC Mismatch, Cache API, Telegram WebView, test on a device | WEBGL_TROUBLESHOOTING.md |
