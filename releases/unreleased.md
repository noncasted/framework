# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

## Шаги

Поиск на 2026-11-02 по HEAD mines-leader, peace-data-reborn, pixel-swarm и storytale-workshop не нашёл совпадений в шагах 1 и 2:
там менять нечего. Шаги нужны игре, которая успела написать такой код.

### 1. Конструкторы `RuntimeChannelClient` и `Listener`

Кого касается: бэкенд, код или тесты, создающие их руками. Через DI ничего менять не надо.

Поиск: `git grep -nE 'new RuntimeChannelClient\b|new (RuntimeChannelClient\.)?Listener\(' -- 'backend/*.cs'`

Сделать: `new RuntimeChannelClient(orleans, logger)` -> `new RuntimeChannelClient(orleans, services, logger)`, `new Listener(id, channel, orleans, logger)` получает
`RuntimeChannelOptions`, для тестового двойника `new RuntimeChannelOptions()`. Контейнер, где слушают канал, должен иметь
`IRuntimeChannelConfig`: его даёт `AddConfigs()`, фикстура теста регистрирует сама (`RegisterConfig<IRuntimeChannelConfig,
RuntimeChannelOptions>`). Без него канал падает при первом `Listen`. Проверка:
`git grep -nE 'IRuntimeChannelClient, *RuntimeChannelClient|\.AddMessaging\(|\.AddConfigs\(|RegisterConfig<IRuntimeChannelConfig' -- 'backend/*.cs'`

### 2. `ILatencyClock` в конструкторах задержки

Кого касается: клиент, код или тесты, создающие эти классы руками. `AddSessionServices` регистрирует часы сам.

Поиск: `git grep -nE 'new (NetworkLatencyMonitor|LatencyProbes|LatencyStats|LatencyPeerStats)\b|\bLastEchoAt\b' -- 'client/*.cs'`

Сделать: конструкторы `NetworkLatencyMonitor`, `LatencyProbes`, `LatencyStats` получают последним аргументом `ILatencyClock`,
`LatencyPeerStats` получает его единственным, вне тестов `new LatencyClock()`. `peerStats.LastEchoAt` -> `peerStats.Silence`: секунды
с последнего ответа, вместо `Time.realtimeSinceStartupAsDouble - peerStats.LastEchoAt`.

## Проверить поведение

- WebRTC (`WebRtcPeerSocket`): кандидаты предложения уходят после установки ответа, на один обмен сигналами позже. Сигнал, который закрытый
  пир не принял, теперь потеря соединения: `Connect` падает в запасной путь с предупреждением, исключения в коде сигналинга нет.
- Ошибки `libdatachannel` пишутся как `Warning`, `Error` остался только у `Fatal`. Кто искал их в логе как ошибки, теперь их не видит.

## Без действий

- `tools/scripts/test-all.sh`: весь набор framework параллельно, таблица этапов.
- `RuntimeChannelOptions`: `ResubscribeMinIntervalMs` 10000, `ResubscribeMaxIntervalMs` 60000, прежние значения; сохранённый конфиг без них
  читается с ними (тест `RuntimeChannelOptions_StoredBeforeTheIntervals_KeepProductionIntervals`).
- `WebRtcPeerRole`, `WebRtcConnectFailedException` в своих файлах, пространство имён прежнее.
- `Directory.Build.props`: у сборок framework нет хеша коммита в версии и Source Link в PDB; новый коммит не пересобирает их заново.
- `com.unity.web.stripping-tool`: нет `FindAssets` на каждое обновление базы.
