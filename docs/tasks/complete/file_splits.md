## Разрез больших файлов

### Что сделано
- В `client/core`, `client/modules` и исходниках генератора нет файлов runtime и инструментов длиннее 300 строк (было 34), один тип на файл.
- Большие классы стали композиторами над настоящими классами, без `partial`, кроме статики с `[AutoStaticsCleanup]`. Генератор разрезан на этапы `Graph/Walker`, `Scope/Resolve`, `Scope/Plan`, `Scope/Emit`, границы проверены независимым просмотром.
- Около 300 новых тестов написаны на старом коде: `UINavigation`, `NetworkLatencyMonitor`, каталоги, HierarchyBindings, `GeneratedScopes`, `ContainerBuilder`.
- `BREAKING:` конструктор `NetworkConnection` принимает `IWebSocketFactory`.

### Ключевые файлы
- `client/core/Source~/ContainerGenerator/Scope/Plan/ScopePlanBuilder.cs`, `Graph/Walker/GraphWalker.cs`
- `client/core/Runtime/Tools/Container/Runtime/GeneratedScopes.cs`: фасад, поверхность для сгенерированного кода прежняя
- `client/core/Editor/Tools/Container/ContainerDebuggerWindow.cs`: окно-композитор над секциями
- `client/modules/audio/Runtime/AudioPlayer.cs`, `client/modules/ui/Runtime/Navigation/UINavigation.cs`

### Заметки
- Поведение доказано побайтным равенством вывода генератора, привязок и каталогов и дампов окон до и после.
- Найденные дефекты и непроверенное: `docs/tasks/current/file_splits_findings/`.
- Backend (15 файлов) не резался: задача родом из аудита клиента.
