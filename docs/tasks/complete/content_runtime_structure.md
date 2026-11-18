## Рефакторинг `client/core/Runtime/Content` и `client/core/Editor/Content`: имена и папки по ролям

### Что сделано
- Рантайм разложен сверху вниз: в корне только `ContentEntryPoint` (корень контента корневого скоупа, `RegisterMain`) и `ContentOptions`;
  `Scopes/` (API скоупов, делегирует корню) -> `Loading/` (`IContentLoadersCollection`, `ContentLoader`, `ContentLoadsCollection`)
  -> `Sources/` (`BundleContentSource`, `EditorContentSource`, `ContentTarget`) -> `Storage/` (`ContentBundleStorage`, `ContentStoredBundle`)
  -> `Distribution/` (`IContentDistribution`, `ContentFileFetches`, `ContentFileFetch`, `ContentShippedPaths`; `Web/` с jslib);
  `Catalog/` (`ContentCatalog*Entry`), `Common/` (ссылки, сцены, прогресс).
- Хранилище только держит бандлы: одна загрузка на файл, байты и префетч вынесены в `ContentFileFetches` (P1); веб-доставку собирает
  `WebContentDistribution.Open`.
- Редактор: `ContentBuild` в корне, `Steps/` -> `Shared/` -> `Layout/` (+ `Graph/`) -> `Pipeline/` -> `Output/` -> `Player/`, данные игры
  в `Definitions/`; пары имён в одну букву убраны (`ContentOwnership`, `ContentAssetOwner`, `ContentLazyReferenceScanner`, `ContentPlayerStamp`).
- Цикл сборки контента с каталогами разорван фазой `IContentLayoutStep` (`BeforeLayout` / `AfterBundles`, `ContentBuildContext.ExcludedBundles`):
  `ContentRoot` без корней исключённых бандлов пишет `ContentRootBuildStep` каталогов.
- BREAKING для игр (`releases/unreleased.md`): `IContentBundles` -> `IContentLoadersCollection` (storytale-workshop, gamejam-autobattler),
  `ContentLoader.Register` -> `ContentEntryPoint.RegisterMain`.

### Ключевые файлы
- `client/core/Runtime/Content/ContentEntryPoint.cs`
- `client/core/Runtime/Content/Distribution/ContentFileFetches.cs`
- `client/core/Editor/Content/ContentBuild.cs`, `client/core/Editor/Content/Steps/IContentLayoutStep.cs`
- `client/core/Editor/Catalogues/Shared/ContentRootBuildStep.cs`
- `.claude/docs/CONTENT.md` § Code layout, § Build

### Заметки
- Поведение доказано сравнением с эталоном старого кода: test-all (EditMode 1299, PlayMode 145, с двумя новыми тестами на старом
  коде), нормализованный каталог тестовых бандлов, GUID всех `.meta` на новых путях, дамп API, WebGL-сборки старого и нового кода
  (бандлы и каталог побайтно равны; `build.wasm.gz` +5.4 КБ gzip от новых имён и файлов).
- Сырой md5 `catalog.json` тестовых бандлов недетерминирован между прогонами (GUID, хеши в именах) — сравнивать нормализованный.
- `client/project` не собирает WebGL-плеер: пустой `DefaultSubmoduleStrippingSettings.asset`, `WebGlStripping.Require()` бросает;
  код под `#if UNITY_WEBGL && !UNITY_EDITOR` проверяется `PlayerBuildInterface.CompilePlayerScripts` или сборкой в копии `.build/`.
- Открыто: `Loading/ContentLoadersCollection` использует `Scopes/ContentRegistration` — зависимость вверх по раскладке.
