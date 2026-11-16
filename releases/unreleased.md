# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

Шаги написаны для игры на `v0.7.0` (Addressables). Ветка прошла промежуточную схему content directories (`v0.7.0-rc.*`, `ExtraContent`,
`AddContentDirectory`, Range-запросы архива, индекс, порядок загрузок): её нет ни в `main`, ни здесь, игре на `v0.7.0` она не нужна.
Единственная игра на ней (gamejam-autobattler, ветка `no-addressables`) берёт из шагов контента только сноски для `v0.7.0-rc.*` в
«Контент 1» (`Framework.json`) и «Контент 8» (переименования).

## Шаги

### ContainerGenerator: сборка без копии в Analyzers

Кого касается: игры, чей скрипт тестов собирает `ContainerGenerator` в Release (или запускает `ContainerGenerator.Verify` в
Release) в том же дереве, где в это время идут тесты Unity.

Release-сборка копирует `ContainerGenerator.dll` в `client/core/Analyzers`. Если это происходит во время batchmode-прогона
EditMode, Unity запрашивает компиляцию скриптов, которая не может пройти до конца прогона; пока она висит,
`EditorApplication.isCompiling == true`, цикл UniTask в редакторе стоит, и каждый тест, который ждёт кадр или таймаут, заканчивается
только по таймауту тест-фреймворка (180 с). Новое свойство `-p:SkipAnalyzersCopy=true` (для `dotnet run` -
`--property:SkipAnalyzersCopy=true`) собирает без копии; без него поведение прежнее.

Проверка: `grep -rn "ContainerGenerator" tools/scripts/*.sh .github/workflows/*.yml`; там, где сборка идёт рядом с Unity на том же
дереве, добавить свойство.

### Контент: с Addressables на AssetBundles движка, порядок шагов (BREAKING)

Кого касается: клиент всех игр. Контент после старта (группы каталогов, сцены скоупов, свои ленивые ссылки) - бандлы движка
(`com.unity.modules.assetbundle`), собранные Scriptable Build Pipeline (`com.unity.scriptablebuildpipeline` 4.0.1) с каждой сборкой
плеера. Форк `com.unity.addressables` и всё, что на нём держалось, удалены. Модель и API: `.claude/docs/CONTENT.md`.

Порядок. Форка больше нет в `tools/framework/client/packages`, поэтому вместе с указателем сразу убрать Addressables из манифеста
(«Контент 5»), затем довести код до компиляции (шаги 2-5), Regenerate All (шаг 3), ассеты `ContentBundle` (шаг 1), ссылки (шаг 4),
`AddressableAssetsData` - последней. Значения полей `AssetReference` сами не переносятся: до правки записать их
(`grep -rn "m_AssetGUID" client/Assets --include='*.asset' --include='*.prefab' --include='*.unity'`; после пересохранения ассета
старое значение есть в `git show <до обновления>:<файл>`).

### Контент 1. Ассеты `ContentBundle`, сборка плеера собирает бандлы (BREAKING)

Любая сборка плеера (`Internal.WebGlBuild.Build`, `deploy-client`, окно сборки, `-build*Player`) собирает каждый ассет `ContentBundle`
проекта (`ContentBuild`: `IPlayerBuildStep` и `BuildPlayerProcessor`); `WebGlBuild` больше не вызывает `SharedAddressablesSync.Sync()`.

Поиск: `grep -rln "ContentBundle" client/Assets --include='*.asset'` (пусто - шаг нужен).

Сделать:
- Create > Framework > Content Bundle - основной бандл; имя ассета = имя бандла = `ContentOptions._name` (шаг 2), буквы, цифры, `_`,
  `-`. Корень - сгенерированный `<ContentFolder>/Runtime/Catalogues/ContentRoot.asset` (шаг 3). `IsPreloaded` - бандл в `.data`
  WebGL (без запросов, больше первая загрузка); `IsGzip` - WebGL-плеер качает `<file>.gz` и распаковывает его на странице.
- Основной бандл (и каждый бандл `AddContentBundle`) грузится целиком и держится до конца корневого скоупа. В бандл входят его корни
  и всё, чего они достигают прямыми и ленивыми ссылками, кроме корней других бандлов: без других бандлов все группы каталогов едут
  в основном и грузятся на старте. Тяжёлое (группы уровней, музыка, сцены) - корнями отдельных бандлов `ContentBundle`: они
  грузятся на lifetime загрузки и выгружаются после неё, а основной держать тонким.
- Сборка падает на корне двух бандлов, на ассете, которого два бандла достигают напрямую и который не назначен ни одному (сделать
  его корнем одного из них или общего бандла), на ленивой цели без корня, которой достигают два бандла, на пустой или битой ленивой
  ссылке, на ссылке на дочерний объект префаба.
- `Assets/FrameworkContentStamp` в `.gitignore` игры: каталог бандлов (`Resources/Content/catalog.json`) пишется туда на время сборки
  и удаляется после неё (остаётся, только если редактор упал посреди сборки).
- Из `ProjectSettings/Framework.json` убрать `WithoutAddressables`, `SharedCommonPaths`, `GroupPhases` (лишние ключи игнорируются).
  Секции `Content` / `ExtraContent` (только у игр на `v0.7.0-rc.*`) - ошибка редактора: на каждую директорию ассет `ContentBundle`
  с её `Roots` и `IsPreloaded` (`IsArchiveGzip` -> `IsGzip`).

Проверка: сборка логирует `[ContentBuild] N content bundles for <target>: ...` и `[ContentBuild] <name>: ...` по каждому бандлу (файлы,
байты, как плеер его получает, зависимости, корни, ассеты, которые движок скопировал внутрь; предупреждение про встроенные ресурсы);
в `StreamingAssets/Content` сборки только `<name>.<hash>.bundle[.gz]` (у бандла со сценами ещё `<name>.scenes.<hash>.bundle[.gz]`).

### Контент 2. `AddContent` в корневом скоупе, скоупы через `IContentLoader` (BREAKING)

Поиск: `grep -rn ": IScopeBuilder\|new ServiceScopeSceneLoader\|WithAssetScene\|FindOrLoadScene\|LoadRoot" client/Assets --include='*.cs'`

Сделать:
- Корневой `Construct`: `await builder.AddContent(options.Content)` один раз, до первой загрузки (`IContentLoader`, `IContentBundles`,
  каталоги привязаны, предзагруженные группы скачаны). `ContentOptions` - поле опций игры: `_name` (шаг 1), `_remoteUrl` для CDN
  (пусто - `StreamingAssets` страницы), `_maxFetches` (64, сколько файлов WebGL качает одновременно).
- Скоупы: `FindOrLoadSceneWithServices(ContentScene)`, `FindOrLoadScene<T>(ContentScene)`, `ScopeLoadOptions.WithAssetScene(ContentScene)`,
  `await builder.LoadContent(reference)`, `builder.RequestContent(reference, out var slot)` (`slot.Value` после сборки скоупа); во время
  игры `[Inject] IContentLoader`: `Load`, `LoadScene`, `Fetch`, `Progress`, `LoadNow` на lifetime владельца (конец lifetime освобождает).
- `IScopeBuilder` получил `ScopeContent Content`, конструктор `ServiceScopeSceneLoader` - `(lifetime, content)`: свои реализации (тесты)
  добавляют свойство. Без `AddContent` сборка скоупа с контентом бросает.

### Контент 3. Каталоги на бандлах: `ContentRoot`, `IsPreloaded`, `Scenes.X` - `ContentScene` (BREAKING)

Группы каталогов (env assets, prefabs, audio, sprites) грузятся через `IContentLoader` по ссылкам сгенерированного `ContentRoot`
(группы, предзагруженные группы, сцены папок сцен вне Build Settings). API групп прежний (`Retain` / `Release`, `RequestAssetGroup`,
`LoadAssetGroup`, `EnsureLoaded`). Удалены `StaticScene` и перегрузки сцен скоупов с `AssetReference`: `ServiceScopeSceneLoader.Load(AssetReference)`,
`AssetScene`, `WithAssetScene(AssetReference)`, `LoadTyped` / `FindOrLoadScene` / `FindOrLoadSceneWithServices(AssetReference)`, `LoadedScene`.

Поиск: `grep -rnE "IsAddressable|SetAddressable|Scenes\.[A-Za-z0-9]+\.Value|StaticScene|\.Address\b|ResourcePath|SetAddress\(" client/Assets --include='*.cs'; grep -rln nonAddressable client/Assets`

Сделать:
- В json-списках групп ключ `nonAddressable` -> `preloaded` (старый ключ - исключение генератора `nonAddressable is replaced by
  preloaded`); `IsAddressable` -> `IsPreloaded` с обратным смыслом (`true` у бывших групп из `Resources`).
- Regenerate All (шаг `Content root` вместо `Shared addressables`; генератор `ContentRoot` ещё и перед каждой сборкой бандлов). Ассеты
  групп из `.../Resources/AssetGroups`, `.../Resources/PrefabGroups` он удаляет и пишет заново в папку каталога; пустые папки `Resources`
  удалить. `ContentRoot.asset` руками не править.
- Сгенерированные классы групп: `AssetGuid` вместо `Address` / `ResourcePath`; свои определения групп (`ICatalogGroupDefinition`) -
  `Guid` / `SetGuid` вместо `Address` / `SetAddress`. `AssetGroup<TAsset>` требует `TAsset : ScriptableObject`.
- `Scenes.X.Value` -> `Scenes.X` (`ContentScene`), читается только после `AddContent`. Сцен Build Settings (`Startup`) в `Scenes` нет.

### Контент 4. Свои ссылки: `AssetReference` -> `ContentReference<T>` / `ContentScene` (BREAKING)

Кого касается: игры со своими ссылками Addressables (уровни, финалы забега, моки).

Поиск: `grep -rnE "AssetReference(T|GameObject|Sprite)?\b|Addressables\.|LoadAssetAsync|InstantiateAsync|AsyncOperationHandle" client/Assets --include='*.cs'`

Сделать:
- Поле `AssetReference` сцены -> `ContentScene`, ассета -> `ContentReference<T>` (`AssetReferenceT<T>` -> `ContentReference<T>`;
  ссылка на компонент - на его префаб, загрузчик отдаёт компонент). Значения назначить заново (записанные GUID, см. «Порядок») в
  инспекторе или генератором (`ContentReferenceFields.AssignObject` / `AssignScene` редактора); пустую ссылку сборка перечислит.
- `Addressables.LoadAssetAsync` / `Release` -> `IContentLoader.Load(lifetime, reference)` (освобождение - конец lifetime), в `Construct`
  скоупа - `LoadContent` / `RequestContent`. Ссылки работают везде, в сцене плеера (`Startup`) тоже: каталог знает бандл цели.
- Моки: `reference.EditorAsset`, `scene.EditorPath` вместо `editorAsset` / пути `AssetReference`.

### Контент 5. Addressables удалены из фреймворка (BREAKING)

Форк `com.unity.addressables` удалён, `com.noncasted.framework.core` зависит от `com.unity.modules.assetbundle` и
`com.unity.scriptablebuildpipeline` 4.0.1 (раньше SBP приходил через Addressables), сборки `Internal`, `Internal.Editor` на
`Unity.Addressables` / `Unity.ResourceManager` / `Unity.Addressables.Editor` не ссылаются. Удалены синхронизации групп
(`CatalogAddressablesSync`, `ScenesAddressablesSync`, `SharedAddressablesSync`, `SharedAddressablesTargets`,
`SharedAddressablesPostprocessor`), `BundleUrlVersioning`, настройки `WithoutAddressables`, `SharedCommonPaths`, `GroupPhases`.

Поиск: `grep -rnE "addressables|Addressab|AssetReference|ResourceManagement|BundleUrlVersioning|WithoutAddressables|SharedCommonPaths|GroupPhases" client/Packages client/Assets client/ProjectSettings/Framework.json --include='*.json' --include='*.asmdef' --include='*.cs' --include='*.xml'; ls -d client/Assets/AddressableAssetsData`

Сделать:
- `client/Packages/manifest.json`: строку `com.unity.addressables` убрать (`file:` на форк больше не разрешится); `packages-lock.json`
  перепишет Unity, закоммитить (`grep -n scriptablebuildpipeline client/Packages/packages-lock.json` - пакет на месте).
- Из своих asmdef - ссылки на `Unity.Addressables`, `Unity.ResourceManager`, `Unity.Addressables.Editor` (GUID
  `9e24947de15b9834991c9d8411ea37cf`, `84651a3751eca9349aac36a66bba901b`, `69448af7b92c7f342b298e06a37122aa`).
- Вызов `BundleUrlVersioning.Setup()` убрать; `link.xml` игры - записи `Unity.Addressables` / `Unity.ResourceManager`.
- `Assets/AddressableAssetsData` удалить последним, после шага 4.

### Контент 6. Шаблоны WebGL, сервер и скрипт деплоя (BREAKING)

Кого касается: клиент игр с WebGL.

Поиск: `grep -rnE "\.bundle|UnityCache|aa/|catalog\.bin|Addressables" client/Assets/WebGLTemplates client/deploy tools/scripts`

Сделать:
- Шаблоны: ветку `cacheControl` для `.bundle` убрать - файлы бандлов качает `WebContentFetch` сам (`fetch` + Cache API `content`, ключ -
  URL, при старте записи файлов другой сборки удаляются), не загрузчик Unity и не UnityCache. Нужен `DecompressionStream` браузера
  для `.gz` бандлов (`IsGzip`).
- Сервер (`nginx.conf`): правила для `StreamingAssets/aa` заменить на `StreamingAssets/Content/`: файлы `.bundle` и `.gz` как есть
  (`.gz` с `Content-Encoding: gzip` или без, обычный `.bundle` сервер может сжать сам), `Cache-Control: "public, max-age=31536000,
  immutable"` (имена - хэши сборки), `try_files $uri =404` (иначе отсутствующий файл приходит `index.html` 200). Для вложенной сборки
  (`/tg/`) то же внутри её `^~`-блока. Range-запросы не нужны.
- Скрипт деплоя игры: проверку `StreamingAssets/aa/catalog.bin` (pixel-swarm `tools/scripts/deploy-webgl.sh`) удалить - она отклонит
  каждую сборку.

### Контент 7. `deploy-client` проверяет бандлы, нужен `brotli` (BREAKING для машины сборки)

Кого касается: игры с `deploy-client`. `verify.sh` вместо `StreamingAssets/aa/catalog.bin` и дублей бандлов проверяет бандлы сборки:
каждый файл `Library/ContentShipped` этой сборки лежит в `StreamingAssets/Content` или предзагружен, назван каталогом в `.data`,
является бандлом (`UnityFS`, `.gz` - его gzip), чужих файлов рядом нет. `deploy-client.json` не меняется.

Поиск: `command -v brotli` - без него проверка падает (`brotli is not installed: the content bundles are not checked`), поставить пакет.

### Контент 8. Дополнительные бандлы со своими корнями (необязательно)

Кого касается: игры, которым нужен свой загрузчик рядом с основным (музыка меню, которая качается после старта). Без шага ничего не
меняется.

В корневом скоупе после `AddContent`: `builder.AddContentBundle(name)` (в фоне, корень и скоупы после него не ждут) или
`builder.AddContentBundle(name, ContentRegistration.OnFirstWait)` (ничего не качается до первого `Wait`). Загрузчик:
`await [Inject] IContentBundles.Wait(lifetime, name)`, корни - `RootAssets<T>()`; `Get(name)` бросает до регистрации, ошибка
регистрации - в лог и в каждый `Wait`. Каталоги к нему не привязываются; любой загрузчик грузит любую ссылку.

Игра на `v0.7.0-rc.*`: `AddContentDirectory(new ContentOptions(name), registration)` -> `AddContentBundle(name, registration)`,
`IContentDirectories` -> `IContentBundles` (`Get` / `Wait` те же), `ContentOptions` без `rangeGap`; `ContentReference` без `Id` и `Loadable`
(конструктор `(guid, localId)`), `ContentReferenceFields.SyncObject` / `SyncScene` удалены, `IContentBuildStep.BeforeContentBuild()`
без параметра. Сериализованные ссылки rc переносятся сами (`_guid` / `_localId`).

### Лямбда как корень скоупа - ошибка компиляции `CINGR017` (BREAKING)

Кого касается: клиент всех игр. Лямбда или анонимный метод на месте корня скоупа (`Func<IScopeBuilder, UniTask>`,
`Func<IEntityBuilder, UniTask>`: `new ScopeLoadOptions(parent, b => ...)`, `LoadRoot(b => ...)`, `construct` у `IEntityScopeLoader.Load`)
раньше компилировалась и роняла каждую загрузку скоупа (`Scope root must be a method group or a local function`); теперь это ошибка
генератора `CINGR017` с местом лямбды.

Поиск: `grep -rnE "(ScopeLoadOptions\(|LoadRoot\(|\.Load\()[^;]*=>" client/Assets --include='*.cs'`

Сделать: корень - method group или локальная функция (она замыкает те же значения); корень с одним аргументом -
`ScopeLoadOptions.Create(parent, Construct, arg)`.

### uGUI вне ядра: сборка `Internal.UGUI` (BREAKING)

Кого касается: клиент, все игры, которые вызывают `ListenClick`, `SetAlpha` (`Image`), `ToLifetimedValue` (`Slider`) или `ButtonSounds`.

Ядро (`Internal`) больше не использует uGUI, чтобы игра без `com.unity.ugui` собиралась. Эти помощники перенесены в сборку
`Internal.UGUI` пакета ядра (`client/core/UGUI`, GUID `1a5b96d3d6de4210867a24fe0a1f3fcb`, компилируется только при установленном
`com.unity.ugui`), пространство имён прежнее (`Internal`); `ColorUtils` переименован в `ImageExtensions`. Модуль `ui` ссылается на неё сам.

Проверка: `grep -rln -E "ListenClick\(|\.SetAlpha\(|ButtonSounds\.|Slider.*ToLifetimedValue" client/Assets --include=*.cs`; в asmdef каждой
найденной сборки добавить `"GUID:1a5b96d3d6de4210867a24fe0a1f3fcb"` в `references`. Ошибка без этого: CS1061 `'Button' does not contain a
definition for 'ListenClick'`.

### Ядро без `Framework.Shared`: `StableHash` в `Framework.Common` (BREAKING)

Кого касается: клиент, все игры.

`Internal` больше не ссылается на `Framework.Shared`, а пакет ядра не тянет `com.noncasted.framework.shared` (и через него MemoryPack):
игра без сетевого протокола собирается без них. `Shared.StableHash` перенесён в `Framework.Common`, пространство имён прежнее.

Проверка: `grep -n '"com.noncasted.framework.shared"' client/Packages/manifest.json` - игра с протоколом (`shared/` игры, модули
`network`, `telemetry`, `lobbies`) указывает пакет в манифесте сама, если ещё не указывает. `grep -rln "StableHash" client/Assets
backend shared --include=*.cs`: asmdef каждой такой сборки ссылается на `Framework.Common` (`GUID:3ea5c3f1172040daa713d5ad71728b78`),
csproj - на `framework.common.csproj`; без этого CS0103 `The name 'StableHash' does not exist`.

### Декодер mp3 аудио-групп в сборке `Internal.EncodedAudio` (BREAKING)

Кого касается: клиент, игры, которые вызывают `EncodedAudio` из своего кода (редакторские инструменты аудио).

Декодирование клипов, отгруженных байтами mp3 (`*.mp3.bytes`), перенесено из `Internal` в сборку `Internal.EncodedAudio` пакета ядра: ядро
больше не ссылается на `UnityWebRequest`. Сборка регистрирует себя при старте (`[AlwaysLinkAssembly]`), группы работают как раньше.
`EncodedAudio.Extension` -> `EncodedAudioDecoder.Extension`, статический `EncodedAudio.Decode` -> `EncodedAudioDecoder.Decode`.
Игра без mp3 в аудио-группах может убрать сборку из плеера (`"StrippedPlayerAssemblies": [ "Internal.EncodedAudio" ]` в
`ProjectSettings/Framework.json`), вместе с ней из плеера уходят `UnityWebRequest` и `Uri` / `Regex` из `System.dll`.

Проверка: `grep -rn "EncodedAudio\." client/Assets --include=*.cs` - заменить по списку выше.

### Телеметрия клиента: сокетный транспорт в сборке `Internal.Telemetry.Socket`, транспорт по HTTP (BREAKING)

Кого касается: клиент, все игры с модулем `telemetry`; бэкенд - только игры, которые хотят телеметрию без мета-сокета.

`Internal.Telemetry` больше не ссылается на `Internal.Network`, `Framework.Shared` и `Framework.Session.Core`, пакет `telemetry` не тянет
пакеты `network` и `shared`. Транспорт по мета-сокету (`AddSocketClientTelemetry`, `ITelemetrySocket`, `TelemetrySocketTransport`,
`TelemetryMessages`, `MetaNetworkTelemetryCollector`) перенесён в сборку `Internal.Telemetry.Socket` (`client/modules/telemetry/Socket`,
GUID `802942ed80b040b1af69d20046688f31`, компилируется только при установленном пакете `com.noncasted.framework.network`), пространство
имён прежнее (`Global.Telemetry`). Новый транспорт без сокета: `AddHttpClientTelemetry(options, new TelemetryHttpOptions("/api/telemetry"))`
- JSON (`JsonUtility`) через `fetch` страницы, на бэкенде `MapClientTelemetryHttpIntake()` на группе игры с её авторизацией
(`backend/ClientTelemetry/Contract/CONTRACT.md`, раздел «HTTP»).

Проверка: `grep -rln "AddSocketClientTelemetry\|ITelemetrySocket" client/Assets --include=*.cs` - asmdef каждой такой сборки добавляет
`"GUID:802942ed80b040b1af69d20046688f31"` в `references`; без этого CS1061 `'IScopeBuilder' does not contain a definition for
'AddSocketClientTelemetry'` / CS0246 `ITelemetrySocket`. Игра, у которой `network` или `shared` приходили в манифест только через
`telemetry`, указывает их в `client/Packages/manifest.json` сама (`grep -n '"com.noncasted.framework.network"\|"com.noncasted.framework.shared"'
client/Packages/manifest.json`).

### Лейауты в пакете `com.noncasted.framework.layout` (сборка `Internal.Layout`) (BREAKING)

Кого касается: клиент, `BREAKING` для сборок, которые используют лейаут из кода (в том числе сгенерированные `*Bindings.g.cs`):
mines-leader (`GamePlay`, `Menu`), peace-data-reborn (`GamePlay`). Сцены и префабы не трогать: GUID скриптов, namespace
(`Global.UI`) и имена классов прежние.

`DesignContainer`, `DesignGrid`, `DesignItem`, `DesignLayoutBase` и их перечисления (`DesignAxis`, `DesignMainAxisMode`, ...) перенесены
из `Internal.UI` в сборку `Internal.Layout` нового пакета `client/modules/layout` (GUID `632dc5ed7f974d89b8b5e2acd4a1bbda`, без ссылок на
uGUI / TMP / inputs), инспекторы - в `Internal.Layout.Editor`, тесты - в `Internal.Layout.Tests`. Сборка `Internal.UI.Editor` удалена
(в ней были только инспекторы лейаута). Пакет `ui` зависит от `layout`, игре без `ui` (только спрайты) лейаут доступен отдельно.

Сделать:
- `client/Packages/manifest.json`: `"com.noncasted.framework.layout": "file:../../tools/framework/client/modules/layout"` рядом с
  `com.noncasted.framework.ui` (зависимость `file:` не подтягивается сама).
- `grep -rln "DesignContainer\|DesignGrid\|DesignItem\|DesignLayoutBase\|DesignAxis\|DesignMainAxisMode\|DesignCrossAxisMode\|DesignContainerFit\|DesignHorizontalPivot\|DesignVerticalPivot\|DesignItemSizing\|DesignRefreshMode\|DesignGridConstraint\|DesignGridCellSizing\|DesignGridFlow" client/Assets --include=*.cs`
  - asmdef каждой такой сборки добавляет `"GUID:632dc5ed7f974d89b8b5e2acd4a1bbda"` в `references`; без этого CS0246
  `The type or namespace name 'DesignContainer' could not be found`.
- Ссылка на `Internal.UI.Editor` (GUID `221aaddb19c647fab6fe86dcb68ac643`) в asmdef игры, если есть, удаляется
  (`grep -rln "221aaddb19c647fab6fe86dcb68ac643\|\"Internal.UI.Editor\"" client/Assets --include=*.asmdef`).

### Локализация без TMP (`LocalizedLabel` в сборке `Internal.Localization.Tmp`) (BREAKING)

Кого касается: клиент, `BREAKING` для сборок игры, которые используют `LocalizedLabel` фреймворка. Сейчас ни одна игра пакет
`com.noncasted.framework.localization` не подключает (у pixel-swarm и peace-data-reborn свои `LocalizedLabel` в `client/Assets`,
их это не касается); шаг для игры, которая подключила пакет до этого релиза.

`Internal.Localization` больше не ссылается на TextMeshPro и собирается в игре без uGUI / TMP. `LocalizedLabel` (namespace
`Global.Localization`, GUID скрипта прежний) перенесён в сборку `Internal.Localization.Tmp` того же пакета
(`client/modules/localization/Tmp`, GUID `efc5f5618d014be4a2157ab8e084fdae`): она компилируется только при `com.unity.ugui` 2.x в
проекте (`versionDefines` -> `FRAMEWORK_TMP`, `defineConstraints`). Тест метки - в `Internal.Localization.Tmp.Tests`.

Сделать:
- `grep -rln "LocalizedLabel" client/Assets --include=*.cs` - asmdef каждой такой сборки добавляет
  `"GUID:efc5f5618d014be4a2157ab8e084fdae"` в `references`; без этого CS0246 `The type or namespace name 'LocalizedLabel' could not
  be found`. Своя копия `LocalizedLabel` в `client/Assets` (pixel-swarm, peace-data-reborn) не трогается.
- Игре без `com.unity.ugui` пакет теперь доступен: `"com.noncasted.framework.localization": "file:../../tools/framework/client/modules/localization"`
  в `client/Packages/manifest.json` (core и common уже должны там быть).

## Проверить поведение

Контент (шаги «Контент»):

- Play Mode: ничего не собирается и не грузится, ссылка отдаёт ассет проекта (`AssetDatabase`), корни - из ассета `ContentBundle`
  (нет ассета с именем `ContentOptions._name` - `No ContentBundle asset <name>`); освобождение ничего не уничтожает.
- Конец lifetime загрузки освобождает объект / выгружает сцену; ожидающий получает `OperationCanceledException`. Бандл выгружается
  с последней загрузкой: пока его держит другая загрузка или регистрация, освобождённая ссылка `IsLoaded == false`, а объект жив.
- `Progress(...).Total` 0 у бандлов на устройстве (standalone, Android, предзагруженные, Play Mode).
- `LoadNow` / `EnsureLoaded` бросают (`... needs a fetch: load it with Load`) для бандла, которого нет на устройстве, кроме
  предзагруженных групп (их качает `AddContent`); ссылка, созданная в коде мимо сборки - `... is in no content bundle`.
- Веб: второе открытие не делает ни одного запроса к `Content/` (Cache API); по http Cache API нет.
- Сцена с `m_LightingDataAsset {fileID: 20201}` (данные освещения по умолчанию) тянет встроенный кубмап в бандл: предупреждение
  `[ContentBuild]` про встроенные ресурсы - очистить поле у сцен без запечённого света.
- Стартовый трейс: `Content: catalog`, `Content: bundle <name>`, `Content: roots <name>`, `Content: bind catalogues`, `Group: <name>`,
  `Content: load <type>`; загрузки - `AssetLoads` с ключом `Content: <name>`.

## Без действий

- `CINGR001` для сцены без сервисов называет причину: asmdef сцены не ссылается на content-сборку (`FrameworkSettings.ContentAssembly`)
  или карту ассетов нужно перегенерировать (`Tools/Framework/Codegen/Container Asset Map`).
- `ScopeArgumentRoot` вызывает корень через `Invoke`: нет `CINGR016` на обобщённой обёртке.
- Игра без uGUI собирает ядро: модули движка объявлены в пакете, тесты uGUI отдельно.
