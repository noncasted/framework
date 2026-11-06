# Unity EditMode-тесты pixel-swarm: куда ушло время

Исследование, код не менялся. 2026-11-04, worktree `/projects/pixel-swarm/.claude/worktrees/client_telemetry` (HEAD `58daa21e7`).
Скрипты разбора: `/tmp/claude-1000/utr/phases.py` (фазы из `unity-fast.log` + XML), `/tmp/claude-1000/utr/xmltop.py` (топы, diff двух XML).

## Главное

1. **Сами тесты почти не выросли.** Сумма тестов на тихой машине 20,7 c утром (1031 тест) -> 22-23 c сейчас (1098). 91 новый тест
   стоит 3,4 c вместе. Ни один тест не дольше 1,1 c, тестов выше `UNITY_FAST_LIMIT` (5 c) нет.
2. **Рост дал старт Unity, а не тесты.** Утренний прогон 06:09 импортировал 159 ассетов. Каждый прогон из worktree `client_telemetry`
   (deploy-client и мой чистый) импортирует **814-904 ассета**: native-время AssetDB 25-64 c против 2-6 c. Причина: `client/Library`
   worktree последний раз обновлялся **03.11 23:28** (в worktree нет своего редактора), а с тех пор в него вмёржено много ассетов
   (`Assets/Art/Research` 432 импорта, только `Recreate/D2BlueRedX2` - 582 файла с 23:00 03.11; `Assets/GamePlay/Assets` 271).
   `run-tests.sh` переносит между прогонами только `Library/Bee` и `Library/ScriptAssemblies`, поэтому один и тот же реимпорт
   повторяется **в каждом прогоне**. Обе сборки deploy-client делают то же самое (1334-1440 импортов, Initial Refresh 51-82 c) и
   при этом идут параллельно с тестами.
3. **Нагрузка deploy-client** (две batchmode-сборки по 8-12 ГБ, другие агенты) растягивает каждую фазу в 1,3-1,6 раза: тесты 25-35 c
   вместо 22, процесс Unity 107-166 c вместо 107 на тихой машине. Плюс ожидание слота / памяти 4-46 c (`Waiting for a free slot`,
   `Waiting for memory: 6 GB available`) и последовательный порядок шагов: backend 158-180 c и generator 22-48 c идут **до** Unity,
   поэтому этап тестов deploy-client 327-627 c.
4. Постоянный пол каждого старта, который не менялся с 03.11: первый domain reload 8-11 c, из них
   `ProcessInitializeOnLoadMethodAttributes` 7-9 c (до компиляции, т.е. это `[InitializeOnLoad*]` пакетов/фреймворка и Licensing).
5. Внутри тестов: ~5,5 c каждого прогона - это **регенерация каталогов** (SpriteGenerator, ContainerGraph, Prefab/Audio catalogue),
   которую запускает постпроцессор ассетов после `AsepriteAnimationBakerTests`; время записывается в фикстуру, идущую следом
   (`CatalogSourceTests` или `CatalogNamingTests`: 4,4-10 c при сумме тестов 0,01 c; в `output` фикстуры строки `[SpriteGenerator]
   Generated 8 sprite group(s)` и т.д.).

## Фазы Unity-этапа

`start` - от запуска до первого теста; `DR1` - первый domain reload (до компиляции), в скобках `ProcessInitializeOnLoadMethodAttributes`;
`Refresh` - Initial Refresh целиком (импорт + компиляция + второй reload); `XML` - длительность test-run; `выход` = seconds - start - XML.

| Фаза, c | Утро 06:09 (тихо, 1031) | Deploy 13:30 под нагрузкой (1098) | Чистый прогон 13:47 (1098, load 4 -> 10) |
|---|---:|---:|---:|
| Ожидание слота / памяти | 0 | 0-46 по прогонам (в 13:30 нет) | 0 |
| Snapshot | ~8 | ~8 | 8 |
| Старт до DR1 (init движка, пакеты) | ~9 | ~19 | ~14 |
| DR1 (из него InitializeOnLoadMethod) | 9,2 (8,0) | 12,0 (10,6) | 10,6 (9,2) |
| Импорт ассетов (native AssetDB), шт | 5,8 c, 159 | 32,7 c, 904 | 25,2 c, 904 |
| Компиляция Tundra (items) | 9,8 (50) | 3,6 (36) | 13,4 (378) |
| Domain reload после компиляции | 2,4 | ~1,6 | 2,7 |
| Initial Refresh всего | 19,2 | 45,0 | 42,9 |
| **start** | **40** | **76** | **68** |
| Тесты: XML / сумма тестов | 27,2 / 20,7 | 43,5 / 34,2 | 30,3 / 23,2 |
| Выход | 4 | 12 | 9 |
| **Процесс Unity** | **71** | **132** | **107** |
| Шаг unity в summary (с ожиданием) | 74 | 132 | 108 |

Ряд прогонов deploy-client сегодня (процесс Unity / start / XML / импортов): 107/63/37/814, 162/106/47/838, 166/120/35/866,
147/101/36/866, 142/90/37/898, 107/55/38/898, 132/76/44/904. Шаг unity в summary: 111, 178, 212, 190, 144, 110, 132 c.

Прогоны `run-tests.sh` из других деревьев (основной `/projects/pixel-swarm`, его Library ведёт редактор пользователя) сегодня:
61-81 c, start 28-40 c, 58-160 импортов, XML 27-38 c - то есть там прежние ~1 минута. Утренний 06:09, судя по 159 импортам, тоже из
дерева со свежей Library. `/tmp/pixel-swarm-tests` общий для всех деревьев, это надо помнить при сравнении.

Tundra прыгает от 0 до 714 items: фиксированные папки `pixel-swarm-tests.fixed0/1` общие для **всех** деревьев, состояние Bee после
прогона одного дерева (main) не подходит другому (worktree) - 378 items в чистом прогоне против 36 в deploy часом раньше.

## Топ-15 (чистый прогон, `/tmp/pixel-swarm-tests/utr-clean1/unity-fast.xml`)

Сборки: `Internal.Editor.Tests` 11,9 c (237), `GamePlay.Tests` 8,9 (421), `Internal.Network.Tests` 3,3 (89), `GamePlay.Tools.Tests` 3,2 (60),
`Internal.Tests` 1,6 (246), `Internal.Resolution.Tests` 0,75 (15, новая), `Internal.FrameRate.Tests` 0,04 (16, новая).

| Классы | c | | Тесты | c |
|---|---:|---|---|---:|
| CatalogNamingTests (5,7 - регенерация каталогов) | 5,72 | | SharedAddressablesSyncTests.PrefabImported_ProjectWithoutAddressables_NothingLogged | 1,06 |
| SwarmFieldTests | 1,82 | | WebGlShaderTablesTests.ShadersOfTheGame_AreSafeForWebGl | 1,06 |
| GenerationMemoTests | 1,64 | | GenerationMemoTests.Generate_PathMoved_SameAsWithNothingRemembered | 1,01 |
| StaticTilemapsTests | 1,19 | | SwarmFieldTests.Map_RefreshOnAWalk_IsTheBuildOfEveryInterval | 0,95 |
| SharedAddressablesSyncTests | 1,09 | | StaticTilemapsTests.Bake_SameFrame | 0,73 |
| WebGlShaderTablesTests | 1,06 | | SwarmViewBenchmarks.View_TheWritesOnChange_... | 0,68 |
| AsepriteAnimationBakerTests | 0,83 | | AutoAimVisibilityTests.ABodyNearerThanTheMuzzleIsVisible | 0,57 |
| CatalogOldYamlTests | 0,79 | | ContainerBenchmarkTests.Measure_CountsManagedAllocations | 0,56 |
| RenderResolutionScriptTests (новый) | 0,74 | | WebRtcPeerSocketTests.Connect_NoAnswer_FailsInTimeout | 0,51 |
| AutoAimVisibilityTests | 0,70 | | StaticTilemapsTests.Bake_KeepsEveryTileAndTheSorting | 0,46 |
| WebRtcPeerSocketTests | 0,68 | | SwarmFieldTests.Map_TheFieldOfEveryCase_IsTheHeapDijkstra | 0,40 |
| SwarmViewBenchmarks | 0,68 | | GenerationMemoTests.Generate_StreamMoved_... | 0,40 |
| NetworkConnectionTests | 0,67 | | CatalogOldYamlTests.AudioGroup_OldKeys_ReadsEveryField | 0,35 |
| SpiritFlightTests | 0,67 | | SocketThreadingTests.NetworkConnection_ConnectTimesOut_SocketIsAborted | 0,31 |
| HierarchyBindingsNamingTests | 0,62 | | ProtocolBytesTests.GoldenBytes_ReadAndWrittenBack_Unchanged | 0,30 |

`WebGlShaderCheck` (фреймворк, `Internal.TestTools`): `WebGlShaderCheckTests` 0,08 c, а игровой
`WebGlShaderTablesTests.ShadersOfTheGame_AreSafeForWebGl` 1,06-1,19 c. Кэш по хэшу шейдеров даст не больше 1 c - не приоритет.
Под нагрузкой (deploy 13:30) в топ выходят `HierarchyBindingsNamingTests` 5,2 c (`ChildNamedAsRoot_Compiles` 1,5 c - компиляция кода)
и `NetworkConnectionTests` 1,7 c: это нагрузка, на тихой машине 0,6-0,7 c.

## Что выросло и с какого коммита

| Что | Когда / коммит | Цена |
|---|---|---|
| Тесты 1031 -> 1098 (+91 новых, -24 убрано) | весь день; новые фикстуры: `RenderResolutionScriptTests` 0,7-1,7 c, `MergeBeachTests` 0,14, `FrameRateLimitTests` 0,09, `WebGlShaderCheckTests` 0,08, Stress*, FrameAllocations - по 0,01-0,03 | +3,4 c суммы |
| testables `framerate`, `resolution`, сборка `Internal.TestTools`; ~15 новых asmdef (telemetry, framerate, resolution, Platforms.Web.Tests, GamePlay.Stress.*, GamePlay.Balance, Flow.Tests) | `e9272395d` (13:12, manifest), `d2431301b` (13:05), `6093775a0` (Stress, 11:48), фреймворк с 02.11 | +~100 узлов Bee (1369 -> 1470 evaluated); при тёплом Bee ~0-2 c, при холодном +~1-2 c |
| Реимпорт ~850-900 ассетов в каждом прогоне из worktree | не коммит, а Library worktree от 03.11 23:28 + мёржи `origin/main` (`ccb1937cc`, `c8becfcb4` D2BlueRedX2, `b4c3235a5`, `e55a467cb` и др.) | **+20-60 c** на прогон (тесты и обе сборки) |
| Нагрузка deploy-client | `73eae1032` / фреймворк `6f49c1df` (09:24): параллельный deploy-client - тесты рядом с двумя сборками | x1,3-1,6 на всё + ожидание слота 0-46 c |
| Общие fixed-папки для разных деревьев | давно; проявляется, когда чередуются main и worktree | Tundra 0-20 c вместо 0-4 |

## План до 60 c

База - чистый прогон сейчас: 107 c (start 68, тесты 30, выход 9). Оценки на тихой машине.

| # | Изменение | Выигрыш, c | Риск |
|---|---|---:|---|
| 1 | **Переносить импорт между прогонами**: в состоянии fixed-папки хранить не только `Bee` + `ScriptAssemblies`, а всю `Library` последнего прогона (reflink, ~0 диска) - `Artifacts`, `SourceAssetDB`, `ArtifactDB`; в snapshot брать её вместо `client/Library` дерева, если она новее. Тот же приём для копий сборок deploy-client. Минимальная альтернатива: держать Library worktree свежей (`control copy` / batchmode `-quit` после мёржа) | 20-35 на тестах (native 25 c -> 2-6 c), столько же на каждой сборке deploy-client и меньше нагрузки на тесты | Средний: AssetDB копии на другом пути - проверить, что импорт не повторяется и что удалённый/изменённый ассет виден (как проверяли для Bee) |
| 2 | **Fixed-папка на дерево**: ключ папки и её состояния - путь/хэш `ROOT` (или ветка), а не номер слота | 5-12 (Tundra 13 c / 378 items -> 1-4 c) | Низкий; больше папок в `/var/tmp` (reflink, мало места) |
| 3 | **Не регенерировать каталоги во время тестов**: `AsepriteAnimationBakerTests` пишет в `Assets/AsepriteBakerTestsContent/.../Catalogues`, постпроцессор запускает Sprite/Prefab/Audio/ContainerGraph-генераторы (5,5 c). Писать в папку вне каталогов или глушить генераторы на время test run (`EditorTestsSetUpFixture`) | 5-6 | Низкий; проверить, что тесты генераторов сами зовут генерацию явно |
| 4 | **Разобрать 8-9 c `ProcessInitializeOnLoadMethodAttributes`** первого reload: прогон с `-profiler-enable -profiler-log-file`, либо бисекция - snapshot без `com.coplaydev.unity-mcp` (9 `[InitializeOnLoad]` в Services, в тестах не нужен), без addressables analyze rules и т.п. Пока гипотеза | 3-7 (оценка) | Низкий для удаления mcp из копии тестов; результат не гарантирован |
| 5 | **deploy-client: тесты не одновременно со сборками** или Unity-шаг тестов первым и backend параллельно с ним (backend 160-180 c и generator ~40 c сейчас идут до Unity последовательно); слоты не делить с двумя сборками | этап тестов 327-627 -> ~180 (max(backend, unity)); Unity-шаг -20-60 под нагрузкой | Средний: дольше общий deploy, если сборки ждут тестов; можно стартовать сборки после импорта (п.1 убирает основную нагрузку) |
| 6 | Выход 4-15 c: посмотреть хвост лога (сохранение, `AssetDatabase` на выходе, quit с SIGKILL после записи XML - скрипт уже ловит XML) | 3-8 | Низкий-средний |
| 7 | Тёплый редактор (`--editor <copy>`): нет старта вовсе, ранее мерили 46-73 c на весь `fast` | 40-60 от start | Есть уже; редактор без окна только, для deploy не годится (снапшот обязателен) |

Не рекомендую: Manual/ночной уровень для тяжёлых тестов (самый тяжёлый тест 1,1 c, весь XML 30 c - переносить нечего, кроме ~5 c
регенерации из п.3); параллельные Unity по сборкам тестов (каждый процесс платит start 30-70 c, а тесты всего 23 c); кэш
`WebGlShaderCheck` (≤1 c); сокращение testables (framerate + resolution = 0,8 c тестов и несколько узлов Bee).

Ожидание после 1+2+3: start 68 -> ~30 (init ~14 + DR1 10 + refresh ~6), тесты 30 -> 25, выход ~6: **~60-65 c**, то есть уровень
прогонов из main сегодня (61-71 c). Уверенно ниже 60 c - только вместе с п.4 и п.6 (ещё 6-15 c).
Под нагрузкой deploy-client без п.5 будет примерно x1,3-1,6.

## Сырые данные

- Чистый прогон: `/tmp/pixel-swarm-tests/utr-clean1/` (log, xml, summary), вывод `/tmp/claude-1000/utr/clean1.out`, load по 10 c
  `/tmp/claude-1000/utr/load.txt` (3,7 -> 10,4 - нагрузку дал сам Unity; кроме него работали только редактор пользователя и два idle
  batchmode-редактора копий).
- Список импортов deploy-прогона 13:12: `/tmp/claude-1000/utr/deploy_imports.txt` (898 строк: 538 TextureImporter, 191 NativeFormat).
- Утро: `/tmp/pixel-swarm-tests/20261104-060915/`; deploy: `/tmp/client_telemetry-deploy-client/20261104-*/tests/`.

## Result

2026-11-04, агент `unitytests_fix_g1`, копия `/var/tmp/ps-tests` (HEAD worktree 58daa21e + framework f5affc6a, `client/Library` -
устаревшая Library worktree от 03.11 23:28). Diff: `/home/nndd/.claude/jobs/9180826f/tmp/tests-framework.diff`, `tests-game.diff`.

Что сделано:
1. `run-tests.sh` (игра): состояние fixed-папки - вся `client/Library` последнего прогона (`mv`, без копии); snapshot берёт её,
   если она новее Library дерева (mtime `ArtifactDB`/`SourceAssetDB`), иначе Library дерева + `Bee`/`ScriptAssemblies` состояния.
   deploy-client (фреймворк): прошедшая сборка оставляет свою Library (без `Bee`, stripping backups, `BuildHistory`) в
   `/var/tmp/<game>-deploy-client.<key>.<target>`, следующая сборка того же target из того же дерева стартует с неё, если она новее.
2. Fixed-папки на дерево: `pixel-swarm-tests.<sha1(path)[:10]>.fixed<N>`; deploy-client передаёт тестам `TEST_TREE=<корень игры>`
   (тесты идут из snapshot на новом пути). Слоты Unity не менялись.
3. `CatalogGenerationRunner.PauseScheduled/ResumeScheduled` (запланированный/отложенный запуск во время паузы отбрасывается,
   `Run`/`RunNow`/меню работают); `EditorTestsSetUpFixture` ставит паузу на сборку `Internal.Editor.Tests`. Тест
   `CatalogGenerationPauseTests.DeletedPrefab_GeneratesNothing`: до фикса FAIL (`[ContainerGraph] Wrote 6...`,
   `[PrefabCatalogGenerator] Generated 3...`), после - PASS. `CatalogNamingTests` 5,7 -> 0,02 c. Источник был не Aseprite, а
   удаление временных prefab'ов тестов (`CatalogMetadataTests` и др.): постпроцессоры Prefab/ContainerGraph реагируют на любой `.prefab`.
4. `run-tests.sh`: backend + generator в фоне рядом с Unity (вывод печатается после Unity, строки summary через файл; при сигнале
   их процессы убиваются по маркеру окружения). Память: минимум MemAvailable в deploy-client 7 и 4 ГБ (было 3-6), слоты и
   проверки памяти Unity/сборок сами ждут - оставлено параллельно.
5. Замер без `com.coplaydev.unity-mcp` (manifest копии, два прогона): `ProcessInitializeOnLoadMethodAttributes` 7,8 c против 7,7 c
   с ним - unity-mcp не причина. Дальше нужен профайлер (`-profiler-enable`) или бисекция пакетов фреймворка; не делалось.

| Прогон (load) | Unity, c | start, c | Tundra, c | импорт при старте | шаг tests целиком |
|---|---:|---:|---:|---:|---:|
| До: старый скрипт, `--unity-only` (load ~8) | 102 | 61 | 3,4 | 904 | backend+gen+unity последовательно: 145+15+103 = 263 |
| После, 1-й полный (load 11-18, после `--test`-прогона) | 66 | 33 | 3,2 | 0 (60 - ассеты самих тестов) | 165 (max(160, 69)) |
| После, 2-й полный (load 7-9) | 67 | 36 | 0,6 | 0 | 160 |
| После, `--unity-only` после прогона другого дерева (load ~10) | 56 | 27 | 0,1 | 0 |  |
| Другое дерево, первый прогон своим ключом | 106 | 73 | 13,9 | 846 |  |
| deploy-client до (исследование) | 107-166 | 55-120 | | 814-904 | 327-627 |
| deploy-client 1 `--no-deploy` | 77 | 38 | 0,3 | 0 | 203 |
| deploy-client 2 `--no-deploy` | 91 | 48 | 0,8 | 0 | 200 |

Сборки deploy-client: 1-й прогон web/telegram 481/467 c, импорт 1440/1440, Initial Refresh 58 c; 2-й (Library прошлой сборки)
462/462 c, импорт 19/19, Initial Refresh 35 c. Оставшиеся 25 c Refresh - Tundra 1114 items: копия сборки на новом пути
(`$WORK/<target>`) пересобирает все скрипты; фиксированный путь копии сборки дал бы ещё ~25 c (и, возможно, инкрементальный
player build), не делалось. Итог deploy-client 539 -> 495 c, суммарно доминирует сама сборка WebGL (~460 c).

Тесты: 1099 (1046 passed, 53 skipped, 0 failed) во всех прогонах; backend 576/576. Ожидаемое число EditMode фреймворка в
CLAUDE.md 1075 -> 1076 (новый тест); сам `client/project` фреймворка не прогонялся.
