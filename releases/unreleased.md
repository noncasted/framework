# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

### ContainerGenerator: сборка без копии в Analyzers

Кого касается: игры, чей скрипт тестов собирает `ContainerGenerator` в Release (или запускает `ContainerGenerator.Verify` в
Release) в том же дереве, где в это время идут тесты Unity.

Release-сборка копирует `ContainerGenerator.dll` в `client/core/Analyzers`. Если это происходит во время batchmode-прогона
EditMode, Unity запрашивает компиляцию скриптов, которая не может пройти до конца прогона; пока она висит,
`EditorApplication.isCompiling == true`, цикл UniTask в редакторе стоит, и каждый тест, который ждёт кадр или таймаут, заканчивается
только по таймауту тест-фреймворка (180 с). Новое свойство `-p:SkipAnalyzersCopy=true` (для `dotnet run` -
`--property:SkipAnalyzersCopy=true`) собирает без копии; без него поведение прежнее.

Проверка: `grep -rn "ContainerGenerator" tools/scripts/*.sh .github/workflows/*.yml`; там, где сборка идёт рядом с Unity на том же
дереве, добавить свойство.
