# Pixel Swarm: поле пути, таймеры `game.*`, кнопка стика на iPad, поколение 1 (ORCH-client_telemetry-perf-fix-g1)

Worktree `/projects/pixel-swarm/.claude/worktrees/client_telemetry`, ветка `client-telemetry`, поверх `e19b6b7e` (прод). Не
запушено, кластер не запускался, submodule не двигался, общее дерево не трогалось. Unity только batchmode через
`tools/scripts/run-tests.sh` с `UNITY_SLOTS=1` (то есть только слот `unity-batchmode.lock`).

## Коммиты
- `cefad88e` `[Client] Swarm: the field of the way rebuilt only on a change, a bucket queue instead of the heap`
- `76cb5d6d` `[Client] Telemetry: the time of the field of the way, the particles, the sparks, the flashes and the light pass`
- `b9d8c18d` `[Client] [UI] Stick button: shown by PlatformOptions.IsMobile, an iPad included`

## 1. Поле пути (`client/Assets/GamePlay/Swarm/Simulation`)
- `SwarmField.Refresh(targets)` вызывается миром раз в `FieldInterval` (0.25 с, как раньше) и строит поле, только если
  изменились стартовые узлы целей (`FindWalkable` -> индекс узла, последовательность сравнивается с прошлой сборкой) или
  `ArenaGrid.Version` (растёт при `Confine`, который реально меняет диапазон). Иначе поле, построенное заново, совпало бы
  с текущим бит в бит: расстояния - функция только множества стартов и проходимости.
- Сборка: Дейкстра на кольце из 15 корзин по стоимости (Dial; шаги 10 и 14 < 15, корзина текущей стоимости не пополняется
  во время чтения), плоские `int[]`, маска открытых шагов узла (`SwarmFieldSteps`, байт на узел) считается раз на
  `ArenaGrid.Version`. Кратчайшие расстояния единственны, поэтому любая корректная очередь даёт тот же массив.
- `TryGetDistance` / `TryGetDirection` не менялись (direction по-прежнему читает проходимость живьём через `IsStep`, как и
  раньше между сборками).
- Момент обновления поля для движущегося игрока **не меняется**: проверка идёт на том же тике таймера 0.25 с, сборка
  пропускается только когда она дала бы то же поле. Эквивалентность точная, а не «незаметная».

### Доказательство (P10)
`GamePlay/Tests/SwarmFieldTests.cs`, эталоны сняты на старом коде (`e19b6b7e`) до изменений:
- `Map_TheFieldOfEveryCase_IsTheReference`: md5 по всем 97 104 узлам `D2BlueRedX2Map` (для каждого узла: есть ли путь,
  расстояние, есть ли направление и оно само из центра и из смещённой точки) для 13 случаев: старт, алтарь, алтарь при
  запечатанном кольце, старт+алтарь, два игрока в одном узле, цель в стене, за сеткой, без целей, 4 случайных узла,
  отряд из 4. Хеши записаны в тест; после изменения совпали все 13.
- `Map_AWalkOfTheSwarm_IsTheReference`: 96 преследователей на арене прода, 40 с по 60 к/с: игрок стоит, бежит к алтарю,
  стоит в запечатанном кольце, выбегает, отряд из двух, снова стоит; md5 ног всех тел каждый кадр. Старый код:
  `48891bb8b3df0d12a29f5371c46815fc` (два прогона одинаковы), новый - тот же хеш.
- `Map_TheFieldOfEveryCase_IsTheHeapDijkstra` и `Map_RefreshOnAWalk_IsTheBuildOfEveryInterval`: сравнение с
  `SwarmFieldReference` (копия старой кучи без изменений) по каждому случаю и на каждом тике прогулки с `Confine`
  (30 тиков: 9 сборок, остальные пропущены, поле каждый раз равно сборке «каждый тик»).
- `BossRingTests.Confine_TheVersionGrowsOnlyWithAChange`.

### Выигрыш (редактор, CoreCLR, `Map_BuildTime` / `Map_BuildsAMinute`, Explicit)
| | до | после |
|---|---|---|
| одна сборка, арена прода, 1 игрок (медиана 20) | 2.08 мс | 0.33 мс (x6.3) |
| отряд из 4 | 2.15 мс | 0.335 мс |
| кольцо босса | 0.19 мс | 0.029 мс |
| сборок в минуту, игрок стоит | 240 | 1 |
| сборок в минуту, 2 ед/с | 240 | 227 |
| сборок в минуту, 7.5 ед/с (скорость статов) | 240 | 234 |
| CPU поля в минуту, стоит | 499 мс | 0.3 мс |
| CPU поля в минуту, бежит | 499 мс | 77 мс |

**[оценка]** на телефоне из отчёта (36-70 мс на сборку): x6 даёт около 6-12 мс на сборку при беге и ноль при стоянии;
проверит `game.swarm.field` после деплоя. Первая сборка после смены `Version` (старт, запечатывание кольца, его снятие)
дополнительно платит пересчёт масок по 97 104 узлам - не измерял отдельно, три раза за забег.

## 2. Таймеры
| Метрика | Единица | Откуда | Нет значения |
|---|---|---|---|
| `game.swarm.field`, `.max` | ms | `SwarmField.Builds` (StepClock вокруг сборки) | нет сборок в сэмпле |
| `game.swarm.field.builds` | count | сборки за сэмпл | нет проверок: гость, игровое время стоит |
| `game.particles.update`, `.max` | ms | `PixelParticles.OnUpdate` (движение + меш) | delta 0 |
| `game.sparks.update`, `.max` | ms | `SparkWorld.OnUpdate` | delta 0 |
| `game.lights.update`, `.max` | ms | `TransientLights.OnUpdate` (затухание вспышек) | delta 0 |
| `game.lights.pass`, `.max` | ms | `LightPass.Draw` в `GamePipeline.Light`, `RenderClocks.Lights` | - (рендер идёт и на паузе) |

- `StepClock`/`StepTimes` перенесены в сборку `Rendering` (`Common/Rendering/Profiling`, namespace `Rendering`, `.meta`
  сохранены): пайплайн создаётся Unity из ассета, вне скоупов, и меряет свой проход тем же классом.
- `StepClock.EndRunning(delta)`: кадр игрового времени с delta 0 не считается шагом. Внимание: `GameTime` на паузе
  **вызывает** updatable с delta 0, поэтому `game.swarm.step` (14b, `End()` без delta) на паузе апгрейда значение имеет -
  на этом и построен разбор «шаг под паузой = системы без поля» в perf_research_2. Его не менял; новые метрики значения на
  паузе не имеют.
- Тени отдельного CPU-прохода нет: AO (`GamePipeline.Occlusion`) - две `DrawProcedural` в буфер команд, таймер не ставил.
- `UpdaterSampling` в релизе не включал: пара `Stopwatch`, `switch` по типу и поиск в словаре на каждый updatable игрового
  времени и фреймворкового Updater каждый кадр (около 40 мест `_gameTime.Add` плюс сущности), а поле всё равно внутри
  одного updatable (`SwarmSimulation`). Точечные часы: 8 вызовов `Stopwatch` за кадр плюс 2 на сборку поля.
- Тесты: `RunTelemetryTests.AStepOfAStandingGameTimeIsNotCounted`, `TheFieldCollectorWritesTheBuildsOfTheRefreshesAndTheirTime`.

## 3. Кнопка стика
`Common/Global/UI/Kit/StickButton.cs`: `PlatformOptions.IsMobile` вместо `Application.isMobilePlatform` (как `InputScheme`;
на вебе это `web-shell.js` через `WebShell.UseMobileCheck` в `GameStartup`). `SettingsWindow` и `RunPauseView` получают
`PlatformOptions` (зарегистрирован в корневом скоупе `OptionsContainer`). `GamePlay/Tests/StickButtonTests.cs`: настоящий
`Kit/Button.prefab`, проверка подменена через `SetMobileCheck`; на старой проверке случай «мобильный» красный, на новой
2/2. В `GamePlay.Tests.asmdef` добавлена ссылка `Internal.Audio` (фейк `ISettings : IAudioVolume`).

## 4. Тесты
- EditMode игры целиком (`run-tests.sh --unity-only`, уровень fast, `UNITY_SLOTS=1`): **925, 875 passed, 0 failed, 50
  skipped** (лог `/tmp/claude-1000/-projects-framework/335627c0-e338-455f-b883-14b9b709f1a2/scratchpad/full`). В 14b было
  909/861/48; новые: SwarmFieldTests 4 + 2 Explicit, BossRing 1, RunTelemetry 2, StickButton 2.
- Компиляция плеера WebGL (`LocationObjectEditorTests.PlayerWebGl_CompilesAndSkipsTheMeta`): PASS (сгенерированные
  контейнеры с `PlatformOptions` в `SettingsWindow`/`RunPauseView` собрались, CINGR нет).
- Бэкенд не трогал, не запускал. WebGL в браузере и устройство не проверялись.

## Осталось
1. После деплоя: `game.swarm.field` и `.builds` против `game.swarm.step` (шаг должен упасть на долю поля), `game.lights.pass`
   против `render.draw_calls` и `game.sparks`, `game.particles.update` против остатка Update (§2 perf_research_2).
2. Скилл `client-telemetry` (фреймворк): добавить новые имена в ожидаемые и в разбор.
3. Файлы больше 300 строк, затронутые, но не разрезанные здесь: `PixelParticles.cs` (452, было 441). `SwarmField.cs` 247,
   `SwarmFieldSteps.cs` 88.
4. При желании выровнять `game.swarm.step` с новыми метриками (`EndRunning`) - но тогда пропадут сэмплы «под паузой».
