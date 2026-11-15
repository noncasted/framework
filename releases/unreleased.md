# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

### Core: удалён мёртвый код (BREAKING)

Кого касается: клиент, игры, которые вызывают удалённые члены core (на момент удаления вызовов нет ни в одной игре).

Удалены `FloatValue` (ScriptableObject, ни одного ассета), `TweenExtensions.MoveAnchored`,
`ProgressionExtensions.CurveDeltaProgression` и оба `UnityLifetimesIntegrationExtensions.ToLifetimedValue` (`Slider`, `UnityEvent<T>`).
Поиск: `git grep -nwE 'FloatValue|MoveAnchored|CurveDeltaProgression|ToLifetimedValue' -- client/Assets` и
`git grep -n '5f70cb9f93b24939b6397c2d7338017b' -- client/Assets` (ассеты `FloatValue`). Найденное заменить своим кодом игры:
`MoveAnchored` - `updater.Tween(lifetime, time, ease, f => target.anchoredPosition = Vector2.LerpUnclamped(start, end, f))`,
`ToLifetimedValue` - `LifetimedValue<T>` и `UnityEvent<T>.Listen(lifetime, ...)`.

### Генераторы каталогов падают на сломанном входе

Кого касается: клиент, все игры с каталогами (префабы, ассеты, звук, спрайты, сцены).
Поиск: в редакторе игры Regenerate All (или Project Tools, Run All) и лог: прежде шли `Included ... has empty group`, `Identifier is empty`,
`Failed to load`, `is not an EnvAsset`, `No sprites found`, `Addressable settings are missing`.
Сделать: раньше такой вход логировался и пропускался, а очистка удаляла ассет группы и её класс; теперь генератор бросает
`InvalidOperationException` с путём до любой записи, и каталог не обновляется, пока вход не исправлен (группа, имя файла, импорт,
настройки Addressables). Файл в Assets без импортера (не импортирован) тоже ошибка; папки, которые Unity не импортирует (`~`, скрытые), пропускаются.
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
