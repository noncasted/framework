# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

### Контент: `ContentLoader.Register` -> `ContentEntryPoint.RegisterMain` (BREAKING)

Кого касается: клиент игр, которые регистрируют главный бандл без скоупа (смоук-плеер, инструмент). На 2026-11-18 таких нет
(поиск по `client/Assets` gamejam-autobattler, mines-leader, peace-data-reborn, pixel-swarm, storytale-workshop пуст); во фреймворке
только `client/project/Assets/ContentSmoke/ContentSmokeProbe.cs`.

Поиск: `git grep -n 'ContentLoader.Register' -- 'client/*.cs'`

Сделать: `ContentLoader.Register(lifetime, options)` -> `ContentEntryPoint.RegisterMain(lifetime, options)` (те же аргументы, тот же
`UniTask<ContentLoader>`); без этого CS0117. Игра со скоупами зовёт `AddContent`, как раньше.
