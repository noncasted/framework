## Ветка no-addressables в main, релизы 0.8.0 и 0.9.0

### Что сделано
- Ветка `no-addressables` (контент на бандлах движка через SBP) влита в main с тем, что ушло дальше в gamejam-autobattler; релиз 0.8.0.
- Сборка бандлов для цели, не выбранной в Build Settings (`ContentBuildSelection`); тесты собирают под активную платформу редактора.
- После перевода pixel-swarm (0.9.0): `build.Exclude(name)`, общие бандлы игры (`IContentSharedBundles`, `ContentSharedSync`),
  раскладка отдаёт ошибки данными (`ContentBundleLayout.Inspect`), грязная сцена называется в ошибке сборки.
- pixel-swarm на 0.9.0: весь билд 19,36 -> 18,53 МБ gzip, холодный старт до забега на медленной сети 27,7 -> 21,2 с
  (pixel-swarm `docs/web_build_size/2026-11-17`).

### Ключевые файлы
- `client/core/Editor/Content/Build/ContentBuildContext.cs`, `ContentBundleLayout.cs`, `ContentBuildSelection.cs`
- `client/core/Editor/Content/Shared/`
- `releases/0.8.0.md`, `releases/0.9.0.md`, `.claude/docs/CONTENT.md`

### Заметки
- SBP переключает редактор на собираемую платформу, `CanBuildPlayer` смотрит окно сборки этой платформы: тест, собирающий не под
  активную платформу, падает с `Unable to build with the current configuration`.
- Сортировка тегов: `v0.7.0-rc.6` выше `v0.7.0`, база релиза выбирается руками.
- Не проверено: Play Mode с WebGL-бандлами в редакторе, сборки telegram / web_stress pixel-swarm, Android и десктоп.
