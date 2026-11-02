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

### 3. Контракты каналов и труб

Кого касается: бэкенд, свои реализации `IRuntimeChannel` и тестовые двойники, код вокруг `IsPipeExists`, `DurableQueueClient.Listener`. Поиск на
2026-11-02: только `IsPipeExists` в `backend/Tools/Tests/Messaging/RuntimePipeTests.cs` у mines-leader и peace-data-reborn, правка не нужна.

Поиск: `git grep -nE ':.*\bIRuntimeChannel\b|DurableQueueClient\.Listener|\bIsPipeExists\b|RuntimePipe\.Exists|\bObserverData\b' -- 'backend/*.cs'`

Сделать:
- `IRuntimeChannel.AddObserver` -> `Task<ChannelPosition>`: реализация возвращает позицию канала, `await` у вызывающих работает как был.
- `IRuntimePipeClient.Exists`, `IsPipeExists`: сбой проверки теперь исключение, а не `false`. `false` значит только "наблюдателя нет": кто считал сбой отсутствием, ловит его сам.
- `DurableQueueClient.Listener.AddHandler` -> `Guid?`: `null`, когда слушатель уходит, берётся новый слушатель (пример: `DurableQueueClient.AddToListener`).
- `ObserverData.UpdateDate` удалён.

### 4. Удалённый API транзакций и состояний

Кого касается: бэкенд. Поиск на 2026-11-02: только `backend/Tools/Tests/Game/SessionTestWorld.cs:141` у peace-data-reborn.

Поиск: `git grep -nE '\b(ILiveState|LiveState|LiveStateChannelId|AddLiveState|DeploymentStateCompat|SetExceptionMessage|TransactionCommitResult|ToDeploymentStateIdentity|ReadDeploymentState|WriteDeploymentState|PublishDeploymentState)\b|\.ExceptionMessage\b|new EventStorage\(|new Session\(' -- 'backend/*.cs'`

Сделать:
- `new Session(...)` без аргумента `ISessionEntities`, `new EventStorage(store, serializer)` без `ILogger<EventStorage>`.
- `ILiveState<T>`, `LiveState<T>`, `LiveStateChannelId<T>`, `AddLiveState` -> `DeploymentState<T>`; методы `DeploymentStateCompat` удалены вместе с классом.
- `TransactionContext.ExceptionMessage`, `SetExceptionMessage` удалены: причину сбоя несёт исключение. `TransactionCommitResult` стал `internal`.

### 5. `.gitignore`: живые графы редактора

Кого касается: клиент, игры без этого правила; на 2026-11-02 mines-leader, peace-data-reborn, pixel-swarm. В storytale-workshop папка закрыта своим `.gitignore`.

Поиск: `git check-ignore -q client/Assets/Common/Internal/Editor/Transient/x.asset || echo надо`

Сделать: строка `/client/Assets/Common/Internal/Editor/Transient*` в `.gitignore` проекта (другой `ContentFolder` в `Framework.json`: путь под ним).
Туда пишут `ContainerLiveGraph` и `LifetimeLiveGraph`.

### 6. Плагин WebRTC переименован

Кого касается: клиент, игра, которая называет библиотеку по имени файла (сборка, `link.xml`, исключения). Поиск на 2026-11-02 ничего не нашёл.

Поиск: `git grep -nE '(^|[^_a-z])datachannel\.(dll|so)|libdatachannel\.so|"datachannel"' -- ':!tools/framework'`

Сделать: `datachannel` -> `framework_datachannel`: `libframework_datachannel.so`, `framework_datachannel.dll` (папка `Runtime/Plugins/libdatachannel` прежняя). Причина:
редактор пробует голый `dlopen` раньше плагинов проекта, и установленная в системе библиотека подменяла нашу.

### 7. Разметка спрайтов: скорость без тега

Кого касается: клиент, игры с `tagSpeeds` в `.meta` спрайтов. На 2026-11-02 у peace-data-reborn (6 записей) и pixel-swarm (192) все записи с тегом, у остальных `tagSpeeds` нет.

Поиск: `git grep -nE '\\"tagSpeeds\\": \[[^]]*\{\\n +\\"fps' -- '*.meta'`

Сделать: дописать `tag` или удалить запись. `SpriteCatalogMetadataJson` теперь бросает на такой записи, и исключение вылетает из `SpriteCatalogPostprocessor`: один битый `.meta` останавливает генератор.

## Проверить поведение

- Канал: подписчик, не получивший ни одного сообщения, при реактивации зерна канала получает gap (`onGapDetected`) и перечитывает состояние.
- Переподписка идёт параллельно по слушателям, проход не ждёт одного слушателя дольше `RuntimeChannelOptions.ResubscribeTimeoutMs` (30000).
- `IRuntimeChannel.AddObserver`: сигнатура метода зерна поменялась; старые и новые узлы при поэтапном деплое, вероятно, несовместимы, вывод из кода, на живом кластере не проверено.
- `[Transaction]` на цели, которая не `IGrainBase`, падает.
- `IGrainTransactionHandler.OnFailure` собирает сбои участников в `AggregateException`. Зерно, чей откат упал, остаётся удержанным: его память неизвестна, откат повторяет перехват.
- Обобщённая функция-корень контейнера компилировалась, теперь `CINGR016`. В играх таких нет, шага нет.
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
- `client/project`: `Assets/common` -> `Assets/framework-common`, в играх ссылок на эту папку нет.
- `FrameworkSettings.WithoutAddressables`: только для `client/project`, игре не ставить: без настроек Addressables она получает прежнюю ошибку.
- Бэкенд: файлы `MessagePipeClient.cs`, `MessagePipe.cs`, `MessagePipeObserver.cs` -> `RuntimePipeClient.cs`, `RuntimePipe.cs`, `RuntimePipeObserver.cs`, имена типов прежние.
- `RuntimeChannelOptions.ResubscribeTimeoutMs` 30000; сохранённый конфиг без него читается со значением по умолчанию.
- `InputManager.asset`: оси `Gamepad Axis 1..10` добавлены в `client/project`, требование к игре прежнее.
- `SharedAddressablesSync`: выбор ассетов и групп вынесен в `SharedAddressablesTargets`, API прежний.
