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
