## Фреймворк и pixel-swarm на Content Directories вместо Addressables (заморожено)

### Что сделано
- Фреймворк в ветке `content-directories` полностью переведён на Content Directories: манифест и индекс загрузок, сборка каталога с плеером, хранилища, `IContentLoader` / `ContentReference<T>` / `ContentScene` на lifetime, DI и сцены скоупов, каталоги; Addressables и его форк удалены; `releases/0.7.0.md`, теги `v0.7.0-rc.1`…`rc.6`.
- pixel-swarm переведён в своей ветке `content-directories` (submodule на rc.6): 24 ссылки, инструменты уровней, каталоги, шаблоны WebGL, nginx; работает в редакторе и в вебе.
- Для веба схема движка (файл на объект, 9218 файлов) заменена своей: один архив UnityFS в записанном порядке загрузок, Range-запросы в файл нулей MEMFS, Cache API, индекс отдельным файлом.
- **Миграция заморожена решением пользователя (2026-11-10): в main не слита, 0.7.0 не выпущен.** В main попали только проверки bindings (`b62ba3f1`, `a1857343`, `2a88ac0b`).

### Итог замеров (стенд pixel-swarm, rc.6 против main)
- `Build/` br −237 KB — единственный выигрыш (ожидали −250…330 KiB).
- Контент на сервере +30% (15.9 против 12.27 MB); первый заход 27 запросов / 10.80 MB против 24 / 10.60 MB.
- Время хуже почти везде: меню +0.4–0.5 с, забег +0.7–1.0 с, вторая локация под 150 мс / 10 Мбит +2.6 с, повторное открытие +0.5–0.8 с (при 0 запросов).

### Ключевые файлы (ветка `content-directories`, worktree `.claude/worktrees/content_directories`)
- `client/core/Runtime/Content/` — загрузчик, ссылки, хранилища, `Plugins/WebContentFetch.jslib`
- `client/core/Editor/Content/` — `Build/`, `Archive/`, `Order/` (запись порядка загрузок)
- `.claude/docs/CONTENT.md`, `releases/0.7.0.md` — описание и шаги игры
- pixel-swarm, ветка `content-directories` (`c79a64d48`); evidence стенда — незакоммиченный `docs/media/research/content_directories/` в её worktree

### Заметки
- Когда вернуться: WebGL-плеер получит LZMA или группировку файлов content directories, либо появится причина уйти от Addressables, не связанная с размером.
- Ветки и теги rc.1–rc.6 локальные, не запушены. Последний зелёный test-all — rc.6 `75c7adad`; после него `a8ad6559`, `73cc7232` (тесты целиком не гонялись) и WIP `b591eaa5` (не компилируется).
- WebGL-плеер 7000.0.0a10 не распаковывает LZMA (`Decompressing this format (1) is not supported on this platform`); потенциал был −23% архива.
- Пофайловая схема движка в вебе не работает: ~4900 одновременных fetch дают `ERR_INSUFFICIENT_RESOURCES`; с очередью — 6800 запросов и забег 12 с.
- Chromium пускает Range-запросы одного URL через запись HTTP-кэша по одному (меню 24 с под 150 мс); лечится `cache: 'no-store'`.
- Типы, нужные только каталогу, вырезаются из плеера: в сборку плеера передаётся отчёт content-сборки (`AddPreviousBuildReportDirectory`).
- Индекс каталога внутри `.data` стоил +500 KB br — вынесен отдельным файлом.
- Замеры на проекте измерений (−310…340 KiB) на игру не переносятся: их съедают индекс и LZ4 вместо сжатия бандлов.
- Устройства (iPhone, iPad, Redmi) не проверены.
