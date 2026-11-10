---
task: content_directories
updated: 2026-11-09
---

## Snapshot

| Шаг | Статус | Evidence | Блокер |
|-----|--------|----------|--------|
| [1] Ветки и разведка | [x] done | worktree `.claude/worktrees/content_directories`; ответы (a)–(g) ниже; `3ac4cbb2` в content-loading-size | — |
| [2] Манифест и индекс | [x] done | `5e44da59`; `ContentIndexWriterTests` 7/7 | — |
| [3] Сборка каталога | [x] done | `ed5083c8`; тесты 23/23; Linux-сборка `Smoke: 7 files, 581462 raw, 77786 br` | WebGL через `WebGlBuild.Build` не проверен (стриппинг client/project) |
| [4] Хранилища и регистрация | [x] done | `27526f8c`; EditMode 20/20, PlayMode 3/3; веб-смоук 6 запросов → 0 при втором открытии | предзагрузка WebGL, AbortController в браузере не проверены |
| [5] `IContentLoader`, ссылки | [x] done | `93bd87b3`; PlayMode 12/12, EditMode 28/28 | смоук-плеер не пересобран |
| [6] DI и сцены скоупов | [x] done | `c06e4658`, `153f69f5`; Verify 171; PlayMode 19/19; EditMode 1205/1209 (4 skip) | `FindOrLoadSceneWithServices(ContentScene)` без рантайм-теста |
| [7] Каталоги | [x] done | `1186b23b`, `529c8d32`, `fe6af624`, `c06f3466`; RegenerateAll exit 0; PlayMode 115/139 (24 skip), EditMode 1203/1210 | — |
| [8] Удаление Addressables | [x] done | `b3334ef6`, `b07ebe08`, `a60cbaea`; grep пуст; EditMode 1197/1204 (7 skip), PlayMode 115/139 | `verify.sh` не прогнан полным deploy-client |
| [9] Тесты, доки, `v0.7.0-rc.1` | [x] done | `6837150a`, `f8e4d861`; тег `v0.7.0-rc.1` (локально); test-all exit 0 | push — после подтверждения пользователя |
| [10] pixel-swarm: ветка | [x] done | worktree `/projects/pixel-swarm/.claude/worktrees/content_directories`; `c5307e352`, `56288ff2a` (WIP); указатель `f8e4d861` | GamePlay 18 ошибок, Stress.Editor 3 — шаги [11]/[12] |
| [11] pixel-swarm: ссылки | [x] done | `142f7ddf7`, `e74617eb1`, `44302e96e`; 24/24 ссылки (diff пуст); EditMode 1151/1201, 3 упали (вне [11]?) | `GamePlay.Tools*`, `Stress.Editor` — [12] |
| [12] pixel-swarm: инструменты | [x] done | `787d01d10`, `0451f851a`, `a281f1ce2`, `f3e4d2a86`, `693af4deb`; RegenerateAll без ошибок; ContentBuild 9218 файлов / 29.1 MB br; тесты 1368/1422, 4 падения с main | WebGL-плеер не собран — [13] |
| [13] pixel-swarm: шаблоны | [x] done | `3f1dc7b69`, `40dff58fa`; обе цели собраны от `3f1dc7b69`, verify проходит без `pipefail` | verify.sh:87 SIGPIPE — [16]; nginx отдаёт `.cf` без сжатия — [15]/[16] |
| [14] pixel-swarm: редактор | [x] done | `94ae4a819` (корень скоупа — локальная функция), `098af36ba` (тайлы CheatPanel); Startup, 3 мока, ~25 повторных входов без ошибок; `FindOrLoadSceneWithServices(ContentScene)` проверен | полный прогон тестов не делался |
| [16a] Фреймворк: сжатие, verify | [x] done | `377a30bd`, `d85df041`, тег `v0.7.0-rc.2` (локально); pixel-swarm `724bf8146`; test-all exit 0 (EditMode 1209/1213); deploy-client exit 0, checks PASS; стенд: старт+меню 713 запросов, 2.54 MB по сети; хэш манифеста детерминирован | повторное открытие из Cache API, Safari — [15] |
| [15] pixel-swarm: замеры | [x] done | размер и стенд в `_result.md` «Замеры [15]»; evidence `docs/media/research/content_directories/step15/` worktree pixel-swarm | забег на rc2 не стартует (2 бага фреймворка → [16]); устройства заблокированы |
| [16b] Очередь, типы каталога, раскладка | [x] done | `df361352`, `b83e768f`, `010b524d`, тег `v0.7.0-rc.3` (локально); pixel-swarm `718e9b27f`; test-all exit 0 (EditMode 1212/1216); стенд rc3: забег стартует, 0 ошибок контента, пик 64 fetch | троттлинг-серия, `UseArchive` на pixel-swarm не сделаны |
| [16c] UseArchive и индекс | [~] фреймворк сквозь done: `041103a9`, тег `v0.7.0-rc.4` = `402395e9`, test-all exit 0 | раздел [16c] (g1–g3); handoff `handoff/16c_g3.md` | WebGL-смоук нового jslib, pixel-swarm rc4 + nginx, сборки rc4, замеры |
| [16] Обратная связь | [ ] | — | — |
| [17] Итог | [ ] | — | — |

## Заметки

### [2026-11-09] Исходные данные
- Прототип и замеры: ветка фреймворка `content-loading-size` (worktree `.claude/worktrees/content_loading_size`),
  `client/measurements`, коммиты `fdf4d977`, `94458ce6`, `a8f3b692`.
- API a10 проверен декомпиляцией: `Loadable<T>` — `Load()` (через `WaitForCompletion()`), `LoadAsync()`, `Release()`,
  `Status`, `Target`. `SceneManager` с `LoadableSceneId`: только `LoadSceneAsync` и `GetSceneByLoadableSceneId`.
  `ContentLoadManager`: `RegisterContentDirectory`, `UnregisterContentDirectory`, `GetRootAssets<T>(handle)`,
  `GetContentDirectories`.
- Addressables во фреймворке: 15 файлов в `client/core` (Runtime 9, Editor 5, Tests 1), `WebGlBuild.cs`, `verify.sh`, форк.
  В pixel-swarm: 9 файлов кода, 40+ групп в `AddressableAssetsData`, 3 шаблона WebGL.

### [1] Разведка
Worktree: `/projects/framework/.claude/worktrees/content_directories`, ветка `content-directories` от `main` (46a6f793),
`client/project/Library` и sln/csproj скопированы `cp -a --reflink=auto`; `.claude/worktrees/` в `.git/info/exclude`.

Пробы: редакторная — копия проекта `7000.0.0a10-content-directory-single` в scratchpad, `-executeMethod Probe.Run`
(сборка каталога `Probe` с двумя корнями, затем Play Mode в batchmode); исходники и выжимка лога —
`docs/tasks/current/content_directories/probe/` (`Probe.cs`, `probe.log`). Веб — новый вариант измерений
`content-directory-sync` (ветка `content-loading-size`).

- **(a) Drawer'ы есть у движка.** Декомпиляция a10: `UnityEditor.ContentLoadModule` — `[CustomPropertyDrawer(typeof(Loadable<>))]
  LoadableDrawer` (поле объекта типа `T` через `LoadableObjectIdEditorUtility.DrawLoadableObjectIdField`, тип из
  `fieldInfo`, понимает массивы, `List<>` и `[SerializeReference]`) и `LoadableObjectIdDrawer`; `UnityEditor.CoreModule` —
  `[CustomPropertyDrawer(typeof(LoadableSceneId))] LoadableSceneIdDrawer` (поле `SceneAsset`). Рефлексия в редакторе
  (`probe.log`): `drawer _wrapped._loadable: drawer UnityEditor.LoadableDrawer, field Loadable`1[ProbeTarget], object type
  ProbeTarget` — внутри обобщённой обёртки `ProbeRef<T>` тип цели выводится верно; у самой обёртки drawer'а нет (`drawer
  _wrapped: none`). Есть и обратные функции: `LoadableObjectIdToObject`, `LoadableSceneIdToScene`. **API:** свой drawer
  обёртки только проксирует дочернее поле (info поправлен).
- **(b) В Play Mode каталог не нужен.** Без зарегистрированного каталога (`b play: directories 0`):
  `Load: status Loaded, result Target, is the AssetDatabase asset True`; `LoadAsync: status Loaded, result Other`;
  `scene loaded True, path 'Assets/Probe/Menu.unity'`; после `Release` ассет жив (`the asset alive True`). Каталог из
  `Library` в Play Mode регистрируется и грузит, но копии (`is the AssetDatabase asset False`). В edit mode регистрация
  запрещена: `InvalidOperationException: ContentLoadManager.RegisterContentDirectory is not supported in edit mode`.
  При входе в Play Mode движок сам снимает все каталоги (`ContentLoadManagerResetHandler.OnContentResetRequested`).
  **API:** `EditorContentStore` = ничего не регистрировать, корень из `AssetDatabase` (info поправлен).
- **(c) Счёт ссылок есть, но по экземплярам `Loadable`.** Два поля `_first` / `_second` на одну цель и `Loadable` внутри
  обёртки дают один объект (`same True, wrapped same True`); после `Release` первого второй жив
  (`second alive True, its texture True, second status Loaded, first status None`); после `Release` всех объект
  уничтожен (`object alive False`); новая загрузка — новый экземпляр (`same instance False`). Один и тот же `Loadable`
  счёта не ведёт (декомпиляция: `LoadAsyncInternal` создаёт операцию один раз, `Release` её освобождает и обнуляет).
  **API:** загрузчик считает lifetime'ы сам по экземпляру `Loadable` (info поправлен). `Release` асинхронный
  (`ReleaseObjectAsync`): объект умирает не сразу, проверка шла через 300 мс.
- **(d) Регистрация открывает зависимости корней.** Настоящие только собственные `.cf` двух корней, остальное нули:
  `Error loading the file 'cah:/70bb…'` (текстура `Tex3` прямой ссылки корня B), `'cah:/0c68…'` (общий файл kind 4,
  guid `00000000000000000000000010000000`, зависимость каждого сериализованного файла), `'cah:/3484…'` (`Direct.asset`);
  `d: roots null, null`. С замыканием зависимостей корней (`d2: real 7bc1…, 3484…, 70bb…, 0c68…, 946f…`) — `roots ProbeRoot,
  ProbeRootB; direct loaded, its texture loaded`. Файлы `Loadable`-целей (Target, Other, сцены) при регистрации не
  открываются — их нули не мешают. Корень, держащий только `Loadable` / `LoadableSceneId`, тянет только себя и файл kind 4.
- **(e) Синхронный `Load()` в вебе работает на обоих.** Вариант `content-directory-sync` (коммит `3ac4cbb2` в
  `content-loading-size`, `results/7000.0.0a10/content-directory-sync.json`), headless Chromium: `[measurement] ready
  synchronous Load of a Loadable: Circle and Tone in 113 ms (sync Load: preloaded 42 in 12 ms, fetched Content in 3 ms)`.
  Предзагруженный каталог (preload manifest `sync.manifest`) читается из `.data` без единого запроса (в `requests` смоука
  только `Content/Game/*`); скачанный — `Load()` после записи его файлов поверх нулей. **API:** предзагруженные группы
  (`IsPreloaded`) — синхронный `Load()`, `Resources` не нужен; для скачиваемого синхронный вызов по-прежнему бросает (на
  нулях `Load()` не проверял: правило API и так запрещает).
- **(f) `UnregisterContentDirectory` уничтожает загруженное.** `after Unregister: loaded object alive False, texture alive
  False, root alive False`; повторная регистрация той же папки — новые корень и объекты (`root same False, object same
  False`). Запасной путь «перерегистрировать после докачки» не годится: работает только схема с нулями; каталог
  снимается, только когда из него ничего не загружено (info поправлен).
- **(g) Встроенные ресурсы pixel-swarm.** YAML: `m_SpotCookie {10001, unity default resources}` стоит в `RenderSettings`
  каждой сцены (16 ссылок = 16 сцен, а не 16 источников света: компонентов `Light` в сценах и префабах нет);
  `m_LightingDataAsset {20201, unity_builtin_extra}` — в `Menu.unity`, `Lobby.unity` (и 6 сценах `Art/Research`);
  скайбокса нет (`m_SkyboxMaterial {fileID: 0}`), `m_DefaultReflectionMode: 0`, `m_CustomReflection` пуст; материалов
  `Sprites-Default` (10754) нет; прочие ссылки на встроенное — `m_Script` нативных классов (13312, тайлы) и профилей
  сборки. Сборка каталога из сцены с заголовком `Menu.unity` (документы 29/104/157/196 скопированы как есть,
  `probe/Menu.unity` строится в `Probe.cs`): зависимости сцены `[0,2]` — `unity default resources` (kind 5, без хэша:
  остаётся в плеере, не копируется) и `Resources/unity_builtin_extra` с хэшем `8556…`, файл 527,292 байт копируется в
  каталог. Та же сцена с `m_LightingDataAsset: {fileID: 0}` — зависимости `[0]`, копии нет. Вывод: виноват
  `m_LightingDataAsset` в Menu/Lobby (освещение не запекается в Custom SRP 2D); `m_SpotCookie` бесплатен. Для [12]:
  очистить `m_LightingDataAsset` у Menu/Lobby, предупреждение сборки [3] ловит `unity_builtin_extra` в каталоге.
  Что добавят префабы и материалы сцен, покажет лог сборки [3] на настоящем каталоге (копию всех сцен pixel-swarm в
  пробу не тащил: зависимостей сотни, а YAML их на встроенное не ссылается).
- **Изменения «Желаемого API» в info по итогам [1]:** drawer'ы обёрток — прокси к встроенным (a); `EditorContentStore`
  без регистрации, из `AssetDatabase` (b); счётчик lifetime'ов в загрузчике по экземпляру `Loadable` (c); «файлы корней»
  = замыкание зависимостей, каталог не снимается, пока из него что-то загружено (d, f); предзагруженные группы —
  синхронный `Load()` вместо `Resources` (e); `m_SpotCookie` в контексте исправлен (g).

### [2] Манифест и индекс
Коммит `5e44da59` в `content-directories` (worktree `.claude/worktrees/content_directories`).

- **Редактор** (`client/core/Editor/Content/`, `Internal.Editor`): `ContentManifest` (единственный читатель формата:
  `BuildManifestHash.txt` → `<hash>.json`, версия 3, иначе `InvalidDataException`; проверяет индексы файлов, GUID'ов,
  зависимостей, kind; хэш пуст ровно у `DefaultResources`), `ContentManifestFile` / `ContentManifestScene` (readonly
  struct), `ContentFileKind` (= `ContentFileSourceType` движка: 1 cluster, 2 imported object, 3 source object,
  4 MonoScript, 5 default resources), `ContentFileClosure` (свой файл первым, затем зависимости в глубину, ресурсы
  `cah:/<hash>` сразу за своим `.cf`; ресурс — ровно один файл `<hash>.*`, иначе исключение; `DefaultResources` не
  копируется, но его зависимости обходятся), `ContentIndexWriter.Write(manifest)`.
- **Рантайм** (`client/core/Runtime/Content/`, `Internal`): `ContentIndex` (имя, хэш манифеста, `Files`, `Manifest`,
  `RootFiles`, `Objects`, `Scenes`, `ObjectFiles(guid, localId)`, `SceneFiles(guid)`, `ToJson` / `FromJson` с проверкой),
  `ContentFile` (имя + размер), `ContentObjectFiles`, `ContentSceneFiles`. Неизменяемые: `[SerializeField] private` поля и
  getter'ы (readonly struct JsonUtility не пишет).
- **Решение: json (`JsonUtility`), а не ScriptableObject с `LoadableObjectId`.** У `LoadableObjectId` поля internal (в рантайме
  его не разобрать), а `LoadableObjectId` в ассете плеера сборка переписывает (LocalID 0, замеры). Поэтому ключ объекта —
  GUID ассета (строка) + local id, ключ сцены — GUID (`LoadableSceneId.ToString()` = GUID). Шаг [3] запекает json как
  `TextAsset` в `Resources` (синхронно на старте, без запроса); тот же класс читается и в тестах без ассетов.
- **Объекты в индексе** — каждый сериализованный файл манифеста с хэшем, кроме MonoScript. Cluster (kind 1) без `Lfid` →
  local id 0 = весь ассет; импортированные объекты (kind 2) — свой файл с `Lfid`. `ObjectFiles` ищет точный (guid,
  localId), затем (guid, 0), иначе `KeyNotFoundException`.
- **Открытый вопрос для [5]:** как загрузчик получит (GUID, local id) из `Loadable<T>` в рантайме. Варианты: разбор
  `LoadableObjectId.ToString()` (`{ guid: X, fileID: N, kind: K }` у собранного id — формат и то, что guid остаётся GUID'ом
  ассета, не проверены в плеере), либо `ContentReference<T>` хранит GUID / local id рядом с `Loadable`, записанные
  редактором. Нужна короткая проба в Play Mode с зарегистрированным каталогом.
- **Тест** `Internal.EditorTests.ContentIndexWriterTests` (`client/core/Tests/EditorTools`, сборка `Internal.Editor.Tests`;
  фикстуры `ContentTestRoot` / `ContentTestTarget` в `Tests/EditorTools/Fixtures/Content`): в EditMode собирает настоящий
  каталог `BuildPipeline.BuildContentDirectory` в `Temp/ContentIndexTests` из ассетов временной папки
  `Assets/ContentIndexTemp` (WAV генерируется, сцена — пустой YAML; папка удаляется после класса, как у прочих тестов
  редактора). Проверяет: все файлы папки с размерами и хэш; файлы корня = замыкание корня, без файлов цели `Loadable` и
  сцены, без ресурсов; файлы ассета начинаются с его `.cf` и содержат файлы клипа с одним ресурсом; файлы сцены без цели;
  round-trip json; неизвестная версия → `InvalidDataException`; чужой объект → `KeyNotFoundException`.
  Запуск: `Unity -batchmode -projectPath client/project -runTests -testPlatform EditMode -testFilter
  Internal.EditorTests.ContentIndexWriterTests` → `total="7" passed="7" failed="0"`, фикстура 11.0 s, компиляция
  `client/project` без ошибок.
- Побочно редактор меняет `ProjectSettings/MultiplayerManager.asset` и создаёт `ProjectSettings/Packages/com.unity.dedicated-server/`
  в worktree — не закоммичено (не наше).

### [3] Сборка каталога
Коммит `ed5083c8` в `content-directories`.

- **Классы** (`client/core/Editor/Content/Build/`, `Internal.Editor`): `ContentBuild` (`IPlayerBuildStep`; `Prepare(target)` /
  `Cleanup()`, `Current`), `ContentBuildPlayerProcessor` (`BuildPlayerProcessor` + `IPostprocessBuildWithReport`),
  `ContentDirectoryBuilder` (шаги `IContentBuildStep` → `BuildContentDirectory` в `Library/Content/<name>`, `FailBuildWhenErrorsLogged`),
  `ContentReferenceCheck`, `ContentBuildLog`, `ContentBuildFiles`, `ContentBuildOutput`, `ContentDirectorySettings`, `IContentBuildStep`;
  рантайм `ContentPaths` (`Content/<name>` в StreamingAssets и в Resources).
- **Настройки:** секция `"Content": { "Name", "Roots": [...], "IsPreloaded" }` в `Framework.json` → `FrameworkSettings.ContentDirectory`.
  Решение fail fast: секция обязательна только для сборки плеера (свойство бросает `InvalidOperationException` с примером секции),
  остальные инструменты без неё работают. Один каталог на проект (несколько — когда понадобится в [7]). Шаг в `releases/unreleased.md`.
- **Точка для [7]:** `IContentBuildStep.BeforeContentBuild(ContentDirectorySettings)` — все реализации через `TypeCache`, по имени типа,
  до `BuildContentDirectory`; генерация `ContentRoot` подключится сюда.
- **Как плеер получает файлы (отклонение от плана):** не временная копия в `Assets/StreamingAssets`, а
  `BuildPlayerContext.AddAdditionalPathToStreamingAssets(Library/Content/<name>, "Content/<name>")` — так же, как в замерах; в `Assets`
  ничего не пишется, чистить нечего. Процессор вызывается и для `BuildPipeline.BuildPlayer` из `PlayerBuildSteps`, и для окна сборки /
  `-buildLinux64Player`. Двойной сборки нет: при `ContentBuild.Current != null` (подготовлено шагом) процессор только добавляет путь; иначе
  готовит сам и чистит в `OnPostprocessBuild` + `EditorApplication.delayCall` (после неудачной сборки колбэка нет); остаток от убитого
  редактора удаляет следующий `Prepare`. Повторный `Prepare` при `Current` бросает.
- **Индекс:** json `TextAsset` `Assets/FrameworkContentIndex/Resources/Content/<name>.json` (`Resources.Load<TextAsset>(ContentPaths.IndexResource(name))`):
  синхронно на старте без запроса на всех платформах, тот же `ContentIndex.FromJson`. Папка принадлежит только сборке и удаляется
  целиком (`AfterBuild` / процессор). Альтернатива «индекс в StreamingAssets» отвергнута: в вебе без предзагрузки — асинхронный fetch.
- **Предзагрузка:** `IsPreloaded` и цель WebGL → `Library/PlayerDataCache/WebGLPreloadedStreamingAssets/framework-content.manifest`
  (все файлы + `BuildManifestHash.txt`, пути `Content/<name>/...`, как `PreloadedContentDirectorySceneStep` замеров); удаляется после сборки.
  Для не-WebGL флаг ничего не делает.
- **Проверка ссылок:** ассеты каталога (по GUID манифеста, кроме MonoScript и сцен) — `SerializedObject` по `MonoBehaviour` /
  `ScriptableObject`, свойства типа `LoadableObjectId` (в т.ч. `m_LoadableObjectId` внутри `Loadable<T>`) и `LoadableSceneId` через
  `boxedValue`; сцены — по тексту, только документы `!u!114`: `{fileID: N, loadable: 1, guid: G, type: T}` и `m_SceneGUID: G`
  (`OcclusionCullingSettings` каждой сцены имеет пустой `m_SceneGUID` — первая сборка Linux на этом ложно упала, отсюда ограничение
  `!u!114`). Пусто → `empty`; GUID нет среди файлов / сцен манифеста → `... is not in the directory` (`(no asset)` для удалённого).
  Сам `BuildContentDirectory` на пустой и висячей ссылке **не падает** (тесты) — проверка нужна.
- **Brotli в логе:** редактор a10 работает на CoreCLR (.NET 10), `BrotliEncoder` / `BrotliStream` из netstandard не грузятся
  (`TypeLoadException ... System.IO.Compression, Version=10.0.0.0`); `BrotliStream` берётся рефлексией из `System.IO.Compression.Brotli`,
  уровень `SmallestSize` (качество 11).
- **Сборка WebGL `client/project` через `Internal.WebGlBuild.Build` невозможна без изменения проекта:** падает до шагов на
  `WebGlStripping.Require()` — `DefaultSubmoduleStrippingSettings list no submodules to strip` (свойство `client/project`, не этого шага).
  Поэтому проверка — Linux standalone `-buildLinux64Player` (путь процессора, без `PlayerBuildSteps`), заодно запускаемый плеер для п.7.
  Путь `PlayerBuildSteps` покрыт тестами `ContentBuild.Prepare/Cleanup`; сборку WebGL проверит [13] в pixel-swarm (или `client/project`
  после настройки стрипа).
- **Лог сборки Linux** (`scratchpad/linux.log`): `[ContentBuild] The content directory Smoke built into Library/Content/Smoke in 1.0 s`;
  `Smoke: 7 files, 581462 bytes raw, 77786 bytes brotli; root files 2 (3576 bytes), 4 objects, 1 scenes`; предупреждение
  `built-in resources in the directory: unity_builtin_extra (8187….cf, 39964 bytes)` (пустая сцена `Level.unity`: `m_LightingDataAsset
  {20201, unity_builtin_extra}`, как в (g)); `Build Finished, Result: Success.` В выходе `smoke_Data/StreamingAssets/Content/Smoke/` —
  7 файлов + `BuildManifestHash.txt`. После сборки `Assets/FrameworkContentIndex` нет, `git status` чистый, кроме
  `MultiplayerManager.asset` / `com.unity.dedicated-server/` и побочки `packages/com.unity.ugui/.../DefaultFont.asset` (атлас шрифта,
  откатил).
- **Смоук `client/project/Assets/ContentSmoke`:** `ContentSmokeRoot` (`Loadable<ContentSmokeTarget>` + `LoadableSceneId` на `Level.unity`),
  `Target.asset`, сцена плеера `Smoke.unity` с `ContentSmokeProbe` (тот же `Loadable` в сцене; читает индекс из Resources, регистрирует
  каталог, грузит цель, `Application.Quit`), в Build Settings только `Smoke.unity`; `Framework.json`: `Content: Smoke`, `IsPreloaded: true`.
- **Ответ на открытый вопрос [2] (п.7)**, запуск плеера `smoke -batchmode -nographics` (`scratchpad/player.log`):
  - `[ContentSmoke] root field: { guid: 6d2f1eeede49524c69ae481d6b2a8ae2, fileID: 11400000, kind: ObjectCluster }` — у `Loadable` из
    каталога в плеере `LoadableObjectId.ToString()` = GUID ассета (совпадает с `Target.asset.meta`), local id файла и вид. Индекс держит
    кластер как `(guid, 0)`, `ObjectFiles` и так берёт `(guid, 0)` после точного ключа. `LoadableSceneId.ToString()` = GUID сцены
    (`b25339b3…`, есть в `index.Scenes`). `[ContentSmoke] loaded 'smoke target', status Loaded` — синхронный `Load()` после регистрации.
  - `[ContentSmoke] scene field: { Invalid }` — `Loadable` в сцене плеера (вне каталога) сборка плеера обнуляет: в сценах плеера
    (`Startup`) ленивые ссылки не работают вообще, только через корень каталога.
  - Публичного API для GUID / local id в рантайме нет: поля `LoadableObjectId` internal, `ToString()` — формат движка без обещаний
    (`{ guid: …, fileID: …, kind: … }`, для невалидного `{ Invalid }`, у объектов вне кластера `type:` вместо `kind:`).
  - **Рекомендация для [5]:** `ContentReference<T>` хранит GUID ассета и local id рядом с `Loadable<T>` (пишет редактор/drawer при
    назначении, проверяет `ContentReferenceCheck` или тест на совпадение с `TryDeconstructLoadableObjectId`); разбор `ToString()` — только
    как проверка в тесте/смоуке, не как основной путь. `LoadableSceneId.ToString()` = GUID можно использовать (тривиальный формат), но для
    единообразия тоже хранить GUID.
- **Тесты:** `Internal.EditorTests.ContentBuildTests` (9: каталог и индекс в Resources с корнем, шаг `IContentBuildStep` вызван,
  preload manifest WebGL, без предзагрузки — нет manifest, `Cleanup`, повторный `Prepare`, пустая ссылка в ассете, ссылка на удалённый
  ассет, пустая ссылка в сцене, нет секции `Content`) + `ContentIndexWriterTests`, `FrameworkSettingsTests`, `EditorToolsTests`:
  `total="23" passed="23" failed="0"` (batchmode `-runTests -testPlatform EditMode`). Полный `test-all.sh` не запускал.

### [4] Хранилища и регистрация
Коммит `27526f8c` в `content-directories`.

- **Классы** (`client/core/Runtime/Content/Stores/`, все `internal`, `InternalsVisibleTo` в `Runtime/AssemblyInfo.cs` для `Internal.Tests`,
  `Internal.PlayMode.Tests`, `ContentSmoke`):
  - `IContentStore`: `LocalPath`, **`Prepare(lifetime, index)`** (отклонение от сигнатуры info: добавлен шаг «сделать папку регистрируемой» —
    иначе `ContentDirectory` писал бы нули поверх StreamingAssets у локального хранилища), `Fetch(lifetime, files, progress)`; progress — байты
    уже настоящих файлов вызова (нарастающий итог).
  - `LocalContentStore` — standalone и **предзагруженный WebGL** (отдельный `PreloadedContentStore` не нужен: путь `streamingAssetsPath`
    регистрируется как есть, (e) шага [1]); проверки папки нет — на WebGL это URL, System.IO его не видит, ошибку даёт регистрация.
  - `FetchedContentStore(IContentFileSource, localPath)` — схема с нулями: чистит папку, качает манифест + `RootFiles`, пишет
    `BuildManifestHash.txt` (`ContentPaths.ManifestHashFile`), остальные — нули через `FileStream.SetLength` (без буфера в managed heap).
  - `IContentFileSource` + `WebContentFetch` (вместо класса `WebContentStore`: веб = `FetchedContentStore` над `WebContentFetch`) +
    `Plugins/WebContentFetch.jslib`: `fetch` с `AbortController`, Cache API `caches.open('content')` (ключ URL) → `FS.writeFile`, проверка
    размера, `KeepOnly(index)` удаляет из кэша URL'ы не из индекса; без Cache API — одна строка `console.log`. Колбэк → UniTask опросом
    статуса (`UniTask.Yield(lifetime.Token)`), конец lifetime → `WebContentFetch_Abort`, прерванный fetch ничего не пишет и не логирует.
  - `ContentFileFetches` / `ContentFileFetch` — общий код дедупликации: `UniTaskCompletionSource` на файл, счёт ожидающих; у скачивания свой
    `Lifetime`, его конец (последний ожидающий ушёл) прерывает источник; вызывающему — `OperationCanceledException`
    (`AttachExternalCancellation`), файл остаётся нулём и снова качается следующей загрузкой. `Run` — граница (top of a task nobody awaits).
  - `ContentStores.Create(index, remoteUrl)` — выбор по `#if UNITY_WEBGL && !UNITY_EDITOR`: `IsPreloaded` → Local, иначе Fetched(Web,
    `remoteUrl ?? streamingAssetsPath` + `/Content/<name>`, локально `temporaryCachePath/Content/<name>`); иначе Local.
- **Каталог** (`client/core/Runtime/Content/`): `IContentDirectory` (`Name`, `RootAssets<T>`, `FetchObject(lifetime, guid, localId,
  progress)`, `FetchScene(lifetime, guid, progress)` — каталог сам держит индекс, загрузчик [5] файлов не знает), `ContentDirectory.Register
  (lifetime, index, store)` — `Prepare` → `RegisterContentDirectory`, `UnregisterContentDirectory` на конце lifetime (в игре — lifetime
  корневого скоупа; в тестах — тест). **`EditorContentStore` заменён на `EditorContentDirectory(name, roots)`**: в Play Mode нет ни индекса,
  ни регистрации, поэтому вариант — на уровне каталога, а не хранилища; корни (`Object`'ы из `FrameworkSettings.ContentDirectory.Roots` через
  `AssetDatabase`) передаст инсталлер [6] (редакторный код, `FrameworkSettings` в `Internal.Editor`).
- **`ContentIndex.IsPreloaded`** (новое поле, пишет `ContentBuild`: `IsPreloaded && WebGL`; `ContentIndexWriter.Write(manifest, isPreloaded)`).
- **Телеметрия** оставлена шагу [5]: `AssetLoads.Begin("Content: <name>")` знает имя ссылки, а хранилище — только файлы; загрузка в [5] покрывает
  и скачивание.
- **Тесты:**
  - EditMode `Internal.Tests.ContentFileFetchesTests` (`Tests/Editor/Content`, медленный `GatedContentFileSource`): `TwoFetchesOfOneFile_DownloadOnce`,
    `LifetimeEnds_CallerCanceled_DownloadAbortedFileZeros` (OCE, источник прерван, файл нули, `LogAssert.NoUnexpectedReceived`, потом качается
    заново), `OneOfTwoWaitersEnds_DownloadGoesOn`, `EndedLifetime_Throws_StartsNothing`. Вместе с `ContentIndexWriterTests` и `ContentBuildTests`:
    `total="20" passed="20" failed="0"`.
  - PlayMode `Internal.Tests.ContentDirectoryTests` (`Tests/Runtime/Content`; каталог собирает `[PrebuildSetup("Internal.EditorTests.ContentDirectoryTestBuild")]`
    в `Temp/ContentDirectoryTests`, индекс и GUID цели рядом — `ContentTestPaths` в фикстурах): `LocalStore_Registered_LoadsRootAndTarget`,
    `FetchedStore_Registered_RealRootsZerosElsewhere` (до регистрации скопированы ровно манифест + корни, остальное нули реального размера),
    `FetchedStore_TargetFetched_LoadsIt` (файлы цели совпадают побайтно, progress = сумма размеров, повторный fetch ничего не копирует, `Load()`):
    `total="3" passed="3" failed="0"`.
- **Веб-смоук (выбран путь `client/project`):** быстрее варианта измерений — смоук-сцена, корень и процессор сборки уже есть с [3]; стриппинг
  обойдён временным `executeMethod` с `BuildPipeline.BuildPlayer` WebGL (`Assets/Editor/ContentSmokeWebGl.cs`, удалён, не закоммичен).
  `ContentSmokeProbe` теперь идёт через `ContentStores` → `ContentDirectory` → `FetchObject` / `Load()` → `FetchScene` / `LoadSceneAsync`;
  `Framework.json` client/project: `IsPreloaded: false`. Лог сборки: `Smoke: 6 files, 575678 bytes raw, 72938 bytes brotli; root files 2`.
  Скрипт — копия `smoke.mjs` измерений с маркерами `[ContentSmoke] done/failed` (scratchpad `content-smoke.mjs`; сервер `no-store`, считает
  запросы на стороне сервера). Результат `--reload`: первое открытие `ready [ContentSmoke] done`, **6 запросов `/StreamingAssets/Content/Smoke/*`**
  (все 6 файлов: манифест, 2 корня, цель, сцена, `unity_builtin_extra`), `errors []`; второе открытие `ready [ContentSmoke] done`, **0 запросов
  к Content** (из Cache API). Строки: `registered FetchedContentStore at /tmp/Content/Smoke: root Root`, `loaded 'smoke target', status Loaded`,
  `scene loaded: Level`.
- **Не проверено:** предзагруженный WebGL через `ContentStores` (путь `LocalContentStore` = (e) шага [1]), отмена в реальном браузере
  (`AbortController` покрыт только кодом; общий код дедупликации — тестами), чистка устаревших URL (`KeepOnly`) на настоящем старом кэше.
- Побочка редактора (`MultiplayerManager.asset`, `com.unity.dedicated-server/`, `DefaultFont.asset`, `client/project/Data/Plugins` после WebGL)
  не закоммичена / удалена. `Library` client/project теперь на платформе WebGL.

### [5] `IContentLoader`, ссылки и drawer'ы
Коммит `93bd87b3` в `content-directories`.

- **Рантайм** (`client/core/Runtime/Content/`): `ContentReference` (база: `Id`, `Guid`, `LocalId`, `IsEmpty`), `ContentReference<T>`
  (`ContentReference.Typed.cs`: `Loadable<T>` + `IsLoaded`, `Target` бросает до загрузки, `EditorAsset`), `ContentScene` (`Id`, `Guid`,
  `EditorPath`), `ContentProgress` (`Loaded`/`Total`/`IsDone`), `IContentLoader`, `ContentLoader`, `ContentObjectLoad<T>` + `ContentLoadPhase`,
  `ContentSceneLoads`, `ContentLoadedScene` (реализует существующий `ILoadedScene` из `Services/Scenes/LoadedScene.cs`), `ContentProgressReport`.
  XML-доки нет (стиль: `//`), ограничение «только из ассетов каталога» записано в комментариях классов ссылок.
- **Отклонения от API:**
  - **Счёт lifetime'ов — по экземпляру ссылки (`Loadable`), а не по id** (как в info, «Правила API»; в брифе шага — «по id»). По id нельзя:
    `Target`/`IsLoaded` читают свой `Loadable`, у второй ссылки на ту же цель они были бы пусты; разные экземпляры движок считает сам и отдаёт
    один объект (шаг [1], c). Скачивание по id/файлам и так одно (`ContentFileFetches`). Прогресс — по ключу GUID:local id.
  - Добавлены `LoadNow(lifetime, reference)` в интерфейс (бросает, если файлы не настоящие или идёт асинхронная загрузка этой ссылки),
    `Progress(ContentScene)`, `ContentLoader.RootAssets<T>()` и `ContentLoader.Register(lifetime, name, remoteUrl)` (индекс из Resources →
    `ContentStores` → `ContentDirectory`) — публичный путь для смоука и, вероятно, для `AddContent` [6]. Конструктор
    `ContentLoader(IContentDirectory)` internal (для [6] в той же сборке и тестов).
  - `IContentDirectory`: `ObjectBytes`, `SceneBytes`, `IsObjectReal`; `IContentStore.IsReal(files)`.
  - Ключ `AssetLoads`: `Begin("Content: <guid>")`, `Finish("Content: <имя объекта>")`; сцены — `Content: scene <guid>` → `Content: scene <name>`.
    Span профайлера `GameProfiler.Concurrent("Content: <тип>")` / `"Content: scene"`.
  - Пачка `Load(list)`: все fetch'и стартуют в одном кадре (хранилище видит их вместе), загрузки параллельно — отдельного batch API нет.
- **Сцена:** `SceneManager.LoadSceneAsync(id, Additive)`, экземпляр — `GetSceneAt(sceneCount - 1)` сразу после вызова (движок добавляет сцену
  в список сразу; проверено тестом на имя/выгрузку). Конец lifetime во время загрузки — сцена догружается и выгружается, вызывающему OCE.
- **Редактор** (`client/core/Editor/Content/Drawers/`): `ContentReferenceDrawer` (`[CustomPropertyDrawer(typeof(ContentReference<>))]`),
  `ContentSceneDrawer` — `PropertyField` дочернего `_loadable` / `_id` (встроенные drawer'ы движка) под меткой обёртки, затем
  `ContentReferenceFields.SyncObject/SyncScene` (GUID/local id из id через `TryDeconstructLoadableObjectId` / `LoadableSceneIdToGuid`).
  `ContentReferenceFields.Create<T>(asset)`, `CreateScene(path)`, `AssignObject/AssignScene(SerializedProperty, ...)` — для генераторов [7].
  `SerializedProperty.boxedValue` пишет `LoadableObjectId`/`LoadableSceneId` (проверено тестом).
- **`ContentReferenceCheck`:** у `ContentReference` / `ContentScene` в ассетах каталога GUID/local id должны совпадать с id, иначе
  `... the reference keeps X:N, its id is G:L (assign it in the inspector)`. В тексте сцен — только id, ключи не сверяются (открыто).
- **Смоук:** `ContentSmokeRoot` — `ContentReference<ContentSmokeTarget>` + `ContentScene`, `Root.asset` переписан руками (`_localId: 11400000`),
  `ContentSmokeProbe` через `ContentLoader.Register` + `IContentLoader`; `InternalsVisibleTo("ContentSmoke")` удалён, client/project
  компилируется. Плеер не пересобирал.
- **Тесты:** фикстура `ContentTestReferences` (второй корень тестового каталога); `ContentDirectoryTestBuild` теперь держит исходные ассеты
  (`ContentTestPaths.Assets`) до конца прогона (`IPostBuildCleanup` + `[PostBuildCleanup]` на обоих классах — без атрибута очистка не вызывается).
  - PlayMode `Internal.Tests.ContentLoaderTests` (9): `Load_LifetimeEnds_Released` (+ `Target` до загрузки бросает), `LifetimeEndsDuringLoad_Canceled_Released`,
    `LifetimeEndsDuringFetch_Canceled_NothingLoaded`, `TwoLoads_FirstEnds_Alive_SecondEnds_Released`, `LoadList_LoadsEach`, `Scene_LoadsAndUnloadsByLifetime`,
    `FetchedStore_LoadFetches_Progress_LoadNowAfter`, `EditorKeys_MatchTheIndex` (GUID/local id ↔ `ObjectFiles`, `ToString()`), `EditorDirectory_LoadsTheProjectAssets`;
    освобождение проверяется числом `Resources.FindObjectsOfTypeAll<ContentTestTarget>()`, у всех `LogAssert.NoUnexpectedReceived`.
    С `ContentDirectoryTests`: `total="12" passed="12" failed="0"`.
  - EditMode `Internal.Tests.ContentReferenceTests` (4), `Internal.EditorTests.ContentReferenceFieldsTests` (3), `ContentBuildTests.ReferenceWithStaleKeys_FailsTheBuild`;
    со всеми `Content*` EditMode (`ContentBuildTests`, `ContentIndexWriterTests`, `ContentFileFetchesTests`): `total="28" passed="28" failed="0"`.
  - Library client/project на WebGL — PlayMode-тесты в редакторе прошли и так.
- Побочка редактора (`MultiplayerManager.asset`, `com.unity.dedicated-server/`) не закоммичена.

### [6] DI и сцены скоупов
Коммиты `c06e4658` ([Core] [Content]) и `153f69f5` ([Core] [ContainerGenerator]) в `content-directories`.

- **Рантайм** (`client/core/Runtime/Content/Scopes/`): `ContentScopeExtensions` — `AddContent(ContentOptions)` (async: в редакторе
  `EditorContentDirectory` через `EditorContentDirectories`, иначе `ContentLoader.Register(lifetime, name, remoteUrl)`; `RegisterInstance(loader)
  .As<IContentLoader>()`), `LoadContent<T>` (на `builder.Lifetime`, `RecordLoadedAsset(lifetime, "Content", name)`), `RequestContent<T>(ref, out
  ContentSlot<T>)` (в `AddBeforeBuild`, параллельно с группами/сценами); `ContentOptions` (`_name`, `_remoteUrl`, `Validate`), `ContentSlot<T>`
  (`Value` бросает до сборки), `ScopeContent` (загрузчик скоупа: у дочернего — `Parent.Container.Resolve<IContentLoader>()` при первом
  использовании, у корня — тот, что поставил `AddContent`; корень без `AddContent` бросает), `EditorContentDirectories` (`#if UNITY_EDITOR`,
  фабрику ставит `Internal.Editor`: `Editor/Content/EditorContentDirectoryInstall.cs`, `[InitializeOnLoadMethod]`, корни из
  `FrameworkSettings.ContentDirectory`, имя сверяется с `ContentOptions.Name`; `InternalsVisibleTo("Internal.Editor")`).
- **Отдельный сегмент профайлера в `LoadContent` не добавлен:** загрузчик [5] уже открывает `GameProfiler.Concurrent("Content: <тип>")` и
  `AssetLoads`; второй span вложил бы тот же интервал в себя.
- **Скоупы:** `IScopeBuilder.Content` (новый член); `ServiceScopeLoader` создаёт lifetime скоупа до сцены регистрации (сцена каталога
  грузится на нём), teardown неудачной загрузки завершает его всегда; `ServiceScopeSceneLoader(lifetime, content).Load(ContentScene, isMain)`
  через `IContentLoader.LoadScene` (результат — `ContentLoadedScene` шага [5], свой `LoadedScene` не нужен); `ContentAssetScene` (вариант
  `AssetScene`, отдельный класс) + `ScopeLoadOptions.WithAssetScene(ContentScene)`; `SceneLoaderExtensions.LoadTyped/FindOrLoadScene<T>(ContentScene)`
  (мок: `SceneManager.GetSceneByPath(EditorPath)` → `FindInOpenScene(name)`); `ScopeBuilderExtensions.FindOrLoadSceneWithServices(ContentScene)`
  (`[ContainerPrimitive(SceneServices)]`, общий `CreateServices`).
- **`ContentLoadedScene.Unload`:** конец lifetime и dispose скоупа зовут его оба; `UniTask.Preserve()` не годится (`MemoizeSource` пробрасывает
  второго ожидающего в незавершённую операцию → `Already continuation registered`), теперь `UniTaskCompletionSource`. `UnloadOnEnd` при
  `ApplicationQuit.IsQuitting` ничего не делает.
- **Старые перегрузки оставлены** (`AssetReference`: `ServiceScopeSceneLoader.Load`, `AssetScene`, `WithAssetScene`, `LoadTyped`, `FindOrLoadScene`,
  `FindOrLoadSceneWithServices`, `LoadedScene`): их зовут pixel-swarm (`Scenes.X.Value`, `level.Scene`) и сгенерированные каталоги (`StaticScene`);
  уходят в [7]/[8]. Из-за перегрузок вызывающая сборка `WithAssetScene` должна видеть Addressables: в `Internal.PlayMode.Tests.asmdef` добавлен
  `GUID:9e24947de15b9834991c9d8411ea37cf` (Unity.Addressables) — убрать в [8].
- **ContainerGenerator:** `PrimitiveKind.Content = 13` (и `ContainerPrimitive.Content` рантайма) на `LoadContent` / `RequestContent`: вызов
  проходит по аргументам, ничего не регистрирует, тело не обходится (`IsPrimitiveBuilder`); без символа — по имени. Якорь сцены уже понимал
  `Scenes.X` без `.Value` (`EntityAssets.SceneField`), поменялись только комментарии. `AddContent` — обычный (async) инсталлер.
  Verify: новый `ContentCases.cs` (3 случая) — **171 PASS** (было 168: в брифе «166» устарело; +3 новых), `ALL PASSED`.
  `ContainerGenerator.dll` пересобран (Release).
- **Тесты:**
  - PlayMode `Internal.Tests.ContentScopeTests` (`Tests/Runtime/Content`, фикстура `ContentScopeRoots`): `RegisteredDirectory_ScopeLoadsSceneAndContent_DisposeReleases`
    (локальный зарегистрированный каталог; корень регистрирует загрузчик, дочерний скоуп `WithAssetScene(ContentScene)` + `LoadContent` + `RequestContent`;
    после dispose сцена выгружена — dispose ждёт выгрузку, ссылка не загружена), `AddContent_EditorDirectory_ChildLoadsTheProjectAssets` (`AddContent`
    в корне, `EditorContentDirectory` через подменённую фабрику), `RootWithoutContent_ContentScene_Throws`.
    PlayMode `-testFilter "Content|Scope|SceneSearch"`: `total="19" passed="19" failed="0"`.
  - EditMode `-testFilter "Scope|Content|AssetGroup|SceneSearch|ProfilerNesting|Container"`: `total="199" passed="199" failed="0"`.
  - EditMode целиком: `total="1209" passed="1205" failed="0" skipped="4"` (benchmarks); client/project компилируется без ошибок.
  - `FindOrLoadSceneWithServices(ContentScene)` в рантайме тестом не покрыт (нужна сцена с `SceneServicesFactory` в каталоге и карта ассетов) —
    генераторная сторона покрыта Verify.
- `releases/unreleased.md`: шаг «Контент и сцены скоупов через `IContentLoader` (BREAKING)».

### [7] Каталоги (поколение 1, не завершён — см. `handoff/7_g1.md`)
Коммиты `1186b23b` (рантайм групп), `529c8d32` (генераторы) и release note в `content-directories`.

- **Рантайм:** `ContentRoot` (`Runtime/Catalogues/Shared`, SO: `_groups`, `_preloadedGroups` — `ContentReference<ScriptableObject>`,
  `_scenes` — `ContentScene`; поиск по GUID ассета/сцены, `KeyNotFoundException` с подсказкой Regenerate All). `AssetGroup<TAsset>`
  (`TAsset : ScriptableObject`): `LoadAsset(guid)` — `IContentLoader.Load` на дочернем lifetime корня, `LoadPreloadedAsset(guid)` —
  `LoadNow`, `UnloadAsset` завершает lifetime (с проверкой `Session`: lifetime прошлого запуска Play Mode не трогается). `IsAddressable`
  → `IsPreloaded` (по умолчанию `false`). `AssetGroup.UnloadAll()` — при конце lifetime корня: группы «не загружены», счётчики Retain
  сохраняются (Release приходит после — `AddAfterDispose` идёт после `lifetime.Terminate`).
- **Решение: как группа получает загрузчик — статический `CatalogContent`**, привязанный `AddContent` к lifetime корневого скоупа
  (`Bind(lifetime, loader, loader.RootAssets<ContentRoot>())`; второй `Bind` при живом — исключение; больше одного `ContentRoot` среди
  корней — исключение; без `ContentRoot` — привязка без корня, доступ к группе/сцене бросает с подсказкой `Content.Roots`). Путь через
  контейнер/`ScopeContent` не подходит: группы и `Scenes.X` — статики, читаются из статических свойств каталогов (`Sprites.X`,
  `EnsureLoaded`) без скоупа. Сброс между запусками — `SubsystemRegistration`.
- **Предзагруженные группы:** `Bind` сразу делает `Fetch` всех `PreloadedGroups` корня (у локального/предзагруженного хранилища —
  ничего), поэтому `EnsureLoaded` → `LoadNow` синхронен и в вебе со скачиванием. Вариант с `Resources` убран целиком.
- **`Scenes.X`** — `public static ContentScene X => CatalogContent.Scene("<guid>")` (не поле: `LoadableSceneId` в рантайме из GUID не
  построить, id в плеере переписывает сборка — берётся из корня). Сцены Build Settings (`Startup`) в `Scenes` и в корень не попадают.
  `ContainerGenerator` форму `.Value` и так не различал (`SceneField` берёт идентификатор после `Scenes.`) — поменялись только
  комментарии, dll не пересобирался, Verify не запускался.
- **Удалено:** `StaticScene` (+ папка `Runtime/Catalogues/Scenes`), `AssetScene`, `LoadedScene`, перегрузки `AssetReference`
  (`ServiceScopeSceneLoader.Load`, `WithAssetScene`, `LoadTyped`, `FindOrLoadScene`, `FindOrLoadSceneWithServices`), ссылка
  `Internal.PlayMode.Tests` на Unity.Addressables; `CatalogGroupAssets.Move` и пути `Resources` групп.
- **Редактор:** `ContentRootGenerator` (`Editor/Catalogues/Shared`; меню Codegen/Content Root, `ScheduleGenerate` из четырёх
  генераторов каталогов и сцен вместо `CatalogAddressablesSync.Sync` / `ScenesAddressablesSync.Sync`; папки групп
  `*CatalogGenerator.GroupsFolder`, флаг предзагрузки — из реестров Assets/Prefabs; пишет только при изменении GUID'ов),
  `ContentRootBuildStep : IContentBuildStep`, шаг Regenerate All «Content root». Генерируемые классы: `AssetGuid` вместо
  `Address`/`ResourcePath`. Реестры: ключ json `nonAddressable` → `preloaded`, `MissingMemberHandling.Error` (старый ключ — исключение,
  тест `CatalogMetadataTests.RegistryWithNonAddressable_Throws`). Определения: `Address`/`SetAddress` → `Guid`/`SetGuid`.
- **client/project:** `Content.Roots` = `[ContentRoot.asset, ContentSmoke/Root.asset]`, сгенерирован пустой
  `Assets/Common/Internal/Runtime/Catalogues/ContentRoot.asset` (каталогов в проекте нет).
- **Больше не вызываются (для [8]):** `CatalogAddressablesSync`, `ScenesAddressablesSync`, `SharedAddressablesSync` (+ `Targets`,
  `Postprocessor` — постпроцессор ещё планирует sync), `ICatalogGroupDefinition` (только для `CatalogAddressablesSync`) — их зовут только
  друг друга и `SharedAddressablesSyncTests`.
- **Evidence:** RegenerateAll batchmode (`-executeMethod Internal.RegenerateAll.Run`) — exit 0, все шаги, без ошибок компиляции.
  EditMode целиком (`-nographics`): `total="1210" passed="1203" failed="0" skipped="7"` (+1 новый тест; 3 лишних skip —
  `PrefabScreenshotTests` без графики из-за `-nographics`, не регрессия).
  PlayMode целиком: `total="135" passed="111" failed="0" skipped="24"` (e2e телеметрии). PlayMode-теста групп (Retain/Release, предзагруженная)
  ещё нет — следующему поколению.
- **Поколение 2 — PlayMode-тест групп** (коммит `c06f3466`): `ContentDirectoryTestBuild` кладёт в тестовый каталог `ContentRoot`
  (одна группа + одна предзагруженная, ассеты `ContentTestGroup`, запись через `ContentReferenceFields.AssignObject`);
  `Tests/Runtime/Content/AssetGroupTests` + группы `TestGroup` / `TestPreloadedGroup` (как пишет генератор), привязка
  `CatalogContent.Bind` на дочернем lifetime: `Retain` грузит и `Release` освобождает (копия уничтожена, `FindObjectsOfTypeAll`),
  два `Retain`, предзагруженная группа на `FetchedContentStore` (до `Bind` — нули, `LoadNow` бросает) доступна через `EnsureLoaded`
  без `Retain` и не выгружается после `Release`, конец lifetime корня → обе `IsLoaded == false`, копии освобождены, `Release`
  после не бросает, `EnsureLoaded` бросает (не привязано). Багов в группах не найдено (тест зелёный с первого прогона).
  **Evidence:** `AssetGroupTests` `total="4" passed="4"`; PlayMode целиком `total="139" passed="115" failed="0" skipped="24"`
  (+4 к 135); EditMode `-testFilter "AssetGroup|Content"` `total="41" passed="41" failed="0"`.

### [8] Удаление Addressables
Коммиты `b3334ef6` ([Core], BREAKING), `b07ebe08` ([Packages] форк), `a60cbaea` ([Tools] [Deploy-client]) в `content-directories`.

- **Удалено:** `CatalogAddressablesSync`, `ScenesAddressablesSync`, `SharedAddressablesSync`, `SharedAddressablesTargets`,
  `SharedAddressablesPostprocessor`, `BundleUrlVersioning` (+ `.meta`); тесты `SharedAddressablesSyncTests` (4) и
  `TypeNameResolverUnloadTests` (2, тест патча форка); `FrameworkSettings.WithoutAddressables`, `SharedCommonPaths`, `GroupPhases` (и ключи
  в `Framework.json` client/project; лишние ключи у игр игнорируются `PopulateObject`); форк `client/packages/com.unity.addressables` целиком
  (общего списка форков вне его `FORK.md` нет); зависимость в `client/core/package.json`, `manifest.json`, `packages-lock.json` (с ним ушёл
  `scriptablebuildpipeline`); ссылки по GUID `9e24947d…` (Unity.Addressables), `84651a37…` (Unity.ResourceManager), `69448af7…`
  (Unity.Addressables.Editor) в `Internal`, `Internal.Editor`, `Internal.Content`.
- **Оставлено:** `ICatalogGroupDefinition` — не только для синхронизации: ограничение `GeneratedFile.GroupByFolder<TGroup>`
  (`Name`), реализуют четыре определения групп. Комментарии без Addressables: `CatalogNaming`, `AssetLoad(s)`, `ContentBuildFiles`,
  `EditorTestsSetUpFixture`, `EditorToolFailureTests`, `SpriteCatalogMetadataTests`.
- **Сторонние форки (не трогал, upstream):** UniTask `Runtime/External/Addressables` (asmdef с versionDefine `com.unity.addressables`,
  без пакета компилируется пустым), unity-mcp `GameObjectSerializer`, URP core `ProbeVolume*`, ugui `TMP_*` — упоминания под `#if` / в комментариях.
- **`verify.sh`:** вместо `aa/catalog.bin` и дублей бандлов — `content_check`: имя и `IsPreloaded` из `client/ProjectSettings/Framework.json`
  копии (контракт `deploy-client.json` не меняется), `Library/Content/<name>/BuildManifestHash.txt` свежее старта сборки, хэш есть в
  распакованном `brotli -dc *.data.br` (индекс в Resources внутри плеера), без предзагрузки `StreamingAssets/Content/<name>` совпадает с
  `Library/Content/<name>` по именам и размерам. Проверено на выходе веб-смоука [4] (data перепакован в br): 7 случаев (ok, лишний файл,
  чужой хэш, старый каталог, предзагруженный без папки, не предзагруженный без папки, нет `Content.Name`). Полный `deploy-client` не запускался.
- **Evidence:** финальный grep `client/core client/flow client/modules client/platforms client/project/Assets client/project/Packages
  shared common tools` — пусто. Компиляция batchmode: 0 `error CS`. RegenerateAll — exit 0, все шаги. EditMode (`-nographics`)
  `total="1204" passed="1197" failed="0" skipped="7"` (−6: удалённые тесты Addressables). PlayMode `total="139" passed="115" failed="0"
  skipped="24"`. Бэкенд и `ContainerGenerator` Addressables не касались — `dotnet build` и Verify не нужны.
- **Для [9] (docs / skills / CLAUDE.md):** `CLAUDE.md` строки ~146-147 (фазы общих адресуемых групп, `WithoutAddressables`), ~168
  («a game that loads Addressables in another way»), раздел Layout `packages/` (форки); `.claude/docs/ARCHITECTURE_PATTERNS.md:53`
  (`Addressables.LoadAssetAsync`), `COMMON_CONTAINER.md:51` (`WithAssetScene(assetReference)`), `WEBGL_TROUBLESHOOTING.md` 44, 218-219,
  456-458 (B6), 475, 488; `.claude/skills/update-override-packages/SKILL.md:15` (форк addressables в списке), `client-telemetry/SKILL.md:93`
  («addressables» в триггерах стартапа); `deploy-client/SKILL.md` — описать новую проверку content directory (нужен `brotli`).
  `AddressableState*` / `AddressableDictionary` в Orleans-доках и бэкенде — не Addressables Unity, не трогать.

### [9] Тесты, доки, релиз-кандидат
Коммиты `6837150a` ([Docs]) и `f8e4d861` ([Release] 0.7.0-rc.1) в `content-directories`; аннотированный тег `v0.7.0-rc.1` → `f8e4d861`
(как `v0.6.0`). Не запушены ни ветка, ни тег.

- **test-all.sh** (worktree, `TEST_ALL_TIMEOUT=900`, логи `.build/test-all/logs/20261109-202940`), exit 0, без падений:
  build PASS; backend 1565/1565; common 96/96; verify 171; control 23; telemetry 84; editmode 1200/1204 (4 skip); playmode 115/139
  (24 skip). Тесты шли до коммитов доков (менялись только md).
- **Числа в `CLAUDE.md`:** backend 1565, common 96, Verify 171, control 23, telemetry 84, EditMode 1204 (4 skip), PlayMode 139 (24 skip).
  Причины в коммите: backend/common выросли в main после `367d9bdf` (последней записи чисел), Verify +3 `ContentCases` (168 до ветки),
  EditMode/PlayMode — тесты контента ветки минус 6 тестов Addressables.
- **Доки:** новый `.claude/docs/CONTENT.md` (модель, API с примерами, сборка, хранилища, телеметрия и проблемы, ограничения a10) + строка
  в `INDEX.md`; `CLAUDE.md` — пункт Content в Extension points, `Framework.json` `Content` вместо фаз адресуемых групп и
  `WithoutAddressables`, `ContentRoot` / `Scenes` в Client core, `AssetLoads` через `IContentLoader`, список форков; `WEBGL_TROUBLESHOOTING.md` —
  B6, B9, строки R10/B11 «before 0.7», новый B15 (Cache API контента, `brotli` в verify), строка P3; `COMMON_CONTAINER.md`,
  `ARCHITECTURE_PATTERNS.md` C-DI-8. Скиллы: `deploy-client` (проверка контента в `checks <t>`, нужен `brotli`), `update-override-packages`,
  `client-telemetry`.
- **Релиз:** `releases/0.7.0.md` (заголовок «0.7.0 (release candidate rc.1)», `base: v0.6.0`), строка в README, `unreleased.md` — заголовок.
  7 шагов: `Framework.json` `Content`; `AddContent` и скоупы; каталоги (`ContentRoot`, `IsPreloaded`, `Scenes.X`, `AssetGuid`, `Guid`/`SetGuid`);
  свои ссылки `AssetReference(T)` / `Addressables.`; удаление Addressables (`manifest.json`, asmdef GUID, `AddressableAssetsData`,
  `BundleUrlVersioning`, ключи `Framework.json`, `link.xml`); шаблоны WebGL (`cacheControl` `.bundle`), `nginx.conf`, проверка `aa/catalog.bin`
  в `deploy-webgl.sh` pixel-swarm; `deploy-client` + `brotli`. Все 7 BREAKING-коммитов ветки покрыты. Поиск по HEAD pixel-swarm `db4968c`
  записан в «Состояние игр»; сборки игр не было (по брифу).
- **Финальный grep** (`AddressableAssets|ResourceManagement|AssetReference|Addressables` по client/core, flow, modules, platforms, shared,
  tools, .claude, CLAUDE.md): только явные пометки — `WEBGL_TROUBLESHOOTING.md` 219, 458-460 (B6), 479 (B9), 492 (B11) «before 0.7»,
  `CONTENT.md:5`, `INDEX.md:34` («removed in 0.7»), `CLAUDE.md` 28, 157 («since/removed in 0.7»).
- **Открытый вопрос для [10]:** `update-framework/status.sh` для каждого `releases/X.Y.Z.md` требует тег `vX.Y.Z`; на кандидате есть только
  `v0.7.0-rc.1`, поэтому `status.sh` на этой ветке завершится `error: release 0.7.0 has no tag v0.7.0`. В [10] двигать указатель
  pixel-swarm вручную (`git -C tools/framework checkout v0.7.0-rc.1`) и выполнять шаги `releases/0.7.0.md`, либо научить `status.sh` rc-тегам.
- Побочка редактора (`MultiplayerManager.asset`, `com.unity.dedicated-server/`) не закоммичена.

### [10] pixel-swarm: ветка и фреймворк rc
Worktree `/projects/pixel-swarm/.claude/worktrees/content_directories`, ветка `content-directories` от `main` `db4968ca2` (shared tree не
тронут; `.claude/worktrees/` уже в `.git/info/exclude`). `client/Library` и `UserSettings` — `cp -a --reflink=auto` из shared tree (локи
удалены). Submodule `tools/framework`: fetch из `/projects/framework` (`content-directories` + тег), `checkout v0.7.0-rc.1` = `f8e4d861`.
Указатель main был `46a6f79` (`v0.6.0-1`), значит только шаги `releases/0.7.0.md`; `status.sh` не запускался (нет тега `v0.7.0`).

Коммиты (не запушены): `c5307e352` [Framework] указатель на rc; `56288ff2a` [Content] WIP механические шаги (не компилируется).

**Выполнено (шаги релиза):**
- 1: секция `Content` в `Framework.json` (`Name: Game`, корень `Assets/Common/Internal/Runtime/Catalogues/ContentRoot.asset`,
  `IsPreloaded: false`); `Assets/FrameworkContentIndex/` в `.gitignore`. Сам `ContentRoot.asset` ещё не сгенерирован (Regenerate All — [12]).
- 2: `await builder.AddContent(content)` первой строкой `InternalScopeExtensions.Construct(builder, ContentOptions)`; `LoadInternal(options)`
  через лямбду в `LoadRoot`. **Решение:** `ContentOptions` не может лежать в ассете группы (все группы грузятся из каталога после
  `AddContent`, `InternalAssets.Group` тоже), поэтому — `[SerializeField] ContentOptions _content` у `GameStartup` и `MockBase`, значение
  `_name: Game` дописано в YAML `Startup.unity`, `Menu.unity`, `Lobby.unity`, `Game.unity` (4 копии имени — кандидат на [16]: имя из
  `Framework.json` или конструктор `ContentOptions` во фреймворке).
- 3: `Scenes.cs` переписан в форму нового `ScenesClassGenerator` (`ContentScene X => CatalogContent.Scene("guid")`, без `Startup` из Build
  Settings) — иначе `Internal.Content` не компилируется и блокирует всё; Regenerate All в [12] должен дать тот же файл. `Scenes.X.Value` →
  `Scenes.X` в `MenuScopeExtensions`, `LobbyScopeExtensions`, `GamePlayScopeExtensions` (снято с [11]). `nonAddressable` → `preloaded` в
  `AssetGroups.json`, `PrefabGroups.json`. `IsAddressable` в коде игры не было.
- 5 (частично): `com.unity.addressables` из `manifest.json`, lock переписан Unity (ушли addressables и scriptablebuildpipeline); ссылки на
  `Unity.Addressables` / `ResourceManager` / `Addressables.Editor` (GUID и имена) убраны из 13 asmdef; `BundleUrlVersioning.Setup()` из
  `GameStartup`; `SharedCommonPaths`, `GroupPhases` из `Framework.json`. `link.xml` без записей Addressables. **Не сделано:**
  `Assets/AddressableAssetsData` (нужен [11]) и `m_ConfigObjects: com.unity.addressableassets` в `ProjectSettings/EditorBuildSettings.asset`
  (ссылка на настройки Addressables — убрать в [12] вместе с папкой).
- 4, 6, 7 — шаги [11]–[13] (`brotli` для 7 проверить там).

**Ошибки компиляции.** Batchmode (`-quit`, логи `scratchpad/open1..6.log` сессии агента). Unity не компилирует зависимые сборки упавшей,
поэтому каскад снят временными заглушками (откачены, не в коммитах): заглушки 4 файлов GamePlay → `#if false` на `LevelAddressables`.
Итог по закоммиченному состоянию (open6): падают `GamePlay` (18) и `GamePlay.Stress.Editor` (3). Полный список по шагам:

- **[11]** (21 + тесты):
  - `GamePlay/Levels/LevelDefinition.cs` — 5 (`AssetReference`, `AssetReferenceT<>`, `using UnityEngine.AddressableAssets`);
  - `GamePlay/Levels/LevelScopeExtensions.cs` — 4 (`AssetReferenceT<>`, `AsyncOperationHandle<>`, usings);
  - `GamePlay/Swarm/Encounters/EncounterFinal.cs` — 5 (`AssetReferenceT<>`, `AssetReference`, using); `Create(type, guids)` строит ссылки
    из GUID — у `ContentReference<T>` нет такого конструктора (нужен `LoadableObjectId`), тестам нужна замена;
  - `GamePlay/Loop/Run/RunFinals.cs` — 4 (`AssetReferenceT<>`, `AsyncOperationHandle<>`, usings);
  - каскад после них: `GamePlay/Tests/EncounterTests.cs` — 3 (`editorAsset`, `AssetGUID` → `EditorAsset`, `Guid`);
    `Common/Global/Tests/Editor/LocalizationTests.cs:201` — 1 (`editorAsset`); не ошибки компиляции, но сломаются в рантайме тестов:
    `GamePlay/Tests/LevelTests.cs:39,132` (`_scene.m_AssetGUID`, `_encounter.m_AssetGUID` через `SerializedProperty`).
- **[12]**:
  - `GamePlay/Stress/Editor/StressBuildStep.cs` — 3 (`UnityEditor.AddressableAssets*`, `BundledAssetGroupSchema`);
  - `GamePlay/Tools/Levels/LevelAddressables.cs` — 4 (`AddressableAssetSettings`, `AddressableAssetGroup`, usings);
  - зависящие от него: `GamePlay/Tools/Levels/LevelAssets.cs` — 3 (`LevelAddressables`; там же `m_AssetGUID` строки 155–178),
    `GamePlay/Tools/Encounters/EnemyAtlases.cs` — 3 (`LevelAddressables`);
  - `GamePlay/Tools/Encounters/EncounterAssets.cs` — 4 CS0012: `GamePlay.Tools` не ссылается на сборку `Internal`, а
    `EncounterFinal.References` станет `ContentReference` (см. «вне плана»);
  - `GamePlay/Tools/Tests/EnemyAtlasTests.cs:101,106` — не скомпилирован (зависит от `GamePlay.Tools`), по grep: `LevelAddressables`,
    `parentGroup` записи Addressables.
- **[13]**: ошибок компиляции нет (шаблоны WebGL, `client/deploy/nginx.conf`, `README.md`, `tools/scripts/deploy-webgl.sh` — по grep).
- Сборки `Flow`, `Menu`, `Global`, `Meta`, `Internal.Content(.Editor)`, `Flow.Tests` с заглушками скомпилировались чисто; ошибок генератора
  контейнера (`CINGR`) нет.

**Вне плана [11]–[13]:**
- `EncounterAssets.cs` (инструмент энкаунтеров, нет в списке файлов [12]) и ссылка asmdef `GamePlay.Tools` → `Internal`
  (`GUID:f039cb8c565843dba6da5b3c1ccde7d6`): комментарий `EncounterFinal.References` говорит, что инструменты нарочно не ссылаются на сборку
  `EnvAsset`; `ContentReference` лежит в `Internal`. Предложение — в [11] (вместе с `EncounterFinal`): либо ссылка на `Internal` в
  `GamePlay.Tools`, либо `References` отдаёт GUID/`EditorAsset` как `Object`.
- `EncounterTests.cs`, `LocalizationTests.cs`, `LevelTests.cs` — в [11] (тесты кода, который он меняет).
- Ассеты с `m_AssetGUID` вне уровней: `Assets/GamePlay/Assets/Locations/Arena/ArenaEncounter.asset` и 4 исследовательских
  `Assets/Art/Research/Recreate/*/*Encounter.asset` (D2BlueRedHuge, SkyGardens, HighFields, WinterForest) — миграция [11] должна их покрыть
  (или явно решить, что research-ассеты не мигрируются: их нет в корне каталога, но пустая ссылка в ассете вне каталога сборку не валит).
- `ProjectSettings/EditorBuildSettings.asset` `m_ConfigObjects.com.unity.addressableassets` — в [12] с `AddressableAssetsData`.
- `Library` копии ещё содержит `com.unity.addressables` / `AddressablesConfig.dat` (локально, не в git).

### [11] pixel-swarm: ссылки и загрузки
Коммиты (worktree pixel-swarm, ветка `content-directories`, не запушены): `142f7ddf7` [Levels] [Run] код, `e74617eb1` [Levels] [Encounters]
миграция данных, `44302e96e` [Tests] тесты.

- **Код:** `LevelDefinition` — `ContentScene _scene`, `ContentReference<ArenaEncounter> _encounter`, `Validate` по `IsEmpty`.
  `AddLevel` — `FindOrLoadScene<ArenaView>(level.Scene)` + `builder.LoadContent(level.Encounter)` параллельно (`UniTask.WhenAll`), без
  ручного `Release` / `ApplicationQuit`. `EncounterFinal._enemies` — `ContentReference<EnemyDefinition>[]`, `Pick` отдаёт ссылку,
  `Create(type, ContentReference<EnemyDefinition>[] candidates, ...)`; `References` удалён (P13). `RunFinals` — `IContentLoader` параметром
  конструктора (как остальные зависимости класса; `[Inject]`-метод не нужен), `Load(_lifetime /*забега*/, reference)` +
  `SuppressCancellationThrow`: конец забега во время загрузки — тихий выход; ручной `AssetLoads` убран (загрузчик сам пишет
  `Content: <guid>` и span). `FindOrLoadSceneWithServices(Scenes.X)` в Menu/Lobby/GamePlay — уже из [10], проверено.
- **Решение `GamePlay.Tools`:** ссылка asmdef на `Internal` **и `Internal.Content`** (по имени, как остальные ссылки этого asmdef).
  `Internal.Content` обязателен: без него `ContainerGenerator` в сборке, ссылающейся на `GamePlay`, обходит `GamePlayScopeExtensions.Construct`
  без карты ассетов и падает `CINGR001 scene 'Scenes.Game' without SceneServicesFactory in the asset index` (так упала временная сборка
  миграции; правило CLAUDE.md «asmdef с Internal ссылается на content assembly»). `EncounterAssets` читает `Enemies` / `EditorAsset`.
- **Тесты:** `GamePlay.Tests` +ссылка `Internal.Editor`; `FinalCandidates` (кандидаты из первых `EnemyDefinition` проекта через
  `ContentReferenceFields.Create`: фейковые GUID больше нельзя — `ContentReference` строится только из реального ассета, а пустая ссылка
  даёт лишнюю ошибку `Validate`); `EncounterTests` (`EditorAsset`, `Guid`, `AreSame`), `LevelTests` (`level.Scene.EditorPath`,
  `ContentReferenceFields.AssignObject` для `_encounter`), `LocalizationTests` (`EditorAsset`), `RunStages/SwarmArena/SwarmDirector/SwarmSlot`
  Tests. Новый `LevelContentReferenceTests` (2): каждая ссылка уровня и каждый кандидат финала каждого `ArenaEncounter` не пусты, за
  ссылкой ассет нужного типа (`SceneAsset` для сцены), `_guid`/`_localId` = GUID/local id ассета.
- **Миграция:** все `m_AssetGUID` в `.asset`/`.unity`/`.prefab` вне `AddressableAssetsData` — 7 ассетов, **24 ссылки**: 2 уровня × (сцена +
  энкаунтер), `ArenaEncounter` и 4 `Art/Research/Recreate/*Encounter.asset` × 4 кандидата финалов. Research-энкаунтеры мигрированы, не
  исключены: `HighFieldsEncounter` и `WinterForestEncounter` — энкаунтеры уровней. В сценах и префабах `AssetReference` нет — мест с
  `Loadable` в сцене плеера нет. Порядок: дамп «до» (`probe/11_refs_dump.py . before`, yaml: ассет → путь свойства → GUID → путь цели),
  смена типов, временная Editor-сборка `Assets/ContentMigration` (`-executeMethod ...ContentReferenceMigration.Run -migrationDump`:
  `ContentReferenceFields.AssignScene/AssignObject` по пути свойства из дампа, проверка GUID↔путь), затем сборка удалена. Unity при смене
  типа значений не терял бы до пересериализации, но запись шла из дампа, так что неважно; `[FormerlySerializedAs]` не нужен (имена полей те же).
  Дамп «после» (`after`: `_guid`, проверено равенство `_guid`/`_localId` с `m_LoadableObjectId` / `m_SceneGUID`) — `diff` с «до» пуст: 24/24,
  пустых 0. Файлы: `probe/11_refs_before.tsv`, `probe/11_refs_after.tsv`, `probe/11_refs_dump.py`. Побочка: Unity дописал в энкаунтеры
  отсутствовавшие поля с их значениями по умолчанию (`_maxAlive: 0`, `_isWave: 0`, `_experience: 1`, `_startDelay: 5`, ...) — то же, что
  десериализатор и так подставлял.
- **Компиляция:** batchmode, `[12]`-сборки временно выключены `defineConstraints: STEP12_PENDING` (`GamePlay.Tools`, `GamePlay.Tools.Tests`,
  `GamePlay.Stress.Editor`; откачено, не в коммитах): 0 `error CS`, 0 `CINGR`. Без выключения падают только они (`LevelAddressables`,
  `LevelAssets`, `EnemyAtlases`, `StressBuildStep`; `EncounterAssets` CS0012 снят этим шагом).
- **Тесты:** EditMode `-testCategory '!Manual;!Generation'` (с графикой): `total="1201" passed="1151" failed="3" skipped="47"`. Все тесты
  уровней/энкаунтеров/финалов/локализации зелёные (`LevelContentReferenceTests` 2/2, `LevelTests` 3/3, `EncounterTests` 13/13). Не
  запускались: `GamePlay.Tools.Tests` (вся сборка, включая `EnemyAtlasTests`) — из-за [12]. Три падения не связаны с [11] (их области не
  менялись ни в ветке игры, ни в `shared`/`common`/`network` фреймворка с `v0.6.0-1`; на main не перепроверял — shared tree не трогаю):
  `SwarmMapBenchmarks.Map_AWalkOfAMixedCrowd_IsTheReference` (md5 эталона), `UpgradeScreensPrefabTests.RunBuild_HoldsEveryTree` (26 тайлов <
  36 деревьев каталога), `Internal.Tests.ProtocolBytesTests.GoldenBytes_ReadAndWrittenBack_Unchanged` (`LobbyLevelState`, байт [2] 255 вместо 0) —
  проверить на main в [12]/[14].
- **Побочка редактора не закоммичена:** открытие проекта само перегенерировало каталоги (`Address` → `AssetGuid` в 20 сгенерированных
  классах, новый `ContentRoot.asset` с 48 GUID) — откачено/удалено, это Regenerate All шага [12].
- **Кандидаты в [16]:** (1) диагностика `CINGR001` при отсутствии ссылки на content assembly вводит в заблуждение — подсказать
  «assembly X не ссылается на content assembly (ContainerGraph.AssetMap)»; (2) нет редакторного способа построить `ContentReference<T>` для
  тестов без реального ассета (обход: `FinalCandidates` на ассетах проекта) — приемлемо, можно не делать.

### [12] pixel-swarm: инструменты, каталоги, данные Addressables
Коммиты (worktree pixel-swarm, ветка `content-directories`, не запушены): `787d01d10` [Levels] [Encounters] [Stress] инструменты,
`0451f851a` [UI] блокеры Regenerate All (с main), `a281f1ce2` [Content] регенерация каталогов, `f3e4d2a86` [Content] удаление данных
Addressables, `693af4deb` [Content] сцены без `m_LightingDataAsset`.

- **Инструменты.** `LevelAssets` — сцена/энкаунтер уровня через `_scene._guid` / `_encounter._guid` и `ContentReferenceFields.AssignScene /
  AssignObject`; `Encounters()` перенесён сюда. `EnemyAtlases` — имя атласа уровня из имени энкаунтера сам (`LevelName`), имена атласов
  прежние. `EnemyAtlasTests`: оставлены 3 теста, вместо 4 тестов групп Addressables — 3: группы `Levels` и `GamePlay` (лобби) не тянут
  прямыми зависимостями виды/атласы/листы врагов, энкаунтер не тянет кандидатов финалов. `GamePlay.Tools` +ссылка `Internal.Editor`.
- **Удалено (P13, вызывающие проверены grep):** `GamePlay/Tools/Levels/LevelAddressables.cs` (группы `Encounter_*`/`Boss_*`, `GroupPhases`;
  вызывали только `LevelAssets`, `EnemyAtlases`, `EnemyAtlasTests`), `GamePlay/Stress/Editor/StressBuildStep.cs` и сборка
  `GamePlay.Stress.Editor` (никем не используется). Решение по stress: группа `Stress` (`StressOptions`, три числа) теперь в каталоге
  каждой сборки, грузится только под `PLATFORM_STRESS`; исключить группу из каталога по сборке фреймворк не умеет (кандидат [16]).
  `Assets/AddressableAssetsData` (220 файлов), `m_configObjects` Addressables в `EditorBuildSettings.asset` (стало `{}`), шаблоны
  Addressables в `.gitignore`, проверка `aa/catalog.bin` в `tools/scripts/deploy-webgl.sh` (шаг 6 `releases/0.7.0.md`, только скрипт).
  Доки игры: `ENCOUNTERS.md`, скилл `stress-test`.
- **Блокеры Regenerate All — состояние main, не ветки:** (1) `CheatPanel.prefab` — тайлы 27–35 простые копии `UpgradeTreeTile`
  (коммит main `0b4bc9ecc`) → `Duplicate bindings types`; заменены экземплярами префаба (временный editor-скрипт, удалён). (2) Префабы
  UI-кита (`Bar`, `Button`, `Key`, `Sign`) остались в `GamePlay/Assets/UI/Kit`, а их bindings — в `Common/Global` (main `fdc5b0f7b`):
  генератор писал второй `UIBarBindings`/`UIButtonBindings` в `GamePlay/Assets/Generated` (дубликат класса) → префабы перенесены
  `git mv` в `Common/Global/Assets/Kit` (GUID те же), пути в `ResolutionSettingTests`, `StickButtonTests`.
- **Regenerate All** (`-executeMethod Internal.RegenerateAll.Run`): без ошибок, `Done: 29 bindings checked`; `ContentRoot.asset`: 21
  групп, 0 предзагруженных, 5 сцен; 22 сгенерированных файла (`Address` → `AssetGuid`). Компиляция: 0 `error CS`, 0 `CINGR`.
- **Финальный grep** `AddressableAssets|ResourceManagement|AssetReference|Addressables\.` по `client/Assets client/Packages
  client/ProjectSettings tools/scripts .claude` — пуст. (Упоминания `SharedAddressablesSync`/бандлов остались в доках игры
  `CODEGEN.md`, `ERRORS.md`, `RUNTIME_VIEWS.md`, `CLAUDE_MISTAKES.md`, `VOCABULARY.md` — история, не код; шаблоны WebGL — [13].)
- **Startup:** ссылается только на `GameStartup.cs` (и встроенное) — дубликатов каталога в `.data` нет.
- **ContentBuild** (`ContentBuild.Prepare(WebGL)` + `Cleanup` из временного editor-метода, batchmode `-buildTarget WebGL`):
  - до: `Game: 9219 files, 301499920 bytes raw, 29194582 bytes brotli; root files 2 (27904 bytes), 8874 objects, 5 scenes` +
    `built-in resources in the directory: unity_builtin_extra (8556527594ad845caba90182d1c11ca2.cf, 527292 bytes)`;
  - после обнуления `m_LightingDataAsset` в 8 сценах (Menu, Lobby, 6 исследовательских; запечённого света нет — `m_EnableBakedLightmaps: 0`,
    `LightingData.asset` в проекте нет): `9218 files, 300972235 bytes raw, 29135742 bytes brotli; ... 8873 objects`, предупреждения нет.
  - строки «файлы без загрузки» нет. `Assets/FrameworkContentIndex` после сборки удалён.
- **Три падения [11] на main** (временный detached worktree `db4968ca2` в `.claude/worktrees/step12_main_check` — не scratchpad: /tmp —
  tmpfs, 23 GB `Library` туда не reflink; удалён): все три падают так же (`939a5333…` vs `11fe75fa…`; `>= 36, was 26`; `LobbyLevelState`
  байт [2] 255) — не от ветки.
- **Тесты** (`tools/scripts/run-tests.sh --unity-only`, уровень fast, теперь вместе с `GamePlay.Tools.Tests`): `total 1422, passed 1368,
  failed 4, skipped 50` (логи `/tmp/pixel-swarm-tests/20261109-211957`). Падают три из [11] (на main те же) и
  `GamePlay.Tools.Tests.BackdropCrownTests.Layer_RestsAtItsAnchor_FollowsAShare_ByWholeGamePixels` (`-20` vs `0` по y): тест устарел после
  коммита main `a6a4fc642` «Backdrop parallax along x only»; `BackdropLayer.cs` и тест веткой не менялись — не от ветки (на main не
  перезапускал: чистая математика, код идентичен). `EnemyAtlasTests` (6) и `LevelsPageTests` зелёные. Все четыре — долг main, не правились.
- **Кандидаты в [16]:** (1) нет способа исключить группу каталога из каталога конкретной сборки (бывший `StressBuildStep`): например
  `define` у группы в json-списке или фильтр `IContentBuildStep`, выполняемый после `ContentRootGenerator` (сейчас шаги упорядочены по
  полному имени типа, игровой шаг `GamePlay.*` идёт раньше `Internal.ContentRootBuildStep` и его запись затирается); обход — группа
  `Stress` в каждом каталоге. (2) Hierarchy bindings: при регенерации класс пишется в `Generated` модуля, где лежит префаб-владелец, без
  поиска уже существующего файла этого класса в другой папке → дубликат класса (CS0101) после ручного переноса bindings; генератору стоит
  находить существующий файл класса или падать с понятной ошибкой. (3) Нет `executeMethod` для сборки только каталога (без плеера) с
  логом `[ContentBuild]` — пришлось временным методом `ContentBuild.Prepare(WebGL)` + `Cleanup()`; полезен `Internal.ContentBuild.BuildOnly`
  для проверок. (4) Наблюдение: 8873 объекта → 9218 файлов (почти файл на объект) — 300 MB raw / 29 MB br; стоит оценить число запросов
  загрузки уровня в [15].
- **Открыто:** Startup чист; WebGL-сборка плеера не делалась (шаг [13] со сборкой целей `deploy-client`).

### [13] Шаблоны WebGL и сборка
Коммиты (worktree pixel-swarm, `content-directories`, не запушены): `3f1dc7b69` [WebGL] шаблоны + 3 комментария, `40dff58fa` [WebGL] [Deploy]
комментарии `client/deploy/nginx.conf`, `client/deploy/README.md` (убран пункт про `catalog.bin`), `tools/scripts/deploy-webgl.sh`.

- **Шаблоны** Default/Telegram/Stress: из `cacheControl` убрано правило `.bundle` → `immutable`; осталось `.data` → `must-revalidate`,
  прочее `no-store` (кэш Unity, `webGLDataCaching: 1`). Файлы каталога jslib качает обычным `fetch` в свой Cache API (`content`),
  `cacheControl` их не касается. `web-shell.js` / `style.css` байт-в-байт (cmp). grep `-i "bundle|addressable"` по трём `index.html` и по
  собранным `index.html` пуст (`assetBundleName:` в `.meta` — стандартное поле Unity, не о бандлах).
- **Профили** website/web_telegram/web_stress: отличаются только шаблоном, define (`PLATFORM_TELEGRAM`/`PLATFORM_STRESS`) и `DEBUG_TOOLS`
  у telegram; `preloadedAssets` — только `TMP Settings`; ссылок на Addressables нет. `web_stress` не собирался.
- **Сборка** `deploy-client.sh --notest --no-deploy` (KEEP_COPY=1), HEAD `3f1dc7b69` (включает `94ae4a819` и `098af36ba` из [14]):
  `build web PASS 721 s`, `build telegram PASS 716 s` (пик RSS 16.5 GB), логи `/tmp/content_directories-deploy-client/20261109-215433`,
  **сборки** в `/var/tmp/content_directories-deploy-client.lluFWG/{web/builds/website,telegram/builds/telegram}` (оставлены для [15],
  удалить вручную). Скрипт кончил кодом 1: `checks FAIL: the .data file has no index of the content directory Game`.
- **Причина FAIL — баг фреймворка** `tools/scripts/deploy-client/verify.sh:87`: `brotli -dc "$data" | grep -aqF -- "$hash"` под
  `set -euo pipefail` (`deploy-client.sh:63`): `grep -q` выходит на первом совпадении, `brotli` получает SIGPIPE, пайплайн = 141. Хэш в
  `.data` есть (1 вхождение в `_manifestHash`). Воспроизведено: с pipefail `141`, без — `0`. `verify_target` из того же verify.sh, вызванный
  вручную без pipefail на этих копиях: web `guid 88d617b8…, payloads fresh, content Game (9219 files), stripped (34 submodules, 3889962 bytes),
  index.html and files ok`; telegram `guid 6c635fba…, … content Game (9219 files), stripped (34 submodules, 3847134 bytes), … ok`. Исправление
  для [16]: `grep -aF -c … >/dev/null` без `-q` или `brotli -dc > tmp` и grep по файлу. Фреймворк не правил.
- **Содержимое**: обе сборки — `StreamingAssets/Content/Game`: 9219 файлов (9218 + `BuildManifestHash.txt`; манифест `32cee090….json`,
  `.cf` и 345 `.resource`), совпадает по именам/размерам с `Library/Content/Game`; `aa/` и `.bundle` нет. Каталог не предзагружен
  (`IsPreloaded: false`), preload manifest не ожидается. Манифест у обеих целей один и тот же — файлы контента общие (link-dest деплоя).
- **Размеры** (br / raw, байт):

  | | web | telegram |
  |-|-|-|
  | wasm | 3837247 / 15744890 | 3842417 / 15744515 |
  | data | 2135764 / 5874427 | 2135402 / 5874115 |
  | framework.js | 75008 / 377214 | 75008 / 377214 |
  | loader.js | 28175 | 28175 |
  | StreamingAssets | 300972267 raw (9219 файлов); br-оценка ContentBuild 29135874 | то же |

- **Находки:** (1) `client/deploy/nginx.conf`: `gzip off`, `.cf` / `.resource` лежат несжатыми и отдаются как есть — игрок качает raw
  (300 MB на весь каталог против 29 MB br; раньше бандлы Addressables были сжаты LZ4 внутри). Нужен либо сжатый на сборке
  `StreamingAssets/Content` (`.br` + `Content-Encoding`, как у `Build/*.br`), либо сжатие у хоста — решить в [15]/[16]; nginx пока не
  менял. Там же `Cache-Control: no-cache` на файлы, названные хэшем (можно `immutable`; jslib всё равно кэширует сам). (2) `location /`
  отвечает `index.html` с 200 на отсутствующий файл — jslib упадёт по размеру (`N bytes instead of M`), понятно, но не 404.
  (3) Хэш манифеста менялся между прогонами от одного HEAD `693af4deb` (`d11daff2…` → `32cee090…`): первый снапшот мог включать
  незакоммиченные правки [14] — не исследовал.

### [14] Проверка в редакторе
Редактор worktree (`control editor start`, порт 6403, с окном на скрытом workspace; MCP у сессии нет — `execute_code` через
`bridge.py`). Сцена Play задаётся `EditorSceneManager.playModeStartScene` (`OpenScene` из `execute_code` роняет CoreCLR-редактор);
Game + локация — через `Library/LastSceneManagerSetup.txt` и перезапуск редактора. Лог каждого сценария — свой файл
(`Application.logMessageReceivedThreaded`, тип + стек), Enter Play Mode Options проекта: `DisableDomainReload, DisableSceneReload`, так
что каждый вход после первого — повторный без domain reload (в первой сессии редактора 10 входов подряд, во второй/третьей ещё по 1–9).
Кластер не запускался: бэкенд `Local`, `localhost:7101` не слушает → офлайн-режим игры (`[Meta] ... the offline mode` — ожидаемые 2
предупреждения в каждом сценарии, других warning/error/exception нет, если не указано).

**Коммиты** (ветка `content-directories`, не запушены): `94ae4a819` [Content] корень скоупа локальной функцией; `098af36ba` [UI] тайлы
деревьев чит-панели в bindings.

**Найдено и исправлено (игра):**
1. Каждый старт (Startup и все моки) падал `ArgumentException: Scope root must be a method group or a local function, not a lambda:
   <LoadInternal>b__0` (`GeneratedScopeIds.BuildRootId` ← `RootScopeExtensions.LoadRoot` ← `GameStartup.Setup:36`): решение [10]
   `LoadRoot(builder => Construct(builder, content))`. Теперь корень — локальная функция `Construct` внутри `LoadInternal` (замыкание
   на `ContentOptions`), id `Internal.InternalScopeExtensions.LoadInternal+Construct`; `Global` объявляет родителя
   `[ContainerScopeParent(typeof(InternalScopeExtensions), InternalScopeExtensions.Root)]` (`Root = nameof(LoadInternal) + "+Construct"`).
   Компиляция без `CINGR`, Global/Meta/Menu/Run резолвят сервисы корня в рантайме. Теста нет (нужен реальный `AddContent`; ловит любой
   Play).
2. Каждый забег падал `NullReferenceException` в `CheatPanelView.BindTrees` (`CheatPanelView.cs:393`): после замены тайлов 27–35 на
   экземпляры префаба в [12] (`0451f851a`) их ссылки в `CheatPanelBindings._trees._tiles` остались `{fileID: 0}`. Тест сначала:
   `UpgradeScreensPrefabTests.CheatPanel_HoldsTheCatalog` + `Has.None.Null` по тайлам — FAIL (9 null), затем
   `HierarchyBindingsBatch.RegeneratePrefabs(CheatPanel.prefab)` + `PumpPending` до `Idle.` → PASS, `HierarchyBindingsValidator.ValidatePrefab` = 0.

**Сценарии (после исправлений, все без ошибок/исключений):**

| Сценарий | Результат |
|---|---|
| Startup, без autorun | Global → Meta (офлайн) → Menu; выход: `Root scope lifetime terminated`, без ошибок |
| Startup, `autorun=1` (SessionState `Autorun.Query`) | меню → Solo → забег WinterForest (уровень по умолчанию), `Game` + `Game_WinterForest`, `The final of stage 1 is loaded` (загрузка `RunFinals` через `IContentLoader`), бой идёт |
| Startup, `autorun=1&level=HighFields` (вторая локация) | `Game_HighFields`, `HighFieldsEncounter`, финал загружен, 40 с боя |
| Забег → `IGameLeave.Leave()` → меню → autorun → второй забег | выход из забега, меню, новый забег с новой загрузкой финала (выгрузка/повторная загрузка сцены уровня и энкаунтера) |
| Выход из Play на 2/3/4 с (во время загрузки забега) ×3, затем полный вход | без `OperationCanceledException` в логе, без ошибок; следующий вход доходит до забега |
| Три входа подряд Startup → забег | каждый доходит до финала, без ошибок |
| MenuMock (`Menu.unity`) | `Mock finished`, меню |
| LobbyMock (`Lobby.unity`) | офлайн-сессия лобби, `Entered the room`, `Lobby_Services` |
| GameMock (`Game.unity`) | забег WinterForest, финал загружен, конец стадии `Lost` |
| GameMock, открыты `Game` + `Game_WinterForest` | открытая сцена локации найдена (`FindOrLoadScene` по `EditorPath`), второй раз не грузится; после выхода из Play сцены редактора на месте |
| GameMock, открыты `Game` + `Game_HighFields` | грузит уровень по умолчанию (`Game_WinterForest`) рядом с открытой HighFields — так же на main (мок находит сцену только выбранного уровня) |
| `Game_HighFields.unity` одна | мока нет — ничего не стартует (как на main) |

Ни в одном сценарии нет `Release without a Retain`, исключений `AssetGroup` / `CatalogContent` / `ContentLoader` при выходе и повторном
входе; `git status` после всех Play — только свои правки (редактор ничего не переписал). `FindOrLoadSceneWithServices(ContentScene)`
проверен рантаймом (Menu, Lobby, Game из Startup и из моков).

**Кандидаты в [16]:**
1. Корень скоупа не принимает параметров, а `AddContent` нужен до первой загрузки группы, т.е. `ContentOptions` приходит снаружи
   корня. Лямбда в `LoadRoot` компилируется и падает только в рантайме. Предложения: (а) `LoadRoot(ContentOptions, construct)` —
   фреймворк сам зовёт `AddContent` перед `construct` (заодно снимает 4 копии имени каталога из [10], если имя брать из `Framework.json`);
   или (б) диагностика генератора/анализатора на лямбду в `LoadRoot` / `ScopeLoadOptions` (сейчас только рантайм `GeneratedScopeIds.cs:63`);
   (в) док `COMMON_CONTAINER.md`/`CONTENT.md`: пример корня с `ContentOptions` через локальную функцию и `ContainerScopeParent(..., "Outer+Construct")`.
2. Hierarchy bindings: замена дочерних объектов на экземпляры префаба оставляет в массиве bindings `{fileID: 0}` без ошибки — ни
   Regenerate All ([12]), ни валидатор этого не поймали до Play. Валидатору стоит падать на null-элементе массива bindings.

**Отклонения:** location-сцена «напрямую» проверена как Game + локация (у сцен локаций нет своего мока). Полный прогон тестов не
делался (две точечные правки; тест чит-панели вызван рефлексией в редакторе). Редактор worktree остановлен (`control editor stop`).

### [16a] Сжатие контента и verify.sh
Коммиты фреймворка (`content-directories`, не запушены): `377a30bd` [Content] каталог WebGL с Brotli как `<file>.br` (BREAKING),
`d85df041` [Tools] [Deploy-client] verify.sh; аннотированный тег `v0.7.0-rc.2` = `d85df041` (локально). pixel-swarm worktree:
`724bf8146` (указатель `tools/framework` → `v0.7.0-rc.2`, объекты взяты fetch'ем из `/projects/framework`; `client/deploy/nginx.conf`, `README.md`).

- **Решение:** не свой декодер в JS, а та же схема, что у `Build/*.br`: сборка пишет `<file>.br` (`ContentBrotli`, качество 11,
  `Parallel.ForEach`, заново каждую сборку в `Library/ContentBrotli/<name>`, без `BuildManifestHash.txt`), сервер шлёт
  `Content-Encoding: br` (общее правило `\.br$` уже есть в nginx игры, `stand/serve_tls.py`, `lan/serve.py`), браузер декодирует.
  Кэш API хранит **распакованные** байты: jslib читает `arrayBuffer`, проверяет размер по индексу и кладёт `new Response(buffer)` — один
  раз распаковка на файл, на чтении из кэша — ничего; плохой ответ (нет кодирования, `index.html` вместо файла) в кэш не попадает, кэш
  неверного размера удаляется; ошибка `N bytes instead of M from the server: the server must send the file with Content-Encoding: br ...`.
  По http браузер br не декодирует, но LAN-сборки и так `Disabled` (P3) → сырые файлы, как раньше. Условие сжатия: WebGL и не
  `IsPreloaded` и `compressionFormat == Brotli`; Gzip / Disabled / standalone / предзагрузка — сырые; Brotli + decompression fallback —
  `BuildFailedException` (ни одна игра его не включает). Индекс: `IsBrotli` / `ShippedSuffix`; URL `…/<file>.br` — он же ключ Cache API и
  `KeepOnly` (старые URL без `.br` удалятся у игроков сами). Лог: `R bytes raw, B bytes shipped (brotli|uncompressed)` — реальные байты
  вместо оценки (рефлексия `BrotliStream` вынесена в `BrotliStreams`).
- **verify.sh:** `grep -acF … >/dev/null` вместо `-q` (без SIGPIPE под pipefail); каталог: ровно `<file>.br` файлов `Library/Content/<name>`
  (без `BuildManifestHash.txt`), манифест `<hash>.json.br` на месте, каждый файл распаковывается в собранный (`xargs -P nproc`, 1 с на
  9218 файлов). Проверено на синтетике (PASS, порча одного файла и удаление файла → FAIL) и на сборках.
- **Тесты:** EditMode +9 (`ContentBuildTests`: brotli-файлы распаковываются в собранные, сырые для Gzip / предзагрузки / Linux, fallback
  падает; `ContentIndexWriterTests`: json хранит `IsBrotli`, preloaded+brotli бросает; `WebContentFetchTests`: URL с `.br`). `test-all.sh`
  exit 0: backend 1565, common 96, Verify 171, control 23, telemetry 84, EditMode 1209/1213 (4 skipped), PlayMode 115/139 (24 skipped);
  в `CLAUDE.md` EditMode 1204 → 1213.
- **Детерминизм (п.4):** да. `ContentSmoke` (`client/project`): 2 процесса редактора × 2 `ContentDirectoryBuilder.Build` → 4 раза
  `d304c0c7a48085eff5f2866bcd652b1f` (временный `executeMethod`, удалён). pixel-swarm: web и telegram (две копии) и сборка [13] — один
  манифест `32cee09064e7390c21d182cf29bcd2c1`; `d11daff2…` в [13] — почти наверняка снимок с незакоммиченными правками [14].
- **nginx pixel-swarm** (проверено в `nginx:stable-alpine` docker): `location ~* ^/StreamingAssets/.+\.br$` (и `^/tg/StreamingAssets/…` внутри
  `^~ /tg/`) перед общим `\.br$`: `Content-Encoding: br`, `Cache-Control: public, max-age=31536000, immutable`; `location /StreamingAssets/`
  — `no-cache`, `try_files $uri =404`. curl: `.cf.br` 200 br immutable, отсутствующий `.br`/файл — 404 (и в `/tg/`), `/Build/*.wasm.br` как
  раньше, `/nothing` по-прежнему `index.html` (SPA-фоллбек только вне StreamingAssets).
- **Сборка** `deploy-client.sh --notest --no-deploy`, exit 0: `build web PASS 621 s`, `build telegram PASS 616 s`, checks PASS: `content Game
  (9218 files, 29135874 bytes brotli of 300972235 raw)`. Лог: `Game: 9218 files, 300972235 bytes raw, 29135874 bytes shipped (brotli)`.
  **Сборки для [15]:** `/var/tmp/content_directories-builds-rc2/{website,telegram}` (49 MB каждая); старые `…lluFWG` и копии `…A6lVUz` удалены.

  | байт (br) | web | telegram |
  |-|-|-|
  | wasm | 3840639 | 3869118 |
  | data | 2134441 | 2135721 |
  | framework.js | 75124 | 75124 |
  | loader.js | 28175 | 28175 |
  | StreamingAssets/Content/Game | 29135874 (9218 `.br`; raw 300972235) | то же |

- **Headless стенд** (без кластера, `run.mjs --device desktop --end 40`, сервер — копия `serve_tls.py` с логом запросов, порт 8793): офлайн
  мета, 0 исключений, ни одной ошибки `[Content]`; к `StreamingAssets/Content` 713 запросов, все `.br` 200, **2 538 184 байт по сети**
  (9 896 778 распакованных) до конца прогона (старт + меню). Логи `scratchpad/stand2/`, `scratchpad/standlog/server.log`.
- **Не проверено:** повторное открытие из Cache API с новым `new Response(buffer)` (в [4] проверялся старый путь `response.clone()`);
  Safari / телефон — [15].

### [15] Замеры, стенд, устройство
- «До»: временный worktree main `db4968ca2` (`.claude/worktrees/step15_main`, Library reflink), `deploy-client web --notest --no-deploy`
  PASS 651 с; сборка оставлена в `/var/tmp/content_directories-step15/main-website` (18 MB), worktree и копии удалены.
- Размер: Build/ br main 5855686 → rc2 6078379 (**+217 KiB**, не −250…330): wasm −207 KiB, `data.unity3d` +494 KiB (индекс каталога;
  разбор `.data` скриптом `webdata.py`). Таблица — `_result.md`.
- Стенд: свой драйвер поверх `cdp.mjs`/`network.mjs` (один профиль Chromium на три открытия, троттлинг CDP) и HTTP/2-сервер
  (`serve_h2.mjs`, те же заголовки, что `serve_tls.py`); мета отрезана, утечек 0. Нагрузка машины: параллельно шли batchmode-сборки
  (load 3–9) — времена ориентировочные, одна серия.
- **Баг 1 (фреймворк):** загрузка уровня — ~4900 fetch'ей разом → `net::ERR_INSUFFICIENT_RESOURCES` (и по HTTP/1.1 официальным
  `run.mjs`: 520 ошибок), забег не стартует. Для измерений дальше — шим `fetch-limit.js` в копии `index.html` (≤64 fetch, слот до тела).
- **Баг 2 (фреймворк):** с шимом всё скачано, но сцены локаций: `undefined types ... stripped`, `PersistentTypeId:58` (CircleCollider2D),
  `colliders group Gaps has no colliders`. Addressables писали link.xml из бандлов (599 типов), ContentBuild — нет. Проба с link.xml
  не удалась: файл назван `Step15ProbeLink.xml` (Unity читает только `link.xml`) — wasm байт-в-байт тот же; повтор не делал (контекст).
  Временный файл удалён, worktree чист.
- Повторное открытие: 0 запросов к `Content/`, 6418 файлов в Cache API (новый путь `new Response(buffer)` работает), меню 3.4 с.
- Трейс запуска: `startupTrace` страницы содержит только спаны шаблона; `AssetLoads` / `load.*` уходят только телеметрией через мету
  (`Request StartupTrace is refused` офлайн) — без кластера не получить.
- Устройства: iPhone 11 / iPad 7 / Redmi 12 видны по USB, но заблокированы (DDI не монтируется, Safari не стартует, keyguard) — не
  проверены; LAN-сервер и `adb reverse` сняты.

### [16b] Очередь, link.xml, раскладка
Коммиты фреймворка (`content-directories`, не запушены): `df361352` [Content] типы каталога в сборке плеера, `b83e768f` [Content] очередь
загрузок веба (BREAKING), докам — см. ниже; тег `v0.7.0-rc.3`.

- **Типы каталога (п.2) — не link.xml, а штатный путь движка.** Сборка `BuildContentDirectory` пишет в каталог отчёта (`Library/BuildHistory/
  <время>-<guid>/`) `ScriptsOnlyCache.yaml` — шаг отчёта `Record type usage`: `usedScripts`, `serializedClasses`, `nativeClasses` (id классов
  движка). Addressables с content directories (форк до [8], `ContentDirectorySchemaBuilder` / `AddressablesPlayerBuildProcessor` под
  `ENABLE_CONTENT_DIRECTORIES`) отдавали его плееру через `BuildPlayerContext.AddPreviousBuildReportDirectory(BuildHistory.TryGetBuildReportDirectory
  (report.summary.buildSessionGuid))`; `report.packedAssets` у content-сборки пуст, в манифесте типов нет. Сделано так же:
  `ContentDirectoryBuilder.Build(..., out reportDirectory)` (нет каталога или файла → `BuildFailedException`), `ContentBuildOutput.ReportDirectory`,
  `ContentBuildPlayerProcessor.PrepareForBuild` → `AddPreviousBuildReportDirectory`. Тест `Prepare_RecordsTheTypesOfTheDirectoryForThePlayerBuild`
  (скрипт цели `Loadable` есть в файле). Смоук: `nativeClasses` 20 классов, `usedScripts` ContentSmokeRoot/Target.
- **Очередь (п.1) — в jslib, не в C#.** В C# каждый fetch опрашивается раз в кадр (`UniTask.Yield`): очередь там пропускала бы ≤N файлов за
  кадр и для чтений из Cache API (6418 файлов при 64 → ~100 кадров). В `WebContentFetch.jslib` слот берётся только перед сетевым `fetch` и
  держится до конца чтения тела (`.finally` освобождает и на ошибке / abort); `cache.match` слота не берёт; FIFO = порядок `Start` (= порядок
  запроса загрузки); abort в очереди удаляет ожидающего, сеть не трогается. Лимит — `ContentOptions._maxFetches` (`[Min(1)]`, по умолчанию 64,
  `Validate` бросает на < 1), до jslib через `ContentStores.Create(index, options)` → `WebContentFetch.Limit`. 64, а не 16–32: средний файл
  ~2.7 KB br, на 10 Мбит / 150 мс нужно ~70 в полёте (1.25 MB/s × 0.15 s / 2.7 KB), иначе латентность, а не канал, определяет время;
  nginx HTTP/2 по умолчанию 128 потоков. `ContentLoader.Register(lifetime, ContentOptions)` вместо `(name, remoteUrl)` (BREAKING), конструктор
  `ContentOptions(name, remoteUrl, maxFetches)`. Проверка очереди — node-харнесс jslib (scratchpad `jsqueue.test.js`: лимит 3, 10 fetch'ей,
  abort ожидающего и идущего → пик 3, FIFO, ожидающий не стартовал, `active 0, waiting 0` в конце) и стенд ниже. Тесты EditMode
  `ContentOptionsTests` (2: сцена без поля → 64, 0 → исключение).
- **test-all.sh** exit 0: backend 1565, common 96, Verify 171, control 23, telemetry 84, EditMode 1212/1216 (4 skipped), PlayMode 115/139
  (24 skipped); в `CLAUDE.md` EditMode 1213 → 1216.
- Доки: `010b524d` [Release] 0.7.0-rc.3 (`CONTENT.md` — очередь и типы каталога, `releases/0.7.0.md` шаг 9, `README.md`, `CLAUDE.md`);
  тег `v0.7.0-rc.3` = `010b524d` (локально). pixel-swarm worktree: `718e9b27f` (указатель → `v0.7.0-rc.3`, fetch из worktree фреймворка);
  кода игры правка не коснулась (`ContentLoader.Register` в игре не вызывается, поле `_maxFetches` сцен — по умолчанию 64).
- **Сборка** `KEEP_COPY=1 deploy-client.sh web telegram --notest --no-deploy` exit 0 (web 501 с, telegram 511 с, checks PASS: `content Game
  (9218 files, 29135458 bytes brotli of 300972379 raw)`). Сборки — `/var/tmp/content_directories-builds-rc3/{website,telegram}` (49 MB); rc2
  и копии удалены. wasm br 3848917 (+8278 к rc2 — сохранённые типы каталога), `.data` br 2135829.
- **Стенд** (те же драйвер / HTTP/2-сервер [15], без шима, без троттлинга, одна серия, load ~5): забег стартует, консоль rc3 —
  только ожидаемое офлайн `[Meta] ... out of reach` (как у main): ни `undefined types`, ни `has no colliders`, ни `ERR_INSUFFICIENT_RESOURCES`,
  0 failed. Пик одновременных запросов к `Content/` (по CDP) — ровно 64. Таблица — `_result.md` «Замеры [16b]». Evidence:
  `/projects/pixel-swarm/.claude/worktrees/content_directories/docs/media/research/content_directories/step16b/` (`r3`, `m3`, планы,
  `indexsize.py`, `jsqueue.test.js`, `archive_smoke.txt`).
- **Исследование раскладки (п.3).**
  - Параметры a10 (рефлексия `BuildContentDirectoryParameters`): `outputPath, rootAssetPaths, options, compression (BuildCompression),
    targetPlatform, subtarget, extraScriptingDefines, name, metadataPath, buildStartTimeTicks, precompiledTypeDB` (в публичном C# API
    `targetPlatform` не виден); `BuildContentOptions`: `None, UseArchive, DisableWriteTypeTree, CleanBuildCache, FailBuildWhenErrorsLogged,
    SerializeUnityVersion, SkipExportToOutputPath`. Группировки по корням / порога мелких объектов нет.
  - **`UseArchive`** на смоуке: 7 файлов (5 `.cf` + манифест + хэш) → **2**: `content0.archive` 576624 байт (всё, включая манифест) +
    `BuildManifestHash.txt`; рантайм регистрации держит `ArchiveHandles` (`ContentLoadManager.ContentDirectoryRegistration`). На pixel-swarm
    не пробовал (контекст): неизвестно, один ли архив на 300 MB или `content0..N`, и как он сжат (`compression`). Addressables-форк держал
    `ContentDirectoryArchiveMode` + `SkipExportToOutputPath` — значит, архивы — штатный путь движка.
  - **Индекс** (восстановлен из манифеста `indexsize.py`, как пишет `ContentIndexWriter`): json **1508265 raw / 295160 br** (`_files` 571858 /
    167937, `_objects` 916856 / 126728, `_scenes` 19371 / 7973); в `.data` он лежит внутри LZ4HC-бандла `data.unity3d` и стоит **+506130 br**
    (LZ4 перед brotli почти не жмётся) — в 1.7 раза дороже, чем сам json под brotli. Бинарный: файлы (хэш 16 + ext + varint размер) 175513 /
    161949, объекты с плоскими замыканиями 216245 / 115669, граф зависимостей (замыкание в рантайме) 48286 / 19317; бинарный итог с графом
    ~365 KB / **216 KB br**. Пол — 9218 × 16 байт случайных хэшей = 147 KB: пока файлов 9218, индекс < ~150 KB br не станет.
  - **Рекомендация:** (1) сначала проба `UseArchive` + `compression` на pixel-swarm (одна batchmode-сборка, скрипт `Probe16b` из `archive_smoke`
    — в evidence нет, восстановить по описанию: `BuildContentDirectory` c `options = UseArchive`, подсчёт файлов): если архивов мало и они
    читаются блоками, схема «нули + Range-запросы в один файл» (FS.write по смещению, оглавление архива → смещения файлов) даёт десятки
    запросов вместо тысяч и убирает 9218 хэшей из индекса; (2) иначе — склейка своих пачек на сборке (по сценам / корням каталогов) и нарезка
    на клиенте в Cache API по ключу файла; (3) HTTP/2 «как есть» с очередью 64 уже работает (WinterForest 15.6 с без троттлинга против 8.1 с
    main), но запросов в 280 раз больше, а байтов в 2 раза (br по файлу хуже LZ4 бандла: мелкие файлы со своими заголовками и type tree).
    Индекс: дешёвое уменьшение — вынести json из `data.unity3d` (отдельный файл каталога, качается в `Register` одним запросом и кэшируется
    по хэшу манифеста, или raw-файл `.data`): −506 KB br в `.data` за +295 KB (json) / +216 KB (бинарный) отдельной загрузки. Не реализовано:
    затрагивает старт без запроса (решение [3]) — нужно согласие оркестратора.
- **Не сделано:** серия с троттлингом 150 мс / 10 Мбит (rc3 и main), проба `UseArchive` на pixel-swarm, устройства.

### [16c] UseArchive и индекс (поколение 1: разведка)
Пробы (временные, удалены из `Assets`, копии в evidence `/projects/pixel-swarm/.claude/worktrees/content_directories/docs/media/research/content_directories/step16c/`:
`probe/Probe16c.cs` — смоук `client/project`, `probe/Probe16cGame.cs` — pixel-swarm, `probe.log`, разбор `archive.py`, `partial.py`, `ranges.py`,
`order.py`, итог `ranges.txt`; выходы pixel-swarm без type tree — `client/Library/Probe16c/NoTT_{None,LZ4}` worktree pixel-swarm).

- **Сколько архивов.** Сборка всегда пишет один `content0.archive` (+ `BuildManifestHash.txt`): в бинарнике редактора есть только строка
  `content0.archive` (`Modules/ContentBuild/Editor/Ucbp/BuildArchiveHelper.cpp`), параметров размера / группировки нет. Рантайм
  (`ContentLoadManager.MountAndRegisterArchives`, декомпиляция) монтирует **все** `content0..999.archive` папки
  (`ArchiveFileInterface.MountAsync` в namespace `contentload`, затем `CAHFileSystem.RegisterArtifactDirectory(mountPath)`); без архивов —
  папку как раньше. pixel-swarm: один архив на весь каталог.
- **Сжатие** (`BuildContentDirectoryParameters.compression`, `BuildCompression`): `Uncompressed`, `LZ4` (= LZ4HC), `LZ4Runtime` (LZ4), `LZMA`,
  блок 128 KiB (`_blockSize` приватный, рефлексией ставится, 16 KiB проверен). Без поля — несжатый архив (так было в [16b]). Смоук: None
  7 файлов 579 KB; архив Uncompressed 579792, LZ4 150464, LZ4Runtime 191184, LZMA 69329 (один блок на всё), LZ4 16 KiB 164320.
- **Формат** — UnityFS v9 (`UnityFS`, `5.x.x`): заголовок + LZ4HC-сжатое оглавление (блоки: u32 размер, u32 сжатый, u16 флаги, **i64 смещение
  в данных** — новое в v9; узлы: i64 смещение, i64 размер, u32 флаги, имя). **Каждый файл начинается с нового блока** (блоки не переходят
  через границу файла), значит у каждого файла свой непрерывный диапазон байт архива. Узлы — имена `.cf` (флаг 4), `.resource` и манифест
  `.json` (флаг 0). LZMA — один блок на весь архив: читается только целиком.
- **Чтение по блокам — да.** Смоук, Play Mode редактора (`probe.log`, `play1`): архив той же длины, где настоящие только заголовок,
  оглавление и блоки манифеста + корней (3875 байт из 150464), остальное нули → `registered ... valid True`, корень получен; затем блоки цели
  дописаны **в смонтированный архив** → `target loaded: smoke target`; блоки сцены → `scene loaded: True`. Монтирование читает только
  заголовок и оглавление, файлы — по требованию. То есть схема «нули + докачка» работает и на архиве.
- **Свой порядок файлов — да.** `ContentBuildInterface.ArchiveAndCompress(ResourceFile[], path, BuildCompression.LZ4)` над выходом обычной
  сборки (имена файлов как `fileAlias`, `serializedFile` для `.cf`) даёт тот же формат (блок на файл, v9) **в заданном порядке**; такой архив
  монтируется и грузится частично так же (`play2`). Несколько архивов (`content1..N`) возможны тем же API, но не нужны.
- **Манифест** — узел `<hash>.json` внутри архива; `BuildManifestHash.txt` снаружи (движок читает его до манифеста).
- **pixel-swarm, байты** (сборки в worktree, цель WebGL, `probe.log`): с type tree LZ4-архив **41.0 MB**, Uncompressed 301 MB, LZMA 9.6 MB —
  повторяющиеся type tree каждого из 9218 файлов. `BuildContentOptions.DisableWriteTypeTree` (требует `SerializeUnityVersion`, иначе
  «Unable to use flags DisableWriteTypeTree unless SerializeUnityVersion is also specified»): raw 225 MB, LZ4-архив **15.6 MB**, LZMA 8.9 MB,
  пофайловый br **10.9 MB** (с type tree 31.5 MB при q9 / 29.1 MB q11). Type tree — 2/3 байтов rc3. Каталог собирается вместе с плеером, так
  что type tree не нужны (тот же код типов); основная причина «байты ×2 к main» — они, а не brotli.
- **pixel-swarm, запросы** (фазы стенда r3 [16b], файлы rc3 сопоставлены файлам сборки без type tree по `(guid, lfid, kind)` манифестов,
  `.resource` по имени; байты = сжатые диапазоны архива; «зазор» — соседние нужные диапазоны с промежутком ≤ N байт сливаются в один запрос):

  | порядок архива / зазор | меню: запросов / байт | забег WinterForest | HighFields |
  |-|-|-|-|
  | main (Addressables) | 13 / 1.62 MB | 10 / 8.74 MB | 3 / 0.89 MB |
  | rc3 (файлы `.br`) | 713 / 2.57 MB | 5870 / 18.2 MB | 767 / 3.17 MB |
  | движка, type tree, 0 | 689 / 2.78 MB | 5631 / 23.8 MB | 741 / 5.59 MB |
  | движка, без type tree, 0 | 691 / 1.35 MB | 5608 / 7.88 MB | 734 / 3.66 MB |
  | движка, без type tree, 4 KiB | 210 / 1.54 MB | 143 / 8.43 MB | 172 / 3.87 MB |
  | первого использования, без type tree, 0 | 197 / 1.35 MB | 226 / 7.88 MB | 24 / 3.65 MB |
  | первого использования, без type tree, 4 KiB | 103 / 1.42 MB | 61 / 8.03 MB | 24 / 3.65 MB |
  | **первого использования, без type tree, 16 KiB** | **69 / 1.69 MB** | **35 / 8.24 MB** | **23 / 3.67 MB** |

  Порядок движка раскидывает файлы меню и забега по всему архиву; «первого использования» = манифест, замыкание корней, замыкание каждой
  сцены, замыкание каждого объекта (`order.py`, как `ContentFileClosure`). Фиксированные страницы (32–256 KiB) хуже: меню 5.6–12.8 MB.
  Плюс при регистрации: заголовок + оглавление архива **381 KB** (LZ4HC, 713 KB raw: 10831 блок × 18 + 9218 узлов) — один Range-запрос.
- **Вывод разведки:** `UseArchive` сам по себе (порядок движка) критерий не проходит (сотни–тысячи диапазонов). Проходит по оценке
  схема «один архив в порядке первого использования + Range»: десятки запросов, байты меню/забега ≈ main (1.69 / 8.24 MB против 1.62 / 8.74),
  HighFields 3.67 MB против 0.89 (main держал общее в бандлах забега; сумма забег+HighFields 11.9 против 9.6 MB), + 381 KB оглавления на
  старте. Оценка — на файлах фаз rc3, не замер; проверка — стенд rc4.
- **Выбранная схема (реализация — следующее поколение, см. `handoff/16c_g1.md`):** сборка — обычный `BuildContentDirectory` с
  `DisableWriteTypeTree | SerializeUnityVersion` → индекс из файлов (как сейчас, `ContentFileClosure`) → порядок первого использования →
  `ArchiveAndCompress(LZ4)` в `content0.archive` → разбор оглавления UnityFS (C#: смещение и длина каждого файла в архиве, длина заголовка) →
  индекс хранит диапазоны вместо имён. Рантайм — `FetchedContentStore` держит один файл архива нулями полной длины (15 MB вместо 300 MB raw
  нулей rc3 в памяти страницы), регистрация качает `[0, header)` + диапазоны манифеста и корней, загрузка — недостающие файлы сливаются в
  Range-запросы (зазор — опция), запись по смещению (`FS.write`), Cache API по ключу файла (`<url>#<offset>`), архив шипится как
  `<hash>.archive` (immutable, несовпадение = 404). Brotli поверх архива не нужен (блоки LZ4). Индекс — отдельный файл каталога
  `<hash>.index.json(.br)`, хэш в плеере (32 байта в Resources).

### [16c] UseArchive и индекс (поколение 2)
- **Проба WebGL (go).** Смоук `client/project`, временные `Assets/Probe16c/Editor/Probe16cWeb.cs` + `Assets/ContentSmoke/Probe16cWebRun.cs` (+ jslib),
  удалены, копии в evidence `step16c/web_probe/` worktree pixel-swarm. Каталог без type tree → `ContentArchiveWriter` (порядок первого
  использования, `ArchiveAndCompress(LZ4)`) → 136784 байт, заголовок 528; WebGL-плеер (headless Chromium, `measurements/tools/smoke.mjs`):
  архив нулями полной длины в MEMFS (`SetLength`), реальны заголовок + манифест + корни (1945 байт) → `RegisterContentDirectory` valid,
  корень получен; блоки цели (426 байт) дописаны **в смонтированный архив** → `target loaded: smoke target`; блоки сцены (134594) →
  `scene loaded: True`.
- **Находка:** без мер движок пишет в консоль `Archive file was modified while it was opened, and can no longer be accessed` на каждую
  загрузку после дозаписи (загрузки при этом проходят). Восстановление времени через `File.SetLastWriteTimeUtc` не помогает (MEMFS хранит
  мс, а `utime` меняет и `ctime`). Помогает вернуть `node.mtime` / `node.ctime` (и `atime`) узла MEMFS к значениям на момент монтирования
  сразу после `FS.write` в jslib → консоль без ошибок (`run3_time_restore.json`: `errors []`). В реализации запись по смещению и
  восстановление времени — в `WebContentFetch.jslib`.
- **Сборка архива (коммит `577b05ea`).** `client/core/Editor/Content/Archive/`: `ContentArchiveOrder` (манифест, замыкание корней, сцен,
  объектов, остальное; каждый файл один раз, неизвестный файл → исключение), `ContentArchiveWriter` (`ArchiveAndCompress(LZ4)` →
  `content0.archive` + `BuildManifestHash.txt`, сверка числа узлов), `ContentArchiveReader` (UnityFS v9, оглавление LZ4 через `Lz4Block`,
  `ContentArchiveLayout`: длина, `HeaderLength`, `ContentArchiveSpan` каждого файла; оглавление в конце / не LZ4 / файл не с начала блока →
  исключение). **Блоки выровнены на 16 байт** — между блоками одного файла и между файлами до 15 байт паддинга (g1 этого не видел; спан
  файла включает паддинг внутри). Тесты EditMode в `ContentIndexWriterTests` (+5): 14/14. В `ContentBuild` ещё не подключено.
- **Без type tree (коммит `b0d4475c`).** `ContentDirectoryBuilder.Options = FailBuildWhenErrorsLogged | DisableWriteTypeTree |
  SerializeUnityVersion`; `ContentBuildTests` 16/16. **Редактор отказывается регистрировать такой каталог**: `InvalidOperationException:
  Cannot register a content directory in the Editor whose content was built without type trees` (проверено: PlayMode `Content` 12/17 упали,
  когда тестовая сборка `ContentDirectoryTestBuild` шла с этими опциями) — это и есть «понятная ошибка несовпадения» на стороне движка;
  тестовая сборка PlayMode оставлена с type tree (откат), Play Mode игры читает ассеты (`EditorContentDirectory`), `ContentDirectoryBuilder`
  зовут только сборки плеера (`ContentBuild.Prepare`). Плеер видит только каталог своей сборки (хэш манифеста в индексе).

### [16c] UseArchive и индекс (поколение 3)

- **`041103a9` [Content] Fetch the archive of a content directory in Range requests, the index a file of its own (BREAKING)**:
  - сборка: `ContentBuild.Prepare` → `ContentDirectoryBuilder` → `ContentArchiveWriter.Write(manifest, Library/ContentArchive/<name>)` →
    `ContentIndexWriter.Write(manifest, layout)` (файлы со спанами по порядку архива; `ArchiveLength`, `Header`) → `ContentShipment.Write`:
    локально (standalone, preloaded) — папка архива как монтируется + `<hash>.index.json`; WebGL fetched — `Library/ContentShipped/<name>`:
    `<hash>.archive` (копия как есть) + `<hash>.index.json(.br)`. В Resources — `ContentStamp` (хэш, preloaded, brotli-индекс),
    `Assets/FrameworkContentStamp`. Preload manifest — `content0.archive` + `BuildManifestHash.txt` (индекс — файл StreamingAssets, качается;
    не проверено: ни одна игра не предзагружает). Пофайловый `.br` каталога удалён (`ContentBrotli` жмёт только индекс).
  - рантайм: `ContentLoader.Register` → `ContentStamp.Load` → `ContentStores.Open` (веб: `WebContentIndex` — jslib-fetch файла в MEMFS,
    Cache API по URL, проверка `{`; иначе `File`), `stamp.Check(index)`; `FetchedContentStore(IContentRangeSource, path, gap)` —
    `content0.archive` нулями `SetLength`, `BuildManifestHash.txt`, Prepare = header+manifest+roots одной пачкой, `source.Pin(archive)`
    перед регистрацией; `ContentFileFetches` → `ContentRanges.Merge` (зазор `ContentOptions.RangeGap` 16 KiB, файлы между — `Files` диапазона,
    становятся real), `ContentRangeFetch` (дедуп по файлу, отмена по lifetime последнего ожидающего). `WebContentArchive` → jslib
    `WebContentFetch_StartRange` (Range, требует 206, длину, без `Content-Encoding`; запись `FS.write` по смещению; Cache API на файл
    диапазона под `<url>?offset=<n>`; из кэша — только если есть все файлы диапазона), `WebContentFetch_Pin` (fail fast без `node.mtime`).
    Очередь/кэш вынесены в `WebContentQueue.jslib` (файлы ≤300 строк).
  - **Ключ Cache API — `?offset=`, не `#offset`**: Cache API сравнивает URL без фрагмента (exclude fragment flag), все `#n` совпали бы.
  - тесты: EditMode `ContentRangesTests` (5), `ContentFileFetchesTests` (+2: соседи одним запросом, файл бегущего запроса),
    `WebContentArchiveTests` (5), `ContentIndexWriterTests` (спаны, json, пересечения), `ContentBuildTests` (шипинг 3 вариантов, stamp);
    PlayMode `ContentDirectoryTests`/`ContentLoaderTests`/`AssetGroupTests` через `CopyContentRangeSource` (архив тестовой сборки с
    type tree, регистрация одним запросом, нули вне, докачка цели) — LocalStore с `<hash>.index.json` внутри папки регистрируется.
  - `verify.sh`: ровно `<hash>.archive` + `<hash>.index.json.br`, архив = `Library/ContentArchive/<name>/content0.archive`, `UnityFS`,
    индекс (jq) = хэш, длина архива, число файлов. Доки `CONTENT.md`, `WEBGL_TROUBLESHOOTING.md` B15.
  - `tools/scripts/test-all.sh` exit 0: backend 1565, common 96, Verify 171, control 23, telemetry 84, EditMode 1231 (4 skipped),
    PlayMode 139 (24 skipped); `CLAUDE.md` 1216 → 1231.
- **`402395e9` [Release] 0.7.0-rc.4** — `releases/0.7.0.md` шаг 10 (nginx `.archive`: immutable, без Content-Encoding/gzip, Range; индекс
  `.br`), `README.md`; тег `v0.7.0-rc.4` локально.
- **Не проверено**: новый jslib в браузере (Range/Pin/кэш) — PlayMode проверяет только C#-сторону; первый шаг g4 — WebGL-смоук.


### [16c] UseArchive и индекс (поколение 4)
- **WebGL-смоук rc.4 (`client/project`) — PASS.** Временный `Assets/Editor/ContentSmokeWebGl.cs` (`BuildPipeline.BuildPlayer` WebGL, удалён
  вместе с побочным `client/project/Data/`), лог сборки: `Smoke: 7 files, 537804 bytes raw, archive 136784 bytes (header 528)`; в
  `StreamingAssets/Content/Smoke/` — `<hash>.archive` + `<hash>.index.json` (в client/project сжатие выключено — индекс raw). Скрипт —
  scratchpad `g4/content-smoke.mjs` (копия `smoke.mjs` с `--reload` и Range: один диапазон, `206`, `Content-Range`, без `Content-Encoding`).
  Первое открытие: `[ContentSmoke] done` за 5.7 с, к `Content/` 4 запроса: индекс, `bytes=0-1971` (заголовок + манифест + корни — регистрация),
  `bytes=136608-136772` (цель), `bytes=1984-136592` (сцена); строки `registered Smoke: root Root`, `loaded 'smoke target', loaded True`,
  `scene loaded: Level`. Второе открытие: `done`, **0 запросов к Content** (индекс и диапазоны из Cache API). `errors []` (нет
  `Archive file was modified`). Фикса фреймворка не потребовалось → rc.5 не нужен.
- **pixel-swarm `61b3e812a`:** `tools/framework` → `v0.7.0-rc.4` (fetch тега из `/projects/framework`); `client/deploy/nginx.conf`:
  `location ~* ^/StreamingAssets/.+\.archive$` (immutable, `try_files $uri =404`, без `Content-Encoding`) перед `\.br$`, то же в `^~ /tg/`
  (без `try_files` — под `alias`); README деплоя. Кода игры шаг 10 не касается (только комментарий в `LevelContentReferenceTests`).
  Проверка в docker `nginx:stable-alpine` (scratchpad `g4/nginx`): `/` и `/tg/` — `Range: bytes=10-99` → `206`, `Content-Range`, без
  `Content-Encoding` при `Accept-Encoding: gzip, br`; 200 с `Accept-Ranges: bytes`; индекс `.br` → `Content-Encoding: br`, immutable;
  отсутствующий `.archive` → 404; `/tg/` отдаёт файлы telegram-сборки.
- **`deploy-client.sh web telegram --notest --no-deploy` (rc.4) exit 0:** web/telegram PASS 557 с, checks `content Game (9218 files, archive
  15565776 bytes, 15885523 bytes shipped)`; сборки → `/var/tmp/content_directories-builds-rc4/{website,telegram}` (21 MB каждая), копии удалены.
  Build/ br: data 1645386, wasm 3868209, framework 76006, loader 28175 = 5617776 (main 5855686, −237910). Индекс `.index.json.br` 319747.
- **Стенд rc.4** (scratchpad `g4/stand`: `drive.mjs`/`mkplan.py`/`summ.py` из s16, `serve_h2.mjs` + Range/206 и серверный шейпинг
  `SHAPE_LAT_MS`/`SHAPE_KBIT`; машина не тихая — открыты 3 чужих редактора pixel-swarm): без троттлинга rc.4 близко к main
  (262 запроса / 11.2 MB / забег 6.1 с против 24 / 10.6 MB / 5.0 с). **С троттлингом 150 мс / 10 Мбит — меню 24 с против 9 с, забег 51 с
  против 19 с** (CDP и серверный шейпинг одинаково). Лог сервера: диапазоны архива приходят по одному, ровно через 150 мс (запрос
  начинается после конца предыдущего) — **Chromium сериализует запросы одного URL через запись HTTP-кэша** (cache lock); без латентности
  не видно. Проба: в копии сборки `framework.js` с `cache:"no-store"` в init диапазона (`website-nostore`) — меню 10.6 с, забег 20.4 с
  (main 9.6 / 19.5).
- **Фикс (тест сначала):** `WebContentFetchJslibTests.RangeRequest_BypassesTheHttpCache` (EditMode, текст jslib: браузер в EditMode не
  запустить) — упал до фикса, прошёл после; `WebContentFetch.jslib`: `cache: 'no-store'` в init диапазона + комментарий; `CONTENT.md`,
  B15, `CLAUDE.md` EditMode 1232, `releases/0.7.0.md` шаг 10 (rc.5, действий нет), `README.md` → rc.5.
- **`v0.7.0-rc.5`:** `440b8466` [Content] Range requests past the HTTP cache, `0a07e2d0` [Release] 0.7.0-rc.5; тег `v0.7.0-rc.5` = `0a07e2d0`
  (локально). `test-all.sh` exit 0: backend 1565, common 96, Verify 171, control 23, telemetry 84, EditMode 1232 (4 skipped), PlayMode 139
  (24 skipped); лог `.build/test-all/logs/20261110-025431`.
- **pixel-swarm `5e63873e8`:** указатель → `v0.7.0-rc.5`. `deploy-client.sh web telegram --notest --no-deploy` exit 0 (431 с, checks PASS,
  в `framework.js` есть `cache:"no-store",headers:{Range:`); сборки → `/var/tmp/content_directories-builds-rc5/{website,telegram}`.
- **Замеры** rc.5 против main (без троттлинга, CDP 150 мс / 10 Мбит, серверный шейпинг) — таблица «Замеры [16c]» в `_result.md`; консоль rc.5
  чистая (только офлайн мета). Evidence: `/projects/pixel-swarm/.claude/worktrees/content_directories/docs/media/research/content_directories/step16c/g4/`
  (не коммитить). Удалены: `/var/tmp/content_directories-builds-rc3`, `-rc4` (вкл. `website-nostore`), `client/Library/Probe16c`; main оставлена.

### [16d] Запросы и объём архива (поколение 1: анализ)
Скрипты — evidence `/projects/pixel-swarm/.claude/worktrees/content_directories/docs/media/research/content_directories/step16d/` (`sim.py`, `run1..3.py`; нужен python с `lz4`): сырые файлы rc.5 из архива, фазы — диапазоны лога сервера
стенда r5 (`g4/r5.server.log.gz`: первые 105 запросов к контенту — меню, 157 — забег, 12 — HighFields, 8 — хвост), «нужные» файлы — целиком
внутри запрошенных диапазонов (с заполнителями зазора 16 KiB, т.е. оценка сверху), вызовы fetch — кластеры запросов с разницей прихода ≤ 2 мс.
Оценка сходится с замером: меню 98 / 2.14 MB (замер 104 диапазона), забег 156 / 8.36 (152). `lz4.block` HC 12 по-файлово даёт ровно 15.19 MB
данных архива rc.5 — модель сжатия точна.

- **Регистрация — 909 KB одним запросом**: оглавление 378 KB (713 KB raw: 10831 блок × 18 + 9218 узлов с 32-hex именами) + манифест движка
  `<hash>.json` 520 KB (1.43 MB raw) + корни 10 KB; плюс индекс 320 KB br. То есть из 2.82 MB меню 1.23 MB — метаданные; у main меню 1.62 MB всего.
- **RangeGap** (порядок rc.5, по вызовам fetch; запросов / MB):

  | зазор | меню | забег | HighFields |
  |-|-|-|-|
  | 16 KiB | 98 / 2.14 | 156 / 8.36 | 12 / 3.62 |
  | 64 KiB | 66 / 3.33 | 112 / 9.80 | 12 / 3.62 |
  | 256 KiB | 43 / 6.74 | 80 / 14.18 | 11 / 3.79 |
  | 1 MiB | 23 / 18.8 | 44 / 32.9 | 3 / 9.04 |

  Зазор не рычаг: файлы меню и забега разбросаны по архиву, рост зазора тянет чужие байты быстрее, чем убирает запросы. Число запросов
  ограничено снизу числом вызовов fetch (меню ~9, забег ~27 кластеров).
- **Порядок — рычаг.** Что качает меню (1043 файла): 452 иконки `Art/UI/Icons/Upgrades`, деревья апгрейдов, спрайты снарядов, звуки UI —
  группы каталогов и ленивые цели, а их файлы уже разложены раньше по замыканиям сцен (сцены идут до объектов, `WinterForest` забирает общее)
  и в порядке манифеста. Порядки (по-файлово LZ4, зазор 16 KiB):

  | порядок | меню | забег | HighFields |
  |-|-|-|-|
  | rc.5 (манифест, корни, сцены, объекты) | 98 / 2.14 | 156 / 8.36 | 12 / 3.62 |
  | статический: корни, группы `ContentRoot`, сцены | 85 / 2.73 | 277 / 9.39 | 12 / 3.63 |
  | статический: корни, сцены, группы | 82 / 2.68 | 274 / 9.38 | 13 / 3.63 |
  | профиль по ключам загрузок (приближение из лога) | 14 / 2.16 | 76 / 8.52 | 8 / 3.63 |
  | идеальный (файлы в порядке трассы, верхняя граница) | 9 / 2.10 | 27 / 8.12 | 2 / 3.62 |

  Статика из графа корня хуже (замыкания групп велики и перемешаны с тем, что качается позже). Профиль — порядок первых загрузок ключей
  (guid / local id объекта, сцена), записанный в прогоне игры; сборка кладёт замыкания ключей в этом порядке, остальное как сейчас. При
  профиле зазор 16/64/256 KiB даёт одно и то же — оставить 16 KiB, опция `ContentOptions.RangeGap` остаётся.
- **Объём +30%.** Бандлы main — тоже LZ4HC (флаги блоков 3, 1772 блока; 12.10 MB сжатых из 223 MB raw, type tree внутри), то есть разница не
  в кодеке и не в type tree: rc.5 сжимает **каждый файл отдельно** (9218 потоков LZ4 с нуля, блок не переходит границу файла, паддинг 16 байт),
  main — 32 больших потока. Оценки (порядок профиля; меню / забег / HighFields первого захода):

  | раскладка | данные архива | меню | забег | HighFields |
  |-|-|-|-|-|
  | по-файлово LZ4HC 128 KiB (rc.5) | 15.19 MB | 2.16 | 8.52 | 3.63 |
  | сплошной поток LZ4HC 128 KiB (блоки через границы файлов) | 12.41 | 2.34 | 9.21 | 4.00 |
  | сплошной LZ4HC 256 KiB / 1 MiB | 12.15 / 11.95 | 2.60 / 5.27 | 11.3 / 24.3 | 4.60 / 6.79 |
  | по-файлово LZMA 128 KiB | 11.58 | 1.78 | 7.17 | 2.08 |
  | сплошной LZMA 128 KiB | 9.31 | 1.95 | 7.77 | 2.46 |
  | LZ4 fast (`LZ4Runtime`), сплошной | 18.1 | — | — | — |

  Больший блок LZ4 почти не жмёт (окно LZ4 64 KiB) и тянет чужие байты; сплошной LZ4 −18% архива, но +0.2–0.7 MB к загрузкам (блок с соседями)
  и требует своего писателя UnityFS (перекрывающиеся спаны файлов). LZMA по-файлово — −24% архива и меньше байт каждой загрузки
  (HighFields −1.5 MB); движок пишет LZMA одним блоком на архив, нужен ли и работает ли многоблочный LZMA в смонтированном архиве и сколько
  стоит распаковка в wasm — не проверено (проба редактора). **Brotli поверх Range невозможен**: `Content-Encoding` относится ко всему
  представлению, `Range` режет уже сжатый поток, середину brotli-потока не распаковать; `Transfer-Encoding` сжатия браузеры не шлют, HTTP/2
  его запрещает.
- **HighFields** — осознанная разница: 3.6 MB — замыкание сцены `Game_HighFields` (416 объектов `Art/Research/Recreate/HighFields` + враги);
  main получил её ещё в первом заходе внутри общего бандла сцен `scenes_gameplay` (3.48 MB, сцены всех уровней), т.е. заплатил вперёд. Подтянуть
  раньше без вреда меню/забегу нельзя (это те же байты раньше); дёшево только сжать (LZMA: 2.1 MB) — порядок тут не помогает (8 запросов).
- **Выбор g1:** (1) профиль порядка загрузок — во фреймворк (запись порядка ключей в Play Mode / плеере, файл игры, `ContentArchiveOrder` кладёт
  его ключи первыми); (2) LZMA по-файлово — только после пробы движка (многоблочный LZMA монтируется и читается частично, время распаковки);
  сплошной LZ4 и рост зазора — нет.

### [16d] Реализация (поколение 1)
- **`4af74831` [Content] Lay out the archive in the recorded load order of the game:** `client/core/Editor/Content/Order/`
  (`ContentLoadKey` — строка `object <guid> <local id>` / `scene <guid>`; `ContentLoadOrder` — `ProjectSettings/ContentLoadOrder/<name>.txt`,
  `Read`, `IsRecording` в EditorPrefs на проект, `Clear`; `ContentLoadOrderRecorder` — дописывает только новые ключи; `RecordingContentDirectory` —
  декоратор `IContentDirectory`, пишет каждый `FetchObject`/`FetchScene`; `ContentLoadOrderMenu` — `Tools/Content/Record Load Order` / `Clear
  Load Order`). `EditorContentDirectoryInstall` оборачивает каталог Play Mode, пока запись включена (`EditorContentDirectories` теперь
  `Func<string, IContentDirectory>`). `ContentArchiveOrder.Of(manifest, loads)` — замыкания ключей сразу после корней (объект без своего файла —
  файл ассета, как `ContentIndex.ObjectFiles`), неизвестные ключи пропускаются; `Known` — для лога `[ContentBuild] ... N of M keys`.
  `ContentArchiveWriter.Write(manifest, loads, output)`, `ContentBuild` читает порядок игры. Тесты: `ContentLoadOrderTests` (5),
  `ContentIndexWriterTests` +2 (ключи первыми, сцена первой); фильтр content-тестов 42/42.
- **`75c7adad` [Release] 0.7.0-rc.6** — `releases/0.7.0.md` шаг 11 (по желанию: записать порядок), `README.md`; тег `v0.7.0-rc.6` локально.
  `test-all.sh` exit 0: backend 1565, common 96, Verify 171, control 23, telemetry 84, EditMode 1239 (4 skipped), PlayMode 139 (24 skipped);
  `CLAUDE.md` 1232 → 1239 (в коммите релиза).
- **Запись порядка pixel-swarm:** редактор worktree (bridge `execute_code`): `ContentLoadOrder.IsRecording = true`, `SessionState
  Autorun.Query = autorun=1` → Play со `Startup` → `[Autorun] run started` через 10 с, ~40 с забега; затем `autorun=1&level=HighFields`;
  запись выключена. 21 ключ: группы Internal / Global / Prefabs Global / Sprites UI, UIIcons / Audio Ui, Music → сцена Menu → GamePlay, Levels →
  сцены Game, WinterForest → WinterForestEncounter, Swarm, Prefabs GamePlay, Sfx, MusicRun, Wanderer, TurtleMech → сцена HighFields,
  HighFieldsEncounter. Лог Play Mode без исключений (кроме офлайн меты).
- **pixel-swarm `c79a64d48`:** `tools/framework` → `v0.7.0-rc.6`, `client/ProjectSettings/ContentLoadOrder/Game.txt`.
- **`deploy-client.sh web telegram --notest --no-deploy` (rc.6) exit 0**, оба раза `DEPLOY-CLIENT PASSED` (первый прогон без `KEEP_COPY` —
  сборки удалены вместе с копиями; второй с `KEEP_COPY=1`, лог `/tmp/content_directories-deploy-client/20261110-051132`). checks web / telegram
  PASS: `content Game (9218 files, archive 15564544 bytes, 15881273 bytes shipped)`, лог сборки `[ContentBuild] Game: load order
  ProjectSettings/ContentLoadOrder/Game.txt, 21 of 21 keys in the directory`. Сборки → `/var/tmp/content_directories-builds-rc6/{website,telegram}`
  (21 MB каждая). Build/ br website: wasm 3868209, data 1647085, framework 76028 (telegram: wasm 3869749, data 1646810). Архив
  `1694ca833c0e2af7eab8bd569db2e59d.archive` 15564544 (rc.5 15565776 — порядок объём не меняет), индекс `.index.json.br` 316729.
- **Предварительно (не серия, решение пользователя — замеры в следующей сессии):** до остановки успели два прогона стенда без троттлинга
  (load ~8–14, чужие редакторы и сборки): main `m6` — первый заход 24 запроса / 10.67 MB (меню 13 / 1.70 MB 2.8 с, забег 10 / 8.74 MB 5.3 с),
  HighFields 4 / 1.05 MB; rc.6 `r6` — **27 запросов / 10.80 MB** (меню 10 / 2.57 MB 2.9 с, забег 10 / 7.94 MB 6.0 с, хвост 7 / 0.29), HighFields
  **2** / 3.67 MB, повторное 0. Против rc.5: 262 → 27 запросов. Evidence `step16d/stand/{m6,r6}` (pixel-swarm, не коммитить); `m6t` прерван, удалён.


### [16d] g2: замеры
- Стенд `step16d/stand` (pixel-swarm worktree, не коммитится): `go.sh <build> <name> 0 0 0` (без троттлинга) и `go.sh <build> <name> 0 150 10240`
  (CDP 150 мс / 10 Мбит), серии `series_g2.sh` (круг 1), `series_g2c.sh` (круги 2–4; ждёт load < 4 не дольше 2 минут), сводка
  `python3 stat_g2.py <stand>` (медиана, мин–макс, разницы в парах), по прогону — `summ.py g2_?_??`. Журнал с нагрузкой — `series_g2.out`.
- Каталоги: `g2_m_n1..4`, `g2_r_n1..4` (без троттлинга), `g2_m_t1..4`, `g2_r_t1..4` (150 мс / 10 Мбит); `m` — main, `r` — rc.6.
  Предварительные `m6` / `r6` в серию не взяты (шли при load 8). `loaded/g2_m_n2` — прогон на старте чужого `deploy-client`, заменён;
  `failed/g2_m_t3` — стенд упал на `Page.captureScreenshot: Internal error` во второй навигации (main, load 11.5), перепрогнан.
- Нагрузка: круг 1 — load 2.4–3.8; затем чужой `deploy-client` и 4 чужих редактора, порог 3.5 не наступил за час (`series_g2b.sh`
  остановлен по решению оркестратора), круги 2–4 сняты при load 5–22. Запросы и байты одинаковы во всех прогонах, времена шумные.
- Итог (таблица «Замеры [16d]» в `_result.md`): rc.6 меню 10 запросов / 2.57 MB, забег 10 / 7.94 MB, первый заход 27 (21–27) / 10.80 MB,
  HighFields 2 / 3.67 MB, повтор 0; main 13 / 1.70, 10 / 8.74, 24 / 10.60, 3–4 / 0.93, 2–3. Разница rc.6 − main в парах: без троттлинга меню
  +0.39 с, забег +1.05 с, HighFields +0.24 с, повтор +0.79 с; 150 мс / 10 Мбит: меню +0.48 с, забег +0.69 с, HighFields +2.64 с, повтор +0.46 с.
  Консоль rc.6 = консоль main (только офлайн-мета), failed 0, все фазы проходят во всех 8 прогонах.
- Доки фреймворка: оценка «98 → ~14, 156 → ~76, 12 → 8» заменена замером «105 → 10, 152 → 10, 12 → 2» (`releases/0.7.0.md` шаг 11,
  `.claude/docs/CONTENT.md`), коммит в `content-directories` без тега. `/var/tmp/content_directories-builds-rc5` удалён.

## Текущий момент остановки (2026-11-10, по решению пользователя)
- Оркестратор поколения 1 остановлен; продолжение — новая сессия: `/orchestrate docs/tasks/current/content_directories/`, начать с `handoff/orchestrator_g1.md` и `handoff/16d_g1.md`.
- Запущенных агентов нет. Редакторы worktree закрыты агентами (проверить `control` при старте).

### [16e] Проба LZMA по файлам — no-go (WebGL-плеер не распаковывает LZMA)
Evidence: `probe/16e/` (скрипты, логи, json стенда). Код фреймворка не менялся (ветка `content-directories` = `5c69bb8e`); временные
`Assets/Probe16e`, `Assets/ContentSmoke/Probe16eWebRun.cs`, `Assets/ContentSmoke/Plugins`, `client/project/Data`, `Library/Probe16e` удалены.

- **1. Формат — да, но только своим писателем.** `ContentBuildInterface.ArchiveAndCompress(files, path, BuildCompression.LZMA)` пишет
  **один блок** на весь архив (флаги блока 65 = LZMA | streamed), `_blockSize` рефлексией (128 KiB, 16 KiB) не влияет: все три архива смоука
  68910 байт, `blocks 1` (`Probe16e.Build`, `build_editor.log`: `archive LZMA / LZMA_128k / LZMA_16k`; разбор `archive.py`). Блок LZMA движка —
  5 байт свойств (`5D`, словарь 512 KiB) + сырой поток. Свой архив: `unityfs_lzma.py <lz4 архив> <out> [--flags N] [--mixed]` перекодирует
  блоки LZ4-архива движка в LZMA с теми же границами (блок на файл, 128 KiB, выравнивание 16), оглавление LZ4HC; смоук 150480 → 76992 байт.
- **2а. Редактор (Play Mode, смоук с type tree) — да.** `play.sh` (`Probe16e.Run`, `PROBE_DIR/DONOR/STEPS`): полный архив с флагами блоков 1 и 65 —
  `registered ... valid True`, `target loaded: smoke target`, `scene loaded: True` (`play_f1_full.log`, `play_f65_full.log`); частичный (нули +
  заголовок + регистрация 3521 из 76992 байт, блоки цели и сцены дописаны в смонтированный архив) — то же, и для смешанного LZ4 / LZMA по блокам
  (`play_My_f1_partial.log`, `play_My_f1_mixed_partial.log`; строка «Archive file was modified...» — известная проверка времени на диске, в
  MEMFS её снимает восстановление времён узла). То есть читатель редактора берёт LZMA по блокам, без распаковки архива целиком.
- **2б. WebGL — нет.** Плеер Unity 7000.0.0a10 не содержит распаковщика LZMA для архивов: `Decompressing this format (1) is not supported on
  this platform.` → `Failed to load build manifest` → `InvalidOperationException: Failed to load content manifest from VFS path`.
  Смоук (`Probe16e.BuildWeb`, плеер без стриппинга игры, `node client/measurements/tools/smoke.mjs <dir> <png>`, 07:38, load ~6): контроль LZ4
  частичный — `ready` (`web_LZ4.json`); свой LZMA по файлам частичный — `failed` (`web_My.json`); **архив LZMA самого движка целиком** — тоже
  `failed` с той же ошибкой (`web_LZMA.json`), т.е. дело не в нашем формате и не в частичной загрузке. pixel-swarm rc.6 с перекодированным
  архивом и переписанным индексом (`reindex.py`; сборка не нужна — плеер знает только хэш) на стенде (`/var/tmp/16e/stand/go.sh
  /var/tmp/16e/builds/lzma l_n0 0 0 0`): индекс + диапазон регистрации `0-683379` и та же ошибка 12 раз на 1.17 с, меню не открылось
  (`stand_l_n0/1_first.console.txt.gz`, `l_n0.server.log`). В wasm смоука строк `lzma` нет (`grep -a -i lzma webplayer.wasm` пусто).
- **3. Цена распаковки — не мерилась** (нечем: распаковщика в плеере нет). **Байты на реальных файлах pixel-swarm** (архив rc.6, тот же порядок,
  блоки 128 KiB; `ls -l`, `phases.py` по Range-запросам прогона `g2_r_n1`, файлы запроса → их спан в LZMA-архиве):

  | | LZ4HC (rc.6) | LZMA по файлам | |
  |-|-|-|-|
  | архив | 15 564 544 | 11 963 008 | −23% (смешанный LZ4 / LZMA: 11 958 672, 39 блоков из 10831 остаются LZ4) |
  | индекс `.br` | 316 729 | 301 677 | |
  | регистрация (заголовок, манифест, корни) | 908 016 | 683 380 | замер стенда |
  | меню (первые 10 запросов, с регистрацией) | 2.72 MB | 2.27 MB | −16% |
  | забег WinterForest (следующие 10) | 7.52 MB | 6.41 MB | −15% (2.9 MB и 0.9 MB диапазоны почти не жмутся: 2943127 → 2903981) |
  | HighFields (2 запроса) | 3.67 MB | 2.11 MB | −43% |

  Оценка [16d] (11.58 MB данных, HighFields 2.08) подтверждена; на 10 Мбит HighFields дал бы ≈ −1.25 с сети, меню ≈ −0.36 с, забег ≈ −0.9 с —
  до вычета распаковки, которую в вебе измерить нельзя.
- **4. Решение: no-go.** Остановка на первом «нет» (п. 2, WebGL). Обойти можно только своей распаковкой вне движка (JS / wasm) с записью в
  MEMFS уже в формате, который плеер читает (LZ4 или без сжатия: 225 MB raw против 15.6 MB сейчас) — это другая схема хранилища, не выбор кодека
  в настройках; в рамках шага не делалась. Не проверялось: есть ли LZMA в плеерах других платформ (для них контент на диске, выигрыша в сети нет).
- Нагрузка машины во время шага: load 6–17 (чужие редакторы pixel-swarm, тесты storytale); времена не сравнивались, вывод от них не зависит.

## [16g] Проверки bindings (ветка bindings-checks от main)

Worktree `/projects/framework/.claude/worktrees/bindings_checks`, ветка `bindings-checks` от main `46a6f793`, в main не слита, не запушена.

- **Дефект 1, что оказалось на самом деле.** `HierarchyBindingsValidator` пустой элемент массива ловил и до правки: тест
  `Validator_EmptyElementOfAnArray_IsAnError` (массив ссылок и массив во вложенном классе) зелёный на коде main. Дыра была в
  Regenerate All: `HierarchyBindingsPreparation.Prepare(bindUnchanged: false)` привязывает заново только при изменённом коде или
  несовпавшем `StructureHash`; замена ребёнка на объект с тем же именем и составом (копия -> экземпляр префаба) не меняет ни код, ни
  хэш, ссылки остаются `{fileID: 0}`, а валидатор в Regenerate All не вызывается (только в `RegeneratePrefabs` и из меню).
  Исправление (`b62ba3f1`): подготовка считает пустые ссылки привязанного компонента (`HierarchyBindingsEmptyReferences`, общий с
  валидатором обход) и ставит объект в очередь привязки с предупреждением `holds N empty references: binding again`.
  Легальных пустых полей нет: биндер заполняет каждое поле из живого скана, вариант зеркалит только свой слой (узлы-проходы без
  полей), bindings базы на варианте регенерируются только в базе (`ValidateLayer`) - их пустые ссылки по-прежнему ошибка валидатора.
- **Дефект 2** (`a1857343`). Сценарий из [12]: папка кода берётся от сборки скрипта-владельца или asmdef над ассетом; префаб в
  другой сборке, чем его `*Bindings.g.cs` -> новый путь, файла там нет, класс писался второй раз. `HierarchyBindingsFileConflicts`
  ищет под `Assets` файл `<Type>.g.cs` с тем же namespace по другому пути: ошибка `is already generated into <файл>, and this
  object resolves to <путь>`, `Prepare` = false, ничего не пишется и не ставится в очередь. Туда же переехала старая проверка
  «файл занят другим классом».
- **Тесты** (`HierarchyBindingsBatchTests`, batchmode, `client/project` worktree). До: `PrepareProject_EmptyReference_IsBoundAgain`
  FAIL (`Sequence contains no elements`: очередь пуста), `Prepare_ClassGeneratedInAnotherFolder_IsRefused` FAIL (`The class is
  written a second time`). После: 38 / 38. Побочно: в красном прогоне записанный дубликат запускал компиляцию и ронял ещё три
  теста класса (`(compiling)`) - артефакт дефекта, после правки нет.
- **test-all** из worktree: exit 0; backend 1565, common 96, verify 168, control 23, telemetry 84, EditMode 1180 / 1184 (4 skipped),
  PlayMode 96 / 120 (24 skipped).
- **Игры:** шаг в `releases/unreleased.md` (Regenerate All, grep `is already generated into`, перенести ассет или файл с `.meta`);
  коммит дефекта 2 помечен `BREAKING:`. Доки: `.claude/docs/HIERARCHY_BINDINGS.md`.
- **Не сделано / вопросы:** Regenerate All по-прежнему не вызывает валидатор (остальные его находки видны только из меню и
  `RegeneratePrefabs`); поиск второго файла идёт по AssetDatabase, не импортированный ещё файл он не видит (в батче это закрывает
  `ValidateUniqueTypes`).
- **Дополнение (решение пользователя): Regenerate All заканчивается валидатором.** `HierarchyBindingsBatch.PrepareProject` ставит
  запрос (`HierarchyBindingsProjectValidation`, SessionState: переживает reload); проход привязки, в том числе пустой, завершается
  `HierarchyBindingsValidator` по всем префабам с bindings и загруженным сценам; итог `Regenerate All: N prefabs and M scenes
  validated in T ms, E validation errors` - ошибка при E > 0, общий с `RegeneratePrefabs` вывод (`HierarchyBindingsLog.ReportResult`).
  `HasPending` остаётся `true` до проверки. В batchmode / без фокуса проход сам не стартует: `PumpPending` до `Idle.`.
  Тест `PrepareProject_ValidatesAfterTheBinding`: до - FAIL (`The validation waits for the binding pass`), после - 39 / 39.
  Время: 4 мс на 1 префаб, 10 мс на 2 (фикстуры `client/project`); на игре не мерил (pixel-swarm не трогал), цифра есть в строке итога.
  test-all: exit 0, backend 1565, common 96, verify 168, control 23, telemetry 84, EditMode 1181 / 1185, PlayMode 96 / 120.
  Числа в `CLAUDE.md` обновлены отдельным коммитом. Шаг для игр и `BREAKING:` добавлены.
