## Телеметрия клиентов — Результат

### Статус: Завершено

### Что сделано
- Модуль `client/modules/telemetry`: базовые коллекторы, ошибки со `Scrubber`, сессии на lifetime с тегами, трейс запуска, транспорт на meta-сокете.
- Сервер `backend/ClientTelemetry`: грейн на сессию, два стейта, постоянный флаг, API чтения под токеном, записи запуска.
- Консоль: список сессий с фильтром тегов, карточка, группы ошибок, окно трейса запуска как Startup Profiler.
- Скилл `client-telemetry`: `monitor --tag`, разбор сессии и трейса на проде.
- Pixel Swarm подключён и развёрнут: сессии `menu`, `lobby`, `run`; web и telegram на коммите `9c3d8b95`; с телефона пришли настоящие данные.

### Измененные файлы

| Файл | Что изменено | Шаг | Evidence |
|------|-------------|-----|----------|
| `backend/ClientTelemetry/*`, `backend/Tests/ClientTelemetry/*`, `framework.slnx`, `backend/Tests/Tests.csproj` | контракт, разбор, лимиты, Scrubber | 1 | 196 тестов |
| `backend/ClientTelemetry/Storage/*`, `backend/Tests/ClientTelemetry/*` | таблицы, очередь, запись, партиции, чтение | 2 | 44 теста на Postgres |
| `backend/ClientTelemetry/Endpoints/*`, `Summary/*` | приём, чтение, лимиты, сводка, приватность спанов | 3 | 313 тестов телеметрии |
| `backend/ClientTelemetry/Sessions/*`, `Storage/*` | грейн сессии вместо таблиц шага 2 | 2b | 302 теста телеметрии |
| `client/modules/telemetry/*` | модуль: сбор, буфер, отправка, устройство | 4 | 86 новых EditMode |
| `client/modules/telemetry/Runtime/Web/*` и правки отправки | видимость, beacon, `hidden` | 5 | 102 теста модуля, chromium |
| `client/modules/telemetry/Runtime/Collectors/*` | базовые метрики и устройство WebGL | 7 | EditMode 806, PlayMode 11 |
| `client/modules/telemetry/Runtime/Errors/*`, включая `Errors/Web/TelemetryBrowserErrors.*` | ошибки клиента: перехват, схлопывание, вырезание | 8 | EditMode 865, node 17 |
| `backend/ClientTelemetry/Flag/*`, `backend/Console/Infrastructure/Features/ClientTelemetryToggle.razor` | постоянный флаг вместо `GameFlags` | 3b | ClientTelemetry 310 |
| `backend/ClientTelemetry/Errors/*`, `Sessions/TelemetrySessionBuffer.cs`, `Summary/*`, `Endpoints/ClientTelemetryReadEndpoints.cs` | отпечаток, группы в итоге, `GET /errors` | 8s | ClientTelemetry 349 |
| `backend/ClientTelemetry.TestHost/*`, `tools/scripts/test-telemetry-transport.sh`, `client/modules/telemetry/Tests/Runtime/*Transport*`, `CLAUDE.md` | сквозной тест транспорта и хост с данными | 6 | драйвер 4 из 4 |
| `backend/Console/Infrastructure/ClientTelemetry/*`, `Common/ConsoleConstants.cs`, `Home/HomeInfrastructureSection.razor`, `backend/ClientTelemetry.TestHost/ConsolePages/*` | панель: сессии, карточка, группы ошибок | 9 | ClientTelemetry 385, скриншоты `media/` |
| `.claude/skills/client-telemetry/*` | скилл: `monitor`, разбор указанной сессии, чтение токена без вывода | 10 | unittest 44, тестовый хост, отчёт свежего агента |
| `shared/ClientTelemetry/*`, `shared/Protocol/INetworkContext.cs`, `backend/ClientTelemetry/Connections/*` и удаление HTTP-приёма | приём через meta-сокет, теги сессий | 4c.1 | Framework.Tests 1438 |
| `backend/Console/Infrastructure/ClientTelemetry/*`, `ClientTelemetryStorageOptions.cs`, `backend/ClientTelemetry.TestHost/*` | теги и фильтр в панели, `LostAfter` 120 с | 9b | Framework.Tests 1445, `media/sessions_tags.png` |
| `client/modules/telemetry/*` | сессии на lifetime с тегами, HTTP, beacon, JSON и повторы удалены | 4c.2a | EditMode 860, PlayMode 12, node |
| `shared/ClientTelemetry/*`, `backend/ClientTelemetry/Startups/*`, `Connections/ClientTelemetryStartups.cs`, `backend/Console/Infrastructure/ClientTelemetry/TelemetryStartup*`, скилл | трейс запуска: контракт, хранение, API, окно, команды скилла | 5s.1 | Framework.Tests 1505, скилл 62, `media/startup_trace.png` |
| `client/modules/telemetry/Runtime/Sending/Socket/*`, `ClientTelemetryExtensions.cs`, asmdef | транспорт на meta-сокете | 4c.2b | EditMode 895, PlayMode 12, WebGL-компиляция |
| `client/core/Runtime/Tools/Profiling/{Profiler,GameProfiler}.cs`, `client/modules/telemetry/Runtime/Startup/*`, `IConnectionWriter.CurrentRun` | отправка трейса запуска с клиента | 5s.2 | EditMode 950, PlayMode 12, Framework.Tests 1511 |
| `client/modules/telemetry/Tests/Runtime/Transport/*`, `tools/scripts/test-telemetry-transport.sh`, `TelemetrySummaryCalculator.cs`, `TelemetryHostMuteSocket.cs`, скилл | сквозная проверка на сокете, исправление `Infinity` в сводке | 6b | скрипт 24 из 24, Framework.Tests 1512, скилл 64, `media/e2e_*.png` |
| `104ae9f9`, `205bcd53` (фреймворк main) | запросы телеметрии не становятся спанами трейса; документация, стадия `telemetry` в `test-all.sh` | 12 | test-all все стадии PASS, сквозной 24 из 24; на проде этой правки ещё нет |
| Pixel Swarm `8cc3cdd6`, `9c3d8b95` | подключение игры, деплой | 11 | сессии телефона через скилл с прода |

### Отличия от плана
Приём переезжает с HTTP на meta-сокет, сессия равна забегу (решение пользователя, шаг 4c); HTTP-приём, beacon, reopen и повторы удаляются.
Шаг 6 идёт через собственный тестовый хост, а не локальный кластер: у фреймворка кластера нет.
Хранение шага 2 заменено грейном и двумя стейтами по решению пользователя.

### Нерешенные вопросы
- iPhone 11, iOS 16, Telegram: чёрный экран около минуты забега, каждый запуск, и до телеметрии тоже (пользователь). Данные: обрыв до одного кадра в 5 с, главный поток 33 мс, игра жива. Гипотеза: потеря WebGL-контекста; метки добавляются шагом 13.
- Живые сессии не попадают в глобальные группы до закрытия; сессия, потерянная при падении силоса, не попадает никогда.
- Номера в сгенерированных именах (`d__12`) могут расколоть группу между билдами: решить по данным шага 11.
- Сообщение с новым id в каждом кадре упирается в бюджет очисток: `repeat` занижен, счётчика потерь нет.
- Правила `Scrubber` в двух копиях (сервер и клиент), менять обе.
- Упавший коллектор игры виден только в консоли, записью об ошибке не становится.
- Фон около 20 с сокет не рвёт, сессия продолжается; фон дольше минуты не проверен. При обрыве сессия закрывается как `lost`, остаток не записывается.
- `Scrubber` превращает `Run.cs:12` в `<url>`; на проде пока ни одной ошибки, настоящий стек не проверен.
- Правка `104ae9f9` дойдёт до игры со следующим обновлением submodule; до тех пор в окне запуска есть спаны `Request: SharedClientTelemetry.*`.
- `/tg/` проверен только на настоящем телефоне пользователя, стоимость подготовки трейса на телефоне отдельно не измерена.
