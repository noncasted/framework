# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

### Логгер фреймворка: `ILog<T>`, уровни в `OptionsContainer`, ошибки телеметрии с категорией

Кого касается: клиент и бэкенд каждой игры.

1. **`DebugOptions.EnableLogs` удалён** (его никто не читал). Поиск: `git grep -nE 'EnableLogs|_enableLogs' -- 'client/Assets/*.cs'`.
   Сделать: убрать обращения; поле `_enableLogs` в `OptionsContainer.asset` Unity выбросит при следующем сохранении.
2. **Уровни логов.** У `OptionsContainer` новое поле `_logs` (`LogOptions`): Project Tools -> Options -> Log Options. По умолчанию
   development `Info`, release `Warning`, марки телеметрии выключены. Сделать: при желании задать правила по префиксу namespace
   (`LogRule`: например `Global.Network` -> development `Debug`), сохранить ассет. `OptionsContainer.Register` применяет их сам.
3. **Протокол телеметрии.** Новый член `SharedClientTelemetry.Error.Category` (`string`, последний): золотые файлы протокола игры
   (`protocol_schema.golden.txt`, `protocol_bytes.golden.txt`, строка `Shared.SharedClientTelemetry+Error`) перезаписать
   `UPDATE_PROTOCOL_GOLDEN=1`, хеш протокола игры (`ProtocolHash`) поднять. Поиск: `git grep -n 'SharedClientTelemetry+Error' -- '*.golden.txt'`.
4. **Свой код на логгер (рекомендуется, не обязательно).** Поиск: `git grep -nE 'Debug\.Log' -- 'client/Assets/*.cs' ':!*/Editor/*' ':!*/Tests/*'`.
   Сделать по `tools/framework/.claude/docs/LOGGING.md`: `ILog<T>` в конструктор или `[Inject]`-метод (регистрации не нужно), для
   статического класса `Logs.Of(typeof(X))`; `$"[Game] Joined {code}"` -> `_log.Info("Joined {Code}", code)` без ручных префиксов.
   Только так ошибка приходит в телеметрию с категорией и шаблоном (одна группа на место вызова), а `Debug` вырезается из release.
5. **Конструкторы типов фреймворка с логом.** Тесты и инструменты игры, которые создают их через `new`, передают `Logs.Of<X>()`:
   `SessionControl`, `SessionConnection`, `NetworkSession`, `NetworkObjectsCollection`, `EntityDestroyedCommand`,
   `NetworkLatencyMonitor`, `FrameRateLimit`, `AutoFrameRate`, `RenderResolution`, `ClientOutdatedNotice`,
   `MatchMetrics`, `MatchMetricsSocketTransport`, `TelemetryHttpTransport`, `TelegramDebugAPI`, `SteamInvites`,
   `SteamPlatformAchievements`, `SteamPlatformInventory`. Регистрации контейнера не меняются (лог даёт генератор).
   Поиск: `git grep -nE 'new (SessionControl|SessionConnection|NetworkSession|NetworkObjectsCollection|EntityDestroyedCommand|NetworkLatencyMonitor|FrameRateLimit|AutoFrameRate|RenderResolution|ClientOutdatedNotice|MatchMetrics|MatchMetricsSocketTransport|TelemetryHttpTransport|TelegramDebugAPI|SteamInvites|SteamPlatformAchievements|SteamPlatformInventory)\(' -- 'client/Assets/*.cs'`
   (на 2026-11-25: neon-dodge `Assets/Common/Match/Tests/Editor/MatchServer.cs`, `Assets/Gameplay/Tests/Network/{PlayerClient,MatchPlayer,RunClient}.cs`;
   storytale-workshop `Assets/Common/Tools/MeshProbe/MeshProbeMesh.cs`). Тексты `LogAssert.Expect` на строки фреймворка - новые
   (`[SoundPool] ...`, `[ConnectionWriter] ...`, `[NetworkLatencyMonitor] ...`).

Проверить поведение: строки фреймворка в консоли теперь `[ИмяКласса] текст` (без `[Network]` и т.п.); в release-плеере `Info`
фреймворка по умолчанию не пишется (уровень `Warning`), `Debug` в нём не скомпилирован. Тесты игры с `LogAssert.Expect` на тексты
фреймворка - новые строки. Ошибки фреймворка в консоли телеметрии - с категорией, сообщение - шаблон (`Lost the room {Code}`).
