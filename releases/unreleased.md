# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

### Бэкенд: исправления аудита 2026-11-16 (BREAKING)

Кого касается: бэкенд всех игр. Отчёт: `docs/audits/2026-11-16_backend.md`. Поиск из корня игры, нет совпадений - пункт пропускается.

- `DbLookup` удалён, таблицы side effects теперь `SideEffectTables` (namespace `Common`): `git grep -n "DbLookup" -- backend` -
  `DbLookup.SE_Queue / SE_Processing / SE_Retry / SE_DeadLetter` -> `SideEffectTables.Queue / Processing / RetryQueue / DeadLetter`
  (mines-leader, pixel-swarm, peace-data-reborn, storytale-workshop, gamejam-autobattler; у post-radio свой `DbLookup`).
- `IClusterParticipantContext.IsInitialized / IsMessagingStarted / CurrentStageIndex` - `ISharedViewableProperty<T>`:
  `git grep -n "Returns(new ViewableProperty" -- backend` - в подменах контекста `new SharedViewableProperty<bool>(true)`.
- `ISessionsCollection` / `SessionsCollection` переехали в `Game.Hosting.Collection`: `git grep -ln "SessionsCollection" -- backend` -
  добавить `using Game.Hosting.Collection;` (pixel-swarm, storytale-workshop).
- `LobbyListing.IsJoinable` удалён (старые строки читаются), `ILobbyCodeLeases.IsMatchJoinable` -> `IsPassedToMatch`:
  `git grep -n "IsJoinable" -- backend` - убрать присваивание (pixel-swarm `Tools/Tests/Meta/OpenLobbiesSubscriptionsTests.cs`).
- `TraceExtensions.Start(name)` удалён: `git grep -nE '\.Start\("' -- backend` - `source.StartActivity("X")` (возвращает `Activity?`;
  storytale-workshop, `MetaGateway`).
- `RuntimeChannelObserver.ResetLastSeen(long)` удалён: `git grep -n "ResetLastSeen(" -- backend` - удалить копию теста
  `Observer_ResetLastSeen_AllowsLowerSequenceAfterGap` (mines-leader, peace-data-reborn).
- `IDeploymentState<T>` теперь `SharedViewableProperty<T>`: свой класс на `ViewableProperty<T>`, реализующий `IDeploymentState<T>`,
  наследует `SharedViewableProperty<T>` (`git grep -n "IDeploymentState<" -- backend`); `Substitute.For<IDeploymentState<T>>()` больше
  не вызывает listener в `View` (член интерфейса) - тест задаёт вызов сам. Сейчас таких мест в играх нет.
- Необязательно, все игры с `Orchestration/Coordinator/DeployIdentity.cs`: `git grep -n "AddPipeRequestHandler<DeployIdRequest" -- backend`
  - привязать обработчик к `_deployContext.DeployLifetime` сразу после `await _deployContext.Set(deployId, lifetime)`. Фреймворк уже
  следует только за более новым деплоем (`DeployState.CreatedAt`), с привязкой старый координатор перестаёт отвечать в pipe сразу.

Проверить поведение:
- Вызов grain-а без `[Transaction]`, который читает или пишет `State` / `EventState`, пока на нём открыта транзакция, бросает
  `InvalidOperationException` (раньше писал в базу её незавершённые изменения): `git grep -ln "\[Reentrant\]" -- backend` и методы без
  `[Transaction]`, которые трогают состояние, - сделать транзакционными или повторять.
- `SideEffectsOptions.MaxRetryCount` - число попыток: 5 даёт 5 попыток (было 6).
- `Connection` закрывается и без `Run`, когда кончается его lifetime (раньше шлюз ждал `Closed` вечно).
- Открытые лобби упавшего сервера уходят из снимка наблюдателей (`LobbyRegistry` раз в 5 с сверяет серверы с discovery).
- Новые поля хранимых состояний: `DeployState.CreatedAt`, `LobbyRegistryState.HiddenServers` (старые строки читаются); у игр с
  закреплёнными JSON состояний (`StoredStatesFormatTests`) обновить эталоны.
- `MatchMetricsOptions.ServerLastWriteRetry` (5 с): последний flush серверного потока повторяется в пределах `CloseGrace`.

### Core: удалён мёртвый код (BREAKING)

Кого касается: клиент, игры, которые вызывают удалённые члены core (на момент удаления вызовов нет ни в одной игре).

Удалены `FloatValue` (ScriptableObject, ни одного ассета), `TweenExtensions.MoveAnchored`,
`ProgressionExtensions.CurveDeltaProgression`.
Поиск: `git grep -nwE 'FloatValue|MoveAnchored|CurveDeltaProgression' -- client/Assets` и
`git grep -n '5f70cb9f93b24939b6397c2d7338017b' -- client/Assets` (ассеты `FloatValue`). Найденное заменить своим кодом игры:
`MoveAnchored` - `updater.Tween(lifetime, time, ease, f => target.anchoredPosition = Vector2.LerpUnclamped(start, end, f))`.

### Генераторы каталогов падают на сломанном входе

Кого касается: клиент, все игры с каталогами (префабы, ассеты, звук, спрайты, сцены).
Поиск: в редакторе игры Regenerate All (или Project Tools, Run All) и лог: прежде шли `Included ... has empty group`, `Identifier is empty`,
`Failed to load`, `is not an EnvAsset`, `No sprites found`.
Сделать: раньше такой вход логировался и пропускался, а очистка удаляла ассет группы и её класс; теперь генератор бросает
`InvalidOperationException` с путём до любой записи, и каталог не обновляется, пока вход не исправлен (группа, имя файла, импорт).
Файл в Assets без импортера (не импортирован) тоже ошибка; папки, которые Unity не импортирует (`~`, скрытые), пропускаются.
Также спрайт с `tagSpeeds` без `fps` теперь ошибка разметки, а не 0.

### Анимации: удалена поворотная анимация спрайтов (BREAKING)

Кого касается: клиент, игры с модулем `animations` (peace-data-reborn, pixel-swarm; ни одна её не использует).
Поиск: `git grep -nE 'RotatableSpriteAnimation|RotatableFrameProvider|IAnimationRotationProvider|FiveAnimationDirection' -- 'client/Assets/*.cs'`
Сделать: удалены `RotatableSpriteAnimation`, `RotatableFrameProvider`, `IAnimationRotationProvider`, `FiveAnimationDirection`;
`SpriteAnimationVariants.cs` переименован в `ForwardSpriteAnimation.cs` (guid тот же). Если поиск что-то нашёл, держать свою копию в игре.

### Flow: `MockEntry.Process` возвращает `UniTask` (BREAKING)

Кого касается: клиент, все игры с моками сцен.
Поиск: `git grep -n 'UniTaskVoid Process' -- 'client/Assets/*.cs'`
Сделать: `public override async UniTaskVoid Process()` -> `public override async UniTask Process()` (и `abstract UniTaskVoid Process()`
в своей базе моков, если она переобъявляет метод). `MockSwitcher` запускает его через `.NoAwait()`.
