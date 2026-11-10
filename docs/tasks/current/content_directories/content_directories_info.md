---
task: content_directories
status: completed
phase: completed
created: 2026-11-09
updated: 2026-11-10
total_steps: 17
completed_steps: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, "16a", "16b", "16c", "16d", "16e", "16g", 17]
blocked_steps: []
---

## Задача: фреймворк на Content Directories вместо Addressables, затем pixel-swarm

### Что я хочу

Замеры на 7000.0.0a10 (`client/measurements`, ветка `content-loading-size`, коммиты `fdf4d977`, `94458ce6`, `a8f3b692`,
`ANALYSIS.md`, раздел «Content loading») показали: Addressables стоит около 420 KiB brotli загрузки страницы, а Content
Directories через API движка — 70–105 KiB. Получается −310…340 KiB, в pixel-swarm реалистично −250…330 KiB. Контент
приходит быстрее: из `.data` за 13–17 мс, с сервера ~100 мс, у Addressables 128–135 мс. Повторное открытие страницы
берёт файлы из Cache API. Сцены грузятся по `LoadableSceneId`, ленивые ссылки — через `Loadable<T>`. Дедупликация
работает только внутри одной сборки каталога. Поэтому каталог один на игру, корней может быть несколько, а скачивание
делится по загрузкам: шаг качает только недостающие файлы.

Работаем в параллельных ветках, main и другие игры это не затрагивает:

1. **Фреймворк, ветка `content-directories`, тег релиз-кандидата `v0.7.0-rc.1`.** Пишем обёртку над Content
   Directories и сразу выпиливаем Addressables из фреймворка: рантайм, редакторные синхронизации, генераторы
   каталогов, форк пакета, проверки сборки, документацию.
2. **pixel-swarm, своя ветка `content-directories`.** Подтягивает фреймворк на `v0.7.0-rc.1` и переводит игру
   с Addressables на Content Directories: ссылки, сцены, каталоги, инструменты уровней, шаблоны WebGL, сборку.

Главное в задаче — API, который мы хотим видеть (раздел «Желаемый API»):
- **Lifetime вместо CancellationToken.** Каждый асинхронный метод загрузки принимает `IReadOnlyLifetime`, а не
  `CancellationToken`. Lifetime и отменяет загрузку, и владеет результатом: когда он заканчивается, ассет освобождается,
  сцена выгружается. Отдельных `Release` у вызывающего кода нет.
- **Интеграция с DI-контейнером сразу.** Регистрация в корневом скоупе, загрузки в `Construct` скоупа на его lifetime,
  сервис для загрузок во время игры, сцены скоупов, генератор контейнера.

### Цель

1. Ветка фреймворка `content-directories` в своём worktree. Shared tree `/projects/framework` не переключать.
2. Обёртка над Content Directories в `client/core`:
   - сборка каталога вместе с плеером;
   - индекс файлов по загрузкам;
   - хранилища файлов: локальное, веб с Cache API, предзагруженное;
   - загрузчик ассетов и сцен на lifetime;
   - сериализуемые ленивые ссылки;
   - интеграция с DI.
3. Каталоги фреймворка (`AssetGroup`, `Scenes`, генераторы) работают на Content Directories.
4. Addressables удалён из фреймворка полностью: код, синхронизации, форк `client/packages/com.unity.addressables`,
   зависимости `package.json`, `client/project`, `verify.sh`, документация.
5. Тесты зелёные (`tools/scripts/test-all.sh`), числа тестов обновлены в `CLAUDE.md`. `ContainerGenerator.Verify` проходит.
6. Документация и `releases/0.7.0.md` (rc): что меняется для игры, команды поиска. Тег `v0.7.0-rc.1`.
7. pixel-swarm в ветке `content-directories`, указатель submodule на `v0.7.0-rc.1`.
8. pixel-swarm без Addressables:
   - `AssetReference*` заменены на ссылки контента;
   - сцены грузятся по ссылкам контента;
   - каталоги перегенерированы;
   - `AddressableAssetsData` и пакет удалены;
   - инструменты уровней, атласы, stress переписаны.
9. Проверено:
   - тесты игры;
   - сборка WebGL;
   - размер до и после;
   - стенд (запуск, меню → забег → локация, повторное открытие из кэша);
   - Play Mode и моки в редакторе;
   - реальное устройство (iPhone Safari).
10. Найденное в pixel-swarm возвращается во фреймворк как `v0.7.0-rc.2` и далее. В main и в обычный релиз — только по
    решению пользователя.

### Контекст

- **Версии.** pixel-swarm на 7000.0.0a10, как и замеры. Другие игры (mines-leader, peace-data-reborn, storytale) остаются
  на `v0.6.0`, их не трогаем. Поэтому релиз-кандидат и ветка: API фреймворка можно ломать (`BREAKING`), обычный релиз
  выйдет после переезда pixel-swarm.
- **Правила.**
  - Комментарии в коде на английском.
  - Fail fast: никаких тихих фолбэков.
  - Файлы до 300 строк.
  - Варианты (локальное, веб, предзагруженное хранилище) — отдельные классы, а не ветки внутри одного.
  - Коммиты без строки Co-Authored-By Claude.
  - Custom SRP, никогда URP.
  - Сгенерированные `projects/` измерений не коммитить.
- **Что уже известно из замеров (a10):**
  - **Корни каталога.** Только ScriptableObject. Сцена входит в каталог через поле `LoadableSceneId` корня.
  - **Ленивые ссылки.** `Loadable<T>` — ленивая ссылка: владелец грузится без цели, цель грузится на `LoadAsync()` /
    `Load()` и освобождается на `Release()`. Работает только внутри своего каталога: `Loadable` в ассете плеера на
    ассет каталога приходит с LocalID 0.
  - **API загрузки.**
    - Ассеты: `Loadable<T>.Load()` — синхронно через `WaitForCompletion()`; `LoadAsync()` → `Awaitable<T>`, без токена
      и без прогресса.
    - Сцены: `SceneManager.LoadSceneAsync(LoadableSceneId, LoadSceneParameters)`, синхронного варианта нет;
      `GetSceneByLoadableSceneId`.
    - Каталог: `ContentLoadManager.RegisterContentDirectory(path)`, `UnregisterContentDirectory`,
      `GetRootAssets<T>(handle)`.
  - **Как ведёт себя регистрация.**
    - Список файлов папки читается один раз: файл, дописанный позже, не находится (`CAHFileSystem failed to find a file
      for hash`).
    - На пустых файлах регистрация зависает.
    - Файлы корней открываются сразу (`File is either empty or corrupted` на заглушке).
    - Рабочая схема: скачать манифест и файлы корней, остальное записать нулями реального размера, зарегистрировать,
      перед каждой загрузкой перезаписать нули нужными файлами. Проверено: root 2, level 4, content 3 из 10 файлов.
  - **Индекс файлов по загрузкам.** Сборка вычисляет его из манифеста: замыкание зависимостей сериализованного файла
    плюс ресурсы `cah:/<hash>` внутри `.cf`. Пример: `client/measurements/parts/content-directory-single/.../Editor/ContentDirectoryManifest.cs`.
  - **Файловая система в вебе.** Нативные чтения идут через emscripten FS. Загрузка: `fetch` → `FS.writeFile`
    (`client/measurements/parts/content-directory/.../Plugins/WebFileDownload.jslib`). Cache API безопасен: имена
    файлов — хэши содержимого. `BuildManifestHash.txt` пишет загрузчик из индекса.
  - **Дубликаты в каталоге.**
    - Встроенные ресурсы, на которые ссылается контент: `Sprites-Default`, шейдер, кубмапа отражений; 554 KiB raw в
      тесте. В pixel-swarm `Sprites-Default` не используется, но есть `m_SpotCookie` в `RenderSettings` 16 сцен (источников света нет; не копируется, шаг [1] g) и
      `m_LightingDataAsset` в `Menu`/`Lobby`.
    - Всё, на что ссылается и плеер (стартовая сцена), и каталог: попадёт в `.data` и в каталог, в памяти будет двумя
      объектами. В плеере должен остаться только минимальный старт.
  - **Формат манифеста** не обещан стабильным. Разбор держать в одном классе с тестом на реальном манифесте.

## Желаемый API

Пространство имён `Internal`, сборка `Internal` (`client/core/Runtime/Content/`), редактор — `Internal.Editor`
(`client/core/Editor/Content/`). Имена рабочие; если шаг [1] опровергнет допущение, API меняется, и это записывается в
progress.

### Правила API

- **Каждая асинхронная загрузка принимает `IReadOnlyLifetime lifetime` первым аргументом.** `CancellationToken` в
  публичном API нет.
- **Lifetime владеет результатом.** Ассет освобождается, сцена выгружается, когда он заканчивается. Если он закончился
  во время загрузки:
  - вызывающий код получает `OperationCanceledException`, как ожидания `Updater`;
  - уже начатая загрузка движка не прерывается: она доходит до конца и сразу освобождается;
  - скачивание файлов прерывается через `AbortController`, но только если файл больше никому не нужен.
- **Повторные загрузки одного объекта учитываются счётчиком ссылок.** Объект выгружается, когда закончился последний
  lifetime. Одна загрузка файла на всех: два шага, которым нужен один файл, ждут одно скачивание.
  Счётчик держит загрузчик (шаг [1], c): у одного `Loadable` повторный `Load` идемпотентен, а первый `Release` сразу
  освобождает объект; разные `Loadable` на одну цель движок считает сам и отдаёт один объект. Поэтому загрузчик
  считает lifetime'ы по экземпляру `Loadable` ссылки и зовёт его `Release` один раз, когда счётчик дошёл до нуля.
- **Каждая загрузка видна в телеметрии и в трейсе.** `AssetLoads.Begin("Content: <name>")` / `Finish` / `Fail` и
  `GameProfiler.Concurrent`.
- **Синхронный доступ** — только к уже загруженному (`Target`) или к предзагруженному в `.data` (`LoadNow`). Для
  удалённого синхронный вызов бросает исключение, а не ждёт.

### Сериализуемые ссылки (замена `AssetReference` / `AssetReferenceT<T>`)

```csharp
// A lazy reference to an asset of the content directory: the owner loads without the target.
// Wraps Loadable<T> (the engine id: GUID + local id), drawn in the inspector as an object field of T.
[Serializable]
public sealed class ContentReference<T> where T : Object
{
    [SerializeField] private Loadable<T> _loadable;

    public LoadableObjectId Id => _loadable.LoadableObjectId;
    public bool IsLoaded => ...;           // Status == Loaded
    public T Target => ...;                // throws when not loaded

#if UNITY_EDITOR
    public T EditorAsset => ...;           // the asset behind the id, for tools and mocks
#endif
}

// A scene of the content directory (replacement of the AssetReference to a scene).
[Serializable]
public sealed class ContentScene
{
    [SerializeField] private LoadableSceneId _id;

    public LoadableSceneId Id => _id;

#if UNITY_EDITOR
    public string EditorPath => ...;       // for FindInOpenScene of the mocks
#endif
}
```

Drawer'ы (`Internal.Editor`): у движка есть свои (шаг [1], a): `LoadableDrawer` для `Loadable<>` (поле объекта типа
`T`, тип берётся из поля и внутри обобщённой обёртки: `_wrapped._loadable` даёт `T`), `LoadableSceneIdDrawer` (поле
`SceneAsset`), `LoadableObjectIdDrawer`. Свой drawer обёртки только рисует дочернее поле (`_loadable` / `_id`) в одну
строку с меткой обёртки, id не пишет сам. `EditorAsset` — `LoadableObjectIdEditorUtility.LoadableObjectIdToObject`,
`EditorPath` — `LoadableSceneIdEditorUtility.LoadableSceneIdToScene`. Пустая ссылка — ошибка валидации каталога при
сборке, а не null в рантайме.

### Загрузчик (сервис контейнера)

```csharp
public interface IContentLoader
{
    // Fetches the files the asset needs that the page has not got yet, then loads it. Released when the lifetime ends.
    UniTask<T> Load<T>(IReadOnlyLifetime lifetime, ContentReference<T> reference) where T : Object;

    // Several at once: one batch of fetches, then the loads in parallel.
    UniTask<IReadOnlyList<T>> Load<T>(IReadOnlyLifetime lifetime, IReadOnlyList<ContentReference<T>> references) where T : Object;

    // Additive; isMain makes it the active scene. Unloaded when the lifetime ends.
    UniTask<ILoadedScene> LoadScene(IReadOnlyLifetime lifetime, ContentScene scene, bool isMain = false);

    // Only the files, no objects: warms the next step (the next location while the run goes on).
    UniTask Fetch(IReadOnlyLifetime lifetime, ContentReference reference);
    UniTask Fetch(IReadOnlyLifetime lifetime, ContentScene scene);

    // The progress of the fetch of one step, for a loading screen (bytes of the files the step needs).
    IViewableProperty<ContentProgress> Progress(ContentReference reference);
}
```

`ContentReference` — необобщённая база `ContentReference<T>`, нужна для `Fetch` и `Progress`. `ContentProgress` —
`readonly struct` с полями `Loaded` и `Total` в байтах.

### DI

```csharp
// Root scope of the game, once: the directory of the build, the store of the platform
// (choice inside the installer with #if: web / local / editor), IContentLoader.
builder.AddContent(ContentOptions options);

// Construct of a scope: loads on builder.Lifetime, profiler segment, ContainerRegistryDebug.RecordLoadedAsset.
var encounter = await builder.LoadContent(level.Encounter);

// Batch before the build, like RequestAssetGroup: loaded in parallel with the groups and scenes of the scope.
builder.RequestContent(level.Encounter, out ContentSlot<ArenaEncounter> encounter);   // encounter.Value after the build

// Scenes of scopes (replacing the AssetReference overloads):
await builder.FindOrLoadSceneWithServices(Scenes.Menu);           // ContentScene
await builder.FindOrLoadScene<ArenaView>(level.Scene);
new ScopeLoadOptions(parent, Construct).WithAssetScene(Scenes.Game);
loader.Load(ContentScene scene, bool isMain)                       // ServiceScopeSceneLoader
```

- **Загрузки во время игры** идут через `[Inject] IContentLoader` на lifetime владельца. Например, финальный враг забега
  на lifetime забега: `RunFinals`.
- **`ContainerGenerator`** знает новые перегрузки `FindOrLoadSceneWithServices(Scenes.X)` (якорь сцены, `EntityAssets`)
  и `LoadContent` / `RequestContent` как примитивы. После изменения — сборка генератора и `ContainerGenerator.Verify`.

### Каталоги

- **Корень каталога генерируется.** Это один ScriptableObject `ContentRoot` в папке контента игры. В нём только
  `Loadable` групп каталогов (`EnvAssetGroupAsset`, `PrefabGroupAsset`, `AudioGroupAsset`, `SpriteGroupAsset`) и
  `LoadableSceneId` сцен, без прямых ссылок: регистрация открывает файлы корня, корень должен быть тонким.
  Генераторы каталогов пишут его вместо адресуемых групп.
- **`AssetGroup<TAsset>.LoadAsset(...)`** грузит ассет группы через `IContentLoader`, а не по адресу Addressables.
  Публичный API групп сохраняется: `Retain` / `Release`, `RequestAssetGroup`, `LoadAssetGroup`, `EnsureLoaded`.
  `Release` группы освобождает её `Loadable`.
- **Группы из `Resources`** (`IsAddressable == false`, синхронный доступ) становятся «предзагруженными»: их файлы
  лежат в `.data` (preload manifest WebGL), доступ синхронный, через `Load()` (шаг [1], e: работает в вебе, без
  запросов). Флаг переименовывается: `IsAddressable` → `IsPreloaded`.
- **`Scenes.X`** генерируется как `ContentScene` (из `LoadableSceneId` корня), а не `StaticScene` с `AssetReference`.
  `Startup` остаётся сценой плеера в списке сборки.

### Сборка

- **`ContentBuild`** — шаг перед сборкой плеера (`IPlayerBuildStep` и `BuildPlayerProcessor` для сборки из меню):
  1. перегенерировать `ContentRoot`;
  2. `BuildPipeline.BuildContentDirectory` в `Library/Content/<name>`;
  3. проверить ссылки (пустые и ведущие вне каталога);
  4. вычислить индекс;
  5. скопировать файлы в `StreamingAssets/Content/<name>` (для предзагруженного — preload manifest
     `Library/PlayerDataCache/WebGLPreloadedStreamingAssets/*.manifest`).
- **Индекс `ContentIndex`** запекается в плеер (ассет в `Resources` или json, который пишет шаг сборки): плеер и
  каталог собираются вместе. В индексе:
  - имя каталога, хэш манифеста;
  - файлы с размерами;
  - файлы корней;
  - для каждого `LoadableObjectId` и `LoadableSceneId` — его файлы.
- **Логи сборки:** число файлов, raw/br по каталогу, файлы, которые не входят ни в одну загрузку, встроенные ресурсы
  в каталоге (предупреждение с именами).

### Хранилища (варианты — отдельные классы)

- **`IContentStore`:** `UniTask Fetch(IReadOnlyLifetime lifetime, IReadOnlyList<ContentFile> files, IProgress<long> progress)`,
  `string LocalPath`.
- **`LocalContentStore`** — standalone и сборка без сети: каталог читается из `StreamingAssets` напрямую, `Fetch`
  ничего не делает.
- **`WebContentStore`** — WebGL:
  - jslib `fetch` + Cache API (`caches.open('content')`, ключ — URL) → `FS.writeFile`; отмена через `AbortController`;
  - базовый URL по умолчанию — `StreamingAssets`, CDN — другой префикс в `ContentOptions`;
  - при старте удаляет из кэша файлы, которых нет в текущем индексе;
  - без Cache API (http, старые браузеры) — только `fetch` и запись в лог.
- **`EditorContentStore`** — Play Mode (шаг [1], b): каталог не регистрируется и не собирается. Без
  зарегистрированного каталога `Loadable<T>.Load()` / `LoadAsync()` и `SceneManager.LoadSceneAsync(LoadableSceneId)`
  отдают сами ассеты и сцены проекта (`AssetDatabase`); `Release` их не уничтожает. Корень — `AssetDatabase`
  (`ContentRoot` по пути), `Fetch` ничего не делает. Регистрировать каталог в edit mode движок запрещает
  (`RegisterContentDirectory is not supported in edit mode`); каталог из `Library` в Play Mode грузится, но даёт
  копии, а не ассеты проекта, — это не нужно.
- **`ContentDirectory`** (внутренний):
  - регистрирует каталог по рабочей схеме: манифест и файлы корней, нули для остальных, `RegisterContentDirectory`;
    «файлы корней» — замыкание зависимостей их сериализованных файлов (шаг [1], d: регистрация грузит корни вместе с
    прямыми ссылками, общий файл скриптов тоже нужен);
  - каталог не разрегистрируется, пока нужен хоть один его объект: `UnregisterContentDirectory` уничтожает все
    загруженные объекты каталога и корень, повторная регистрация даёт новые экземпляры (шаг [1], f);
  - знает, какие файлы уже настоящие;
  - отдаёт корень (`GetRootAssets<ContentRoot>`).

## План реализации

#### [1] Ветки и разведка открытых вопросов
- **Статус:** [x] done
- **Цель:** у фреймворка своя ветка и worktree; ответы на вопросы, от которых зависит API, проверены на a10 и записаны.
- **Как:**
  - `git worktree add .claude/worktrees/content_directories -b content-directories main` во фреймворке, с копией `Library`
    для `client/project`.
  - Разведка в проекте измерений (`client/measurements`, новый вариант или ветка `content-loading-size`). Вопросы:
    - (a) Рисует ли инспектор поле `Loadable<T>` / `LoadableSceneId` и чем.
    - (b) Грузит ли `Loadable<T>.LoadAsync()` в Play Mode без собранного каталога; нужна ли регистрация каталога из
      `Library`.
    - (c) Считает ли движок ссылки: два разных `Loadable` на одну цель, `Release` одного — жив ли объект у второго.
    - (d) Регистрация с несколькими корнями: открывает ли она зависимости файлов корня или только их самих.
    - (e) Работает ли синхронный `Load()` в вебе на скачанных и на предзагруженных файлах.
    - (f) `UnregisterContentDirectory` и повторная регистрация после докачки — переживают ли её загруженные объекты
      (запасной путь, если нули перестанут работать).
    - (g) Какие встроенные ресурсы попадают в каталог из сцен pixel-swarm (`m_SpotCookie`, `m_LightingDataAsset`,
      отражения).
- **Проверка:** ответ на каждый вопрос с доказательством (строка смоука, лог, тест) в `content_directories_progress.md`;
  API в info поправлен, если ответ его меняет.
- **Файлы:** `client/measurements/parts/*`, `client/measurements/variants/*`.
- **Зависит от:** —
- **Блокирует:** [2]–[9]

#### [2] Разбор манифеста и индекс загрузок
- **Статус:** [x] done
- **Цель:** редакторный код читает манифест и для каждого id выдаёт его файлы.
- **Как:**
  - Перенести `ContentDirectoryManifest` из измерений в `client/core/Editor/Content/` и разделить: `ContentManifest`
    (чтение), `ContentFileClosure` (замыкание и `cah:/`), `ContentIndexWriter`.
  - Рантаймовый `ContentIndex` и `ContentFile` — в `client/core/Runtime/Content/`.
- **Проверка:** EditMode-тест на манифесте, собранном из тестовых ассетов: файлы корня, сцены, ассета; ресурсы аудио
  входят в замыкание.
- **Файлы:** `[новые файлы — Internal.Editor, Internal]`, тест `[новый файл — Internal.Tests]`.
- **Зависит от:** [1]
- **Блокирует:** [3], [4]

#### [3] Сборка каталога вместе с плеером
- **Статус:** [x] done
- **Цель:** любая сборка плеера (batchmode `Internal.WebGlBuild.Build`, `deploy-client`, сборка из меню) собирает
  каталог, индекс и StreamingAssets.
- **Как:**
  - `ContentBuild` (`IPlayerBuildStep`) и `ContentBuildPlayerProcessor`.
  - Настройки в `FrameworkSettings`: имя каталога, папка `ContentRoot`, предзагрузка.
  - Убрать вызов `SharedAddressablesSync.Sync()` из `client/platforms/web/Editor/WebGlBuild.cs`.
- **Проверка:** batchmode-сборка WebGL `client/project` или тестового проекта: `StreamingAssets/Content/<name>`,
  индекс в плеере, лог сборки с размерами.
- **Файлы:**
  - `client/core/Editor/Content/*` `[новые]`;
  - `client/core/Editor/Tools/Build/*` (`IPlayerBuildStep`);
  - `client/platforms/web/Editor/WebGlBuild.cs`;
  - `FrameworkSettings`.
- **Зависит от:** [2]
- **Блокирует:** [5], [9]

#### [4] Хранилища и регистрация каталога
- **Статус:** [x] done
- **Цель:** каталог регистрируется на каждой платформе, файлы шага докачиваются по требованию.
- **Как:**
  - `IContentStore`, `LocalContentStore`, `WebContentStore` + `WebContentFetch.jslib`, `EditorContentStore`.
  - `ContentDirectory`: регистрация над нулями, учёт настоящих файлов.
  - Одна загрузка на файл (`UniTaskCompletionSource` на файл), отмена через `AbortController`, чистка устаревших
    хэшей в Cache API.
- **Проверка:** PlayMode-тест на `LocalContentStore`; вариант измерений поверх кода фреймворка проходит смоук в вебе
  с `smokeReload` (0 fetched при втором открытии). Тест отмены: lifetime закончен во время скачивания → в лог ни одной
  ошибки, файл остаётся нулём.
- **Файлы:** `client/core/Runtime/Content/Stores/*` `[новые]`, `client/core/Runtime/Content/Plugins/*.jslib` `[новый]`.
- **Зависит от:** [2]
- **Блокирует:** [5]

#### [5] `IContentLoader`, ссылки и drawer'ы
- **Статус:** [x] done
- **Цель:** игра грузит ассеты и сцены по `ContentReference<T>` / `ContentScene` на lifetime.
- **Как:**
  - `ContentReference<T>`, `ContentReference`, `ContentScene` и их drawer'ы.
  - `ContentLoader`: счётчик ссылок по id, освобождение по окончании lifetime, `OperationCanceledException`, `Fetch`,
    `Progress`.
  - `AssetLoads` и `GameProfiler` на каждую загрузку.
- **Проверка:** EditMode/PlayMode-тесты:
  - загрузка и освобождение по lifetime;
  - lifetime закончен во время загрузки;
  - две загрузки одного объекта и освобождение одной;
  - пустая ссылка бросает исключение;
  - `Target` до загрузки бросает исключение.
- **Файлы:** `client/core/Runtime/Content/*`, `client/core/Editor/Content/Drawers/*` `[новые]`.
- **Зависит от:** [3], [4]
- **Блокирует:** [6], [7]

#### [6] Интеграция с DI и сценами скоупов
- **Статус:** [x] done
- **Цель:** скоупы грузят контент и сцены через контейнер, генератор видит новые примитивы.
- **Как:**
  - `ContentScopeExtensions.AddContent`, `LoadContent`, `RequestContent`.
  - `ServiceScopeSceneLoader.Load(ContentScene)`; `LoadedScene` через `SceneManager.UnloadSceneAsync`.
  - `AssetScene(ContentScene)`, `ScopeLoadOptions.WithAssetScene(ContentScene)`.
  - `SceneLoaderExtensions.FindOrLoadScene` / `LoadTyped` / `FindInOpenScene` по `ContentScene.EditorPath`.
  - `ScopeBuilderExtensions.FindOrLoadSceneWithServices(ContentScene)`.
  - `ContainerGenerator`: `InvocationWalker`, `Primitives`, `EntityAssets`.
- **Проверка:** `ScopeLoaderFixtures` и тесты скоупов на новых сценах; сборка `ContainerGenerator` (Release) и
  `ContainerGenerator.Verify`.
- **Файлы:**
  - `client/core/Runtime/Scopes/Services/Builder/ServiceScopeSceneLoader.cs`;
  - `client/core/Runtime/Scopes/Services/Builder/ScopeBuilderExtensions.cs`;
  - `client/core/Runtime/Scopes/Services/IScopeServiceSceneLoader.cs`;
  - `client/core/Runtime/Scopes/Services/ScopeLoadOptions.cs`;
  - `client/core/Runtime/Services/Scenes/SceneLoaderExtensions.cs`;
  - `client/core/Runtime/Services/Scenes/LoadedScene.cs`;
  - `client/core/Source~/ContainerGenerator/Graph/Walker/InvocationWalker.cs`;
  - `client/core/Source~/ContainerGenerator/Graph/Primitives.cs`;
  - `client/core/Source~/ContainerGenerator/Entity/EntityAssets.cs`;
  - `client/core/Analyzers/ContainerGenerator.dll`.
- **Зависит от:** [5]
- **Блокирует:** [7]

#### [7] Каталоги на Content Directories
- **Статус:** [x] done
- **Цель:** генераторы каталогов пишут `ContentRoot`, группы грузятся через `IContentLoader`, `Scenes.X` — это
  `ContentScene`.
- **Как:**
  - `AssetGroup<TAsset>` без Addressables; `IsAddressable` → `IsPreloaded`.
  - Генераторы: `AssetsCatalogClassGenerator`, `AudioCatalogClassGenerator`, Prefab, Sprite, Scene.
  - `CatalogGroupAssets` и запись `ContentRoot` вместо `CatalogAddressablesSync`.
  - Шаг `RegenerateAll` «Shared addressables» → «Content root».
- **Проверка:**
  - `RegenerateAll` в `client/project` без ошибок;
  - тесты каталогов: `AssetGroupTests`, `EnvAssetGroupAccessTests`, `SpriteCatalogMetadataTests`,
    `TypeNameResolverUnloadTests`, `EditorToolFailureTests`;
  - группа грузится и выгружается по `Retain` / `Release` в PlayMode.
- **Файлы:**
  - `client/core/Runtime/Catalogues/Shared/AssetGroup.cs`, `AssetGroup.Typed.cs`;
  - `client/core/Runtime/Catalogues/Scenes/StaticScene.cs` (удалить или заменить);
  - `client/core/Editor/Catalogues/{Assets,Audio,Prefabs,Sprites,Scenes,Shared}/*`;
  - `client/core/Editor/Tools/Codegen/RegenerateAll.cs`.
- **Зависит от:** [5], [6]
- **Блокирует:** [8]

#### [8] Удаление Addressables из фреймворка
- **Статус:** [x] done
- **Цель:** ни одной ссылки на Addressables во фреймворке (кроме истории и `client/measurements`).
- **Как:** удалить:
  - `BundleUrlVersioning.cs`;
  - `CatalogAddressablesSync.cs`, `SharedAddressablesTargets.cs`, `SharedAddressablesSync.cs`, `ScenesAddressablesSync.cs`;
  - `FrameworkSettings.WithoutAddressables` и фазы общих адресуемых групп;
  - форк `client/packages/com.unity.addressables`;
  - зависимость в `client/core/package.json` и `client/project/Packages/manifest.json`.
  - Проверку `aa/catalog.bin` в `tools/scripts/deploy-client/verify.sh` заменить проверкой каталога и индекса.
- **Проверка:**
  - `grep -rE "AddressableAssets|ResourceManagement|AssetReference|Addressables" client/core client/flow client/modules client/platforms shared tools .claude`
    находит только release notes и измерения;
  - `client/project` компилируется.
- **Файлы:** перечисленные выше, `client/core/Tests/EditorTools/EditorTestsSetUpFixture.cs`.
- **Зависит от:** [7]
- **Блокирует:** [9]

#### [9] Тесты фреймворка, документация, релиз-кандидат
- **Статус:** [x] done
- **Цель:** ветка готова к pixel-swarm.
- **Как:**
  - `tools/scripts/test-all.sh`, обновить ожидаемые числа в `CLAUDE.md`.
  - `CLAUDE.md`: строки про `AssetLoads`, startup trace, модуль контента; `.claude/docs/WEBGL_TROUBLESHOOTING.md`:
    B6 и CRC/UnityCache — пометить «до 0.7», добавить новое.
  - Скиллы `client-telemetry`, `update-override-packages`, `deploy-client` (`verify.sh`).
  - Новый док `CONTENT.md` в `.claude/docs` со строкой в `INDEX.md`: ключевые слова `ContentReference`, `Loadable`,
    `ContentScene`, `IContentLoader`, content directory.
  - `releases/unreleased.md` → `releases/0.7.0.md` с шагами для игры: команды поиска `AssetReference`, `Addressables.`,
    `WithAssetScene`, `StaticScene`, шаблоны WebGL.
  - Тег `v0.7.0-rc.1`. Push — после подтверждения пользователя.
- **Проверка:** таблица `test-all.sh` без падений; `ContainerGenerator.Verify` — 166 или новое число с причиной; тег
  указывает на коммит ветки.
- **Файлы:** `CLAUDE.md`, `.claude/docs/*`, `.claude/skills/*`, `releases/*`.
- **Зависит от:** [3]–[8]
- **Блокирует:** [10]

#### [10] pixel-swarm: ветка и фреймворк rc
- **Статус:** [x] done
- **Цель:** pixel-swarm в своей ветке на `v0.7.0-rc.1`.
- **Как:**
  - Worktree pixel-swarm (или `control copy create`) с веткой `content-directories`.
  - `git -C tools/framework checkout v0.7.0-rc.1`.
  - Шаги `releases/0.7.0.md` — по скиллу `update-framework` с версией rc.
- **Проверка:** проект открывается в batchmode, список ошибок компиляции соответствует шагам [11]–[13].
- **Файлы:** `/projects/pixel-swarm/tools/framework` (указатель).
- **Зависит от:** [9]
- **Блокирует:** [11]

#### [11] pixel-swarm: ссылки и загрузки
- **Статус:** [x] done
- **Цель:** код игры без Addressables.
- **Как:**
  - `LevelDefinition`: `AssetReference _scene` → `ContentScene`, `AssetReferenceT<ArenaEncounter> _encounter` →
    `ContentReference<ArenaEncounter>`.
  - `LevelScopeExtensions.LoadEncounter` → `builder.LoadContent(level.Encounter)`, без ручного `Release` и
    `ApplicationQuit`.
  - `EncounterFinal._enemies` → `ContentReference<EnemyDefinition>[]`.
  - `RunFinals.LoadFinal` → `IContentLoader.Load(runLifetime, reference)`.
  - `FindOrLoadSceneWithServices(Scenes.X)` в `MenuScopeExtensions`, `LobbyScopeExtensions`, `GamePlayScopeExtensions`.
  - Миграция сериализованных значений: перенос GUID из `m_AssetGUID` в id `Loadable` / `LoadableSceneId`
    редакторным скриптом на временной копии с проверкой, что все ссылки на месте.
- **Проверка:** компиляция; редакторный тест «все `ContentReference` уровней и энкаунтеров не пустые»; тесты игры.
- **Файлы:**
  - `client/Assets/GamePlay/Levels/LevelDefinition.cs`, `LevelScopeExtensions.cs`;
  - `client/Assets/GamePlay/Swarm/Encounters/EncounterFinal.cs`;
  - `client/Assets/GamePlay/Loop/Run/RunFinals.cs`;
  - `client/Assets/GamePlay/Loop/GamePlayScopeExtensions.cs`;
  - `client/Assets/Menu/Main/MenuScopeExtensions.cs`;
  - `client/Assets/Menu/Lobby/LobbyScopeExtensions.cs`.
- **Зависит от:** [10]
- **Блокирует:** [12]

#### [12] pixel-swarm: инструменты, каталоги, данные Addressables
- **Статус:** [x] done (`787d01d10`…`693af4deb`)
- **Цель:** редакторные инструменты игры на новом API, Addressables удалён из проекта.
- **Как:**
  - Переписать `GamePlay/Tools/Levels/LevelAddressables.cs` (вероятно, удалить) и `LevelAssets.cs`.
  - Переписать `GamePlay/Tools/Encounters/EnemyAtlases.cs` и `GamePlay/Tools/Tests/EnemyAtlasTests.cs`.
  - Переписать `GamePlay/Stress/Editor/StressBuildStep.cs`.
  - `RegenerateAll`.
  - Удалить `client/Assets/AddressableAssetsData` и `com.unity.addressables` из `Packages/manifest.json`.
  - Стартовая сцена ссылается только на минимум; проверить лог сборки на встроенные ресурсы и дубликаты из плеера.
- **Проверка:** `grep -rE "AddressableAssets|AssetReference|Addressables\." client/Assets` пуст; `RegenerateAll` без
  ошибок; тесты игры (`tools/scripts/run-tests.sh`).
- **Файлы:** перечисленные выше, `client/Assets/Common/Internal/Runtime/Catalogues/*` (сгенерированные).
- **Зависит от:** [11]
- **Блокирует:** [13]

#### [13] pixel-swarm: шаблоны WebGL и профили сборки
- **Статус:** [x] done (`3f1dc7b69`, `40dff58fa`; checks FAIL из-за `verify.sh:87` pipefail — чинит [16a])
- **Цель:** страница не держит логику бандлов.
- **Как:**
  - `client/Assets/WebGLTemplates/{Default,Telegram,Stress}/index.html`: убрать обработку `.bundle` / UnityCache для
    бандлов. Cache API контента ведёт jslib фреймворка.
  - Профили `Settings/Build Profiles/{website,web_telegram,web_stress}.asset`: проверить, что ничего не ссылается на
    Addressables.
- **Проверка:** сборка каждой цели `deploy-client` без загрузки на хост (`--notest` / только сборка) проходит `verify.sh`.
- **Файлы:** перечисленные выше.
- **Зависит от:** [12]
- **Блокирует:** [14]

#### [14] pixel-swarm: проверка в редакторе
- **Статус:** [x] done (`94ae4a819`, `098af36ba`)
- **Цель:** разработка в редакторе не стала хуже.
- **Как:** Play Mode со стартовой сцены; моки, открытые со сцены меню, лобби, игры и локации (`MockSwitcher`,
  `FindInOpenScene`); быстрый перезапуск Play Mode без domain reload (сброс `AssetGroup`, счётчики `ContentLoader`).
- **Проверка:** каждый мок доходит до своей сцены без ошибок в консоли; второй вход в Play Mode без утечек и без
  `Release without a Retain`.
- **Файлы:** —
- **Зависит от:** [12]
- **Блокирует:** [15]

#### [16a] Фреймворк до замеров: сжатие контента в вебе, verify.sh
- **Статус:** [x] done (`377a30bd`, `d85df041`, тег `v0.7.0-rc.2`; pixel-swarm `724bf8146`)
- **Цель:** файлы каталога в вебе идут сжатыми (сейчас 300 MB raw против 29 MB br), `deploy-client` проходит checks.
- **Как:** brotli-файлы контента в WebGL по образцу `Build/*.br` (сборка, jslib, nginx игры, стенды); `verify.sh:87` без SIGPIPE под pipefail; 404 на отсутствующий файл контента; `v0.7.0-rc.2`, указатель pixel-swarm.
- **Зависит от:** [13]
- **Блокирует:** [15]

#### [15] pixel-swarm: замеры, стенд, устройство
- **Статус:** [x] done — выгода НЕ подтверждена: Build +217 KiB br, забег не стартует (fetch без очереди, стриппинг типов); устройства заблокированы
- **Цель:** подтверждена выгода и работа в браузерах.
- **Как:**
  - **Размер:** до/после, wasm / data / framework / StreamingAssets, raw и br; ожидание −250…330 KiB br.
  - **Стенд `device-lab`:** старт → меню → забег → вторая локация; число и объём скачанного на каждом шаге; второе
    открытие из Cache API.
  - **Трейс запуска и телеметрия загрузок** (`load.*`, `AssetLoads`).
  - **iPhone Safari:** старт, переходы, повторное открытие, Safari без Cache API по http.
- **Проверка:** таблица чисел в `content_directories_result.md`; скриншоты или логи стенда; без ошибок в консоли.
- **Файлы:** —
- **Зависит от:** [13], [14]
- **Блокирует:** [16]

#### [16b] Фреймворк: очередь загрузок, link.xml, раскладка файлов
- **Статус:** [x] done (`df361352`, `b83e768f`, `010b524d`, тег `v0.7.0-rc.3`; pixel-swarm `718e9b27f`) — забег стартует, но хуже main по всем осям; ждёт решения пользователя
- **Цель:** забег стартует в браузере; ясно, можно ли уменьшить число файлов каталога и индекс в `.data`.
- **Как:** ограничение одновременных fetch; link.xml типов каталога; исследование группировки файлов `BuildContentDirectory` и формы индекса; `v0.7.0-rc.3`; повтор замеров уровня.
- **Зависит от:** [15]

#### [16c] Проба UseArchive, индекс вне .data
- **Статус:** [x] done (фреймворк `577b05ea`…`0a07e2d0`, теги `v0.7.0-rc.4`, `v0.7.0-rc.5`; pixel-swarm `61b3e812a`, `5e63873e8`) — критерий по времени и байтам выполнен, запросов 262 на первый заход
- **Решение пользователя (2026-11-10):** проба `UseArchive` на pixel-swarm; если до десятков запросов и байтов как у main не дойдёт — остановить миграцию. Индекс вынести из `.data` в файл каталога.
- **Зависит от:** [16b]

#### [16d] Дожать запросы и объём архива
- **Статус:** [x] done (фреймворк `4af74831`, `75c7adad` = `v0.7.0-rc.6`, доки `5c69bb8e` без тега; pixel-swarm `c79a64d48`) — запросы на уровне main (первый заход 27 против 24), время чуть хуже, HighFields +2.6 с под троттлингом
- **Решение пользователя (2026-11-10):** перед [16] уменьшить число запросов (`RangeGap`) и объём (блок, сжатие архива), новая серия замеров.
- **Зависит от:** [16c]

#### [16e] Проба LZMA по файлам
- **Статус:** [x] done — no-go: WebGL-плеер 7000.0.0a10 не распаковывает LZMA (`Decompressing this format (1) is not supported on this platform`); код не менялся; потенциал: архив −23%, HighFields −43% (`probe/16e/`)
- **Решение пользователя (2026-11-10, сессия 2):** делать пробу.
- **Цель:** ясно go / no-go: архив с блоками LZMA по файлам монтируется и догружается частями в WebGL, объём и байты фаз против LZ4HC, время распаковки в wasm. При go — реализация во фреймворке (войдёт в rc.7), при no-go — запись в «Отличия от плана».
- **Зависит от:** [16d]

#### [16f] Обратная связь: запуск и корень
- **Статус:** [-] остановлен при заморозке: `a8ad6559` (CINGR001 с подсказкой), `73cc7232` (CINGR017: лямбда-корень — ошибка компиляции, BREAKING), `b591eaa5` WIP без тестов (имя каталога в одном месте, `ContentFetchOptions`)
- **Цель:** имя каталога контента задаётся в одном месте (сейчас `Framework.json` + поле `_content` в 4 сценах игры); корень скоупа получает `ContentOptions` без лямбды, падающей в рантайме (`LoadRoot(ContentOptions, construct)` или диагностика генератора); `CINGR001` подсказывает про content-сборку.
- **Зависит от:** [16e]

#### [16g] Обратная связь: bindings
- **Статус:** [x] done — ветка `bindings-checks` от main: `b62ba3f1` (Regenerate All заново привязывает объект с пустой ссылкой), `a1857343` (генератор отказывает во втором файле класса, BREAKING); `2a88ac0b` (Regenerate All заканчивается валидатором, BREAKING), `fb6b8ec4` (числа тестов); test-all exit 0; слито в main `fb6b8ec4`, не запушено
- **Цель:** `HierarchyBindingsValidator` находит `{fileID: 0}`; генератор bindings даёт ошибку вместо дубликата класса.
- **Зависит от:** [16f]

#### [16h] Обратная связь: сборка контента
- **Статус:** [-] отменён (миграция заморожена)
- **Цель:** группу можно исключить из контента одной сборки (stress); есть точка входа «только ContentBuild» с логом.
- **Зависит от:** [16g]

#### [16i] Обратная связь: тесты и update-framework
- **Статус:** [-] отменён (миграция заморожена)
- **Цель:** `ContentReference` создаётся в тесте без ассета; `update-framework/status.sh` понимает rc-теги.
- **Зависит от:** [16h]

#### [16j] rc.7 и pixel-swarm на нём
- **Статус:** [-] отменён (миграция заморожена, rc.7 не выпускается)
- **Цель:** `releases/0.7.0.md` дополнен шагами [16e]–[16i], test-all зелёный, тег `v0.7.0-rc.7`; pixel-swarm на rc.7 без локальных обходов (имя каталога, корень, тесты), сборка и смоук стенда.
- **Зависит от:** [16e]–[16i]

#### [16] Обратная связь во фреймворк
- **Статус:** [-] закрыт заморозкой: сделаны [16a]–[16e], частично [16f]; [16g] идёт отдельно от main
- **Цель:** исправления, найденные в pixel-swarm, лежат во фреймворке, а не в игре.
- **Как:** правки в ветке фреймворка `content-directories`, тег `v0.7.0-rc.N`, указатель pixel-swarm на него, повтор [15]
  по затронутому.
- **Проверка:** pixel-swarm на последнем rc без локальных обходов фреймворка.
- **Файлы:** —
- **Зависит от:** [15]
- **Блокирует:** [17]

#### [17] Итог и решение о релизе
- **Статус:** [x] done — миграция заморожена решением пользователя (2026-11-10); итог в `_result.md`
- **Цель:** пользователь решает: слить в main, выпустить `0.7.0`, переводить ли другие игры.
- **Как:** `content_directories_result.md` с цифрами, отличиями от плана и нерешёнными вопросами.
- **Проверка:** пользователь прочитал и принял решение.
- **Файлы:** `docs/tasks/current/content_directories/*`.
- **Зависит от:** [16]
- **Блокирует:** —

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `client/core/Runtime/Catalogues/Shared/AssetGroup.cs` | счётчик `Retain` / `Release` групп каталогов, `IsAddressable`, сброс между запусками |
| `client/core/Runtime/Catalogues/Shared/AssetGroup.Typed.cs` | `Addressables.LoadAssetAsync` ассета группы → `IContentLoader` |
| `client/core/Runtime/Catalogues/Shared/AssetGroupBuilderExtensions.cs` | `RequestAssetGroup` / `LoadAssetGroup`: образец для `RequestContent` / `LoadContent` |
| `client/core/Runtime/Catalogues/Scenes/StaticScene.cs` | `AssetReference` сцены → `ContentScene` |
| `client/core/Runtime/Scopes/Services/Builder/ServiceScopeSceneLoader.cs` | `Addressables.LoadSceneAsync` → `LoadSceneAsync(LoadableSceneId)` |
| `client/core/Runtime/Services/Scenes/LoadedScene.cs` | выгрузка `SceneInstance` |
| `client/core/Runtime/Services/Scenes/SceneLoaderExtensions.cs` | `FindOrLoadScene`, `FindInOpenScene` моков (`editorAsset.name`) |
| `client/core/Runtime/Scopes/Services/Builder/ScopeBuilderExtensions.cs` | `FindOrLoadSceneWithServices(AssetReference)` — примитив генератора |
| `client/core/Runtime/Scopes/Services/IScopeServiceSceneLoader.cs`, `ScopeLoadOptions.cs` | `AssetScene`, `WithAssetScene` |
| `client/core/Runtime/Services/Assets/BundleUrlVersioning.cs` | удалить |
| `client/core/Runtime/Services/Assets/AssetLoads.cs` | телеметрия загрузок, сохраняется |
| `client/core/Editor/Catalogues/Shared/*Addressables*.cs`, `Scenes/ScenesAddressablesSync.cs` | удалить, заменить записью `ContentRoot` |
| `client/core/Editor/Catalogues/Assets/AssetsCatalogClassGenerator.cs`, `Audio/AudioCatalogClassGenerator.cs` | генерируемый `LoadAsset(Address)` |
| `client/core/Editor/Tools/Codegen/RegenerateAll.cs` | шаг «Shared addressables» |
| `client/core/Source~/ContainerGenerator/{Graph/Walker/InvocationWalker.cs,Graph/Primitives.cs,Entity/EntityAssets.cs}` | распознавание `FindOrLoadSceneWithServices(Scenes.X.Value)` |
| `client/platforms/web/Editor/WebGlBuild.cs` | вызов `SharedAddressablesSync.Sync()` |
| `tools/scripts/deploy-client/verify.sh` | проверка `aa/catalog.bin` |
| `client/packages/com.unity.addressables` | форк, удалить |
| `client/measurements/parts/content-directory*/` (ветка `content-loading-size`) | рабочий прототип: jslib, загрузка, манифест, нули |
| pixel-swarm `client/Assets/GamePlay/Levels/LevelDefinition.cs`, `LevelScopeExtensions.cs` | `AssetReference` сцены и энкаунтера |
| pixel-swarm `client/Assets/GamePlay/Swarm/Encounters/EncounterFinal.cs`, `Loop/Run/RunFinals.cs` | ссылки и загрузка финальных врагов |
| pixel-swarm `client/Assets/GamePlay/Tools/{Levels,Encounters,Tests}/*`, `Stress/Editor/StressBuildStep.cs` | редакторные инструменты на Addressables |
| pixel-swarm `client/Assets/AddressableAssetsData/` | 40+ групп, удалить |
| pixel-swarm `client/Assets/WebGLTemplates/*/index.html` | обработка бандлов на странице |

### Документация к прочтению
- `.claude/docs/COMMON_CONTAINER.md` — `IScopeBuilder`, `Register` / `As<>`, примитивы генератора, `ISceneService`.
- `.claude/docs/COMMON_LIFETIMES.md`, `COMMON_LIFETIMES_PATTERNS.md` — lifetime как владелец результата загрузки,
  `Listen`, `Token`.
- `.claude/docs/API_DESIGN_FULL.md` — UniTask, `IReadOnlyList`, файловый ввод-вывод, обёртка колбэков (jslib).
- `.claude/docs/CODE_STYLE_FULL.md` — порядок членов, `NoAwait`, catch на границе (скачивание).
- `.claude/docs/WEBGL_TROUBLESHOOTING.md` — Cache API по http, стриппинг, Brotli, B6 / CRC Mismatch (что уходит).
- `client/measurements/ANALYSIS.md` (ветка `content-loading-size`) — замеры и поведение Content Directories на a10.
- Unity: [Content Directories](https://docs.unity.com/en-us/engine/6000.7/manual/assets-and-media/assets-managing-runtime/content-directories/introduction),
  [блог](https://unity.com/blog/content-directories-beyond-the-assetbundle),
  [формат](https://github.com/Unity-Technologies/UnityDataTools/blob/main/Documentation/contentdirectory-format.md).

### Риски
- **Альфа API.** Content Directories в a10 — экспериментальный API. Удалённая доставка официально не поддерживается,
  наша схема (нули + докачка) опирается на поведение, а не на контракт. Новая альфа Unity может её сломать. Смоук
  измерений `content-directory-single` — сторож при каждом обновлении Unity.
- **Формат манифеста** может измениться: разбор в одном классе с тестом.
- **Play Mode без сборки** (вопрос b). Если `Loadable` в редакторе не грузит без каталога, каждый вход в Play Mode
  требует сборки каталога (секунды или минуты) или своего пути через `AssetDatabase`. Это решает, насколько удобной
  останется разработка.
- **Счётчик ссылок движка** (вопрос c). Если `Release` одного `Loadable` выгружает объект для всех, счётчик целиком
  на нашей стороне, по id.
- **Память в вебе.** Файлы в MEMFS живут в памяти JS всё время регистрации, плюс загруженные объекты. На iPhone это
  важно. Выгрузка файлов зарегистрированного каталога не проверена (вопрос f).
- **Дубликаты из плеера и встроенных ресурсов** съедают выгоду. Лог сборки должен их показывать.
- **Миграция сериализованных ссылок** pixel-swarm: потерянная ссылка ломает уровень молча. Нужна проверка «ни одной
  пустой ссылки» до удаления `AddressableAssetsData`.
- **Другие игры на `v0.6.0`** не получат исправлений, сделанных в ветке, пока она не слита. Срочные исправления для
  них делаются в main отдельно.
- **Push тега и веток** — действие наружу, только после подтверждения пользователя.
