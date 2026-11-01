# Разрез больших файлов

## Контекст

Вынесено из второго этапа аудита (`docs/audits/2026-11-02_client_stage2.md`), пункты P11, G11, U7, E11, I6, I7, N8, N9, C11. Большой рефакторинг, поведение менять нельзя.

Правила: `ARCHITECTURE_PRINCIPLES.md` P1 и P11 (файл до 300 строк, один тип в файле), P10 (поведение сохраняется и доказывается). Подсчёт от 2026-11-02: 30 файлов длиннее 300 строк в runtime и инструментах редактора, ещё 6 в исходниках генератора. Менять API можно, такой коммит помечается `BREAKING:`.

## Что сделать

1. Для каждого разреза до правок зафиксировать поведение: существующие тесты, при необходимости новые на старом коде.
2. Резать по одному классу на коммит, после каждого прогонять тесты.
3. После `client/core/Source~` собирать генератор и прогонять `ContainerGenerator.Verify`.
4. В конце пересчитать файлы длиннее 300 строк. Остаток либо разрезать, либо перечислить с причиной.

Критерий готовности: файлов длиннее 300 строк в runtime и инструментах нет или они перечислены с причиной; все тесты проходят (EditMode, PlayMode, Verify).

## Предложенные разрезы

Строки могли сдвинуться, перед работой перепроверить.

- **G11, генератор.** `Graph/GraphWalker.cs` (1807): обход, вызовы, `switch`, точки загрузки, условия вариантов. `Scope/EdgeResolver.cs` (1088): `Flatten`, экспорты, привязка параметров, циклы. `Scope/ScopePlan.cs` (1036): модель, диагностика, альтернативы, дыры, маркеры, экспорты, dispose. `Scope/ScopeEmitter.cs` (974): конструктор, аргументы, Resolve, Register, фабрики. `Injector/InjectorModel.cs:130` проверить на мёртвый код.
- **E11.** `client/core/Editor/Tools/Container/ContainerDebuggerWindow.cs`: окно-композитор и класс на секцию, дубли `ContainerGraphRead` убрать.
- **U7.** `client/modules/ui/Runtime/Navigation/UINavigation.cs`: `NavigationCandidates`, `NearestSelectableFinder`, `SelectionNavigationBinding`, `ScrollIntoSelection`, `NavigationWindow`.
- **I6.** `client/modules/audio/Runtime/AudioPlayer.cs`: `AudioLineVolumes`, `SoundPool`, `MusicPlayer`.
- **I7.** `client/modules/inputs/Runtime/GamepadReader.cs`: `GamepadButton`, `GamepadState`, `GamepadLayout`.
- **N8.** `client/modules/network/Runtime/Latency/NetworkLatencyMonitor.cs`: `LatencyProbes`, `LatencyStats`, отчёты. Исключение форматтера сейчас останавливает пинги.
- **N9.** `client/modules/network/Runtime/Connections/NetworkConnection.cs`: вынести `IWebSocketFactory`.
- **C11.** `client/core/Runtime/Tools/Container/Runtime/GeneratedScopes.cs`: реестр, id, сборка и хелперы отдельно. Меняется эмиттер.

Остальные файлы длиннее 300 строк: `HierarchyBindingsScanner.cs` (712), `SpriteGenerator.cs` (696), `ProfilerTraceWindow.cs` (651), `HierarchyBindingsBatch.cs` (620), `PrefabCatalogGenerator.cs`, `ProfilerFrameCapture.cs` (501), `AudioCatalogGenerator.cs` (500) и другие. Полный список даёт подсчёт из шага 4.
