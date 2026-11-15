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
