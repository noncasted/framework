---
task: client_telemetry
updated: 2026-11-03
---

## Snapshot

| Шаг | Статус | Evidence | Блокер |
|-----|--------|----------|--------|
| 0 Проверка метрик на телефоне | отменён | решение пользователя, перенос в шаг 11 | — |
| 1 Контракт и проект бэкенда | [x] | Framework.Tests -namespace ClientTelemetry: 196 зелёных | — |
| 2 Хранилище | [x] | ClientTelemetry 240 зелёных, весь Framework.Tests 1342 | — |
| 2b Грейн сессии | [x] | Framework.Tests 1404, ClientTelemetry 302 | — |
| 3 Эндпоинты и флаг | [x] | Framework.Tests 1415, ClientTelemetry 313 | — |
| 3b Постоянный флаг | [x] | Framework.Tests 1412, ClientTelemetry 310 | — |
| 4 Ядро клиентского модуля | [x] | EditMode 731: 727 passed, 4 skipped, 0 failed (отчёт агента) | — |
| 5 Жизненный цикл страницы | [x] | EditMode 747: 743 passed, 4 skipped; chromium beacon; телефон на шаге 11 | — |
| 4c Meta-сокет (4c.1, 4c.2, 6b) | [x] | 4c.1: Framework.Tests 1438 из 1438, хост на сокете; 4c.2a, 4c.2b, 6b: EditMode 950 (946 passed, 4 skipped), PlayMode 113, Framework.Tests 1512, сквозной скрипт 24 из 24 | — |
| 6 Сквозная проверка транспорта (HTTP, заменяется 6b) | [x] | `test-telemetry-transport.sh`: 4 из 4, код 0; PlayMode телеметрии 15 (11 passed, 4 skipped); ClientTelemetry 349 | — |
| 7 Базовые коллекторы | [x] | EditMode 806: 802 passed, 4 skipped; PlayMode телеметрии 11; node 25 | — |
| 8 Ошибки | [x] | EditMode 865: 861 passed, 4 skipped; PlayMode 11; node 17; ClientTelemetry 349; Framework.Tests 1451 | — |
| 9 Панель в консоли | [x] | ClientTelemetry 385, Framework.Tests 1487; браузер: списки, карточка, живое обновление, флаг, audit log; `media/*.png` | — |
| 10 Инструмент агента | [x] | скилл `client-telemetry`: unittest 64 OK, повтор на сокете в 6b, разбор свежим агентом | — |
| 5s Трейс запуска клиента | [x] | 5s.1: Framework.Tests 1505, скилл 62, окно; 5s.2: EditMode 950 (946 passed, 4 skipped), PlayMode 12; сквозная 6b | — |
| 11 Подключение Pixel Swarm и проверка на телефоне | [/] | 11a; прод: `9c3d8b95`, телефон web: сессии menu, lobby, run, трейс `final`, фон 20 с, закрытие `lost`; Telegram: сессии получены | — |
| 12 Документация и релиз | [x] | test-all: все стадии PASS (backend 1512, telemetry 64, EditMode 949 из 953, PlayMode 89 из 113); коммит `205bcd53` | push за пользователем |

## Заметки

### [2026-11-03] Решения при постановке

- Старый план `docs/plan/client_error_reports.md` удалён по просьбе пользователя. Он не был в git, поэтому его содержимое не восстановить. Из него в задачу вошла отправка ошибок и группировка; статусы групп, символы, дисковая очередь и минидампы не входят.
- Изначально обсуждался приём в ConsoleGateway и включение по ключу в ссылке. Пользователь решил трекать всех игроков с общим флагом, поэтому приём перенесён в публичный meta gateway, а ключ на клиенте убран.
- Базовые коллекторы лежат во фреймворке, это подтвердил пользователь.

### [2026-11-03] Приватность: только метрики

Пользователь: личные данные не трекаем, если встречаются — вырезаем, шлём только метрики, даже заряд батареи не трекаем.

Изменения плана:
- Обычные логи убраны из отправки: свободный текст нельзя надёжно очистить. Вместо них метки.
- Ошибки оставлены, потому что пользователь отдельно просил их отправлять; текст чистится на клиенте и на сервере.
- Устройство сокращено до закрытого списка, без полного UA.
- `sessionId` на один запуск, IP не хранится и не логируется.

### [2026-11-03] Находки

- `ConsoleAccess` и `RequireConsoleToken` лежат в игре (`Orchestration/Extensions`), таблица `audit_log` создаётся в `DeploySetup` игры. Отсюда расширения фреймворка, на которые игра вешает токен, и `ClientTelemetrySetup.Run`.
- В pixel-swarm `webGLExceptionSupport: 1`, `webGLDebugSymbols: 0`.
- `RateLimiter` и лимитов размера тела в `backend` и `Orchestration` пока нет: это первые в проекте.

### [2026-11-03] Что поменять в игре на шаге 11 (из отчёта шага 3)
- `ProjectReference` на `backend/ClientTelemetry` там, где используется.
- `MetaGateway/Program.cs`: `builder.AddClientTelemetry()`, после `AddBackendMiddleware`: `app.MapClientTelemetryIngest()`, `app.MapClientTelemetryRead().RequireConsoleToken()`.
- `ServiceDefaultsExtensions.cs`: в `Filter` исключить `ClientTelemetryRoutes.Ingest`; там есть второй голый вызов `AddAspNetCoreInstrumentation`, после правки проверить, что спана preflight нет.
- На проде проверить: два запроса с разной левой частью `X-Forwarded-For` попадают в один счётчик; access log Traefik в Coolify (может содержать IP); `Origin` в `CORS_ALLOWED_ORIGINS`; лимит 600 запросов в минуту с IP для мобильных операторов.
- Шаг 2b может заменить `ClientTelemetrySetup.Run` в `PostResourcesSetup.cs`.

### [2026-11-03] Метрики шага 7 (для панели и скилла)
`frame.fps`; `frame.dt.avg/p50/p95/max` мс; `frame.over33/over50`; `frame.cpu`, `frame.cpu.main`, `frame.gpu` мс; `memory.allocated/reserved/mono.used/mono.heap/textures` байты; `gc.collections`; `render.draw_calls/setpass/triangles/vertices/textures/render_textures`; `web.wasm.heap`, `web.js.used/total/limit` байты.

Для шага 11: в pixel-swarm `enableFrameTimingStats: 0`, нужно включить, иначе `frame.cpu*` и `frame.gpu` не появятся. `FrameProfiler` в development тоже вызывает `CaptureFrameTimings`. Chrome на Android урезает UA до «Android 10», `osMajor` может быть занижен.

### [2026-11-03] Что поменять в игре на шаге 11 (из отчёта 2b, заменяет пункт про `ClientTelemetrySetup`)
- Роль Silo: `AddClientTelemetrySessions()` через GameModule. Meta: `AddClientTelemetry()`. Console: `AddClientTelemetryReader()`.
- `ProjectReference` на ClientTelemetry в GameComposition/Extensions: там грейн и `StatesLookup`, таблицы создаёт `StatesSetup`. `ClientTelemetrySetup.Run` больше нет.
- Шаг 8 сервер: отпечаток считать в `TelemetrySessionBuffer.Apply`, группы класть в `Digest`, глобальные группы считать по коллекции.

### [2026-11-03] Шаг 8, клиент: что проверить на шаге 11
Настоящий браузер и jslib с wasm (`_free`, `makeDynCall('vii')`, маршалинг строк); скорость регулярок на телефоне (худший текст на десктопе около 160 мс); при `webGLExceptionSupport: 1` запись может уйти без `stack`.

### [2026-11-03] Шаг 3b: на шаги 9, 11, 12
- Шаг 11: Gateway `AddClientTelemetry()`, консоль `AddClientTelemetryReader()`, `AddConsoleGameFlag` не вызывать. Хост обязан проходить стадию `IOrleansStarted` (`ServiceLoop`), иначе приём навсегда отвечает 503. Миграция не нужна, флаг один раз включить на Features.
- Шаг 11: `Console.csproj` ссылается на ClientTelemetry, транзитивные ссылки отключены. Проверить, попадает ли `Framework.ClientTelemetry.dll` в вывод `ConsoleGateway`; если нет, страница Features упадёт при загрузке типа: нужна ссылка и шаг в `releases/unreleased.md`.
- Шаг 9 или 11: секция переключателя и запись в audit log не проверены в браузере.
- Шаг 12: шаг в `releases/unreleased.md`.

### [2026-11-03] Шаг 8s: поля для шагов 9 и 10
Строка ошибки: `fingerprint` (16 hex). `digest.errorGroups`: `groups[]` (`fingerprint`, `level`, `message`, `count`, `rows`, `firstSeenAt`, `lastSeenAt`), `omittedGroups`, `omittedCount`; 12 групп. `GET /api/client-telemetry/errors?limit=` (1..5000, по умолчанию 100): `totalGroups`, `countedSessions`, `sessionsWithoutDigest`, `omittedGroups`, `omittedCount`, `droppedErrors`, `groups[]` (`fingerprint`, `level`, `message`, `count`, `sessions`, `firstSeenAt`, `lastSeenAt`, `sessionIds` до 20). Порядок: `count`, `lastSeenAt`, `fingerprint`. `IClientTelemetryReader.GetErrorGroups(int limit)`. Стека в группах нет: он в `/sessions/{id}/errors`. Параметра сортировки и фильтров по билду нет.

### [2026-11-03] Шаг 6: хост с данными для шагов 9 и 10
После `dotnet build`: `<WT>/.build/bin/ClientTelemetry.TestHost/Debug/net10.0/Framework.ClientTelemetry.TestHost --token dev --port 5099 --seed`, нужен Docker, свой Postgres удаляется при SIGTERM. Адрес `http://127.0.0.1:5099`, `Authorization: Bearer dev`. `--seed`: 6 `ended`, 1 `live` (через 15 с уходит в `lost`, не наблюдалось), 2 группы в `/errors`, флаг включён. Служебные `POST /test-host/flag/{true|false}`, `GET /test-host/stats`. UI консоли в хосте нет. Шаг 12: числа тестов, PlayMode +4 skipped.

### [2026-11-03] Шаг 9: на шаги 11 и 12
- Шаг 11: `ConsoleGateway` нужна прямая ссылка на `ClientTelemetry.csproj` и `AddClientTelemetryReader()`; панель появится сама. Вопрос 3b снят: `Framework.ClientTelemetry.dll` попадает в вывод игры, которая ссылается только на Console (scratch-проект, build и publish).
- Шаг 11: графики реальных метрик `frame.*`, `web.*` на настоящем клиенте. Живая сессия не показывает `Dropped` и `Rejected`: они приходят с digest при закрытии.
- Шаг 12: в `CLAUDE.md` дописать, что `app.css` игры закоммичен и собирается вручную; панель использует только классы из `app.css` всех игр. Аргументы хоста `--console <папка>`, `--no-telemetry`, новые числа тестов.

### [2026-11-03] Решение: транспорт через meta-сокет, сессия = забег
Полный текст в `_info.md`. Устаревают заметки шага 3 (`MapClientTelemetryIngest`, `Filter` спанов, `X-Forwarded-For`, Traefik, CORS, лимит 600 запросов), reopen и заметка о beacon в фоне из `_result.md`. Новый риск для шага 11: сокет фоновой вкладки может закрыться у живого клиента.

### [2026-11-03] Шаг 5s добавлен
Трейс запуска через meta-сокет под флагом телеметрии, окно как Startup Profiler в консоли, API и скилл. Полный текст в `_info.md`.

### [2026-11-03] Шаг 10: на шаги 6b, 11, 12
- 6b: повторить через новый транспорт `monitor` (закрытие и `lost`), живые `summary`, `watch`, закрытую сессию, отчёт свежего агента; старый скрипт отправки лежал вне репозитория на HTTP.
- 11: раздел `## client-telemetry` в `.claude/project-skills.md` (`url`, `env-file`), ссылка `ln -s ../../tools/framework/.claude/skills/client-telemetry .claude/skills/client-telemetry`.
- 12: автотесты скилла не входят в `test-all.sh`, решить там.

### [2026-11-03] 10b: теги в скилле
`monitor --tag`, `sessions --tag`, теги в отчётах; 57 тестов на фейковом сервере. На 6b: повтор на настоящем хосте, в том числе `tags` и `watch` (`ackSeq`).

### [2026-11-03] 4c.1: контракт для 4c.2b и игры
Контракт: `backend/ClientTelemetry/Contract/CONTRACT.md`, `shared/ClientTelemetry/SharedClientTelemetry.cs`, теги `NetworkContextTags.Telemetry*` 200-204. Порядок: `SessionStart` -> пачки по одной, следующая после ответа -> `SessionEnd`; смена сессии: новый `SessionStart`. Клиент сам обрезает тексты до лимитов.
Шаг 11, игра: пять тегов в `NetworkContextUnion`; Gateway `AddClientTelemetry()`, silo `AddClientTelemetrySessions()`, консоль `AddClientTelemetryReader()`; три `UserCommand` в `MetaGateway/UserFlow/Commands` и `AddUserCommand`, вид: `await telemetry.Of(session.Lifetime).Start(request)`, также `.Accept`, `.End`; `MapClientTelemetryRead().RequireConsoleToken()`; хост проходит `IOrleansStarted`. Заметки шага 3 про ingest, CORS, XFF, Traefik устарели.
6b: драйвер: `ws://<addr>/test-host/meta` без токена, `/test-host/stats` отдаёт `{messages, byType}`; образец `TelemetryMetaClient` в тестах.

### [2026-11-03] 4c.2a: на 6b и 12
`CLAUDE.md`: абзац про `test-telemetry-transport.sh` и `TelemetryTransportEndToEndTests` устарел (6b). Шаг 12: числа EditMode. `TelemetryWeb.jslib` передаёт устройство в C# через JSON: это обмен страницы и C#, не отправка.

### [2026-11-03] 5s.1: контракт для 5s.2 и игры
`StartupTrace { StartupId, Device, Name, Final, DurationMs, Frames, Spans }`, `StartupSpan { ParentIndex, Name, StartMs, DurationMs, Frames, Unfinished, AllocBytes }`, `StartupTraceResult { Enabled, StartupId }`, `SessionStart.StartupId`. Первая отправка с `Guid.Empty`; id действует, пока жив сокет; после реконнекта слать заново. `ParentIndex` меньше своего индекса, корень -1. `Enabled=false`: трейсы на этом сокете больше не слать.
Шаг 11, игра: теги 205, 206 в union; четвёртая команда `telemetry.Of(session.Lifetime).AcceptStartup(request)`; таблицы `state_client_telemetry_startups`, `state_client_telemetry_startup_traces` через `StatesLookup`, проверить на деплое.

### [2026-11-03] 4c.2b: шаг 11, игра
`ITelemetrySocket` на `MetaBackend` (`.As<ITelemetrySocket>()`); в `MetaScopeExtensions.Construct` `builder.AddSocketClientTelemetry(options)` и `AddStandardTelemetryCollectors()`; теги 200-206 в union (забытый тег: строка в логе, без падения). Сессию открывать только после авторизации meta-сокета, иначе `Failed` и потеря сессии. Коллекторы в скоупе Meta: коллектор из GamePlay в список не попадёт. Проверить: офлайн-режим (`UnknownCommand`, ожидается `Failed`); `ForceSendAll` в `GamePlayLoop` на соединении матча, не meta.
Шаг 12: записи о модуле в `CLAUDE.md` нет.

### [2026-11-03] 5s.2: шаг 11, игра
После каждой авторизации meta-сокета вызывать `IClientTelemetry.SendStartupTrace()` до первого `StartSession`. На телефоне измерить стоимость подготовки трейса (бюджет 2 мс выбран по редактору).

### [2026-11-03] 6b: на шаг 11
- Стили для `--console`: копия `app.css` и темы из `ConsoleGateway/wwwroot` игры в одну папку как `app.css`, `theme.css`.
- Стек: `Run.cs:12` превращается в `<url>`, смотреть на настоящем стеке; правила в двух копиях.
- Пустая единица `Gauge(name, v, "")` отбрасывает значение, в игре писать `null`.
- `client/project/traces/` не в git: перед коммитом исключить.

### [2026-11-03] 11a: на 11b и 12
- 11b: таблицы `state_client_telemetry_*` на настоящей мете, приём, чтение через API и консоль; флаг на Features; настоящие `frame.*`, `web.*`, время подготовки трейса; сборка telegram; сокет фоновой вкладки; стек `Run.cs:12`.
- 12: в `releases/unreleased.md` добавить строку `manifest.json`, ссылку `GameComposition` на `ClientTelemetry`, `enableFrameTimingStats` в каждом Build Profile, список состояний в `StatesLookupGeneratorTests`. Числа тестов в `CLAUDE.md`, скилл в `test-all.sh`. `client/project/traces/` не в git.
