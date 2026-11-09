# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.


### Сборка плеера собирает content directory (BREAKING)

Кому: клиент всех игр. Любая сборка плеера (`Internal.WebGlBuild.Build`, `deploy-client`, окно сборки) теперь собирает content
directory из секции `Content` файла `ProjectSettings/Framework.json` (`ContentBuild`): без секции сборка падает. `WebGlBuild` больше не
вызывает `SharedAddressablesSync.Sync()`.

Поиск: `grep -n '"Content"' client/ProjectSettings/Framework.json`

- Добавить `"Content": { "Name": "<имя>", "Roots": [ "<путь ContentRoot>" ], "IsPreloaded": false }`; корень пишет генератор каталогов
  (шаг перед сборкой каталога — `IContentBuildStep`).
- Пустой `Loadable` / `LoadableSceneId` или ссылка на ассет вне каталога в ассетах и сценах каталога — ошибка сборки со списком.
- На время сборки создаются `Assets/FrameworkContentIndex` (индекс в `Resources`) и
  `Library/PlayerDataCache/WebGLPreloadedStreamingAssets/framework-content.manifest`; после сборки удаляются. Папку
  `Assets/FrameworkContentIndex` можно добавить в `.gitignore` игры (останется, только если редактор упал посреди сборки).

### Контент и сцены скоупов через `IContentLoader` (BREAKING)

Кому: клиент всех игр. Скоупы грузят сцены и объекты content directory: `builder.AddContent(ContentOptions)` в корневом скоупе (один
раз: каталог сборки в хранилище платформы, в Play Mode — ассеты проекта по секции `Content` файла `Framework.json`; имя в
`ContentOptions` должно совпадать с `Content.Name`), затем в скоупах `FindOrLoadSceneWithServices(ContentScene)`,
`FindOrLoadScene<T>(ContentScene)`, `ScopeLoadOptions.WithAssetScene(ContentScene)`, `await builder.LoadContent(reference)` и
`builder.RequestContent(reference, out var slot)` (`slot.Value` после сборки). Перегрузки с `AssetReference` удалены (см. ниже).
`IScopeBuilder` получил `ScopeContent Content`, конструктор `ServiceScopeSceneLoader` — `(lifetime, content)`.

Поиск: `grep -rn ": IScopeBuilder\|new ServiceScopeSceneLoader" client/Assets`

- Свои реализации `IScopeBuilder` (тесты) — добавить `public ScopeContent Content => ...`.
- Корневой `Construct` — `await builder.AddContent(options)` до первой загрузки контента; `ContentOptions` — поле опций игры
  (`_name`, `_remoteUrl` для CDN, пусто — `StreamingAssets`).

### Каталоги на content directory: `ContentRoot`, `Scenes.X` — `ContentScene` (BREAKING)

Кому: клиент всех игр. Группы каталогов (env assets, prefabs, audio, sprites) грузятся не из Addressables, а через `IContentLoader`
по ссылкам сгенерированного `ContentRoot` (`<ContentFolder>/Runtime/Catalogues/ContentRoot.asset`: группы, предзагруженные группы,
сцены папок сцен вне Build Settings). `AddContent` привязывает к нему каталоги (`CatalogContent`) и скачивает файлы предзагруженных
групп. API групп прежний (`Retain` / `Release`, `RequestAssetGroup`, `LoadAssetGroup`, `EnsureLoaded`). Удалены `StaticScene` и
перегрузки сцен скоупов с `AssetReference`: `ServiceScopeSceneLoader.Load(AssetReference)`, `AssetScene`, `WithAssetScene(AssetReference)`,
`LoadTyped` / `FindOrLoadScene` / `FindOrLoadSceneWithServices(AssetReference)`, `LoadedScene`.

Поиск: `grep -rn "IsAddressable\|SetAddressable\|Scenes\.[A-Za-z0-9]*\.Value\|StaticScene\|AssetReference" client/Assets; grep -rln nonAddressable client/Assets`

- `Content.Roots` в `ProjectSettings/Framework.json` — добавить путь `ContentRoot` (`Assets/<ContentFolder>/Runtime/Catalogues/ContentRoot.asset`),
  затем Regenerate All (шаг «Content root» вместо «Shared addressables»; генератор ещё и перед каждой сборкой каталога).
- `IsAddressable` → `IsPreloaded` (обратный смысл: `true` у бывших групп из `Resources`); в json-списках групп ключ `nonAddressable`
  переименовать в `preloaded` (старый ключ — исключение генератора). Ассеты групп из `.../Resources/AssetGroups`, `.../Resources/PrefabGroups`
  Regenerate All удаляет и пишет заново в папку каталога; пустые папки `Resources` удалить.
- `Scenes.X.Value` → `Scenes.X` (`ContentScene`); `Scenes.X` читается только после `AddContent` (иначе исключение). Сцены Build Settings
  (`Startup`) в `Scenes` больше нет.
- Свои ссылки `AssetReference` на сцены (`level.Scene`) — `ContentScene`, на ассеты — `ContentReference<T>`.

