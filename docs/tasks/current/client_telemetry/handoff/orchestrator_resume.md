## Оркестратор: продолжение после компакта (2026-11-03, вторая половина)

Оркестрация остаётся в этой сессии, агентов-оркестраторов не запускать. Шаги выполняют обычные агенты (`.claude/skills/orchestrate/SKILL.md`, Phase 3-4). Прочитать: журнал `client_telemetry_orchestration.md` (конец), шаг 14 в `client_telemetry_info.md`, `handoff/perf_research.md`. `CONSOLE_TOKEN` лежит в `~/.config/pixel-swarm/deploy.env`, значение нигде не упоминать.

### Что уже сделано и выпущено
- Телеметрия в фреймворке main, релиз `v0.3.0` запушен (тег, `releases/0.3.0.md`). Файлы задачи закоммичены в main фреймворка (`022a0dae`); их дальнейшие правки коммитить со следующим коммитом фреймворка (пользователь просил коммитить файлы задач перед релизом).
- Pixel Swarm main `9c3d8b95` запушен, бэкенд и клиент (web `20261103-061825-9c3d8b95`, telegram `20261103-062509-9c3d8b95`) на проде, флаг включён пользователем. Откат клиента: web `20261103-050316-8b52f07f`, telegram `20261103-051239-8b52f07f`.
- Чтение прода скиллом: `cd /projects/pixel-swarm; CLAUDE_PROJECT_DIR=/projects/pixel-swarm .claude/skills/client-telemetry/telemetry sessions|summary <id> --windows N|startups|startup <id> --top N|errors`.

### Ветка фреймворка `client-telemetry` (дерево `/projects/framework/.claude/worktrees/client_telemetry`), не запушено поверх `v0.3.0`
- `1cae4708` шаг 13: метки и ошибка потери WebGL-контекста, `web.gl.context_lost`.
- `55bf1b3b` шаг 9c: стрелка назад в панели (`PageHeader.BackHref`).
- В работе 14a: больше метрик фреймворка, `AddCollector(lifetime, collector)`. Агент ORCH-client_telemetry-14a-g1. `dotnet` и `test-all.sh` ему запрещены: после него оркестратор сам прогоняет `tools/scripts/test-all.sh`.

### Worktree игры `/projects/pixel-swarm/.claude/worktrees/client_telemetry`, ветка `client-telemetry`
- В работе 14c: фон меню и лобби (`MenuBack.shader`, `LobbyBack.shader`) рисуется в RT экран / `_PixelSize`, решение пользователя. Агент ORCH-client_telemetry-14c-g1. Скриншоты до и после в `media/menuback_*.png`, `lobbyback_*.png`.
- Общее дерево `/projects/pixel-swarm` занято другими сессиями (там чужие незакоммиченные правки): не трогать, собирать в worktree. Кластер из worktree не запускать.

### Дальше
0. Пользователь: «изменения в самом проекте тоже бери в деплой». Перед сборкой влить локальный `main` Pixel Swarm в ветку worktree (на 2026-11-03 там 2 незапушенных коммита других сессий: `38ec4088` баланс забега, `59eb52ee` Fire Trail, музыка меню, камера; брать только закоммиченное); push main игры унесёт и их.
1. По отчёту 14a: проверить, `test-all.sh`, запустить 14b (коллекторы игры в worktree игры: тела, снаряды, частицы, свет, искры; `SparkWorld._count` приватный; через `AddCollector` из скоупа забега; см. `perf_research.md`).
2. После 14b и 14c: submodule игры на ветку фреймворка, тесты, WebGL-сборка в worktree.
3. Спросить пользователя про деплой (merge и push фреймворка и игры; бэкенд через push main игры; клиент сборкой в worktree, как в 11b). Если 14a тронул контракт, предупредить про уведомление об устаревшем клиенте.
4. После деплоя: пользователь проверяет Android (меню до и после правки фона), iPhone 11 (чёрный экран: контекст и `web.raf.*`), iPad 2020 (2-3 минуты забега).
5. Предложено пользователю, ответа нет: фильтры списка сессий консоли в query string.

### Факты с телефонов
- Android Mali-G52: меню 8.6 FPS (кадр 116 мс, главный поток 15 мс, 11 draw calls), лобби 35, забег 19-26 FPS (главный поток 35-49 мс, растёт).
- iPhone 15 Pro: 60 FPS везде, фризы до 2 с в забеге.
- iPhone 11, iOS 16 (Telegram и Chrome): каждый раз чёрный экран около минуты забега, и до телеметрии тоже; один кадр в 5 с, главный поток 33 мс, игра жива. Unity в Chromium при потере контекста останавливает цикл, поэтому гипотеза потери контекста ослабла; WebKit не проверен. Нет звука: беззвучный режим, решено.

### Состояние на 2026-11-03, после свода веток
- Фреймворк `client-telemetry` = `d50671b8` (14a, 14a-g2, 15, 16, merge), все тесты зелёные, не запушено. Ветки `client-telemetry-15`, `-16` и их worktree (`client_telemetry_15`, `client_telemetry_16`) после деплоя удалить.
- Игра worktree `client-telemetry`: `28325bf0` 14c, `2cde0234`+`334cbf25` 14b, `68f84f97` main, `77bd03cd` шаблоны. Агент game-g1 обновляет submodule, тестирует, собирает (handoff `game_g1.md`).
- Деплой: протокол телеметрии BREAKING, бэкенд и клиент выкладывать вместе. Порядок как в 11b: push фреймворка (main fast-forward на ветку, затем релиз по желанию пользователя), merge в main игры и push (Coolify деплоит бэкенд через ветку deploy), затем `deploy-webgl.sh --target web` и `--target telegram` из worktree.
