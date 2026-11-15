# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

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
