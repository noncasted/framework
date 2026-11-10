## Оркестратор - handoff поколения 1 (остановка 2026-11-10 по решению пользователя)

Продолжение: новая сессия `/orchestrate docs/tasks/current/content_directories/`, с Phase 4 по `_orchestration.md` и этому файлу.
Запущенных агентов нет.

### Сделано
- Шаги [1]–[15], [16a], [16b], [16c] done; [16d] — реализация готова, осталась серия замеров (`handoff/16d_g1.md`).
- Фреймворк: ветка `content-directories` в `/projects/framework/.claude/worktrees/content_directories`, HEAD `75c7adad`.
  Локальные теги (НЕ запушены): `v0.7.0-rc.1` `f8e4d861`, `rc.2` `d85df041`, `rc.3` `010b524d`, `rc.4` `402395e9`,
  `rc.5` `0a07e2d0`, `rc.6` `75c7adad`. Теги не переставлять — исправления идут новым `rc.N`.
- pixel-swarm: worktree `/projects/pixel-swarm/.claude/worktrees/content_directories`, ветка `content-directories`, HEAD `c79a64d48`,
  submodule на rc.6. Не закоммичено (решить в [17]): `docs/media/research/content_directories/` (evidence стенда).
- Схема каталога сейчас: один архив UnityFS (LZ4, без type tree в плеере) в записанном порядке загрузок игры
  (`ProjectSettings/ContentLoadOrder/Game.txt`), Range-запросы в файл нулей в MEMFS, Cache API `<url>?offset=<n>`,
  индекс отдельным файлом `<hash>.index.json(.br)`, в плеере `ContentStamp`.

### Решения пользователя
- 2026-11-10: проба UseArchive (критерий: десятки запросов, байты и время близки к main, иначе стоп); индекс вынести из `.data`.
- 2026-11-10: rc.5 не принят как есть — «сначала дожать запросы и объём» → [16d].
- 2026-11-10: после сборок rc.6 остановиться, продолжить в другой сессии.

### Что дальше
1. [16d] g2: серия замеров main (`/var/tmp/content_directories-step15/main-website`) против rc.6
   (`/var/tmp/content_directories-builds-rc6/`) без троттлинга и с 150 мс / 10 Мбит, на тихой машине; таблица «Замеры [16d]»
   в `_result.md`; поправить оценки в `releases/0.7.0.md` / `CONTENT.md` на замер; удалить `/var/tmp/content_directories-builds-rc5`;
   по желанию проба LZMA по файлам (объём +30%, HighFields 3.67 → ~2.1 MB). Показать цифры пользователю.
2. [16] остальная обратная связь (кандидаты — `_result.md` «Нерешенные вопросы», progress [10]–[14]): имя каталога в одном месте
   (`Framework.json` + 4 сцены), `LoadRoot(ContentOptions, construct)` / диагностика лямбды-корня, валидатор bindings на `{fileID: 0}`,
   генератор bindings — ошибка вместо дубликата класса, исключение группы из контента одной сборки (stress), точка входа «только
   ContentBuild», `CINGR001` без подсказки про content-сборку, `ContentReference` в тестах без ассета, `update-framework/status.sh` и
   rc-теги. Спросить пользователя, что из этого делать сейчас (он спрашивал «долго ещё?»).
3. [17] итог: общий прогон, `_result.md`, уборка (`/var/tmp/content_directories-*`, в т.ч. `.lock` deploy-client, `content_directories-step15`),
   решение пользователя о push веток/тегов, слиянии в main и релизе 0.7.0.

### Не проверено
- Реальные устройства (iPhone / iPad / Redmi были заблокированы: DDI, keyguard); Safari по http; предзагруженный каталог на WebGL;
  `load.*` телеметрия (нужен кластер). Хост Coolify не трогали.

### Правила
- Push, деплой, слияние в main — только после подтверждения пользователя. Shared trees (`/projects/framework`, `/projects/pixel-swarm`)
  не переключать. Кластер из worktree не запускать. Около лимита контекста не запускать оркестратора агентом — попросить пользователя
  сжать контекст.
