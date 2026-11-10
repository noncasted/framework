---
task: content_directories
status: completed
phase: completed
updated: 2026-11-10
---

## Фреймворк и pixel-swarm на Content Directories — Результат

### Статус: Завершено — миграция заморожена решением пользователя (2026-11-10); в main не слито, 0.7.0 не выпущен. [16g] (bindings) слит в main (`fb6b8ec4`), не запушен

### Что сделано
- Фреймворк полностью переведён с Addressables на Content Directories в ветке `content-directories` (HEAD `b591eaa5`, локальные теги `v0.7.0-rc.1`…`rc.6`, последний зелёный test-all — rc.6 `75c7adad`): манифест и индекс, сборка каталога с плеером, хранилища, `IContentLoader` / `ContentReference<T>` / `ContentScene`, DI и сцены скоупов, каталоги, удаление Addressables и форка, доки и `releases/0.7.0.md`.
- pixel-swarm переведён в ветке `content-directories` (`c79a64d48`, submodule на rc.6): 24 ссылки, инструменты, каталоги, шаблоны WebGL, nginx; запускается в редакторе и в вебе.
- Для веба пришлось уйти от схемы движка (файл на объект, 9218 файлов): один архив UnityFS в записанном порядке загрузок, Range-запросы в файл нулей MEMFS, Cache API, индекс отдельным файлом.

### Итог и решение
**Миграция заморожена.** Сравнение rc.6 с main (Addressables) на стенде pixel-swarm, 4 прогона:

| | main | rc.6 |
|-|-|-|
| `Build/` br | 5 855 686 | 5 618 527 (−237 KB) |
| контент на сервере | 12.27 MB | 15.9 MB (+30%) |
| первый заход: запросы / байты | 24 / 10.60 MB | 27 / 10.80 MB |
| меню / забег без троттлинга | 2.76 / 5.30 с | 3.14 / 6.27 с |
| меню / забег, 150 мс / 10 Мбит | 9.54 / 19.27 с | 10.04 / 19.67 с |
| HighFields, 150 мс / 10 Мбит | 9.87 с (0.82 MB) | 12.76 с (3.67 MB) |
| повторное открытие, забег | 4.13 с, 2–3 запроса | 4.92 с, 0 запросов |

Причины заморозки: единственный измеримый выигрыш — −237 KB сборки; по времени rc.6 медленнее почти во всех фазах; объём +30% не убирается средствами движка (LZMA в WebGL-плеере 7000.0.0a10 не поддержан — [16e]); вариант «brotli-транспорт + перепаковка в LZ4 на клиенте» по офлайн-оценке дал бы 7.96 MB контента, но это ещё один слой своей инфраструктуры ради транспорта и 225 MB сырого объёма, который нельзя держать в MEMFS (`probe/brotli_estimate/`); своя схема архива держится на внутренностях UnityFS / MEMFS / emscripten; реальные устройства не проверены.

Когда вернуться: WebGL-плеер Unity получит LZMA или группировку файлов content directories; либо появится причина отказаться от Addressables, не связанная с размером.

Что осталось и где:
- ветки `content-directories` во фреймворке и pixel-swarm и теги rc.1–rc.6 — локальные, не запушены;
- незавершённое [16f] в ветке фреймворка: `a8ad6559`, `73cc7232` (тесты шага целиком не гонялись), WIP `b591eaa5` (не компилировалось);
- evidence стенда — незакоммиченный `docs/media/research/content_directories/` в worktree pixel-swarm (43 MB); пробы — `probe/` задачи;
- находки, не связанные с контентом, — ветка `bindings-checks` от main (`b62ba3f1`: Regenerate All заново привязывает объект с пустой ссылкой; `a1857343`: генератор отказывает во втором файле класса, BREAKING, шаг в `releases/unreleased.md`); `2a88ac0b`: Regenerate All заканчивается валидатором, BREAKING); test-all exit 0; слито в main fast-forward `46a6f793` → `fb6b8ec4` по решению пользователя, не запушено.
- HighFields: 130 из 143 MB сырого объёма локации — несжатые запечённые текстуры `Art/Research/Recreate/HighFields/Generated` (RGBA 1600×1824); это контент игры, на main те же байты лежат в бандле сцен. Задачу в pixel-swarm пользователь решил не заводить.

### Измененные файлы

| Файл | Что изменено | Шаг | Evidence |
|------|-------------|-----|----------|
| `client/measurements/parts/content-directory-sync/`, `variants/content-directory-sync.json`, `results/7000.0.0a10/content-directory-sync.json` (ветка content-loading-size) | смоук синхронного `Load()` в вебе | 1 | `3ac4cbb2`, `sync Load: preloaded 42 in 12 ms, fetched Content in 3 ms` |
| `client/core/Editor/Content/{ContentManifest*,ContentFileKind,ContentFileClosure,ContentIndexWriter}.cs`, `client/core/Runtime/Content/{ContentIndex,ContentFile,ContentObjectFiles,ContentSceneFiles}.cs`, тест `ContentIndexWriterTests` | манифест и индекс загрузок | 2 | `5e44da59`, 7/7 |
| `client/core/Editor/Content/Build/*`, `Runtime/Content/ContentPaths.cs`, `FrameworkSettings.cs`, `platforms/web/Editor/WebGlBuild.cs`, `client/project/Assets/ContentSmoke/*`, `releases/unreleased.md` | сборка каталога с плеером | 3 | `ed5083c8`, 23/23, Linux-сборка |
| `client/core/Runtime/Content/{Stores/*,Plugins/WebContentFetch.jslib,IContentDirectory,ContentDirectory,EditorContentDirectory}.cs`, тесты `ContentFileFetchesTests`, `ContentDirectoryTests`, `client/project/Assets/ContentSmoke/*` | хранилища и регистрация | 4 | `27526f8c`, 20/20 + 3/3, веб-смоук |
| `client/core/Runtime/Content/{ContentReference*,ContentScene,ContentProgress,IContentLoader,ContentLoader,ContentObjectLoad,ContentSceneLoads,ContentLoadedScene,...}.cs`, `client/core/Editor/Content/Drawers/*`, тесты `ContentLoaderTests`, `ContentReferenceTests`, `ContentReferenceFieldsTests` | загрузчик, ссылки, drawer'ы | 5 | `93bd87b3`, 12/12 + 28/28 |
| `client/core/Runtime/Content/Scopes/*`, `Runtime/Scopes/Services/*` (сцены скоупов), `Source~/ContainerGenerator/*`, `Analyzers/ContainerGenerator.dll`, `ContentScopeTests` | DI и сцены скоупов | 6 | `c06e4658`, `153f69f5`, Verify 171 |
| `client/core/Runtime/Catalogues/Shared/{AssetGroup*,CatalogContent,ContentRoot}.cs`, `client/core/Editor/Catalogues/**` (`ContentRootGenerator`, `ContentRootBuildStep`), `RegenerateAll.cs`, удалён `StaticScene.cs`, тесты `AssetGroupTests`, `client/project/.../ContentRoot.asset` | каталоги на Content Directories | 7 | `1186b23b`, `529c8d32`, `fe6af624`, `c06f3466` |
| удалены `*AddressablesSync*`, `SharedAddressables*`, `BundleUrlVersioning`, их тесты, форк `client/packages/com.unity.addressables`; `FrameworkSettings`, asmdef, `package.json`, `manifest.json`, `tools/scripts/deploy-client/verify.sh` | удаление Addressables | 8 | `b3334ef6`, `b07ebe08`, `a60cbaea` |
| `CLAUDE.md`, `.claude/docs/{CONTENT,INDEX,WEBGL_TROUBLESHOOTING,COMMON_CONTAINER,ARCHITECTURE_PATTERNS}.md`, скиллы `deploy-client`, `update-override-packages`, `client-telemetry`, `releases/0.7.0.md`, `releases/README.md`, `releases/unreleased.md` | доки, числа тестов, релиз-кандидат | 9 | `6837150a`, `f8e4d861`, тег `v0.7.0-rc.1`, test-all exit 0 |
| pixel-swarm: `tools/framework` → `v0.7.0-rc.1`, `manifest.json`, 13 asmdef, `Framework.json`, `Scenes.cs`, корневой `Construct`, сцены Startup/Menu/Lobby/Game | ветка и механические шаги 0.7.0 | 10 | `c5307e352`, `56288ff2a` |
| pixel-swarm: `LevelDefinition`, `LevelScopeExtensions`, `EncounterFinal`, `RunFinals`, `EncounterAssets`, asmdef `GamePlay.Tools`, ассеты уровней и энкаунтеров, тест `LevelContentReferenceTests` | ссылки и загрузки, миграция 24 ссылок | 11 | `142f7ddf7`, `e74617eb1`, `44302e96e`, diff дампов пуст |
| pixel-swarm: `LevelAssets`, `EnemyAtlases`, `EnemyAtlasTests`, удалены `LevelAddressables`, `StressBuildStep` + `GamePlay.Stress.Editor`; `CheatPanel`, UI kit → `Common/Global/Assets/Kit`; `ContentRoot.asset` и каталоги; удалён `AddressableAssetsData`; `m_LightingDataAsset` в 8 сценах | инструменты, каталоги, удаление данных Addressables | 12 | `787d01d10`…`693af4deb`, ContentBuild 9218 файлов / 29.1 MB br |
| pixel-swarm: `InternalScopeExtensions`, `GlobalScopeExtensions`, `CheatPanel.prefab`, `UpgradeScreensPrefabTests` | корень скоупа без лямбды, bindings тайлов чит-панели | 14 | `94ae4a819`, `098af36ba`, Play Mode/моки без ошибок |
| pixel-swarm: `WebGLTemplates/{Default,Telegram,Stress}/index.html`, комментарии `StressOptions`, `GameAudioTests`, `LoadedTelemetryFonts`, `client/deploy/{nginx.conf,README.md}`, `deploy-webgl.sh` | шаблоны без логики бандлов | 13 | `3f1dc7b69`, `40dff58fa`; web/telegram собраны |
| фреймворк: `client/core/Editor/Content/Build/*`, `WebContentFetch.jslib`, хранилища, `verify.sh`, доки, `releases/0.7.0.md`; pixel-swarm: указатель, `client/deploy/nginx.conf`, README | контент WebGL как brotli `<file>.br`, verify без SIGPIPE | 16a | `377a30bd`, `d85df041`, `v0.7.0-rc.2`, `724bf8146`; test-all 0, deploy-client 0 |
| фреймворк: `client/core/Editor/Content/Archive/*`, `ContentDirectoryBuilder` (без type tree), индекс отдельным файлом, `ContentStamp`, Range-хранилище, `WebContentFetch.jslib`, `verify.sh`, доки, `releases/0.7.0.md`; pixel-swarm: указатель, `nginx.conf` | каталог одним архивом в своём порядке, Range-загрузки | 16c | `577b05ea`, `b0d4475c`, `041103a9`, `402395e9`, `440b8466`, `0a07e2d0`; rc.4, rc.5; `61b3e812a`, `5e63873e8` |
| фреймворк: `client/core/Editor/Content/Order/*` (`ContentLoadKey`, `ContentLoadOrder`, `ContentLoadOrderRecorder`, `RecordingContentDirectory`, `ContentLoadOrderMenu`), `ContentArchiveOrder`, `ContentArchiveWriter`, `ContentBuild`; pixel-swarm: указатель, `client/ProjectSettings/ContentLoadOrder/Game.txt` | архив в записанном порядке загрузок | 16d | `4af74831`, `75c7adad`, `v0.7.0-rc.6`, `c79a64d48`; test-all 0 |

### Замеры [15]

«До» — main pixel-swarm `db4968ca2` (Addressables), сборка website от `deploy-client --notest --no-deploy` (сохранена:
`/var/tmp/content_directories-step15/main-website`); «после» — rc2 `724bf8146` (`/var/tmp/content_directories-builds-rc2/website`).

**Размер (байт, br / raw):**

| | main (Addressables) | rc2 (Content Directories) | Δ br |
|-|-|-|-|
| wasm | 4052901 / 16320787 | 3840639 / 15745073 | −212262 |
| data | 1700266 / 5468178 | 2134441 / 5874683 | +434175 |
| — из них `data.unity3d` | 421103 / 516928 | 926517 / 1154336 | +505414 |
| — `global-metadata.dat` | 1412588 / 4477020 | 1342443 / 4246076 | −70145 |
| framework.js | 74344 / 374037 | 75124 / 377564 | +780 |
| loader.js | 28175 | 28175 | 0 |
| **Build/ итого** | **5855686** | **6078379** | **+222693 (+217 KiB)** |
| StreamingAssets | `aa/`: 36 файлов, 32 бандла LZ4, 12269593 | `Content/Game`: 9218 `.br`, 29135874 (raw 300972235) | +16866281 |

Ожидание плана (−250…330 KiB) **не подтвердилось**: wasm −207 KiB, но `data.unity3d` +494 KiB — почти наверняка индекс каталога
(`ContentIndex`: 9218 файлов с hex-именами + 8874 объекта с guid и замыканиями; оценка ~1.1 MB raw), он съедает выигрыш.

**Стенд** (headless Chromium, mali-g52 эмуляция, `--gpu`, CPU ×1, HTTP/2 сервер стенда `serve_h2.mjs`; `?autorun=1` = WinterForest,
затем `&level=HighFields`, затем повторное открытие; один профиль браузера):

| шаг | main: запросов / байт / время | rc2: запросов / байт / время |
|-|-|-|
| старт → меню (без троттлинга) | 13 / 1.70 MB / меню 2.7 с | 713 / 2.57 MB / меню 4.7 с |
| меню → забег WinterForest | 10 / 8.74 MB / забег 8.0 с | 4929 запросов сразу → `ERR_INSUFFICIENT_RESOURCES`, **забег не стартует** |
| то же с шимом-очередью ≤64 fetch | — | 4929 / 13.3 MB, всё скачано к 10.8 с; забег падает (п.2 багов) |
| HighFields (вторая локация) | 4 / 1.05 MB / забег 4.2 с | 776 / 3.22 MB (шим), забег падает |
| повторное открытие | 2 / 73 KB (каталог) / забег 4.9 с | **0 запросов к Content**, 6418 файлов в Cache API, меню 3.4 с |
| старт → меню, 150 мс / 10 Мбит | меню 9.4 с, забег 19.7 с | меню 12.5 с, весь контент забега (шим) к 29.3 с |
| HighFields, 150 мс / 10 Мбит | забег 10.0 с | меню 8.8 с, контент к 13.3 с |

**Устройства:** iPhone 11 (iOS 16.6.1), iPad 7 и Redmi 12 подключены, но заблокированы (`PasswordProtected: true`, DDI не
монтируется, `mWakefulness=Asleep`, keyguard) — Safari / Chrome на них не проверены.

**Баги (во фреймворк, вход [16]; не правились):**
1. **Все fetch'и каталога сразу.** `ContentFileFetches` / `WebContentFetch_Start` запускают fetch на каждый файл без ограничения:
   загрузка уровня — ~4900 запросов одновременно; Chromium (HTTP/1.1 `serve_tls.py` и HTTP/2) отвечает `net::ERR_INSUFFICIENT_RESOURCES`
   на ~2700 из них, загрузка падает `IOException ... status 2`. Нужна очередь (например 32–64 в полёте, слот до чтения тела), и/или
   меньше файлов (9218 файлов на 8873 объекта).
2. **Типы, нужные только каталогу, вырезаны из плеера.** `ContentFile cah:/<scene> contained objects with undefined types. These types
   were likely stripped ...` + `could not find local object LFID ..., PersistentTypeId:58` (CircleCollider2D) в сценах
   `Game_WinterForest` / `Game_HighFields` → `[Arena] ...Location: the colliders group Gaps has no colliders`. На main Addressables
   писали `Library/com.unity.addressables/aa/WebGL/AddressablesLink/link.xml` (599 типов, в т.ч. `BoxCollider2D`, `CircleCollider2D`,
   `PolygonCollider2D`, `Rigidbody2D`, игровые MonoBehaviour); `ContentBuild` ничего такого не пишет. Нужен link.xml / список типов
   из манифеста каталога в сборку плеера.
3. **Индекс в `.data`** +494 KiB br (см. размер): хранить хэши бинарно / индекс отдельным файлом каталога / объекты только корней.
4. Байты забега больше: WinterForest 13.3 MB br против 8.7 MB бандлов LZ4 main.

Evidence: `/projects/pixel-swarm/.claude/worktrees/content_directories/docs/media/research/content_directories/step15/` (результаты
`t1` rc2 без шима, `t2`/`t3` rc2+шим, `m2`/`m3` main, `h1_run` официальный `run.mjs` по HTTP/1.1; скрипты стенда).

### Замеры [16b]

«До» — main `db4968ca2` (`/var/tmp/content_directories-step15/main-website`), «после» — rc3 `718e9b27f` (`/var/tmp/content_directories-builds-rc3/website`),
стенд [15] (headless Chromium, mali-g52, `--gpu`, CPU ×1, HTTP/2 `serve_h2.mjs`, без шима и без троттлинга, одна серия, load ~5).

| шаг | main: запросов / байт / время | rc3: запросов / байт / время |
|-|-|-|
| старт → меню | 13 / 1.62 MB / меню 2.5 с | 713 / 2.57 MB / меню 4.6 с |
| меню → забег WinterForest | 10 / 8.74 MB / забег 4.8 с | 5870 / 18.2 MB / **забег 12.2 с** (в [15] не стартовал) |
| весь первый заход (до конца окна) | 24 / 10.5 MB, последний 8.1 с | 6801 / 21.5 MB, последний 15.6 с, 0 failed, пик в полёте 64 |
| HighFields | 3 / 0.89 MB / забег 4.0 с | 767 / 3.17 MB / забег 6.7 с |
| повторное открытие | 2 / 73 KB / забег 3.9 с | 145 (дозагрузка хвоста забега) / 0.48 MB / забег 6.8 с, 7713 файлов в Cache API |
| консоль | офлайн мета | офлайн мета; нет `undefined types`, `has no colliders`, `ERR_INSUFFICIENT_RESOURCES` |

Размер Build/ (br): main 5855686 → rc3 6088318 (**+232632**): wasm 4052901 → 3848917 (−203984; +8278 к rc2 — сохранённые типы каталога),
`.data` 1700266 → 2135829 (+435563; `data.unity3d` +506130, `global-metadata` −73050).

Индекс каталога (json как пишет `ContentIndexWriter`): 1508265 raw / 295160 br сам по себе, внутри LZ4HC `data.unity3d` ≈ +506 KB br;
бинарный с графом зависимостей ≈ 365 KB / 216 KB br; пол — 147 KB хэшей 9218 файлов. `UseArchive` на смоуке: 7 файлов → 1 `content0.archive`.

### Замеры [16c]

«До» — main `db4968ca2` (`/var/tmp/content_directories-step15/main-website`, Addressables), «после» — rc.5 pixel-swarm `5e63873e8`
(`/var/tmp/content_directories-builds-rc5/website`: архив `<hash>.archive` 15565776 байт + индекс `.index.json.br` 319747), для справки
rc.4 `61b3e812a`. Стенд [15]/[16b] (headless Chromium, mali-g52, `--gpu`, CPU ×1, HTTP/2 `serve_h2.mjs` + Range/206), одна серия,
машина не тихая (3 чужих редактора pixel-swarm, load ~2–5). Шаги: старт → меню → забег WinterForest; HighFields; повторное открытие.
Время — от навигации до метки (меню / `[Autorun] run started`). Запросы и байты — к `StreamingAssets/` (контент); Build/ одинаков у всех.

Без троттлинга:

| шаг | main: запросов / байт / время | rc.5: запросов / байт / время |
|-|-|-|
| старт → меню | 13 / 1.62 MB / меню 2.6 с | 105 / 2.82 MB (вкл. индекс 0.32) / меню 2.9 с |
| меню → забег WinterForest | 10 / 8.74 MB / забег 5.0 с | 152 / 8.13 MB / забег 6.0 с |
| весь первый заход | 24 / 10.6 MB, последний 8.2 с | 262 / 11.2 MB, последний 9.3 с, 0 failed |
| HighFields | 4 / 1.05 MB / забег 4.0 с | 12 (+8 хвост) / 3.62 (+0.30) MB / забег 4.4 с |
| повторное открытие | 2 / 73 KB / забег 3.9 с | 0 (из Cache API) / забег 4.8 с |
| консоль | офлайн мета | офлайн мета; нет `Archive file was modified`, `undefined types`, `instead of 206`, `Content-Encoding` |

С троттлингом 150 мс / 10 Мбит (CDP `emulateNetworkConditions`; в скобках — шейпинг на сервере: 150 мс до заголовков, один канал 10 Мбит):

| шаг | main | rc.4 (до фикса) | rc.5 |
|-|-|-|-|
| меню | 9.2 с (9.6) | **24.2 с (24.3)** | 10.2 с (10.4) |
| забег WinterForest | 18.7 с (19.5) | **50.6 с (50.7)** | 19.9 с (20.3) |
| HighFields, забег | 9.8 с (10.1) | 12.3 с (12.6) | 12.3 с (12.5) |
| повторное открытие, забег | 9.3 с (9.6) | 9.6 с (9.9) | 9.7 с (9.8) |

rc.4 под латентностью: Chromium пускал запросы одного URL через запись HTTP-кэша по одному — диапазоны архива приходили на сервер
ровно через 150 мс (лог сервера). Фикс rc.5 — `cache: 'no-store'` у Range-запросов (Cache API и так хранит байты).

Размер Build/ (br): main 5855686 → rc.5 5618527 (**−237159**): wasm 4052901 → 3868209, `.data` 1700266 → 1646115 (индекс больше не в
Resources). Весь контент: main 12.3 MB (32 бандла) → rc.5 15.9 MB (архив LZ4 + индекс br).

**Вывод по критерию.** Время и байты первого захода близки к main: без троттлинга меню +0.3 с, забег +1.0 с, байты +6%; под 150 мс /
10 Мбит меню +1.0 с (+11%), забег +1.2 с (+6%), повторное открытие на уровне main. Запросов первого захода 262 (≈100–150 на шаг, а не
6801 у rc3) — больше «десятков», но под HTTP/2 они не стоят времени; уровень после первого — 12–20 запросов. Хуже main: HighFields
(+2.5 с под троттлингом, 3.6 MB против 1.05 MB — main получил сцену в общем бандле сцен ещё в первом заходе) и весь контент на 30% больше
(LZ4 против сжатия бандлов). Критерий по времени и байтам выполнен, по числу запросов — частично (сотни на первый заход).

### Замеры [16d]

«До» — тот же main `db4968ca2` (`/var/tmp/content_directories-step15/main-website`), «после» — rc.6: фреймворк `75c7adad` (`v0.7.0-rc.6`),
pixel-swarm `c79a64d48` (`/var/tmp/content_directories-builds-rc6/website`, архив в записанном порядке загрузок). Стенд и шаги те же, что в
[16c] (headless Chromium, mali-g52, `--gpu`, CPU ×1, HTTP/2 + Range/206; старт → меню → забег WinterForest; HighFields; повторное открытие),
но серия: 4 прогона каждой из 4 конфигураций, сборки чередуются вплотную (main, rc6). В ячейках — медиана (мин–макс) по 4 прогонам;
«разница в паре» — rc.6 минус main внутри одного круга, медиана (мин…макс). Запросы и байты — к `StreamingAssets/`.

**Нагрузка машины.** Тихой машины не было: круг 1 — load 2.4–3.8 (3 чужих редактора pixel-swarm простаивают), круги 2–4 — load 5–22
(чужой `deploy-client` с тестами и сборкой WebGL, 5–9 процессов редактора Unity). Числа запросов и байты от нагрузки не зависят
(совпадают во всех прогонах) — надёжны; времена шумные, сравнивать по разнице в паре, а не по абсолютным значениям.

| круг (пара main / rc6) | без троттлинга: время, load 1 мин | 150 мс / 10 Мбит: время, load 1 мин |
|-|-|-|
| 1 | 05:28, 2.4 / 3.0 | 05:30, 3.8 / 2.9 |
| 2 | 06:42, 10.0 / 14.3 | 06:48, 22.4 / 12.1 |
| 3 | 06:55, 11.8 / 9.9 | main 07:21, 7.9 (перепрогон: в 07:00 стенд упал на снимке экрана) / rc6 07:07, 8.7 |
| 4 | 07:10, 5.4 / 9.4 | 07:16, 9.3 / 9.2 |

Без троттлинга:

| шаг | main: запросов / байт / время | rc.6: запросов / байт / время | разница в паре |
|-|-|-|-|
| старт → меню | 13 / 1.70 MB / меню 2.76 с (2.51–3.09) | **10** / 2.57 MB (вкл. индекс 0.32) / меню 3.14 с (2.83–3.83) | +0.39 с (+0.31…+0.74) |
| меню → забег WinterForest | 10 / 8.74 MB / забег 5.30 с (4.89–5.92) | **10** / 7.94 MB / забег 6.27 с (6.00–10.56) | +1.05 с (+0.94…+4.65) |
| весь первый заход | 24 / 10.60 MB, последний 8.56 с | 27 (21–27: хвост после старта забега 1–7) / 10.80 MB, последний 9.54 с, 0 failed | байты +2% |
| HighFields | 3–4 / 0.93 MB (0.89–1.12) / забег 4.42 с (4.24–4.86) | **2** (+0–2 хвост) / 3.67 (+0–0.34) MB / забег 4.68 с (4.64–4.77) | +0.24 с (−0.09…+0.45) |
| повторное открытие | 2–3 / 0–0.30 MB (каталог Addressables) / забег 4.13 с (4.10–4.16) | 0 (из Cache API) / забег 4.92 с (4.81–5.29) | +0.79 с (+0.69…+1.14) |
| консоль | офлайн мета | то же, что у main: 1 error на навигацию (`JsWebSocket OnError`, мета отрезана стендом), предупреждения офлайн-меты; нет `Archive file was modified`, `undefined types`, `instead of 206`, `Content-Encoding` |

С троттлингом 150 мс / 10 Мбит (CDP `emulateNetworkConditions`; серверный шейпинг в этой серии не гонялся):

| шаг | main | rc.6 | разница в паре |
|-|-|-|-|
| меню | 9.54 с (9.22–9.91) | 10.04 с (9.93–10.10) | +0.48 с (+0.11…+0.81) |
| забег WinterForest | 19.27 с (18.75–19.96) | 19.67 с (19.60–20.49) | +0.69 с (−0.08…+0.86) |
| HighFields, забег | 9.87 с (9.77–12.27) | 12.76 с (12.37–13.18) | **+2.64 с** (+0.64…+3.35) |
| повторное открытие, забег | 9.66 с (9.51–9.85) | 10.15 с (9.71–10.75) | +0.46 с (−0.03…+1.18) |
| повторное открытие, меню | 7.33 с (7.29–7.42) | 6.91 с (6.82–7.03) | −0.41 с (−0.59…−0.30) |

Запросы и байты под троттлингом те же (меню 13 / 1.62 MB против 10 / 2.57 MB; забег 10 / 8.74 против 10 / 7.94; HighFields 2 / 3.67 MB).
У main 1–2 запроса на навигацию оборваны самим Addressables (`catalog.bin` / `settings.json`, `ERR_ABORTED`, статус 200) — не ошибка.

**Вывод.** Число запросов — цель шага — достигнуто: первый заход 262 (rc.5) → 27 при тех же байтах, меню 105 → 10, забег 152 → 10,
HighFields 12 (+8) → 2 (+0–2); по запросам rc.6 на уровне main (24). Время rc.6 против rc.5 в пределах шума этой серии (rc.5 в [16c] —
одна серия). Хуже main, с цифрами: меню первого захода +0.4–0.5 с (на 0.87 MB больше до меню: индекс 0.32 MB и общие файлы групп);
забег первого захода +0.7–1.0 с; HighFields под 150 мс / 10 Мбит +2.6 с (3.67 MB против 0.82 — main скачал сцены уровней в первом заходе);
повторное открытие — забег +0.5–0.8 с при 0 запросов (причина не разбиралась; предположительно чтение архива из Cache API против HTTP-кэша бандлов — так же было у rc.5 в [16c]), зато меню под троттлингом −0.4 с.
Сырые данные: pixel-swarm worktree, `docs/media/research/content_directories/step16d/stand/g2_{m,r}_{n,t}{1..4}`, `series_g2.out`,
`stat_g2.py`; не вошли в таблицу `loaded/` (прогон на старте чужой сборки) и `failed/` (падение стенда).

### Отличия от плана
- [7] Группы берут загрузчик из статического `CatalogContent` (привязка в `AddContent` на lifetime корня), не из контейнера: каталоги статические. `Scenes.X` — свойство из корня, до `AddContent` бросает.
- [6] `LoadContent` без своего сегмента профайлера (его открывает загрузчик); lifetime скоупа создаётся до загрузки его сцены.
- [5] Счётчик lifetime'ов по экземпляру ссылки, а не по id; в API добавлены `LoadNow`, `Progress(ContentScene)`, `ContentLoader.Register`.
- [4] Варианты хранилищ: `LocalContentStore`, `FetchedContentStore` над `IContentFileSource` (веб — `WebContentFetch`), редактор — `EditorContentDirectory` уровнем выше; `IContentStore.Prepare`.
- [3] Проверка сборкой Linux standalone, а не WebGL: `WebGlStripping.Require()` отвергает `client/project`.
- [3] → [5] `ContentReference<T>` хранит GUID и local id рядом с `Loadable` (у движка нет публичного API для id).
- [12] Stress-группа в контенте каждой сборки (грузит её только stress): во фреймворке нет исключения группы из контента одной сборки.
- [12] UI kit (`Bar`, `Button`, `Key`, `Sign`) перенесён в `Common/Global/Assets/Kit`, тайлы `CheatPanel` — экземпляры префаба: иначе Regenerate All не проходил (унаследовано с main).
- [16b] Типы каталога — не link.xml, а `AddPreviousBuildReportDirectory` (каталог отчёта content-сборки с `ScriptsOnlyCache.yaml`), как Addressables c content directories; очередь — в jslib (только сетевые fetch'и), лимит 64, а не 16–32.
- [13] → [16a] Часть обратной связи во фреймворк (сжатие контента в вебе, `verify.sh`) выполняется до замеров [15]: без сжатия игрок качает 300 MB raw.
- [16c] Каталог плеера — один архив UnityFS (LZ4, без type tree) в порядке первого использования, грузится Range-запросами в файл нулей; индекс — отдельный файл каталога (решение пользователя), в плеере только хэш. Пофайловый `.br` [16a] удалён.
- [16d] Порядок архива — записанный порядок загрузок игры (Play Mode), а не порядок `ContentRoot`: статический порядок оказался хуже; `RangeGap` оставлен 16 KiB.
- [16d] Порядок архива — по записанному профилю загрузок игры (`ProjectSettings/ContentLoadOrder/<name>.txt`), а не по порядку первого использования из [16c]; замер — «Замеры [16d]» (серия из 4 пар под нагрузкой машины, не одна серия на тихой).
<!-- Recorded immediately when approach changes -->

### Нерешенные вопросы
- [10] Имя каталога `Game` в `Framework.json` и 4 сценах (`ContentOptions` в `GameStartup`/`MockBase`) — свести в одно место во фреймворке ([16]).
- [4] Не проверены: предзагруженный каталог WebGL через `ContentStores`, `AbortController` в реальном браузере, `KeepOnly` на устаревшем Cache API.
- [12] 4 падения EditMode унаследованы с main (3 подтверждены на `db4968ca2`; `BackdropCrownTests` устарел после `a6a4fc642`) — не чинятся в этой задаче.
- [12] Кандидаты [16]: исключение группы из контента сборки; генератор bindings пишет дубликат класса вместо ошибки; точка входа «только ContentBuild» с логом; ~1 файл на объект (9218 файлов) — число запросов в [15].
- [14] Кандидаты [16]: корень скоупа не получает `ContentOptions` (лямбда компилируется и падает в рантайме) — `LoadRoot(ContentOptions, construct)` или диагностика генератора; валидатор bindings молча пропускает `{fileID: 0}`.
- [16d] Контент на сервере +30% к main (пофайловое LZ4HC); LZMA по файлам проверен в [16e]: WebGL-плеер его не распаковывает. HighFields 3.67 MB против 1.05 (main качает сцены уровней заранее).
- [16e] LZMA по файлам — no-go: WebGL-плеер Unity 7000.0.0a10 не распаковывает LZMA (`Decompressing this format (1) is not supported on this platform`, и для архива LZMA самого движка); редактор такой архив монтирует и читает частично. Потенциал на файлах rc.6: архив 15.56 → 11.96 MB, HighFields 3.67 → 2.11 MB, меню 2.72 → 2.27, забег 7.52 → 6.41. +30% к main и HighFields остаются; путь — своя распаковка вне движка (другая схема хранилища), не делалась. Evidence `probe/16e/`.
- [16d] Повторное открытие rc.6: 0 запросов, но забег на 0.5–0.8 с медленнее main (то же у rc.5) — причина не разобрана (кандидат: чтение ~7700 записей Cache API).
<!-- Populated as open questions arise -->
