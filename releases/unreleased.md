# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

### ContainerGenerator: сборка без копии в Analyzers

Кого касается: игры, чей скрипт тестов собирает `ContainerGenerator` в Release (или запускает `ContainerGenerator.Verify` в
Release) в том же дереве, где в это время идут тесты Unity.

Release-сборка копирует `ContainerGenerator.dll` в `client/core/Analyzers`. Если это происходит во время batchmode-прогона
EditMode, Unity запрашивает компиляцию скриптов, которая не может пройти до конца прогона; пока она висит,
`EditorApplication.isCompiling == true`, цикл UniTask в редакторе стоит, и каждый тест, который ждёт кадр или таймаут, заканчивается
только по таймауту тест-фреймворка (180 с). Новое свойство `-p:SkipAnalyzersCopy=true` (для `dotnet run` -
`--property:SkipAnalyzersCopy=true`) собирает без копии; без него поведение прежнее.

Проверка: `Tools/Framework/Codegen/Regenerate All` (`-executeMethod Internal.RegenerateAll.Run`), затем
`grep -n "is already generated into" client/Logs/*.log` (или лог редактора). На каждую строку: вернуть ассет под сборку файла
(`git mv` вместе с `.meta`) либо перенести файл `*.g.cs` с его `.meta` по пути из ошибки.

Заодно Regenerate All заново привязывает bindings с пустой ссылкой (`{fileID: 0}`), у которых код не менялся: в логе
предупреждение `holds N empty references: binding again`, префаб пересохраняется. Действий не требует, изменённые префабы закоммитить.

### Bindings: Regenerate All заканчивается валидатором

Кого касается: клиент, все игры с bindings.

После прохода привязки Regenerate All прогоняет `HierarchyBindingsValidator` по всем префабам с bindings и загруженным сценам.
Раньше валидатор вызывался только из `RegeneratePrefabs` и из меню, поэтому старые находки (вариант убрал компонент базы, пустая
ссылка, устаревшая структура, bindings не в списке view) теперь появятся ошибками в консоли; последняя строка
`Regenerate All: N prefabs and M scenes validated in T ms, E validation errors` - ошибка при `E > 0`.

Проверка: Regenerate All, в batchmode или без фокуса редактора затем `HierarchyBindingsGenerator.PumpPending()` до `Idle.`;
`grep -n "HierarchyBindingsValidator\|validation errors" <лог редактора>`. Каждую находку исправить, как сказано в её тексте
(Regenerate в ассете, Scan на view, вернуть компонент в варианте). Скрипты игры, которые ждут конца Regenerate All по
`HierarchyBindingsGenerator.HasPending()`, учитывают, что он остаётся `true` до этой проверки.

## Окно реконнекта у сессии хоста-клиента

Кому: играм с P2P-матчами (`LocalSessionServer` как хост матча). Действие необязательно, без него поведение прежнее.

- `LocalSessionServer.Create(id, type, maxUsers, services, reconnectWindow)`: при ненулевом окне потеря соединения гостя без
  `Leave` не завершает его слот: остальные получают `RemoteAway`, гость с билетом того же пользователя возвращается в слот со
  своими сущностями (`LocalUpdate.IsReturn`, `SeatEntities`), по истечении окна слот завершается (`RemoteDisconnect`).
- `SessionCore(..., isReconnectable)`, `SeatAway`, `ExpireAway`: то же для своего хоста без `LocalSessionServer`.
- Поиск мест: `grep -rn "LocalSessionServer\|new SessionCore(" client/Assets`.

### Новый модуль match-metrics: игровые данные матча

Кого касается: клиент и протокол игр, которые хотят писать ряды и события матча (модуль необязательный).

Модуль `client/modules/match-metrics` (сборка `Internal.MatchMetrics`, namespace `Global.MatchMetrics`): поток на забег
`IMatchMetrics.Open(lifetime, matchId, IMatchMetricsClock)`, ряды `Counter` / `Gauge` с тегами (`MatchTags`, до 4), события
`Event(name, MatchEventField...)`, корзины 1 с часов забега, порции раз в 30 с по мета-сокету, одна в полёте, повтор после
реконнекта. Протокол - `shared/MatchMetrics/SharedMatchMetrics.cs`, теги `NetworkContextTags.MatchMetrics*` 220-223 (диапазон 220-229).

Что сделать игре, которая берёт модуль: `client/Packages/manifest.json` - `"com.noncasted.framework.match-metrics":
"file:../../tools/framework/client/modules/match-metrics"`; в union игры (`grep -n "TelemetryStartupTraceResult"
shared/Protocol/NetworkContextUnion.cs`) - четыре строки `SharedMatchMetrics.Open/Opened/Batch/BatchResult`, затем обновить
golden и хеш протокола; мета-бэкенд регистрируется как `IMatchMetricsSocket`, в его скоупе `AddSocketMatchMetrics()`; офлайн
мета отвечает на `Open` - `Opened { Ok }`, на `Batch` - `BatchResult { Ok }`. Бэкенд приёма - следующим шагом. Игре без модуля -
ничего (теги в её union не обязательны).

### match-metrics на бэкенде: приём и хранилище

Кого касается: игры с модулем `match-metrics` (бэкенд приёма). Игре без модуля - ничего.

Проект `backend/MatchMetrics` (сборка `Framework.MatchMetrics`, namespace `Game.MatchMetrics`, правила - `Contract/CONTRACT.md`):
потоки клиентов по мета-сокету (`IMatchMetricsConnections.Of(session.Lifetime, session.UserId)`: `Open`, `Accept`), парсер с
лимитами (`MatchMetricsBatchParser`), поток сервера (`IMatchMetricsWriter.Open(lifetime, matchId)`), хранилище - 6 своих таблиц
Postgres через `IDbSource` (`MatchMetricsSchema`), не состояния грейнов. Кто может писать в матч - ответ игры (`IMatchMetricsAdmission`).

Что сделать игре:
- csproj мета-гейтвея, deploy setup и тестов - `ProjectReference` на `tools/framework/backend/MatchMetrics/MatchMetrics.csproj`,
  в `.slnx` игры - проект.
- Мета-гейтвей: `builder.AddMatchMetrics()` (или свой `AddMatchMetricsCommands` с командами `SharedMatchMetrics.Open` / `Batch`
  поверх `IMatchMetricsConnections`, id пользователя - из сессии сокета) и `builder.AddMatchMetricsAdmission<T>()` - своя
  реализация `IMatchMetricsAdmission.Check(matchId, userId)` (`MatchMetricsAdmit(IsPlayer, ClosedAt)`); без неё хост не собирает
  команды.
- Deploy setup: `await MatchMetricsSchema.Create(connection)` (рядом с `AuditLogTable.Create`).
- Хост, который пишет поток сервера (игровой сервер): `builder.AddMatchMetricsWriter()`.

### match-metrics на бэкенде: чтение

Кого касается: игры с модулем `match-metrics` (консоль, агенты, история матчей). Игре без модуля - ничего.

`IMatchMetricsReader` (`backend/MatchMetrics/Reading`): потоки матча, словарь рядов, точки матча по группам тегов (шаг 1 / 10 /
60 с, ось часов забега или серверная), итоги матча и между матчами, события матча. GET эндпоинты `/api/match-metrics/...`
(`MapMatchMetricsRead`), контракт чтения - раздел «Reading» в `Contract/CONTRACT.md`. Таблицы те же, схема не менялась.

Что сделать игре: в хосте с чтением (мета-гейтвей) `builder.AddMatchMetricsReader()` и
`app.MapMatchMetricsRead().RequireConsoleToken()` (токен - свой у игры, как у `MapClientTelemetryRead`).

### uGUI вне ядра: сборка `Internal.UGUI`

Кого касается: клиент, все игры, которые вызывают `ListenClick`, `SetAlpha` (`Image`), `ToLifetimedValue` (`Slider`) или `ButtonSounds`.

Ядро (`Internal`) больше не использует uGUI, чтобы игра без `com.unity.ugui` собиралась. Эти помощники перенесены в сборку
`Internal.UGUI` пакета ядра (`client/core/UGUI`, GUID `1a5b96d3d6de4210867a24fe0a1f3fcb`, компилируется только при установленном
`com.unity.ugui`), пространство имён прежнее (`Internal`); `ColorUtils` переименован в `ImageExtensions`. Модуль `ui` ссылается на неё сам.

Проверка: `grep -rln -E "ListenClick\(|\.SetAlpha\(|ButtonSounds\.|Slider.*ToLifetimedValue" client/Assets --include=*.cs`; в asmdef каждой
найденной сборки добавить `"GUID:1a5b96d3d6de4210867a24fe0a1f3fcb"` в `references`. Ошибка без этого: CS1061 `'Button' does not contain a
definition for 'ListenClick'`.

### Ядро без `Framework.Shared`: `StableHash` в `Framework.Common`

Кого касается: клиент, все игры.

`Internal` больше не ссылается на `Framework.Shared`, а пакет ядра не тянет `com.noncasted.framework.shared` (и через него MemoryPack):
игра без сетевого протокола собирается без них. `Shared.StableHash` перенесён в `Framework.Common`, пространство имён прежнее.

Проверка: `grep -n '"com.noncasted.framework.shared"' client/Packages/manifest.json` - игра с протоколом (`shared/` игры, модули
`network`, `telemetry`, `lobbies`) указывает пакет в манифесте сама, если ещё не указывает. `grep -rln "StableHash" client/Assets
backend shared --include=*.cs`: asmdef каждой такой сборки ссылается на `Framework.Common` (`GUID:3ea5c3f1172040daa713d5ad71728b78`),
csproj - на `framework.common.csproj`; без этого CS0103 `The name 'StableHash' does not exist`.

### Декодер mp3 аудио-групп в сборке `Internal.EncodedAudio`

Кого касается: клиент, игры, которые вызывают `EncodedAudio` из своего кода (редакторские инструменты аудио).

Декодирование клипов, отгруженных байтами mp3 (`*.mp3.bytes`), перенесено из `Internal` в сборку `Internal.EncodedAudio` пакета ядра: ядро
больше не ссылается на `UnityWebRequest`. Сборка регистрирует себя при старте (`[AlwaysLinkAssembly]`), группы работают как раньше.
`EncodedAudio.Extension` -> `EncodedAudioDecoder.Extension`, статический `EncodedAudio.Decode` -> `EncodedAudioDecoder.Decode`.
Игра без mp3 в аудио-группах может убрать сборку из плеера (`"StrippedPlayerAssemblies": [ "Internal.EncodedAudio" ]` в
`ProjectSettings/Framework.json`), вместе с ней из плеера уходят `UnityWebRequest` и `Uri` / `Regex` из `System.dll`.

Проверка: `grep -rn "EncodedAudio\." client/Assets --include=*.cs` - заменить по списку выше.

### Телеметрия клиента: сокетный транспорт в сборке `Internal.Telemetry.Socket`, транспорт по HTTP

Кого касается: клиент, все игры с модулем `telemetry`; бэкенд - только игры, которые хотят телеметрию без мета-сокета.

`Internal.Telemetry` больше не ссылается на `Internal.Network`, `Framework.Shared` и `Framework.Session.Core`, пакет `telemetry` не тянет
пакеты `network` и `shared`. Транспорт по мета-сокету (`AddSocketClientTelemetry`, `ITelemetrySocket`, `TelemetrySocketTransport`,
`TelemetryMessages`, `MetaNetworkTelemetryCollector`) перенесён в сборку `Internal.Telemetry.Socket` (`client/modules/telemetry/Socket`,
GUID `802942ed80b040b1af69d20046688f31`, компилируется только при установленном пакете `com.noncasted.framework.network`), пространство
имён прежнее (`Global.Telemetry`). Новый транспорт без сокета: `AddHttpClientTelemetry(options, new TelemetryHttpOptions("/api/telemetry"))`
- JSON (`JsonUtility`) через `fetch` страницы, на бэкенде `MapClientTelemetryHttpIntake()` на группе игры с её авторизацией
(`backend/ClientTelemetry/Contract/CONTRACT.md`, раздел «HTTP»).

Проверка: `grep -rln "AddSocketClientTelemetry\|ITelemetrySocket" client/Assets --include=*.cs` - asmdef каждой такой сборки добавляет
`"GUID:802942ed80b040b1af69d20046688f31"` в `references`; без этого CS1061 `'IScopeBuilder' does not contain a definition for
'AddSocketClientTelemetry'` / CS0246 `ITelemetrySocket`. Игра, у которой `network` или `shared` приходили в манифест только через
`telemetry`, указывает их в `client/Packages/manifest.json` сама (`grep -n '"com.noncasted.framework.network"\|"com.noncasted.framework.shared"'
client/Packages/manifest.json`).

### Дополнительные content directories (`ExtraContent`, `AddContentDirectory`)

Кого касается: клиент; обязательных шагов нет, только игры, которые хотят отдельную директорию (например музыку, которая качается по
сети при входе в меню, рядом с предзагруженной основной), и игры со своей реализацией `IContentLoader`.

`ProjectSettings/Framework.json` принимает список `"ExtraContent": [ { "Name": "Stream", "Roots": [ ... ], "IsPreloaded": false } ]`;
каждая директория собирается с каждым плеером (`StreamingAssets/Content/<name>`, свой штамп, свой report directory для линкера), ассет,
достижимый из двух директорий, валит сборку. Корневой скоуп после `AddContent` вызывает `await builder.AddContentDirectory(options)`;
загрузчик директории - `[Inject] IContentDirectories` -> `Get(name)`, ассеты - из её корней: `loader.RootAssets<T>()`, затем
`Load(lifetime, reference)`. Каталоги остаются на основной директории. `IContentLoader` получил `RootAssets<T>()`,
`ContentBuild.Prepare` / `ContentBuild.Current` - список директорий. Кэш страницы (Cache API) чистится по каждой директории отдельно.

Проверка: `grep -rn ": IContentLoader\|ContentBuild\.\(Prepare\|Current\)" client/Assets --include=*.cs` - своя реализация добавляет
`RootAssets<T>()`, вызовы `ContentBuild` берут список.

### Лейауты в пакете `com.noncasted.framework.layout` (сборка `Internal.Layout`)

Кого касается: клиент, `BREAKING` для сборок, которые используют лейаут из кода (в том числе сгенерированные `*Bindings.g.cs`):
mines-leader (`GamePlay`, `Menu`), peace-data-reborn (`GamePlay`). Сцены и префабы не трогать: GUID скриптов, namespace
(`Global.UI`) и имена классов прежние.

`DesignContainer`, `DesignGrid`, `DesignItem`, `DesignLayoutBase` и их перечисления (`DesignAxis`, `DesignMainAxisMode`, ...) перенесены
из `Internal.UI` в сборку `Internal.Layout` нового пакета `client/modules/layout` (GUID `632dc5ed7f974d89b8b5e2acd4a1bbda`, без ссылок на
uGUI / TMP / inputs), инспекторы - в `Internal.Layout.Editor`, тесты - в `Internal.Layout.Tests`. Сборка `Internal.UI.Editor` удалена
(в ней были только инспекторы лейаута). Пакет `ui` зависит от `layout`, игре без `ui` (только спрайты) лейаут доступен отдельно.

Сделать:
- `client/Packages/manifest.json`: `"com.noncasted.framework.layout": "file:../../tools/framework/client/modules/layout"` рядом с
  `com.noncasted.framework.ui` (зависимость `file:` не подтягивается сама).
- `grep -rln "DesignContainer\|DesignGrid\|DesignItem\|DesignLayoutBase\|DesignAxis\|DesignMainAxisMode\|DesignCrossAxisMode\|DesignContainerFit\|DesignHorizontalPivot\|DesignVerticalPivot\|DesignItemSizing\|DesignRefreshMode\|DesignGridConstraint\|DesignGridCellSizing\|DesignGridFlow" client/Assets --include=*.cs`
  - asmdef каждой такой сборки добавляет `"GUID:632dc5ed7f974d89b8b5e2acd4a1bbda"` в `references`; без этого CS0246
  `The type or namespace name 'DesignContainer' could not be found`.
- Ссылка на `Internal.UI.Editor` (GUID `221aaddb19c647fab6fe86dcb68ac643`) в asmdef игры, если есть, удаляется
  (`grep -rln "221aaddb19c647fab6fe86dcb68ac643\|\"Internal.UI.Editor\"" client/Assets --include=*.asmdef`).

### Локализация без TMP (`LocalizedLabel` в сборке `Internal.Localization.Tmp`)

Кого касается: клиент, `BREAKING` для сборок игры, которые используют `LocalizedLabel` фреймворка. Сейчас ни одна игра пакет
`com.noncasted.framework.localization` не подключает (у pixel-swarm и peace-data-reborn свои `LocalizedLabel` в `client/Assets`,
их это не касается); шаг для игры, которая подключила пакет до этого релиза.

`Internal.Localization` больше не ссылается на TextMeshPro и собирается в игре без uGUI / TMP. `LocalizedLabel` (namespace
`Global.Localization`, GUID скрипта прежний) перенесён в сборку `Internal.Localization.Tmp` того же пакета
(`client/modules/localization/Tmp`, GUID `efc5f5618d014be4a2157ab8e084fdae`): она компилируется только при `com.unity.ugui` 2.x в
проекте (`versionDefines` -> `FRAMEWORK_TMP`, `defineConstraints`). Тест метки - в `Internal.Localization.Tmp.Tests`.

Сделать:
- `grep -rln "LocalizedLabel" client/Assets --include=*.cs` - asmdef каждой такой сборки добавляет
  `"GUID:efc5f5618d014be4a2157ab8e084fdae"` в `references`; без этого CS0246 `The type or namespace name 'LocalizedLabel' could not
  be found`. Своя копия `LocalizedLabel` в `client/Assets` (pixel-swarm, peace-data-reborn) не трогается.
- Игре без `com.unity.ugui` пакет теперь доступен: `"com.noncasted.framework.localization": "file:../../tools/framework/client/modules/localization"`
  в `client/Packages/manifest.json` (core и common уже должны там быть).

### Content: доп. каталог регистрируется в фоне, индекс предзагруженного - в Resources

Кого касается: клиент, `BREAKING` для игр с `AddContentDirectory` (gamejam-autobattler); всех игр с WebGL и `IsPreloaded` - без
действий.

- `builder.AddContentDirectory(options)` теперь `void` и не ждёт регистрации: индекс, заголовок и корни доп. каталога грузятся в
  фоне, скоупы после корня не ждут его запросов. Загрузчик - `await IContentDirectories.Wait(lifetime, name)`; `Get(name)` бросает,
  пока каталог регистрируется. Ошибка регистрации - в лог и в каждый `Wait`.
- Индекс каталога с `IsPreloaded` в WebGL-сборке лежит TextAsset'ом `Resources/Content/<name>.index` рядом со штампом (в `.data`),
  без запроса на старте; индекс пишется без отступов (было 60% байт). Спаны `Content: index`, `Content: register <name>`.

Сделать: `grep -rn "AddContentDirectory\|IContentDirectories" client/Assets --include=*.cs` - убрать `await` перед
`AddContentDirectory`, `_directories.Get(name)` в коде после старта заменить на `await _directories.Wait(lifetime, name)`.

### Контент: предзагруженный каталог WebGL без индекса

Кого касается: клиент, игры с `"IsPreloaded": true` каталога (`Content` / `ExtraContent` в `ProjectSettings/Framework.json`) на WebGL.
`BREAKING` только для кода, который читает `IContentLoader.Progress(...)` предзагруженного контента или собирает `ContentDirectory` /
`FetchedContentStore` сам (тесты).

Индекс предзагруженного каталога больше не кладётся в `.data` (`Resources/Content/<name>.index` удалён, `ContentIndex.FromResource`,
`ContentPaths.IndexResource`, `ContentBuildFiles.IndexAsset` / `WriteIndex` удалены): все файлы реальны с начала, загрузке нечего
скачивать, а разбор JSON индекса стоил ~120 мс старта на Mali-телефоне (`.data` меньше на размер индекса, у autobattler -83 КБ gzip).
Плеер берёт `PreloadedContentFiles`: пустые списки файлов, поэтому `Progress(...)` предзагруженного объекта / сцены теперь
`Total` 0 (как у контента на диске standalone и в Play Mode). Ссылка другого каталога, загруженная лоадером предзагруженного,
падает теперь в движке (`The engine did not load ...`), а не `KeyNotFoundException` индекса. `IContentStore.Prepare(lifetime)` без
индекса, `FetchedContentStore` получает индекс в конструкторе (`new FetchedContentStore(source, folder, gap, index)`).

Сделать: ничего, если игра не показывает прогресс предзагруженного контента. Проверка:
`grep -rn "\.Progress(" client/Assets --include=*.cs` - полоса загрузки по предзагруженному объекту или сцене должна принимать
`Total == 0` (`IsDone` сразу true). Новые спаны стартового трейса: `Content: stamp`, `Content: index validate`, `Content: bind catalogues`,
`Group: <name>` (синхронный первый доступ к предзагруженной группе), `Content: load <type>` (`LoadNow`).
### Бэкенд: владелец соединения ждёт его закрытия, сервер не шлёт запросы клиенту

Кого касается: бэкенд всех игр (meta- и game-шлюзы, свои реализации соединений и копии тестов сети).

`Connection.Run` без подписчика у `Reader` закрывает сокет и бросает исключение. `IConnection.Closed` - задача «сокет закрыт», после
неё владелец выходит из обработчика (иначе Kestrel рвёт сокет вместе с close frame, клиент видит 1006). Серверных запросов к клиенту
больше нет: `WriteRequest`, `Responses`, `OnRequestHandled`, `PendingRequestsCount` удалены. Непринятое сообщение клиента
(не читается, null, ответ клиента) закрывает соединение (`InvalidPayloadData` / `ProtocolError`). Handshake ограничен 16 KiB.

- Meta-шлюз: `grep -rn "connection.Run().NoAwait()" backend/Orchestration`. Сначала подписать dispatcher
  (`_entryPoint.OnConnected` / `UserCommandsDispatcher.Run`), затем `var run = connection.Run();`; после конца сессии
  `lifetime.Terminate()` и `await connection.Closed`, только потом выход из middleware.
- Game-шлюз: `grep -n "user.Lifetime.Listen" backend/Orchestration/GameGateway/SessionGate.cs` - в `Join` после конца lifetime
  пользователя `await ((IHostedUser)user).Socket.Closed`.
- Dispatcher: `grep -rn "reader.Responses\|OnRequestHandled" backend` - удалить подписку на `Responses` и `HandleResponse`.
- Свои `IConnection` / `IConnectionReader` / `IConnectionWriter` (тестовые миры pixel-swarm `SessionTestWorld`, storytale
  `ConstructorTestWorld`): `grep -rn "IConnectionWriter\b\|: IConnection" backend` - добавить `Task Closed`, удалить `Responses`,
  `WriteRequest`, `OnRequestHandled`.
- Копии тестов сети (mines-leader, peace-data-reborn): `grep -rn "WriteRequest\|PendingRequestsCount\|RequestTimeout\|Responses" backend/Tools/Tests`
  - удалить тесты серверных запросов; нечитаемый payload теперь закрывает соединение.

### Бэкенд: `OperationProgress` ушёл из framework

Кого касается: mines-leader, pixel-swarm, peace-data-reborn (бенчмарки, `Console/Infrastructure/Benchmark/BenchmarkEntry.razor`).

`grep -rln "IOperationProgress\|OperationProgress\|OperationStatus" backend` - перенести копию `IOperationProgress` /
`OperationStatus` / `OperationProgress` (из `git show v0.6.0:backend/Common/Extensions/OperationProgress.cs` framework) в
`Tools/Benchmarks/Common` игры, в сборку, которую видит её консоль. При переносе: `Release` семафора в `finally`, `Set` вне lock
(иначе подписчик, который бросает, вешает следующий `SetStatus`).

### Бэкенд: резервирование места в сессии возвращает причину

Кого касается: бэкенд всех игр с сессиями.

`grep -rn "TryReserve\|IsHeldOnlyBy" backend --include=*.cs`. `session.TryReserve(id)` -> `session.Reserve(id)`, результат
`SessionReservation` (`Reserved`, `Sealed`, `Closed`, `Full`, `HeldOnlyByOwnStale`): `== true` -> `== SessionReservation.Reserved`;
моки - `.Reserve(...).Returns(SessionReservation.X)`; `SessionAdmission.TryReserve(id, max, now)` -> `Reserve(id, max, now)`;
`ISessionAdmission.IsHeldOnlyBy` удалён. `HeldOnlyByOwnStale` - сессия, которую держит только зависшее соединение самого
пользователя: она сейчас закроется, лобби отвечает `NotFound`.

`LobbyJoinStatus` переехал в `Game.Lobbies` (`Hosting/Lobbies/LobbyJoinStatus.cs`): `grep -rln "LobbyJoinStatus" backend --include=*.cs`
- добавить `using Game.Lobbies;`. `SessionPayloads` стал `static`.

### Бэкенд: состояния кластера меняются методами, координатор выходит из старого деплоя

Кого касается: бэкенд всех игр (тесты, код, который трогает состояния кластера).

- `grep -rn "IsDevelopment = " backend/Tools/Tests` - удалить `IsDevelopment = true,` (`IServiceEnvironment.IsDevelopment` удалён).
- `grep -rnE "\.Slices\[[^]]+\] =|Slices = \{|Members = new|Entries\.(Add|Remove)|GameFlags\[" backend` - вместо прямой записи
  `SetSlice(id, slice)` / `RemoveSlice(id)`, `ServiceDiscoveryStorageState.Set(overview)` / `Remove(id)`,
  `LobbyRegistryState.Add` / `Remove`, флаги через `SetGameFlag`. Известное место: peace-data-reborn `Tools/Tests/Meta/GameServerBalancerTests.cs:113`.
- Свои `IDeployContext` реализуют `Leave()` (`grep -rn ": IDeployContext" backend`). Заменённый координатор теперь выходит из
  деплоя: всё, что игра повесила на `DeployLifetime`, у него останавливается.
- Список открытых лобби не показывает лобби серверов, которых нет в discovery.

### Бэкенд: метрики и трейсы регистрируют сами фичи, `RequeueStuck` удалён

Кого касается: бэкенд всех игр.

- `grep -rn 'TraceExtensions.AllSources\|AddMeter("Backend")\|BackendMetrics' backend --include=*.cs` - в
  `Orchestration/Extensions/ServiceDefaultsExtensions.cs` убрать `foreach (var source in TraceExtensions.AllSources) ...` и
  `.AddMeter("Backend")`: их регистрируют `AddOrleansUtils`, `AddMessaging`, `AddTaskScheduling`. `BackendMetrics.X` ->
  `<Feature>Telemetry.X` (`TransactionTelemetry`, `SideEffectTelemetry`, `DurableQueueTelemetry`, ...). Имена метров и
  инструментов прежние.
- storytale-workshop: `grep -rn 'TraceExtensions.PlayerEndpoints' backend --include=*.cs` - свой `ActivitySource("Player.Endpoints")`
  в игре, `builder.AddTraceSource(source)`; `.Start(name)` остался в `Common.Extensions`.
- `grep -rn 'RequeueStuck()' backend --include=*.cs` - удалить `await _sideEffectsStorage.RequeueStuck();` из
  `Orchestration/Coordinator/ClusterCoordinator.cs` (он возвращал в очередь эффекты, которые silo выполняли, и они выполнялись
  дважды; записи упавшего worker-а возвращает сам `SideEffectsWorker` через `RequeueStuckOlderThan`). В
  `Tools/Tests/State/SideEffectTests.cs` -> `RequeueStuckOlderThan(TimeSpan.Zero)`.
- Удалены `CommonGrain`, `OrleansUtils.Iterate`, `AsSetupLoopStage`, `PostgresJsonbConverter`, `AddEnvironmentOptions`: игры их не используют.
- `DurableQueue`: повтор ждёт наблюдателя, у которого доставка упала; если он не вернулся, сообщение уходит в dead letter после
  `MaxRetryCount`. `AddressableState.SetValue` повторяет publish сохранённого значения. `IsOrleansStarted` становится `true` на
  стадии `Active`. `StateStorageReader.ReadPage` бросает вместо пустой страницы.

### Консоль: `AddConsoleInfrastructure`, таблица аудита из framework

Кого касается: игры с консолью (mines-leader, peace-data-reborn, pixel-swarm, storytale-workshop, gamejam-autobattler).

- `grep -rn "AuditLogStorage\|HeapReportStorage\|HeapReportCollector\|SideEffectsAggregatorService" backend/Orchestration/ConsoleGateway/Program.cs`
  - шесть строк регистрации заменить на `builder.AddConsoleInfrastructure(new ConsoleInfrastructureOptions { CoordinatorConfigFolder = ... })`.
  Папку конфигов координатора для кнопки Write JSON игра передаёт сама (прежнее значение -
  `Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Coordinator")`, например только в Development); без неё кнопки нет.
- `grep -rln "audit_log" backend/Tools/DeploySetup` - удалить `AuditLogSetup.cs`, в `PostResourcesSetup` вызвать
  `await AuditLogTable.Create(connection)`.
- `grep -n "downloadFileFromBase64" backend/Orchestration/ConsoleGateway/App.razor` - удалить функцию: Monitoring импортирует
  `_content/Console/file-download.js` сам.
- `IAuditLogStorage.Write` / `GetPage` бросают при сбое базы, конструктор `AuditLogStorage` без logger:
  `grep -rn "IAuditLogStorage" backend/Console` - на своих страницах try/catch с toast или `AuditLog.Report(...)`.
- Удалены `OperationView.razor`, `HomeGridButton.razor`, `IHeapReportStorage.TryGet`: игры их не используют.

### match-metrics: имена и лимиты в shared, поток сервера после конца бросает

Кого касается: игры с модулем `match-metrics` (pixel-swarm).

- `grep -rn "MatchMetricsLimits\.\|MatchMetricNames\." client/Assets backend` - `Shared.SharedMatchMetricsLimits` /
  `Shared.SharedMatchMetricNames` (`IsName`, `IsKey`, `IsValue`); клиентский `MatchMetricsLimits`, `MatchMetricNames.IsValid` и
  серверный `Game.MatchMetrics.MatchMetricNames` удалены. Типы хранения и парсер бэкенда стали `internal`.
- `grep -rn "IMatchMetricsServerStream" backend` - `Event` / `Gauge` после конца lifetime потока бросают: не писать после конца сессии.
- Парсер отклоняет (`Rejected`) пересекающиеся чанки одного слота и ряд, объявленный в двух слотах.

### Content: встроенные AssetBundles вместо content directory (BREAKING)

Кого касается: клиент, все игры с `Content` / `ExtraContent` в `ProjectSettings/Framework.json`.

Контент теперь - бандлы движка (`com.unity.modules.assetbundle`, зависимость пакета core; без Addressables и UnityWebRequest,
+~40 KB br к `Build/` против голого плеера). Бандл задаётся ассетом `ContentBundle` (Create > Framework > Content Bundle): корни,
`IsPreloaded` (в `.data` WebGL), `IsGzip` (WebGL качает `<file>.gz`, распаковывает страница). В бандл входят корни и всё, чего они
достигают прямыми и ленивыми ссылками, кроме корней других бандлов (на них - зависимость, грузится вместе). Сборка падает на корне
двух бандлов, на ассете, который нужен двум бандлам напрямую и не назначен ни одному, на ленивой цели двух бандлов без корня, на
пустой / битой ленивой ссылке. Range, индекс, архив, порядок загрузок (`ContentLoadOrder`, меню Record Load Order) удалены.

Что сделать игре:
- Секции `Content` и `ExtraContent` из `Framework.json` убрать (с ними редактор бросает исключение) и на каждую директорию создать
  ассет `ContentBundle` с её `Roots` и `IsPreloaded` (`IsArchiveGzip` -> `IsGzip`; `Fetch` не нужен: бандл всегда качается целиком).
  Имя ассета = имя бандла.
- `builder.AddContentDirectory(new ContentOptions(name), registration)` -> `builder.AddContentBundle(name, registration)`;
  `IContentDirectories` -> `IContentBundles` (`Get` / `Wait` те же). `ContentOptions` без `rangeGap`.
- Зарегистрированный бандл держится до конца рут-скоупа и не выгружает свои объекты: держать его тонким (только ленивые ссылки), а
  тяжёлое - корнями отдельных бандлов: они грузятся на лайфтайм загрузки и выгружаются после неё.
- Сериализованные ссылки переносятся сами (`_guid` / `_localId` остались, `_loadable` / `_id` больше не читаются). `ContentReference`
  без `Id` и `Loadable`: конструктор `(guid, localId)`, `ContentScene(guid)`; `ContentReferenceFields.SyncObject` / `SyncScene`
  удалены (`Target` / `TargetScene` вместо них). `IContentBuildStep.BeforeContentBuild()` без параметра.
- Сервер: файлы `StreamingAssets/Content/<name>.<hash>.bundle[.gz]` как есть, Range не нужен.

Проверка: `grep -rn "AddContentDirectory\|IContentDirectories\|ExtraContent" client/ProjectSettings client/Assets` пусто; сборка
игры логирует `[ContentBuild] <name>: ...` по каждому бандлу; в `StreamingAssets/Content` только файлы `<name>.<hash>.bundle[.gz]`.

### Content: бандлы собирает Scriptable Build Pipeline

Кого касается: клиент, все игры на content bundles (0.8).

`ContentBundleBuilder` собирает бандлы через `ContentPipeline.BuildAssetBundles` пакета `com.unity.scriptablebuildpipeline` 4.0.1
(новая зависимость `com.noncasted.framework.core`), а не через `BuildPipeline.BuildAssetBundles`. Тот пишет type trees в бандлы сцен
при любых опциях, и первая сцена из бандла на телефоне грузилась на ~100 мс дольше. Бандлы сцен стали меньше примерно вдвое, хэши
всех бандлов один раз меняются.

Проверка: открыть проект (Unity ставит пакет сам), `grep -n scriptablebuildpipeline client/Packages/packages-lock.json`, собрать
клиент; в логе `[ContentBuild] N asset bundles ... built`. Закоммитить изменённый `packages-lock.json`.
Проверка: `grep -rn "ContainerGenerator" tools/scripts/*.sh .github/workflows/*.yml`; там, где сборка идёт рядом с Unity на том же
дереве, добавить свойство.
