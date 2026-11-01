# Architecture Patterns (база для проверки планов)

Назначение: список проверяемых паттернов, общих для игр на framework, для скилла, который создаёт или правит план
задачи. Формат строки: **если** в задаче есть X -> **план должен содержать** Y. У каждого паттерна - источник
(`DOC.md` § Раздел). Идентификаторы сквозные с `ARCHITECTURE_PATTERNS.md` игры, номера с пропусками: остальные
паттерны (сущности, сеть, UI, тесты, редактор) привязаны к докам игры и лежат там. Источник, которого нет рядом
(`CLAUDE_MISTAKES.md`, `ENTITIES.md`, `CODEGEN.md`, `CLAUDE.md`), - это док игры, см. `INDEX.md`.

---

## 0. Общие правила (G)

- **G-1** Если план добавляет обработку ошибок, fallback, `try/catch` или null-проверки -> в плане явно указано, что
  пользователь просил деградацию; иначе - throw / `Debug.LogError` (fail fast). `CLAUDE.md` § MOST IMPORTANT RULE.
- **G-2** Если задача трогает `tools/framework` -> план содержит: прочитать `tools/framework/CLAUDE.md`, коммит и push
  внутри сабмодуля, затем коммит указателя сабмодуля; оценку влияния на другие игры. `CLAUDE.md` § Framework.
- **G-3** Если появляется новый asmdef, ссылающийся на `Internal` (в том числе временный, бенчмарк, build-скрипт) ->
  в плане есть ссылка и на `Internal.Content`. `CLAUDE.md` § Framework; `CLAUDE_MISTAKES.md` § G37.
- **G-4** Если тип регистрируется из другой сборки и имеет `[Inject]`-метод -> метод `public`.
  `CLAUDE.md` § Framework; `COMMON_CONTAINER.md` § MonoBehaviour Services.
- **G-5** Если план смешивает слои -> клиентские паттерны (контейнер, MonoBehaviour) только в клиенте, grain-паттерн
  только в backend. `CLAUDE.md` § Critical Rules (8).
- **G-6** Если задача переносит тип в framework -> тот же шаг удаляет игровой дубликат (иначе игровой интерфейс с тем же
  именем затеняет framework-овый при регистрации). `CLAUDE_MISTAKES.md` § G22.
- **G-8** Если в задаче новые знания об ошибках агента -> шаг «обновить базу знаний» (`CLAUDE_MISTAKES.md`, профильный
  док). `CLAUDE.md` § Critical Rules (5).

---

## 1. Клиент

### 1.1 DI и сервисы (C-DI)

- **C-DI-1** Если нужна логика / состояние / фабрика -> plain class с конструкторной инъекцией, регистрация в
  installer-extension `builder.Register<T>().As<I>()`, installer возвращает builder. `DECISION_TREES.md` § 3;
  `COMMON_CONTAINER.md` § Registration API, § Plain Service.
- **C-DI-2** Если сервису нужны подписки / инициализация -> `.As<IScopeSetup>()` и подписка в `OnSetup(lifetime)`;
  никаких подписок в конструкторе или `Awake`; `.WithScopeLifetime()` только если lifetime нужен конструктору.
  `COMMON_CONTAINER.md` § Lifecycle Phases, § Common Mistakes; `DECISION_TREES.md` § 2.
- **C-DI-3** Если сервис B зависит от инициализации A -> A в более ранней фазе (`BaseSetup` -> `Setup` ->
  `SetupCompletion` -> `Loaded`), порядок внутри фазы не используется. `COMMON_CONTAINER.md` § Lifecycle Phases.
- **C-DI-4** Если появляется новый scope -> корень `Construct` передаётся method group-ом (не лямбдой), у дочернего
  корня `[ContainerScopeParent]`, выбрана сцена scope (`WithRuntimeScene` / `WithAssetScene` / без сцены).
  `COMMON_CONTAINER.md` § Service scope, § Common Mistakes.
- **C-DI-5** Если реализации выбираются по условию -> варианты = разные корни через `switch` на месте вызова;
  внутри корня граф меняют только `bool` / закрытый enum (CINGR005). Платформа: `#if` или ветки внутри одного installer (`CLAUDE.md` framework § Modules, publish).
  `COMMON_CONTAINER.md` § Entity scope («One graph per root»); `CLAUDE_MISTAKES.md` § F2, § F3.
- **C-DI-6** Если в одном scope несколько альтернатив с маркер-интерфейсом фазы -> не более одной группы альтернатив
  с `IScopeSetup` и т.п.; платформенный сервис с фазой регистрируется на всех платформах и сам проверяет платформу.
  `CLAUDE_MISTAKES.md` § F37.
- **C-DI-7** `WithParameter` принадлежит своей регистрации и важнее регистрации того же типа, родительского scope и `IContainer` / `ILifetime` / `IReadOnlyLifetime` scope: параметр берёт значение точного типа, иначе значение, которое может принять (несколько таких: CINGR014). `COMMON_CONTAINER.md` § WithParameter belongs to its registration and wins.
- **C-DI-8** Если сервису нужны ассеты каталога -> `Request*Group` в installer (или `Load*Group`, только если
  `Construct` читает ассеты сам); никаких `Addressables.LoadAssetAsync` / `Retain` в геймплее.
  `COMMON_CONTAINER.md` § Asset Groups.
- **C-DI-9** Если код берёт `IContainer` / `Resolve` -> только фабрики и инструменты; сервисы - через конструктор;
  контейнер только в main thread. `COMMON_CONTAINER.md` § Runtime Access.
- **C-DI-10** Если возможен цикл зависимостей -> план разрывает его (событие, `Lazy<>`, разделение сервиса); в backend
  цикл через `As<>` вешает сервисы без исключения. `COMMON_CONTAINER.md` § Diagnostics (CINGR004); `CLAUDE_MISTAKES.md` § F50.
- **C-DI-11** Если меняется foundation-asmdef (`Internal`, `Internal.Editor`) -> никаких ссылок на `Flow`, `Global`,
  `Meta`, `Menu`, `GamePlay`; редакторский код игры - в editor-asmdef своего слоя. `CLAUDE_MISTAKES.md` § F5.

### 1.2 Сцена и MonoBehaviour (C-SC)

- **C-SC-1** Если объект живёт в сцене -> MonoBehaviour + `ISceneService.Create(IScopeBuilder)` +
  `RegisterComponent(this).As<I>()`, зависимости через `[Inject]`-метод, фаза через `.As<IScopeSetup>()`.
  `DECISION_TREES.md` § 3; `COMMON_CONTAINER.md` § MonoBehaviour Services; `CLAUDE_MISTAKES.md` § Key Rules (1), § F1.
- **C-SC-2** Если новый scene service и на него появляется зависимость -> порядок шагов: компонент в сцене ->
  `Assets/Scan services` -> сохранить сцену -> обновить asset map -> только потом зависимость в конструкторе
  (иначе CINGR003). `COMMON_CONTAINER.md` § MonoBehaviour Services; `CODEGEN.md` § Bootstrap Order.
- **C-SC-3** Если префаб инстанцируется сервисным scope (экран загрузки, оверлей) -> `builder.Instantiate` +
  `builder.Inject` / `RegisterInstance(...).As<>`. `DECISION_TREES.md` § 3; `COMMON_CONTAINER.md` § MonoBehaviour Services.
- **C-SC-4** Если MonoBehaviour создаётся в runtime и нуждается в зависимостях -> `builder.Injectable<T>()` +
  `IViewInjector.Inject`; у `T` видимый `[Inject]`-метод (CINGR008). `DECISION_TREES.md` § 3; `COMMON_CONTAINER.md` § Registration API.
- **C-SC-5** Если у MonoBehaviour есть обработчик -> имя не совпадает с Unity message (`OnPreRender` и т.п.).
  `CLAUDE_MISTAKES.md` § F44.
- **C-SC-6** Если runtime-пакет с MonoBehaviour нужно исключить из билда -> `defineConstraints: UNITY_EDITOR`, а не
  `includePlatforms: Editor`. `CLAUDE_MISTAKES.md` § F38.

### 1.4 Машины состояний и деревья поведения (C-SM)

- **C-SM-1** Если у сущности состояния -> `IState` + `ObjectStateDefinition`, вход через
  `TryCreateHandleFromPriorityQueue`, все подписки состояния на `handle.Lifetime`; селектор на `Exited`.
  `STATE_MACHINES.md` § API, § Entry via priority queue, § Selector on Exited.
- **C-SM-2** Если состояние видно другим клиентам -> пара Local / Remote в одной папке, `RemoteStateMachine`,
  данные в `IStatePayload` с union-тегом; remote-состояние не выходит само. `STATE_MACHINES.md` § Remote copies;
  `ENTITIES.md` § Contexts and states.

### 1.5 Lifetime (C-LT)

- **C-LT-1** Если есть `Advise` / `View` / `Listen` / `ListenClick` -> у каждой подписки lifetime; в плане указано,
  чей. `COMMON_LIFETIMES.md` § Key Rules; `DECISION_TREES.md` § 2.
- **C-LT-2** Если подписка на элемент коллекции -> `item.Lifetime` или lifetime входа из `View(lifetime, (entryLifetime, item) => ...)`.
  `DECISION_TREES.md` § 6; `COMMON_REACTIVE_COLLECTIONS.md` § Key Rules; `CLAUDE_MISTAKES.md` § F7.
- **C-LT-3** Если вложенные объекты (персонаж -> способности) -> иерархия `Child()`; `Intersect` - только когда
  нужен кратчайший из двух несвязанных. `COMMON_LIFETIMES_PATTERNS.md` § Lifetime Hierarchy, § Lifetime Intersection, § Common Mistakes.
- **C-LT-4** Если одноразовая операция / обёртка колбэка -> `new Lifetime()` с гарантированным `Terminate`;
  в cluster-бенчмарках root `new Lifetime()` запрещён, в тестах - `Terminate` в `finally`.
  `DECISION_TREES.md` § 2; `CLAUDE_MISTAKES.md` § F11.
- **C-LT-5** Если используется `TerminatedLifetime` -> только как «уже закончилось» (слушатели вызываются сразу),
  не как «ещё не настроено». `CLAUDE_MISTAKES.md` § F35; `COMMON_LIFETIMES_PATTERNS.md` § Conditional Lifetimes.
- **C-LT-6** Если план меняет создание lifetime-ов в долгоживущих объектах -> шаг проверки утечек через
  `Tools/Lifetime Graph`. `COMMON_LIFETIMES.md` § Lifetime Graph.

### 1.6 Reactive (C-RX)

- **C-RX-1** Если нужен сигнал без состояния -> `EventSource`; состояние -> `ViewableProperty`; коллекция ->
  `ViewableList` / `ViewableDictionary`; наружу - read-only интерфейс. `DECISION_TREES.md` § 1;
  `COMMON_REACTIVE_BASICS.md` § Key Rules.
- **C-RX-2** Если UI показывает значение -> `View` (текущее + будущие); `Advise` только для событий.
  `DECISION_TREES.md` § 5; `COMMON_REACTIVE_VALUES.md` § Common Mistakes; `CLAUDE_MISTAKES.md` § F8.
- **C-RX-3** Если элемент коллекции обновляется на месте -> мутация + `NotifyChangedAt`, не `RemoveAt` + `Add`.
  `COMMON_REACTIVE_COLLECTIONS.md` § Inplace Update.
- **C-RX-4** Если используется value lifetime из `View((lt, v) => ...)` -> не передавать его для стартового
  (не установленного) значения; `ViewNotNull`, срабатывающий при setup, защищён проверкой готовности.
  `CLAUDE_MISTAKES.md` § F9, § F10; `COMMON_REACTIVE_VALUES.md` § Key Rules.
- **C-RX-5** Если реактивное значение ставится из нескольких потоков (backend) -> весь apply под одним lock, без
  await внутри. `CLAUDE_MISTAKES.md` § F33.
- **C-RX-6** Если обработчик события может вызваться внутри вызывающего кода (хит, смерть тела) -> реакция ставится
  в очередь и выполняется в своём update, списки вызывающего не трогаются. `CLAUDE_MISTAKES.md` § G10.

### 1.7 View и префабы (C-VW)

- **C-VW-1** Если нужен runtime-вид -> префаб: составной - дети entity-префаба через bindings; много коротких копий -
  пул над шаблонным префабом; никаких `new GameObject` + `AddComponent`
  вне редакторских билдеров и процедурных мешей; в плане - grep-проверка. `HIERARCHY_BINDINGS.md` § Runtime Views
  Come From Prefabs; `CLAUDE_MISTAKES.md` § G36.
- **C-VW-2** Если код ищет объекты в иерархии -> generated `*Bindings` вместо `transform.Find` / `GetComponentInChildren`;
  один bindings-класс на тип объекта, у варианта префаба - свой класс. `HIERARCHY_BINDINGS.md` (TL;DR), § Rules.
- **C-VW-3** Если меняется иерархия префаба сущности -> шаги: Regenerate bindings -> rescan `ScopeEntityView` ->
  обновление asset map; проверка, что новые ссылки заполнены (unchanged-биндинги не перепривязываются).
  `HIERARCHY_BINDINGS.md` § Rules; `CODEGEN.md` § Regenerate All.

### 1.12 API и стиль кода (C-API)

- **C-API-1** Если асинхронный метод -> `UniTask` / `UniTask<T>` без суффикса `Async`; fire-and-forget - `.NoAwait()`.
  `API_DESIGN_FULL.md` § Rule 1; `DECISION_TREES.md` § 4, § 8; `CODE_STYLE_FULL.md` § UniTask Fire-and-Forget.
- **C-API-2** Если метод возвращает коллекцию -> `IReadOnlyList<T>`, пустая вместо null. `API_DESIGN_FULL.md` § Rule 2, § Rule 3.
- **C-API-3** Если callback-API -> обёртка в UniTask. `API_DESIGN_FULL.md` § Rule 5.
- **C-API-4** Если новый класс -> порядок членов (конструктор -> поля -> публичные -> приватные -> локальные),
  `_camelCase` без сокращений, коллекции инициализируются inline, `TryGetValue`. `CODE_STYLE_FULL.md` § Member Order,
  § Field Naming, § Collections, § Dictionary Lookup.
- **C-API-5** Если `WhenAll` -> каждая задача - именованная локальная переменная, без inline-лямбд.
  (Пользовательская память `named-tasks-before-whenall.md`, не `.claude/docs`.)

---

## 3. Backend (B)

### 3.1 Orleans и grains

- **B-1** Если новый grain -> интерфейс с одним типом ключа, `[Transaction]` только для транзакционных методов,
  конструкторная инъекция, `[State]` / `[EventState]`. `COMMON_ORLEANS.md` § Grain Pattern.
- **B-2** Если код вне grain обращается к Orleans -> `IOrleans` и extension-методы (`GetGrain`, `InTransaction`);
  внутри grain - `GrainFactory` / `Grains`. `COMMON_ORLEANS.md` § IOrleans Interface.
- **B-3** Если grain держит observer-ссылку процесса, который может умереть -> `[Reentrant]`, observer сбрасывается
  при ошибке Send. `CLAUDE_MISTAKES.md` § F15.
- **B-4** Если нужно держать активацию -> keep-alive на вызовах (`AddObserver` / `BindObserver` / `Publish`), не в
  `OnDeactivateAsync`. `COMMON_ORLEANS.md` § Messaging; `MESSAGING.md` § RuntimeChannel; `CLAUDE_MISTAKES.md` § F29.
- **B-5** Если меняется набор сервисов роли / модуля (`IGameModule`) -> проверены конструкторы grain-ов,
  активируемых на этой роли (`GameCompositionTests`). `COMMON_ORLEANS.md` § Hosts, roles and modules; `CLAUDE_MISTAKES.md` § F32.
- **B-6** Если процесс сам записал состояние grain-а -> не поллить его для подтверждения; не держать локальные
  lock-и доставки через привязку observer-а. `CLAUDE_MISTAKES.md` § F16, § F17.

### 3.2 State

- **B-7** Если новое состояние -> `[GrainState]` или `[GrainEventState]`, `[GenerateSerializer]`, `[Id(N)]` подряд,
  `Version`; регистрация генератором `StatesLookup`; таблица - deploy setup. `COMMON_ORLEANS.md` § State, § Adding New State.
- **B-8** Если event-sourced состояние -> `Apply` на каждый тип события, `Append` + `Write` парой до любого await,
  `Append` асинхронный. `COMMON_ORLEANS.md` § Event-sourced state; `CLAUDE_MISTAKES.md` § F18-F21.
- **B-9** Если состояние на DeployId -> `IDeployScopedState`, только direct. `COMMON_ORLEANS.md` § Adding New State (5).
- **B-10** Если коллекция, адресуемая по ключу на всех сервисах -> `StateCollection` (+ `AddStateCollection`).
  `COMMON_ORLEANS.md` § StateCollection. (См. K-6.)
- **B-11** Если нужно перечислить записи -> только ключи (`ReadAllKeys`), без десериализации. `CLAUDE_MISTAKES.md` § F31.
- **B-12** Если меняется схема БД -> шаг мигратора идемпотентен (чистая БД, текущая, любая старая).
  `DEPLOY.md` § (Migrations, строки про `PostResourcesSetup`).

### 3.3 Messaging

- **B-13** Если рассылка изменений состояния -> `RuntimeChannel` + обработчик `onGapDetected` (перечитать источник).
  `MESSAGING.md` § RuntimeChannel, § Choosing.
- **B-14** Если команда должна пережить рестарт -> `DurableQueue` с идемпотентным обработчиком, без расчёта на
  порядок и на доставку всем. `MESSAGING.md` § DurableQueue.
- **B-15** Если запрос к одному сервису с ответом -> `RuntimePipe`, вызывающий повторяет сам. `MESSAGING.md` § RuntimePipe.
- **B-16** Если нужна гарантированная доставка каждому подписчику -> сохранить данные и дать подписчикам читать.
  `MESSAGING.md` § Choosing.
- **B-17** Если сообщения Orleans -> `[GenerateSerializer]` + `[Id(N)]`; подписка на lifetime. `COMMON_ORLEANS.md` § Messaging.

### 3.4 Console (Blazor)

- **B-18** Если страница консоли -> ранний return, `[Inject]` в `@code`, реактивные страницы от `UiComponent`.
  `BLAZOR.md` (правила в начале и разделы про inject / UiComponent).

---

## 4. Shared (S)

- **S-1** Если тип нужен клиенту и backend -> `shared/` (протокол, модели, конфиги); состояния grain-ов - только
  backend. `CLAUDE.md` § Overview; `COMMON_ORLEANS.md` § Direct state class.
- **S-2** Если payload / state payload / event -> MemoryPack, тег в partial-объявлении union рядом с типом,
  член union-типа с `[MemoryPackAllowSerialize]`. `NETWORK.md` § Union tags; `ENTITIES.md` § Contexts and states.
- **S-3** Если логика сессии -> `Framework.Session.Core` (C# 9, синхронно, без Unity и Microsoft.Extensions); списки
  частей `SessionCore` и `AddSessionServices` синхронны. `NETWORK.md` § Session.
- **S-5** Если в backend используются `StableHash` / `ErrorCode` -> полное имя `Shared.*` (конфликт с Orleans).
  `NETWORK.md` § NetworkProperty, § Request errors.
