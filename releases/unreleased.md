# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

### Контент: `IContentBuildStep.BeforeContentBuild(ContentBuildContext)`, бандлы вне сборки (BREAKING)

Кого касается: клиент игр со своими `IContentBuildStep`, и игр, которым часть контента нужна не в каждой сборке (pixel-swarm: Stress).

Поиск: `grep -rn "IContentBuildStep\|BeforeContentBuild" client/Assets --include='*.cs'`

Сделать:
- `public void BeforeContentBuild()` -> `public void BeforeContentBuild(ContentBuildContext build)`; без этого CS0535.
- Бандл, которого не должно быть в сборке, оставляет шаг: `if (<не эта сборка>) build.Exclude("Stress");` (`build.Target` - платформа,
  профиль - `BuildProfile.GetActiveBuildProfile()`). Его нет в бандлах, каталоге и `ContentRoot` этой сборки, ассет в проекте остаётся;
  ссылка собранного бандла на то, что он держит, - ошибка сборки (`... held by the content bundle Stress, which this build leaves out`).
  pixel-swarm: шаг `PLATFORM_STRESS` профиля, как раньше `StressBuildStep` с группой Addressables, вместо обхода с бандлом Stress в
  каждой сборке; проверить, что website и telegram собираются без Stress, а web_stress с ним.

### Контент: общие бандлы игры - `IContentSharedBundles` вместо своего синка

Кого касается: клиент игр, которые пишут общие бандлы сами, разбирая текст ошибок раскладки (pixel-swarm `ContentSharedRoots`).

Поиск: `grep -rln "ContentBundleLayout.Lay\|is needed by the content bundles" client/Assets --include='*.cs'`

Сделать:
- Удалить свой синк и его `IContentBuildStep` (pixel-swarm: `ContentSharedRoots.cs`, `ContentSharedRootsStep.cs` и пункт меню
  `Tools/Game/Content/Sync Shared Bundles`); вызовы (`LocationLevel` и др.) - на `ContentSharedSync.Run()`.
- Один класс редактора игры: `IContentSharedBundles { Folder; Bundles }` - папка игры с ассетами общих бандлов и упорядоченный список
  `new ContentSharedBundle(name, isPreloaded, isGzip, params serves)`. pixel-swarm: `Folder` - `Assets/Common/Internal/Editor/Content`,
  `SharedCommon` (preloaded, serves `Main`), `SharedLobby` (gzip, serves `Lobby`), `SharedGamePlay` (gzip, без списка - остальное).
  Синк идёт перед каждой сборкой контента (после шагов), в Regenerate All и из Tools/Framework/Codegen/Sync Shared Content Bundles.
- Проверить: Sync Shared Content Bundles не меняет ни одного файла (корни те же, что писал свой синк); сборка без ошибок раскладки.
- Раскладка отдаёт ошибки данными: `ContentBundleLayout.Inspect(bundles, excluded)` - `Duplicates` (путь, бандлы), `Errors`, без
  исключения; для своих инструментов вместо разбора текста `BuildFailedException`.

### Контент: грязная сцена останавливает сборку с понятной ошибкой

Кого касается: игры, чьи тесты или инструменты собирают контент (`ContentBuild.Prepare` / `Build`, `ContentBundleBuilder.Build`).

Поиск: `grep -rn "ContentBuild.Prepare\|ContentBuild.Build\|ContentBundleBuilder.Build" client/Assets --include='*.cs'`

Сделать: ничего обязательного. Сборка бросает `The open scenes ... have unsaved changes` до SBP (было `UnsavedChanges` кодом возврата);
свой тест, который собирает, открывает перед сборкой новую сцену и восстанавливает сцены после (как `ContentTestScenes` тестов фреймворка),
и до создания бандлов в памяти: новая сцена их уничтожает. Тесты фреймворка `ContentBuildTests` в прогоне игры больше не падают от
сцены, оставленной другими тестами.
