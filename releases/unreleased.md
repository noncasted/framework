# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

### Бэкенд: `AcceptStartup` принимает backend trace id сокета (BREAKING)

Кого касается: бэкенд игр с телеметрией клиентов (`AddClientTelemetry`). Запись startup trace хранит id трейса авторизации сокета
в OTLP-бэкенде (`BackendTraceId`, консоль: `Backend trace`, чтение: `backendTraceId`), по нему трейс бэкенда ищется в Aspire dashboard -> Traces.
Поиск: `grep -rn "AcceptStartup(" backend`
Сделать: `telemetry.Of(session.Lifetime).AcceptStartup(request)` -> `.AcceptStartup(request, backendTrace)`, где `backendTrace` -
`ActivityTraceId` span-а авторизации этого сокета (`System.Diagnostics`); у игры нет такого span-а - передать `default` (запись без id).
Тесты игры, которые зовут `AcceptStartup`, получают второй аргумент `default`.

### Консоль: backend-трейс startup клиента из Aspire dashboard (опционально)

Кого касается: консоль игры с телеметрией клиентов (`AddClientTelemetryReader`), которая хочет видеть трейс бэкенда на странице startup.
Без шага ничего не меняется: страница показывает только id (`Backend trace`).
Поиск: `grep -rn "AddClientTelemetryReader" backend`
Сделать: в консоли `builder.AddConsoleBackendTraces(builder.Configuration.GetSection("AspireDashboard"))` (секция `Url`, `ApiKey`; пустое
значение бросает) или `builder.AddConsoleBackendTraces(new ConsoleBackendTraceOptions { BaseUrl, ApiKey, Timeout })` (namespace
`Console.Infrastructure.BackendTraces`). Дашборду - `DASHBOARD__API__AUTHMODE=ApiKey` и `DASHBOARD__API__PRIMARYAPIKEY=<ключ>` (тот же ключ
консоли); `Url` - адрес фронтенда дашборда (`GET <Url>/api/telemetry/traces/{id}`, header `x-api-key`). Дашборд хранит телеметрию в памяти:
после его рестарта трейс - "no longer in the dashboard".
