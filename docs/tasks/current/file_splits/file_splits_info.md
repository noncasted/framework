---
task: file_splits
status: in_progress
phase: implementation
created: 2026-11-02
updated: 2026-11-02
total_steps: 9
completed_steps: [1, 2, 3, 4]
blocked_steps: []
---

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

## План реализации

Область: `client/core`, `client/flow`, `client/modules`, `client/platforms` и исходники генератора, как в подсчёте аудита. Backend, форки `client/packages` и тестовый код (`ContainerGenerator.Verify`, `Tests`) не режутся и перечисляются в итоге с причиной.

#### [1] Замер поведения до правок
- **Статус:** [x] completed
- **Что:** EditMode, PlayMode, Verify на исходном коде; скрипт снимка вывода генератора; список файлов длиннее 300 строк
- **Зависит от:** —

#### [2] G11, генератор
- **Статус:** [x] completed
- **Что:** `GraphWalker`, `EdgeResolver`, `ScopePlan`, `ScopeEmitter`, `ManifestReader`, `GraphEmitter`, проверка `InjectorModel.cs:130`. Отдельный worktree. Проверка: Verify и побайтно равный вывод генератора
- **Зависит от:** 1

#### [3] U7, I6, I7
- **Статус:** [x] completed
- **Что:** `UINavigation`, `AudioPlayer`, `GamepadReader`
- **Зависит от:** 1

#### [4] N8, N9, сеть
- **Статус:** [x] completed
- **Что:** `NetworkLatencyMonitor`, `NetworkConnection`, `ConnectionWriter`, `JsWebSocket`, `NativeWebRtcPeer`
- **Зависит от:** 3

#### [5] E11, окна и графы
- **Статус:** [ ] pending
- **Что:** `ContainerDebuggerWindow`, `ContainerGraphAssetScanner`, `ContainerGraph`, `LifetimeGraph`, `ProfilerTraceWindow`, `ProfilerFrameCapture`, `PrefabScreenshot`
- **Зависит от:** 4

#### [6] HierarchyBindings
- **Статус:** [ ] pending
- **Что:** `HierarchyBindingsScanner`, `HierarchyBindingsBatch`, `HierarchyBindingsGenerator`, `HierarchyBindingsCodeGenerator`
- **Зависит от:** 5

#### [7] Каталоги
- **Статус:** [ ] pending
- **Что:** `SpriteGenerator`, `PrefabCatalogGenerator`, `AudioCatalogGenerator`, `SpriteCatalogMetadata`, `AsepriteAnimationBaker`, `SpritesClassGenerator`, `AssetCatalogGenerator`, `AsepriteDocument`
- **Зависит от:** 6

#### [8] C11 и ContainerBuilder
- **Статус:** [ ] pending
- **Что:** `GeneratedScopes` (меняется эмиттер), `ContainerBuilder`
- **Зависит от:** 2, 7

#### [9] Итоговая проверка
- **Статус:** [ ] pending
- **Что:** Пересчёт файлов, все тесты, документация, остаток с причинами
- **Зависит от:** 8

