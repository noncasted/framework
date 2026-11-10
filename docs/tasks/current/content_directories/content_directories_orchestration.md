---
task: content_directories
orchestrator_session: a0dc60a9-d496-4179-963c-5467c6763573
orchestrator_generation: 2
updated: 2026-11-10 05:30
---

## Граф шагов

Почти всё последовательно: шаги [1]–[9] идут в одном worktree фреймворка и делят один `client/project` (одна Unity),
[10]–[16] — в одной копии pixel-swarm. Параллельны только [13] и [14]: [13] собирает в reflink-копиях `deploy-client`
без открытого редактора, [14] работает в редакторе и не меняет файлов.

| Шаг | Зависит от | Волна | Агент (marker / agentId) | Поколение | Статус | Handoff |
|-----|-----------|-------|---------------------------|-----------|--------|---------|
| 1 Ветки и разведка | — | 1 | ORCH-content_directories-1-g1 / a48fce67f7ab1dd7d | 1 | done | — |
| 2 Манифест и индекс | 1 | 2 | ORCH-content_directories-2-g1 / ad2512ee7b216a918 | 1 | done | — |
| 3 Сборка каталога | 2 | 3 | ORCH-content_directories-3-g1 / ac36c31be1d2d9089 | 1 | done | — |
| 4 Хранилища | 2 | 4 | ORCH-content_directories-4-g1 / a73a4ccd23108e20a | 1 | done | — |
| 5 IContentLoader, ссылки | 3, 4 | 5 | ORCH-content_directories-5-g1 / ab31256974f4eb2a1 | 1 | done | — |
| 6 DI и сцены скоупов | 5 | 6 | ORCH-content_directories-6-g1 / a6ffbe2d52e755076 | 1 | done | — |
| 7 Каталоги | 5, 6 | 7 | ORCH-content_directories-7-g2 / af9258cd3e0d351fb (g1: aa0474be37f391efe) | 2 | done | handoff/7_g1.md |
| 8 Удаление Addressables | 7 | 8 | ORCH-content_directories-8-g1 / acd87e0b4ecf4d609 | 1 | done | — |
| 9 Тесты, доки, rc.1 | 3–8 | 9 | ORCH-content_directories-9-g1 / a8b6b6cb18b50d8ac | 1 | done | — |
| 10 pixel-swarm: ветка | 9 | 10 | ORCH-content_directories-10-g1 / a5656c6d7430482f4 | 1 | done | — |
| 11 pixel-swarm: ссылки | 10 | 11 | ORCH-content_directories-11-g1 / aed2177bcee0d0929 | 1 | done | — |
| 12 pixel-swarm: инструменты | 11 | 12 | ORCH-content_directories-12-g1 / a07ad8023fcc0e314 | 1 | done | — |
| 13 pixel-swarm: шаблоны | 12 | 13 | ORCH-content_directories-13-g1 / ad06481409056a6b5 | 1 | done | — |
| 14 pixel-swarm: редактор | 12 | 13 | ORCH-content_directories-14-g1 / ac48110fe6ac7817a | 1 | done | — |
| 16a Фреймворк: сжатие, verify | 13 | 14 | ORCH-content_directories-16a-g1 / a1ef509d81c0464ed | 1 | done | — |
| 15 pixel-swarm: замеры | 13, 14, 16a | 15 | ORCH-content_directories-15-g1 / a3bce3c7f596f1091 | 1 | done | — |
| 16b Фреймворк: очередь, link.xml, раскладка | 15 | 16 | ORCH-content_directories-16b-g1 / a746a15693f1e99c4 | 1 | done | — |
| 16c Проба UseArchive, индекс вне .data | 16b | 17 | ORCH-content_directories-16c-g4 / a1f5bfe0ad83ca13c | 4 | done | handoff/16c_g3.md |
| 16d Запросы и объём архива | 16c | 18 | ORCH-content_directories-16d-g2 / ad7a1d553eaa9ee33 (g1: a9609be7bc60a5894) | 2 | done | handoff/16d_g1.md |
| 16e Проба LZMA по файлам | 16d | 19 | ORCH-content_directories-16e-g1 / a659f04893fa42039 | 1 | done (no-go) | — |
| 16f Запуск и корень | 16e | 20 | ORCH-content_directories-16f-g1 / ab949e6b00948cc4d | 1 | stopped (заморозка) | — |
| 16g Bindings (ветка `bindings-checks` от main) | — | 21 | ORCH-content_directories-16g-g1 / a3283e6538488a154 | 1 | done | — |
| 16h Сборка контента | 16g | 22 | — | — | cancelled | — |
| 16i Тесты и update-framework | 16h | 23 | — | — | cancelled | — |
| 16j rc.7 и pixel-swarm | 16e–16i | 24 | — | — | cancelled | — |
| 17 Итог | 16g | 25 | — (оркестратор) | — | done | — |

## Ресурсы

- Worktree фреймворка `.claude/worktrees/content_directories` (ветка `content-directories`) — создаёт шаг [1], держат шаги [1]–[9] по очереди.
- Unity `client/project` этого worktree (batchmode) — один владелец за раз.
- Прототип: worktree `.claude/worktrees/content_loading_size` (ветка `content-loading-size`), `client/measurements` — только чтение, кроме разведки шага [1].
- Push тега и веток — только после подтверждения пользователя. Шаг [10] берёт ветку и тег из локального `/projects/framework` (`git fetch` по пути), push для него не нужен.
- pixel-swarm: worktree `/projects/pixel-swarm/.claude/worktrees/content_directories` (ветка `content-directories`) — шаги [10]–[16]; shared tree pixel-swarm (main, чужие незакоммиченные правки) не трогать; кластер из копии не запускать.

## Журнал

### [2026-11-09 18:40] Старт
- Граф построен, запущен шаг [1].

### [2026-11-09] Шаг [1] готов
- Проверено: worktree на `content-directories` (46a6f793) с Library; коммит `3ac4cbb2` в content-loading-size; раздел «[1] Разведка» в progress; пробы в `probe/`.
- Решения агента: drawer обёртки проксирует встроенный; `EditorContentStore` берёт корень из `AssetDatabase` без регистрации; счётчик по экземпляру `Loadable` в загрузчике; файлы корней = замыкание; каталог не снимается; предзагруженное через синхронный `Load()`.
- Шаги коммитятся в ветке `content-directories` по одному коммиту.
- Запущен шаг [2].

### [2026-11-09] Шаг [2] готов
- Проверено: коммит `5e44da59` (29 файлов, с .meta), тесты `ContentIndexWriterTests` 7/7 по отчёту.
- Индекс — json (TextAsset), ключ объекта — GUID ассета + local id, сцены — GUID.
- Открытый вопрос (передан в [3]): как рантайм получает GUID/local id из `Loadable<T>` — разбор `LoadableObjectId.ToString()` в плеере или хранение рядом в `ContentReference<T>`.
- Побочка редактора в worktree: `ProjectSettings/MultiplayerManager.asset`, `ProjectSettings/Packages/com.unity.dedicated-server/` — не коммитить.
- Запущен шаг [3] (с вопросом об id `Loadable` в плеере).

### [2026-11-09] Шаг [3] готов
- Проверено: коммит `ed5083c8` (49 файлов), `client/project` получил `Assets/ContentSmoke`, `Framework.json` `Content`, сцену в списке сборки.
- Linux-сборка вместо WebGL: `WebGlStripping.Require()` отвергает client/project (нет списка сабмодулей стриппинга). WebGL-путь — проверить в [4] (веб-смоук) или [13].
- Id в плеере: `ToString()` даёт GUID/local id, но формат не обещан; `Loadable` в сцене плеера стирается сборкой → в [5] `ContentReference<T>` хранит GUID + local id рядом (пишет редактор).
- Индекс: `Assets/FrameworkContentIndex/Resources/Content/<name>.json` временно на сборку; файлы через `AddAdditionalPathToStreamingAssets`.
- Запущен шаг [4] (с веб-смоуком WebGL).
- Агент [3] завершился окончательно; WebGL-сборка `ContentSmokeWebGl.Build` в worktree — это работа агента [4], не [3]. Лог `scratchpad/webgl.log` общий по имени — не доказательство [3].

### [2026-11-09] Шаг [4] готов
- Проверено: коммит `27526f8c` (55 файлов). EditMode 20/20, PlayMode `ContentDirectoryTests` 3/3, веб-смоук: 6 запросов → 0 при повторе.
- Отличия: `IContentStore.Prepare`, `FetchedContentStore` + `IContentFileSource` (`WebContentFetch`) вместо `WebContentStore`, без `PreloadedContentStore`, `EditorContentDirectory` за `IContentDirectory`, `ContentIndex.IsPreloaded`.
- Временно: `InternalsVisibleTo ContentSmoke` — убрать в [5]. Library client/project на WebGL. `Framework.json` `IsPreloaded: false`.
- Не проверено: предзагруженный WebGL, `AbortController` в браузере, `KeepOnly` на устаревшем кэше — в [9]/[15].
- Запущен шаг [5].

### [2026-11-09] Шаг [5] готов
- Проверено: коммит `93bd87b3` (52 файла). PlayMode 12/12, EditMode 28/28 по отчёту.
- Отличия: счёт по экземпляру ссылки (движок сам сводит разные `Loadable` в один объект); добавлены `LoadNow`, `Progress(ContentScene)`, публичный `ContentLoader.Register(lifetime, name, remoteUrl)`, `RootAssets<T>()`; ключ `AssetLoads` начинается с GUID, заканчивается именем.
- Открыто: `ContentReferenceCheck` в тексте сцен сверяет только id; `Root.asset` смоука правлен руками, плеером не проверен.
- Запущен шаг [6].

### [2026-11-09] Шаг [6] готов
- Проверено: коммиты `c06e4658` (BREAKING), `153f69f5` (генератор + dll). Verify 171 (база была 168, не 166), PlayMode 19/19, EditMode 1205/1209.
- Старые перегрузки `AssetReference` оставлены (сцены каталогов, pixel-swarm) — убрать в [7]/[8]; GUID Addressables `9e24947d…` в `Internal.PlayMode.Tests.asmdef` — убрать в [8].
- Lifetime скоупа создаётся до загрузки сцены; контент освобождается раньше групп (асинхронно) — проверить в [7].
- Запущен шаг [7].

### [2026-11-09] Шаг [7] g1 → handoff
- g1 закоммитил `1186b23b`, `529c8d32`, `fe6af624`: группы через `IContentLoader` (статический `CatalogContent`, привязка в `AddContent`), `ContentRoot` + генератор, `Scenes.X` — `ContentScene`, удалены `StaticScene` и перегрузки `AssetReference`. `RegenerateAll` exit 0; EditMode 1203/1210 (7 skip, 3 из них — `-nographics`); PlayMode 111/135 (24 skip).
- Осталось: PlayMode-тест групп → g2 запущен.
- Для [8]: `CatalogAddressablesSync`, `ScenesAddressablesSync`, `SharedAddressablesSync`(+Targets, Postprocessor), `ICatalogGroupDefinition`, `SharedAddressablesSyncTests`, `BundleUrlVersioning`, форк, зависимости.
- Риск для [11]–[12]: `Scenes.X` до `AddContent` бросает — инструменты редактора pixel-swarm, читающие `Scenes` в edit mode.

### [2026-11-09] Шаг [7] готов (g2)
- `c06f3466`: PlayMode `AssetGroupTests` 4/4, весь PlayMode 115/139 (24 skip), EditMode `AssetGroup|Content` 41/41. Багов не нашлось.
- Запущен шаг [8]. Документы и скиллы с Addressables (`.claude/docs/*`) — шаг [9].

### [2026-11-09] Шаг [8] готов
- Проверено: коммиты `b3334ef6`, `b07ebe08`, `a60cbaea`; форка нет в `client/packages`; grep по коду пуст.
- `ICatalogGroupDefinition` оставлен (ограничение `GeneratedFile.GroupByFolder`). Сторонние форки (UniTask и др.) упоминают Addressables за `#if` — оставлены.
- `verify.sh` → `content_check` (нужен `brotli`), проверен на выходе смоука [4], не полным deploy-client.
- Запущен шаг [9].

### [2026-11-09] Шаг [9] готов
- Проверено: `6837150a`, `f8e4d861`, аннотированный тег `v0.7.0-rc.1` = HEAD `f8e4d861`, не запушен.
- test-all exit 0: backend 1565, common 96, Verify 171, control 23, telemetry 84, EditMode 1200/1204 (4 skip), PlayMode 115/139 (24 skip).
- `releases/0.7.0.md` (rc.1), 7 шагов на 7 BREAKING-коммитов; для pixel-swarm: `tools/scripts/deploy-webgl.sh` проверяет `aa/catalog.bin` — убрать.
- `update-framework/status.sh` требует тег `v0.7.0` → в [10] указатель двигать вручную на `v0.7.0-rc.1` и выполнять шаги `0.7.0.md`.
- Запущен шаг [10] в worktree pixel-swarm.

### [2026-11-09] Шаг [10] готов
- Проверено: worktree pixel-swarm на `content-directories`, коммиты `c5307e352`, `56288ff2a` (WIP, не компилируется), submodule = `f8e4d861`; shared tree pixel-swarm на main.
- `ContentOptions` — поле `_content` у `GameStartup`/`MockBase`, имя `Game` в 4 сценах + `Framework.json` → кандидат в [16].
- Вне плана → в [11]: `EncounterAssets.cs` (CS0012, нет ссылки на `Internal`), `ArenaEncounter.asset` и 4 `Art/Research/Recreate/*Encounter.asset` с `m_AssetGUID`, `EncounterFinal.Create(type, guids)` в тестах.
- Запущен шаг [11].

### [2026-11-09] Шаг [11] готов
- Проверено: коммиты `142f7ddf7`, `e74617eb1`, `44302e96e`; дампы `probe/11_refs_{before,after}.tsv` — 24 строки, diff пуст.
- EditMode 1151/1201 (47 skip), 3 падения вне зоны [11]: `SwarmMapBenchmarks.Map_AWalkOfAMixedCrowd_IsTheReference`, `UpgradeScreensPrefabTests.RunBuild_HoldsEveryTree`, `ProtocolBytesTests.GoldenBytes_ReadAndWrittenBack_Unchanged` → проверить на main (db4968ca2) в [12].
- Кандидаты в [16]: сообщение `CINGR001` без ссылки на content-сборку; создание `ContentReference` в тестах без ассета.
- Запущен шаг [12].

### [2026-11-09] [12] done
- Коммиты pixel-swarm `787d01d10` (инструменты), `0451f851a` (CheatPanel, UI kit в Global), `a281f1ce2` (ContentRoot 21 группа, 5 сцен), `f3e4d2a86` (удалён AddressableAssetsData), `693af4deb` (m_LightingDataAsset в 8 сценах).
- Проверено оркестратором: git log, grep — в клиенте остались только комментарии (StressOptions, GameAudioTests, LoadedTelemetryFonts) и шаблоны WebGL (шаг [13]); backend `AddAddressableState` — это состояния кластера, не относится.
- ContentBuild: 9218 файлов, 29.1 MB br, без предупреждения unity_builtin_extra. Тесты 1368/1422, 4 падения с main (3 подтверждены на `db4968ca2`, `BackdropCrownTests` устарел после `a6a4fc642`).
- Кандидаты [16]: исключение группы из контента одной сборки (stress); дубликат класса bindings вместо ошибки; точка входа «только ContentBuild»; ~1 файл на объект — число запросов в [15].

### [2026-11-09] Волна 13: [13] ‖ [14]
- [13] deploy-client `--notest --no-deploy` в reflink-копиях, без редактора; [14] владеет редактором worktree. Коммиты обоих — только явными путями.

### [2026-11-09] [14] done
- `94ae4a819`: лямбда в `LoadRoot` (решение [10]) роняла каждый старт — корень стал локальной функцией. `098af36ba`: bindings тайлов CheatPanel `{fileID: 0}` после [12] → NRE в забеге; тест дополнен. Проверено оркестратором: git log/show, status — чисто, кроме файлов [13].
- Агенту [13] отправлено: сборки для [15] должны включать оба коммита, указать HEAD сборок.
- Кандидаты [16]: `LoadRoot(ContentOptions, construct)` / диагностика лямбды-корня; валидатор bindings пропускает `{fileID: 0}`.

### [2026-11-09] [13] done, вставлен [16a]
- `3f1dc7b69` (шаблоны без правила `.bundle`, 3 комментария), `40dff58fa` (комментарии nginx/README/deploy-webgl). Сборки от `3f1dc7b69` в `/var/tmp/content_directories-deploy-client.lluFWG/` — удалить после замеров.
- checks FAIL: `verify.sh:87` `brotli -dc | grep -q` под pipefail → 141. Контент в StreamingAssets несжат, nginx `gzip off` → игрок качает 300 MB raw вместо 29 MB br. Замерять [15] до исправления бессмысленно → шаг [16a] (часть обратной связи [16]) до [15].

### [2026-11-09] [16a] done
- Фреймворк `377a30bd` (контент WebGL+Brotli как `<file>.br`, Cache API хранит распакованное, проверка размера по индексу), `d85df041` (verify.sh), тег `v0.7.0-rc.2` = `d85df041` локально. pixel-swarm `724bf8146` (указатель, nginx: `.br` под StreamingAssets — Content-Encoding br + immutable, 404). Проверено оркестратором: git log/tag/submodule status.
- Сборки для [15]: `/var/tmp/content_directories-builds-rc2/{website,telegram}`. Reflink-копии `/var/tmp/content_directories-deploy-client.a1c1802659.*` — убрать в [17].
- Внимание [15]: старт+меню — 713 запросов к контенту (≈1 файл на объект).

### [2026-11-09] [15] done — выгода не подтверждена
- Build br: main 5 855 686 → rc2 6 078 379 (+217 KiB; data.unity3d +505 KiB — вероятно индекс). Меню: 713 запросов/4.7 с против 13/2.7 с. Забег: ~4900 одновременных fetch → ERR_INSUFFICIENT_RESOURCES; с шимом ≤64 — 13.3 MB против 8.7 MB, затем падение: стриппинг типов каталога (CircleCollider2D), link.xml не пишется. Повтор из Cache API: 0 запросов.
- Устройства подключены, но заблокированы (DDI, keyguard) — не проверены. Evidence: pixel-swarm worktree `docs/media/research/content_directories/step15/` (не закоммичено), сборка main `/var/tmp/content_directories-step15/main-website`.
- Решение оркестратора: до вопроса пользователю о судьбе миграции — [16b] чинит блокеры (очередь fetch, link.xml) и исследует уменьшение числа файлов/индекса, чтобы у пользователя были данные.

### [2026-11-09] [16b] done — ждём решения пользователя
- Фреймворк `df361352` (типы каталога через `ScriptsOnlyCache.yaml` + `AddPreviousBuildReportDirectory`), `b83e768f` (очередь fetch в jslib, `ContentOptions._maxFetches`=64, BREAKING `ContentLoader.Register(lifetime, ContentOptions)`), `010b524d` релиз, тег `v0.7.0-rc.3`. pixel-swarm `718e9b27f`. Проверено: git log/tag/submodule.
- Замеры rc3 против main: меню 713/2.57 MB/4.6 с против 13/1.62 MB/2.5 с; забег 5870/18.2 MB/12.2 с против 10/8.74 MB/4.8 с; Build br +232 KB. В a10 нет группировки, есть `UseArchive` (смоук: 1 архив) — кандидат.
- Вопрос пользователю: проба `UseArchive` на pixel-swarm (ограниченный шаг) / остановить миграцию / другое. Также: вынос индекса из `.data`.
- Мусор для [17]: `/var/tmp/content_directories-deploy-client.a1c1802659.*`, `/var/tmp/content_directories-step15`, `/var/tmp/content_directories-builds-rc3`, незакоммиченные `docs/media/research/content_directories/` в pixel-swarm worktree.

### [2026-11-10] Решение пользователя
- Проба `UseArchive` (критерий: десятки запросов и байты как у main, иначе стоп миграции); индекс — вынести из `.data` отдельным файлом каталога.

### [2026-11-10] [16c] g1 → HANDOFF (разведка готова)
- Один `content0.archive` на каталог, блоки 128 KiB, каждый файл с нового блока; частичный архив (нули + дописанные блоки) работает в Play Mode редактора; свой порядок через `ArchiveAndCompress`; без type tree LZ4 41.0 → 15.6 MB.
- Оценка: порядок движка — сотни/тысячи запросов (не проходит); свой порядок + Range со склейкой ≤16 KiB — меню 69 / 1.69 MB, забег 35 / 8.24 MB, HighFields 23 / 3.67 MB (хуже main 0.89). Критерий по оценке проходит → g2 реализует схему handoff, первым делом проверка частичного архива на WebGL.

### [2026-11-10] [16c] g2 → HANDOFF
- WebGL go: частичный архив в MEMFS работает; предупреждение `Archive file was modified while it was opened` убирается восстановлением mtime/ctime/atime узла MEMFS в jslib (внутренности emscripten — fail fast, если полей нет).
- Коммиты `577b05ea` (порядок, запись архива, читатель UnityFS, LZ4; не подключено), `b0d4475c` (плеер без type tree; редактор/PlayMode-тесты с type tree). g3: индекс со спанами, ContentBuild, Range-хранилище, jslib, verify, доки, rc.4, pixel-swarm, замеры.

### [2026-11-10] [16c] g3 → HANDOFF
- Фреймворк `041103a9` (архив + Range, индекс отдельным файлом, `ContentStamp`, verify.sh, BREAKING), `402395e9` релиз, тег `v0.7.0-rc.4`. test-all 0 (EditMode 1231, PlayMode 139). Ключ кэша `<url>?offset=<n>` (фрагмент Cache API игнорирует).
- jslib в браузере не запускался. Решение оркестратора: локальные теги не переставлять — исправления после rc.4 идут в `v0.7.0-rc.5`.

### [2026-11-10] [16c] done (g4)
- Смоук WebGL rc.4 PASS. Найдено: Range к одному URL сериализуются HTTP-кэшем Chromium (150 мс на запрос) → `440b8466` `cache: 'no-store'`, тег `v0.7.0-rc.5`. pixel-swarm `61b3e812a` (nginx `.archive`), `5e63873e8` (rc.5). Проверено: git log/tag/submodule.
- Замеры rc.5 против main: первый заход 262/11.2 MB против 24/10.6 MB; троттлинг меню 10.2/9.2 с, забег 19.9/18.7 с; HighFields 12.3/9.8 с; Build br −237 KB. Сборки `/var/tmp/content_directories-builds-rc5/`.
- Вопрос пользователю: принять результат и идти в [16] (остальная обратная связь) → [17].

### [2026-11-10] Решение пользователя
- Перед [16] дожать запросы и объём → шаг [16d].

### [2026-11-10] [16d] g1 → HANDOFF, остановка по решению пользователя
- Фреймворк `4af74831` (архив в записанном порядке загрузок игры, `ProjectSettings/ContentLoadOrder/<name>.txt`, `Tools/Content/Record Load Order`), `75c7adad`, тег `v0.7.0-rc.6`. pixel-swarm `c79a64d48`. Проверено: git log/tag/submodule.
- RangeGap больше 16 KiB хуже; +30% объёма — пофайловое LZ4HC движка; LZMA по файлам (11.6 MB, HighFields 2.1 MB) не проверен в смонтированном архиве; brotli поверх Range невозможен.
- Сборки rc.6: `/var/tmp/content_directories-builds-rc6/{website,telegram}`. Предварительно (не серия): первый заход 27 / 10.80 MB против main 24 / 10.67 MB.
- Пользователь: «после следующих билдов останови агента, дай промежуточный отчет, продолжим в другой сессии». Агентов в работе нет. Handoff оркестратора обновлён.

### [2026-11-10 05:30] Оркестратор g2 (новая сессия)
- Состояние проверено: фреймворк `75c7adad` = `v0.7.0-rc.6`, pixel-swarm `c79a64d48`, сборки rc6 и main на месте, стенд `step16d/stand` на месте, load average 2.4.
- Запущен [16d] g2: серия замеров main против rc.6, таблица в `_result.md`, цифры в `releases/0.7.0.md` / `CONTENT.md` коммитом без нового тега (rc.7 выйдет с правками [16]), удаление `builds-rc5`. Проба LZMA не входит.

### [2026-11-10 05:40] Решение пользователя (сессия 2)
- [16]: делать все четыре группы (запуск и корень, bindings, сборка контента, тесты и update-framework) и пробу LZMA. Разбито на [16e]–[16j], всё последовательно (один worktree фреймворка, одна Unity); [16e] ждёт конца замеров [16d] — им нужна тихая машина.

### [2026-11-10 07:25] [16d] done (g2)
- Серия 4×4 прогона, круг 1 при load 2.4–3.8, круги 2–4 под чужой нагрузкой 5–22 (порог снят решением оркестратора: тихой машины не было больше часа). Проверено: `stat_g2.py` (16 прогонов, errors None), коммит `5c69bb8e` (только `CONTENT.md`, `releases/0.7.0.md`), тегов на HEAD нет, rc.6 на `75c7adad`, `builds-rc5` удалён, раздел «Замеры [16d]» в `_result.md`.
- rc.6 против main: первый заход 27 / 10.80 MB против 24 / 10.60 MB; меню +0.4 с, забег +1.05 с; 150 мс / 10 Мбит: меню +0.5 с, забег +0.7 с, HighFields +2.6 с (3.67 MB против 0.82); повтор: 0 запросов, но забег +0.5–0.8 с (причина не разобрана — открытый вопрос).
- Правка доков `5c69bb8e` лежит после тега rc.6 — войдёт в rc.7.
- Запущен [16e] (проба LZMA).

### [2026-11-10] [16e] done — no-go
- WebGL-плеер не умеет LZMA в архивах вообще (и архив движка, и свой по файлам): та же ошибка на смоуке и на pixel-swarm rc.6 с перекодированным архивом. В редакторе частичный LZMA-архив работает. Потенциал: 15.56 → 11.96 MB, HighFields 3.67 → 2.11 MB. Код не менялся (HEAD `5c69bb8e`), evidence `probe/16e/`. `/var/tmp/16e` удалён оркестратором.
- Остаётся только своя распаковка вне движка (JS / wasm → MEMFS) — другая схема, вопрос пользователю в [17].
- Запущен [16f].

### [2026-11-10] Оценка brotli (оркестратор, по вопросу пользователя; офлайн, не замер стенда)
- `probe/brotli_estimate/`: сырые блоки архива rc.6 по Range-запросам прогона `g2_r_n1`, brotli q11 одним потоком на фазу: весь контент 7.96 MB (LZ4 15.56, main 12.27), меню 1.50, забег 4.61, HighFields 1.53 MB. Сырой объём 225 MB (HighFields 155 MB) — несжатый архив в MEMFS не годится, нужна перепаковка на клиенте. Кандидат в шаг-пробу, решение за пользователем.

### [2026-11-10] Решение пользователя: заморозить миграцию
- Пользователь: «выигрыш 200 КБ базовой загрузки не стоит того, слишком много нюансов, выиграем только на транспорте». Выбрано: заморозить (остановить [16f], без rc.7, в main не сливать, 0.7.0 не выпускать); находки bindings — отдельной работой от main.
- [16f] остановлен (TaskStop). В ветке `content-directories` остались его коммиты `a8ad6559`, `73cc7232` и незавершённое сохранено оркестратором как WIP `b591eaa5` (не компилировалось, тесты не гонялись). HEAD ветки `b591eaa5`, теги rc.1–rc.6 на месте, ничего не запушено.
- [16h]–[16j] отменены. [16g] запущен в новом worktree `.claude/worktrees/bindings_checks` (ветка `bindings-checks` от main).
- Уборка: удалены `/var/tmp/content_directories-builds-rc6`, `-step15` (сборка main), `.lock` deploy-client, `/var/tmp/16e`. Evidence стенда (43 MB) остаётся незакоммиченным в worktree pixel-swarm `docs/media/research/content_directories/`.

## Ресурсы (на момент заморозки)
- Фреймворк: worktree `.claude/worktrees/content_directories`, ветка `content-directories` @ `b591eaa5` — заморожена, никто не держит. Worktree `.claude/worktrees/bindings_checks` — агент [16g].
- pixel-swarm: worktree `/projects/pixel-swarm/.claude/worktrees/content_directories`, ветка `content-directories` @ `c79a64d48` (submodule на rc.6) — заморожена.

### [2026-11-10] [16g] done, [17] done — оркестрация закрыта
- `bindings-checks` (worktree `.claude/worktrees/bindings_checks`, от main `46a6f793`): `b62ba3f1`, `a1857343` (BREAKING). Проверено: `git log main..bindings-checks --stat` — только bindings, тесты, доки, `releases/unreleased.md`; test-all exit 0 по отчёту (backend 1565, common 96, Verify 168, control 23, telemetry 84, EditMode 1180 / 4 skip, PlayMode 96 / 24 skip). Дефект 1 был не в валидаторе, а в Regenerate All (не привязывал заново при неизменном `StructureHash`).
- Открыто для пользователя: слияние `bindings-checks` в main; вызывать ли валидатор из Regenerate All; числа тестов в CLAUDE.md main устарели.
- Итоги: шагов 24 (done 19, остановлен 1 — [16f], отменены 3 — [16h]–[16j], no-go 1 — [16e]); запусков агентов 29 за две сессии оркестратора (в этой — 4); handoff: 5 агентских ([7], [16c]×3, [16d]) + 1 оркестратора; blocked нет. Миграция заморожена, в main ничего не слито, ничего не запушено.

### [2026-11-10 08:22] Решения пользователя и слияние bindings
- Пользователь: слить `bindings-checks` в main — да; валидатор из Regenerate All — да; задача про текстуры HighFields в pixel-swarm — нет.
- Агент [16g] дополнил ветку: `2a88ac0b` (Regenerate All заканчивается валидатором, BREAKING), `fb6b8ec4` (числа тестов в CLAUDE.md). Проверено оркестратором: `.build/test-all/logs/20261110-081830/*.result` — все PASS (backend 1565, common 96, Verify 168, control 23, telemetry 84, EditMode 1181 / 1185, PlayMode 96 / 120).
- main fast-forward `46a6f793` → `fb6b8ec4` (4 коммита). Не запушено.
