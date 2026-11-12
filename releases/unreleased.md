# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

### Bindings: класс уже сгенерирован в другой папке

Кого касается: клиент, игры с префабами или сценами, чьи `*Bindings.g.cs` лежат не в `Generated` той сборки, куда их сейчас
определяет генератор (префаб перенесён в папку другой сборки, файл bindings перенесён руками).

Раньше генератор молча писал второй файл того же класса (CS0101 / два класса с одним именем), теперь отказывает с ошибкой
`... is already generated into <файл>, and this object resolves to <новый путь>`, а Regenerate All падает с
`N bindings failed to regenerate`.

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
- Deploy setup: `await MatchMetricsSchema.Create(connection)` (рядом с `AuditLogSetup`; `grep -rn "AuditLogSetup.Run" backend`).
- Хост, который пишет поток сервера (игровой сервер): `builder.AddMatchMetricsWriter()`.

### match-metrics на бэкенде: чтение

Кого касается: игры с модулем `match-metrics` (консоль, агенты, история матчей). Игре без модуля - ничего.

`IMatchMetricsReader` (`backend/MatchMetrics/Reading`): потоки матча, словарь рядов, точки матча по группам тегов (шаг 1 / 10 /
60 с, ось часов забега или серверная), итоги матча и между матчами, события матча. GET эндпоинты `/api/match-metrics/...`
(`MapMatchMetricsRead`), контракт чтения - раздел «Reading» в `Contract/CONTRACT.md`. Таблицы те же, схема не менялась.

Что сделать игре: в хосте с чтением (мета-гейтвей) `builder.AddMatchMetricsReader()` и
`app.MapMatchMetricsRead().RequireConsoleToken()` (токен - свой у игры, как у `MapClientTelemetryRead`).
