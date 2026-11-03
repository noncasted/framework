# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

## Профайлер: стадии трейса

Кому: каждой игре, `BREAKING`. `GameProfiler.Branch` переименован в `GameProfiler.Stage`. Запись при пустом стеке и `Detached` теперь
идут в последнюю открытую стадию, а не в корень трейса, родитель покрывает поздних детей, `LoadRoot` открывает стадию `Startup`.
Пока шаг не сделан, `Scope("Global")` и подобные вкладываются в `Startup`.

Поиск: `git grep -nE 'GameProfiler\.(Branch|Detached)\(|GameProfiler\.Scope\("(Global|Meta|Game loop|Menu|Load)' -- 'client/Assets/*.cs'`

- Большой шаг игры (`Global`, `Meta`, `Game loop`, меню, лобби, загрузка матча), открытый как `Scope` или `Branch`, заменить на
  `GameProfiler.Stage(...)`. Загрузке меню или лобби без своего спана добавить стадию.
- Параллельную ветку стадии открыть через `Detached` в синхронной части (`OnBaseSetup`): после `await` текущая стадия уже другая. Её
  шаги вешать на этот спан (`span.Child`), а запросы создавать внутри `GameProfiler.Ambient(span)`. Пример: `MetaLoop` в pixel-swarm.

## Сетевой клиент: `IConnectionWriter.CurrentRun`

Кому: игре со своей реализацией `IConnectionWriter`, `BREAKING`. Добавлено свойство `CurrentRun`: номер запуска соединения. Реализации из `network` уже его дают, ломаются только фейки в тестах игры.

Поиск: `git grep -nE ':.*\bIConnectionWriter\b' -- 'client/**/*.cs'`

- Добавить свойство в найденные реализации.

## Телеметрия клиентов (необязательно)

Кому: игре, которой нужны метрики и ошибки клиентов. Новый модуль `telemetry`, проекты `backend/ClientTelemetry` и скилл `client-telemetry`. Игра без него ничего не делает. Шаги игры уточняются при подключении первой игры.

Поиск: `git grep -n 'AddSocketClientTelemetry\|AddClientTelemetry' -- '*.cs'`

- Union: теги `NetworkContextTags.Telemetry*` (200-206).
- Бэкенд: gateway `AddClientTelemetry()`, silo `AddClientTelemetrySessions()`, консоль `AddClientTelemetryReader()`, `MapClientTelemetryRead().RequireConsoleToken()`; команды meta-сокета над `telemetry.Of(session.Lifetime)`: `Start`, `Accept`, `End`, `AcceptStartup`.
- Клиент: `AddSocketClientTelemetry(options)` и `AddStandardTelemetryCollectors()` в скоупе meta, `ITelemetrySocket` на соединении с meta. После авторизации сокета `SendStartupTrace()`, затем `StartSession(lifetime, tags)` на каждый участок игры (меню, забег).
- Включить флаг на странице Features консоли: без него телеметрия не пишется.

