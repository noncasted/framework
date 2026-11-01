# Эталон перед разрезом файлов (шаг 1)

Снято 2026-11-02 на коммите `4004262`, ветка `file_splits`, worktree `/projects/framework/.claude/worktrees/file_splits`. Код фреймворка не менялся. Логи и результаты: `/tmp/claude-1000/-projects-framework/0b8312a5-015f-428b-99c3-51bf78155ba7/scratchpad/baseline/`.

## Итоги

| Проверка | Итог | Длительность |
|---|---|---|
| Сборка генератора, Release | 0 ошибок, 0 предупреждений | 2 с |
| `ContainerGenerator.Verify` | 149 PASS, 0 FAIL, `ALL PASSED`, код выхода 0 | 14 с |
| Unity EditMode | total 365, passed 361, failed 0, skipped 4 | тесты 30,0 с; весь запуск 45 с |
| Unity PlayMode | total 12, passed 12, failed 0, skipped 0 | тесты 4,7 с; весь запуск 26 с |
| Снимок генератора | md5 `4924df6ac5ed9289c96848ffad95eb4f`, 66 файлов | 21 с |

Упавших тестов нет. Длительности Unity взяты из второго запуска. Первый запуск EditMode шёл 66 с, из них 17,9 с заняла компиляция скриптов после копирования `Library`; тесты в нём шли 29,9 с. PlayMode в первом запуске занял те же 26 с, тесты 4,7 с. Итоги обоих запусков совпали.

Пропущенные тесты EditMode запускаются только вручную (`ContainerBenchmarkTests`):
- `GeneratedContainer_BuildAllocations_ByStep`
- `GeneratedContainer_EventLoopPhases_RecordsAllocations`
- `GeneratedContainer_ManyScopes_RecordsMetrics`
- `GeneratedContainer_SyntheticGraph_RecordsMetrics`

Команды Unity:

```bash
W=/projects/framework/.claude/worktrees/file_splits
/tools/unity/7000.0.0a10/Editor/Unity -batchmode -projectPath $W/client/project -runTests -testPlatform EditMode -testResults <файл> -logFile <файл>
/tools/unity/7000.0.0a10/Editor/Unity -batchmode -projectPath $W/client/project -runTests -testPlatform PlayMode -testResults <файл> -logFile <файл>
```

## Снимок вывода генератора

Скрипты лежат в `harness/`, запускать через `bash`:

```bash
H=/projects/framework/.claude/worktrees/file_splits/docs/tasks/current/file_splits/harness
$H/build_generator.sh <дерево> <папка для dll>
$H/gen_snapshot.sh <ContainerGenerator.dll> <корень исходников> <пустая выходная папка> [дерево с csproj и Library]
diff $H/baseline_manifest.md5 <выходная папка>/MANIFEST.md5
```

- **Эталон:** итоговый md5 `4924df6ac5ed9289c96848ffad95eb4f`, md5 по файлам в `harness/baseline_manifest.md5`, полный снимок в `/tmp/claude-1000/-projects-framework/0b8312a5-015f-428b-99c3-51bf78155ba7/scratchpad/baseline/snap1/`.
- **Итоговый md5** считается по `MANIFEST.md5`. В него входят сгенерированные файлы и диагностика генератора. Ошибки компиляции и сводка лежат в `_compile_*` и в md5 не входят, поэтому смотрите `_compile_summary.txt`: на эталоне во всех 44 сборках `compileErrors=0`, `generatorDiagnostics=0`, `notInCsproj=0`, `missingFromTree=0`.
- **Исходники** берутся из папок asmdef и asmref в указанном корне, а не из списка Compile. Поэтому новые файлы от разрезов попадают в компиляцию сами, а расхождение со списком csproj видно в сводке.
- **Сборки фреймворка** компилируются в памяти в порядке зависимостей вместе с `MemoryPack.Generator`. Манифесты зависимостей поэтому строит проверяемый генератор, а не берутся из `Library/ScriptAssemblies`.
- **Запись:** скрипт пишет только в выходную папку и в `$TMPDIR/gen_snapshot`. `build_generator.sh` собирает копию исходников во временной папке, поэтому `client/core/Analyzers` не меняется.

### Проверка скрипта

| Проверка | Результат |
|---|---|
| Два запуска подряд в разных процессах, плюс третий после правки скрипта | `4924df6a...` все три раза, первые два сравнены `diff -r`: побайтно равны |
| Закоммиченный `client/core/Analyzers/ContainerGenerator.dll` | `4924df6a...` |
| Генератор, пересобранный в worktree с копированием в `Analyzers` | `4924df6a...` |
| Исходники из `git archive HEAD` во временной папке | `4924df6a...`: результат не зависит от корня |
| csproj с путями `/projects/framework/client/...` во всех 85 файлах | `4924df6a...`: префикс подменяется |
| В архив добавлен новый файл с корнем скоупа | `128fa910...`: изменился `ContainerGraph.g.cs`, появился новый класс |
| В `ScopeEmitter` изменён выводимый текст | `bcac5e9e...`: изменились 26 файлов |

Подмена путей дополнительно защищена проверкой: путь, оставшийся в `/projects/framework/` вне указанного дерева, вызывает исключение. Исходник, симлинк которого ведёт за пределы корня, тоже. `strace` на машине нет, поэтому фактические открытия файлов на уровне системы не проверены.

### Что снимок не покрывает

Классы контейнеров есть только в `Internal.Tests`: 26 корней и манифест. В остальных 39 сборках с выводом снимок содержит только `ContainerGraph.g.cs`. `Framework.Common`, `Framework.Shared`, `Framework.Session.Core` и `Internal.Browser` вывода не дают. Игровых скоупов в `client/project` нет, поэтому редкие ветки генератора побайтно проверяются только там, где их затрагивают эти 26 корней. Остальное проверяет только `Verify`, и шагу 2 нужны обе проверки.

### Хеш dll генератора зависит от пути сборки

Сравнивать надо вывод генератора, а не хеш dll. Сборка из одних и тех же исходников в разных папках даёт разные dll одного размера (171520 байт), но одинаковый вывод:

| Сборка | md5 dll |
|---|---|
| Закоммиченная | `a94f45b2551723e69d1cf5ed7ace7c5f` |
| `/projects/framework` | `4b0e2b48a9664130a0799a571c4c569e` |
| worktree | `4b3f191cc3f2d6f3c84068308a92efaa` |
| Временная папка, `build_generator.sh` | `e6b074e4feeed189e84694d4d6a5d97a` |

После сборки в worktree dll отличался от закоммиченного. Файл возвращён через `git restore` до запуска Unity, поэтому Unity работал с закоммиченным генератором.

## Состояние проекта в worktree

- **Прогон тестов не перегенерирует csproj.** После четырёх запусков Unity все 82 скопированных файла по-прежнему содержали пути основного дерева.
- **Скопированные csproj были устаревшими.** В них не было новых файлов и сборки `shared/SessionCore`, из-за чего в снимке было 57 ошибок компиляции в `Internal.Network`, `Internal.Lobbies`, `Internal.Network.Tests` и `Internal.Flow.Editor`.
- **Исправлено через SyncSolution** в worktree, 16 с: `/tools/unity/7000.0.0a10/Editor/Unity -batchmode -projectPath $W/client/project -executeMethod Packages.Rider.Editor.RiderScriptEditor.SyncSolution -quit -logFile <файл>`. Теперь csproj 85, путей основного дерева в них нет, ошибок компиляции нет. Эти csproj в git не входят. Эталонный md5 снят после этого шага.
- **Файл от Unity:** `client/project/ProjectSettings/SceneTemplateSettings.json` появился как неотслеживаемый. Unity создаёт его сам, коммитить не надо. Других изменений вне `docs/tasks/current/file_splits/` нет.

## Файлы длиннее 300 строк

Команда, запускать из корня дерева: `bash docs/tasks/current/file_splits/harness/count_over_300.sh`

Сейчас 34 файла: 6 в исходниках генератора и 28 в runtime и инструментах. В `file_splits_info.md` указано 30 и 6. Числа строк там тоже местами устарели, например `SpriteGenerator` 696 вместо 684 и `ProfilerTraceWindow` 651 вместо 636.

```
1807 client/core/Source~/ContainerGenerator/Graph/GraphWalker.cs
1157 client/core/Editor/Tools/Container/ContainerDebuggerWindow.cs
1088 client/core/Source~/ContainerGenerator/Scope/EdgeResolver.cs
1036 client/core/Source~/ContainerGenerator/Scope/ScopePlan.cs
974 client/core/Source~/ContainerGenerator/Scope/ScopeEmitter.cs
712 client/core/Editor/Tools/HierarchyBindings/HierarchyBindingsScanner.cs
684 client/core/Editor/Catalogues/Sprites/SpriteGenerator.cs
636 client/core/Editor/Tools/Profiling/ProfilerTraceWindow.cs
620 client/core/Editor/Tools/HierarchyBindings/HierarchyBindingsBatch.cs
502 client/core/Editor/Catalogues/Prefabs/PrefabCatalogGenerator.cs
501 client/core/Editor/Tools/Profiling/ProfilerFrameCapture.cs
495 client/core/Editor/Tools/ContainerCodegen/ContainerGraphAssetScanner.cs
482 client/core/Editor/Catalogues/Audio/AudioCatalogGenerator.cs
440 client/core/Editor/Tools/HierarchyBindings/HierarchyBindingsGenerator.cs
436 client/core/Runtime/Tools/Container/Runtime/ContainerBuilder.cs
431 client/modules/audio/Runtime/AudioPlayer.cs
420 client/core/Editor/Catalogues/Sprites/SpriteCatalogMetadata.cs
417 client/core/Editor/Tools/ContainerGraph/ContainerGraph.cs
408 client/core/Runtime/Tools/Container/Runtime/GeneratedScopes.cs
391 client/modules/ui/Runtime/Navigation/UINavigation.cs
386 client/core/Editor/Catalogues/Sprites/AsepriteAnimationBaker.cs
370 client/core/Editor/Catalogues/Sprites/SpritesClassGenerator.cs
360 client/modules/network/Runtime/Sockets/JsWebSocket.cs
355 client/core/Editor/Tools/LifetimeGraph/LifetimeGraph.cs
350 client/modules/network/Runtime/Latency/NetworkLatencyMonitor.cs
348 client/modules/network/Runtime/Connections/ConnectionWriter.cs
343 client/core/Editor/Catalogues/Assets/AssetCatalogGenerator.cs
343 client/modules/inputs/Runtime/GamepadReader.cs
335 client/core/Editor/Catalogues/Sprites/AsepriteDocument.cs
330 client/modules/network/Runtime/Sockets/WebRtc/NativeWebRtcPeer.cs
320 client/core/Editor/Tools/HierarchyBindings/HierarchyBindingsCodeGenerator.cs
320 client/core/Source~/ContainerGenerator/Scope/ManifestReader.cs
317 client/core/Editor/Tools/PrefabScreenshot.cs
303 client/core/Source~/ContainerGenerator/Graph/GraphEmitter.cs
```

## Покрытие классов из плана

Числа тестов взяты из `EditMode.2.xml` и `PlayMode.2.xml`. Оценка сделана по ссылкам на классы в тестовых файлах и по названиям тестов, замера покрытия кода не было.

| Класс | Тесты | Что не покрыто |
|---|---|---|
| `UINavigation` | Напрямую только `ClientOutdatedNoticeTests` (1, PlayMode): выбор остаётся в окне-оверлее | Поиск ближайшего по направлению, память выбора в окне, прокрутка к выбранному, снятие выбора. Самый слабый из UI-разрезов: перед ним нужны новые тесты на старом коде |
| `AudioPlayer` | `AudioPlayerTests` (2): фейд музыки на паузе апдейтера, два трека за кадр | Лимиты и выбор источника, обрезка звуков, громкость линий, остановка музыки, музыка на lifetime |
| `GamepadReader` | `GamepadReaderTests` (11): оси проекта, отсутствие геймпада и другое; `BackInputTests` (2) работают через `NoGamepadReader` | Чтение реального устройства (`ReadInput`). У `GamepadLayout` проверяются `HasLayout` и `ForPlatform`, раскладки кнопок по списку тестов не сверялись |
| `NetworkLatencyMonitor` | Прямых тестов нет: имя класса не встречается в тестах. `LocalSessionServerTests` и `ConnectionWriterTests` проверяют только протокольную часть пингов | Весь класс: пробы, истечение, статистика, отчёты, включая поведение из N8 (исключение форматтера останавливает пинги). Перед разрезом нужны новые тесты |
| `NetworkConnection` | `NetworkConnectionTests` (4): обрыв при подключении, отказ, код 4001, отправка после закрытия; `SocketThreadingTests` (2): таймаут подключения. Косвенно `PeerHostTests`, через `INetworkConnection` `MatchmakingTests`, `NetworkSessionTests`, `NetworkPropertiesCollectorTests` | `Drop`, расширения `OneWay` и `Request` напрямую. `IWebSocketFactory` по имени в тестах не встречается |
| `GeneratedScopes` | `ScopeRuntimeTests` (10), `ScopeCodegenEmitTests` (6), `ScopeLoaderTests` (11), `GeneratedScopeHarness`. Бенчмарки частично пропускаются. `Verify` работает со своими заглушками `Internal`, настоящий класс он не проверяет | `Unregister` вызывается только из хоста бенчмарков. Варианты сущностей (`RegisterVariant`, `HasVariants`, `VariantKey`): в `Tests/Editor/Container` и `ScopeEntityViewTests` слово `Variant` не встречается. Проверить перед шагом 8 |
| `ContainerBuilder` | `ScopeRuntimeTests` (10, в том числе отказ при незнакомой генератору регистрации и повторах), `ContainerThreadTests` (2), `GeneratedScopeHarness`; косвенно все тесты через скоупы | Отдельных тестов на класс нет, покрытие идёт через скоупы. Покрытие отдельных методов не оценивалось |
