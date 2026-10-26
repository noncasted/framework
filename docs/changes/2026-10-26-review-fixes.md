# Исправления по ревью фреймворка, 2026-10-26

Каждое исправление сделано через тест, который сначала падал по ожидаемой причине. Итог: backend 783/783,
common 92/92, ContainerGenerator.Verify 62/62, Unity EditMode 277/281 (4 пропущенных бенчмарка), PlayMode 9/9.
Пометка **BREAKING** означает изменение API или поведения, на которое может опираться игра.

## common (реактивные примитивы)

- Слушатель lifetime, снятый `RemoveListener` во время `Terminate`, больше не вызывается в этом проходе.
- `LifetimedValue`: `Set` внутри обработчика больше не доставляет оставшимся обработчикам устаревшее значение после
  нового.
- `ViewTrue`: `Notify` при значении true завершает предыдущий true-lifetime.
- **BREAKING** `ViewableList` и `ViewableDictionary` больше не наследуют `List` / `Dictionary`: базовые мутаторы
  обходили lifetimes. Оставлены Count, индексатор, Add, Insert, Remove, RemoveAt, AddRange, RemoveRange, Clear,
  Contains, IndexOf, ContainsKey, TryGetValue, TryGetSourceValue, Keys, Values, GetEnumerable, foreach.
- `ViewableList.Add` / `Insert` дубликата бросает до изменения списка (раньше элемент попадал без lifetime).
- Индексатор `ViewableDictionary` с новым ключом вызывает `OnModified` один раз.
- Завершение родителя не ищет детей в своём списке слушателей (иначе квадратичная стоимость).
- **BREAKING** удалены мёртвые `TerminatedLifetime.CreateChild` и `RemoveTerminationListener`.
- `ReactiveExceptions.Report` стал публичным (им пользуется updater клиента).

## shared, backend/Session, backend/Hosting

- Соединение, закрытое до `Start()`, уходит из сессии; сессия закрывается, `SessionGate.Join` не висит. Ошибка
  `Connection.Run` логируется, отключение ставится в очередь всегда.
- Ожидание `ExpectedUsers` считает места, а не соединения: реконнект до старта не запускает матч раньше времени.
- Матч лобби получает игроков без ботов и без дублей `BackendId`; `PlayersCount` листингов тоже.
- `CloseIfNotJoined` сначала закрывает admission, потом завершает сессию (`IsClosed` верен).
- Код лобби освобождается, если фабрика лобби бросила.
- **BREAKING** владелец сессии никогда не бот.
- Отказанная `TryReserve` больше не рвёт живое соединение.
- Таймеры сессии и лобби (`Task.Delay`) отменяются с lifetime.
- Lifetime соединения пересекается с lifetime пользователя: слушатели не копятся на lifetime сессии при реконнектах;
  `SessionEntities` забывает завершённые места.
- Регистрация сервиса `ServiceFactory` атомарна при коллизии id.
- Возвращающееся соединение не получает `RemoteUpdate` о самом себе.
- `SessionsCollection` отправляет снимки live-data по порядку.
- **BREAKING** `ISession.AllUsersConnected` теперь `Task` (запоминает, что все пришли; отменяется с сессией).
- **BREAKING** `ServiceFactory.OnSessionCreated` выполняет setup в очереди сессии и завершается после него.
- **BREAKING** `IUserFactory.Create(lifetime, id, connection)`, `ISessionAdmission.Close()` / `IsHeldOnlyBy()`.
- Комментарий диапазонов тегов протокола: `SessionCreateSolo = 184` и неиспользуемый 52 (теги не перенумерованы).

## backend/Infrastructure, Common, Cluster

- Транзакционное состояние регистрируется до чтения: упавшее чтение больше не блокирует грейн навсегда;
  `EventState.Append` тоже регистрируется.
- Перехват блокировки «зависшей» транзакции не даёт ей потом закоммитить старый снимок (`Seal` перед коммитом,
  `idle_in_transaction_session_timeout`).
- Неизвестный исход коммита проверяется через `pg_xact_status`; если неясно, бросается
  `TransactionOutcomeUnknownException` вместо «провала» с повтором.
- **BREAKING** `AddressableState.OnOrleansStarted` и `ConfigsInitializer.Init` больше не глотают ошибки: сбой загрузки
  останавливает старт вместо перезаписи конфигов seed-значениями.
- `DurableQueue` доставляет всем подписчикам; повтор с тем же id идёт только тем, кто не получил.
- Pipe повторяет только «no observer bound», таймаут не выполняет обработчик повторно и не сбрасывает observer.
- Каналы: номер эпохи активации, после реактивации грейна сообщения не теряются молча; переподписка одного слушателя
  сериализована.
- `DeploymentState` / `AddressableState`: подписка до чтения, перезагрузка при разрыве, порядок записей по Lamport-метке
  вместо часов хоста; значение в памяти меняется только после записи в БД.
- `OpenLobbiesWatcher`: разрыв канала перечитывает реестр.
- Нетранзакционные записи `State` сериализованы; упавшая запись сбрасывает кэш.
- `EventState.Load` вне транзакции не теряет ожидающие события.
- **BREAKING** `NoAwait`: ошибка сразу уходит в `NoAwaitExceptions.Handler` (кластер ставит `LogCritical`), иначе в stderr.
- `TaskBalancer`: drain ждёт все задачи, штраф за ошибку сохраняется.
- Side effects: долгий эффект не перезапускается параллельно (`TouchProcessing`), dead letter хранит исходную ошибку,
  операции консоли не глотают ошибки; at-least-once описан в `ISideEffect`.
- Ожидания `ReactiveTaskExtensions` отписываются после завершения и продолжаются асинхронно.
- Discovery: остановленный сервис не регистрируется заново, время ставит грейн, балансировщик пропускает сервер,
  отстающий больше 10 с; предупреждение готовности кластера не спамит.
- **BREAKING** `DeployCleanup` отказывается работать, если его deploy id не совпадает с ответом `DeployIdPipe`
  (конструктор получил `IMessaging`).
- `SideEffectsWorker` завершается при остановке; `Connection.Run` чистит в finally, `ForceDisconnect` шлёт close frame.
- Fail fast: без `?? new TransactionOptions()`, `EventStorage` не пропускает нечитаемые записи, типы событий через
  `MovedTypesBinder`, `StateIdentity` с равенством по значению, кэш запросов по таблице.

## client/core: DI, scopes, генератор

- **BREAKING** значения `WithParameter` / `RegisterInstance` / `RegisterComponent` берутся по регистрации, а не по типу
  (раньше побеждала последняя регистрация типа).
- **BREAKING** `[Inject]` базового класса вызывается.
- Новые диагностики: CINGR010 (несколько, generic или private `[Inject]`), CINGR011 (abstract, struct, private nested,
  два конструктора одной длины), CINGR012 (transient слушает несколько фаз), CINGR009 warning (параметр берёт одну из
  нескольких регистраций). Исключение эмиттера становится CINGR001, а не теряет все контейнеры сборки.
- `if (x) builder.Register<T>()` без else больше не валит сборку scope, если T никому не нужен.
- **BREAKING** runtime-проверка: регистрация, которую генератор не видит (через интерфейс, в цикле, повторный вызов
  installer), бросает при сборке scope; `ScopeContainer.CreateEntity` без варианта для view type бросает.
- Упавшая загрузка scope откатывается (lifetime, контейнер, сцены, after-dispose).
- `LoadedScope.Dispose` идемпотентен, выполняет все шаги и диспоузит дочерние scope первыми.
- Generated `Dispose`: сначала lifetime, потом сервисы в обратном порядке, ошибки собираются; `Resolve` после
  `Dispose` бросает `ObjectDisposedException`.
- Entity-scope выполняют фазы loaded, dispose и after-dispose.
- `GameLoopScopeLoader` выполняет загрузки по очереди, не ждёт одну UniTask дважды.
- Комментарии генератора на английском, фикстуры Verify без игровых имён, стабы Verify совпадают с runtime API.

## client/core: runtime

- `UpdaterProxy` применяет скорость родителя один раз и свою собственную; пропускает завершённые updatable.
- **BREAKING** `Pause` / `Continue` считаются (Continue без Pause бросает), `SetSpeed` во время паузы применяется
  после последнего `Continue`.
- Updatable, бросивший исключение, не останавливает остальных и следующие фазы.
- **BREAKING** `RunDelay`, `RunUpdateAction`, `RunFixedAction` завершаются `OperationCanceledException`, когда кончается
  lifetime (как Progression и Tween); `RunUpdateAction(lifetime, time, …)` считает время updater, а не `Time.time`.
- Ожидания `WaitInvoke` / `WaitTrue` / `WaitFalse` отписываются после завершения.
- `BehaviourSequence` сбрасывает выполняющийся первый узел.
- **BREAKING** `GameObjectLifetime`: скрытый до `Start` объект получает живой lifetime (раньше завершённый, и
  `ListenClick` молча не работал); lifetime завершается в `OnDestroy`.
- `AssetGroup`: release во время загрузки и упавшая загрузка не текут; **BREAKING** `Release` без `Retain` бросает.
- `StateMachine`: новое состояние ставится до завершения старого (реентерабельность), `Initialize` завершает текущее.
- **BREAKING** `Direction8` из 8 направлений, `ToDirection4` по доминирующей оси, нулевой вектор бросает;
  `RotateClamped` по кратчайшему пути, `ToAngle` в [0, 360).
- **BREAKING** `FindOrLoadScene` в моке больше не берёт объект из другой сцены; `LoadTyped` бросает
  `InvalidOperationException`.
- Нулевое время Progression / Curve завершается сразу; отрицательное бросает.
- **BREAKING** fail fast: `CollectionsExtensions.Random` на пустом списке, `ProfilerTraceStorage.Save` / `Load`.
- `BackendOptions.SocketUrl` меняет только схему; `LocalStorageKeys.Get` отвергает имена с суффиксом окружения.

## client/core: editor-инструменты

- Hierarchy bindings: вложенный класс с именем родителя, дочерние `Register` / `Create` / члены `ObjectBindings`,
  поля `HPBar` / `HpBar` больше не дают ошибок компиляции.
- Два типа bindings в одном файле или чужой namespace отвергаются; объекты ищутся по индексам соседей, а не по именам.
- Batch после исключения сбрасывается, «Cancel Prefab Batch» доступен, пока что-то осталось; запись файла бросает при
  сбое; batch отказывается при открытом prefab stage; проверка «чужой слой» не путается после Ctrl+D.
- Общий сервис идентификаторов `CodeIdentifiers` для всех генераторов каталогов: ключевые слова, члены класса, имя
  класса, дубли.
- Каталог сцен: дубли имён, ведущая цифра, порядковая сортировка, postprocessor сцен, пустой каталог.
- **BREAKING** каталоги бросают на дубли имён, отсутствующий root-компонент, нечитаемый json реестра или userData,
  типы из Assembly-CSharp.
- Postprocessor prefab и audio реагируют на исключение из каталога.
- **BREAKING** `ProjectSettings/Framework.json` обязателен, нет игровых путей по умолчанию; перечитывается при изменении.
- Поиск asmdef учитывает `.asmref`; `SerializeReferencePicker` понимает вложенные типы; `PlayerBuildSteps` пропускает
  открытые generic-типы; сканер графа контейнера не глотает ошибки.

## client/modules и platforms

- Network: `ConnectionWriter` после повторного `Run` не смешивает старый и новый сокет; `ForceSendAll` не шлёт
  параллельно с циклом; несериализуемый запрос падает сразу.
- Network: `DefaultWebSocket` читает на thread pool и отвечает на ping при занятом main thread; сокет упавшего connect
  закрывается; `JsWebSocket` доставляет в player loop.
- Network: синхронизация свойств и фейды музыки идут по реальному времени (пауза игры их не останавливает);
  `NetworkEntityFactory` бросает на чужой id; `NetworkSession.Start` не висит при обрыве.
- **BREAKING** `INetworkProperty.OnWriteSent()`: локальная версия растёт на записи (сходимость при одновременных записях).
- Тестовый union протокола в сетевых тестах (9 тестов фреймворка падали без игры).
- Matchmaking: убран таймаут 10 с меньше таймаута writer (20 с), дублей лобби при повторе нет.
- UI: вершина стека при закрытии не по порядку, видимость под диалогом, `Exit` в finally (ввод не остаётся выключенным).
- Animations: цепочка `PlayAsync`, остаток времени цикла, первый кадр сразу, анимация без кадров бросает.
- **BREAKING** cameras: `SetCamera(lifetime, camera)`, камера сбрасывается с lifetime; `ScreenToWorld` без камеры бросает;
  разрешение экрана ставится только в `SetOutputResolution`.
- **BREAKING** backend HTTP: таймаут из `BackendOptions.TimeoutSeconds`, `IBackendPost.Post` возвращает тело ответа,
  `GetImage` шлёт заголовки.
- Itch: битое сохранение переносится в `save_v2_corrupt`; `Settings.Apply` хранит копию.
- **BREAKING** localization: язык по умолчанию и маппинг системного языка в `LocalizationOptions`.
- **BREAKING** telegram: путь к токену debug API в `TelegramEditorOptions`; web: таймаут `WebLoadingScreen.Show`,
  понятные ошибки jslib без `window.webShell`.

## Не исправлено

- Owner-проверки команд сессии: решение не нужны сейчас.
- Аутентификация консоли (код хоста игры), лицензия FluentAssertions.
- Не подтвердилось: отсутствующий игрок не убивает матч лобби (`ExpectedUsers = 0`), отмена `WaitInvoke` уже Canceled,
  ошибка `SetFlag` недостижима.
- Ограничение: версии свойств сходятся для пары одновременных записей; точное решение требует, чтобы сервер
  присылал писателю его новую версию.
- Оставлено: `ActionBlock` сессии не завершается; flush сохранений itch на каждом сохранении; владелец общей
  RenderTexture камеры; параметры веток `switch` в генераторе по типу; особые случаи `GraphWalker` для
  `AddSnapshotHandler` / `AddCardActionSyncResolver` (игровые имена).
