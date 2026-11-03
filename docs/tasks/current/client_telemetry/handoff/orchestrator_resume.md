## Оркестратор: продолжение после компакта (2026-11-03, после деплоя 09:11 и тестов на устройствах)

Оркестрация в этой сессии, агентов-оркестраторов не запускать (`.claude/skills/orchestrate/SKILL.md`, Phase 3-4). `CONSOLE_TOKEN` в `~/.config/pixel-swarm/deploy.env`, значение нигде не упоминать. Общее дерево `/projects/pixel-swarm` занято другими сессиями (не трогать, кроме fast-forward main и submodule при деплое); кластер из worktree не запускать. Журнал: `client_telemetry_orchestration.md` (конец). План шагов: `client_telemetry_info.md` (шаги 14-17).

### Решения пользователя (в силе)
- Правки копить и выкладывать одним деплоем («давай сначала накопим правки и потом разом за деплоем»). Деплой только когда пользователь скажет.
- В деплой брать закоммиченные изменения других сессий из main игры (влить main в ветку перед сборкой; незакоммиченное не брать).
- Файлы задачи коммитить с коммитами фреймворка перед релизом.

### Прод сейчас (деплой 2026-11-03 09:11)
- Фреймворк main `9aef2cd6` (в нём `a88498ba`), игра main `e19b6b7e`, протокол `65cd3fcd58328aa7`, web `20261103-091135-e19b6b7e`, telegram `20261103-091148-e19b6b7e`.
- Деплой клиента из worktree: `DEPLOY_PASSWORD_FILE=/projects/pixel-swarm/tools/scripts/.deploy-password tools/scripts/deploy-webgl.sh --target web|telegram`. Бэкенд: push main игры → `sync-deploy-branch.yml` → Coolify (ждать, пока мета примет новый хеш: `node` WebSocket к `wss://meta.pixel-swarm.xyz/?protocol=<hash>`, 4001 = старый бэкенд). Порядок как в журнале: ff main фреймворка → push; в `/projects/pixel-swarm` `git merge --ff-only client-telemetry` → push; submodule общего дерева `git -C tools/framework checkout <commit>` (там чужая незакоммиченная правка `ContainerGraphAssetScanner.cs`, checkout её сохраняет, если файл не меняется).
- Скилл: `cd /projects/pixel-swarm; CLAUDE_PROJECT_DIR=/projects/pixel-swarm .claude/skills/client-telemetry/telemetry sessions|summary <id> --windows N|errors <id>|startup <id> --top N|monitor --os X --tag run`.

### Накоплено, не запушено
- Фреймворк, ветка `client-telemetry` (worktree `/projects/framework/.claude/worktrees/client_telemetry`), HEAD `bba43a1f`: шаг 17 (`18e08d6c` шрифт оверлея BREAKING, `f2a8baaf` сэмплы в `PreviousSession` BREAKING протокол, `367d9bdf` перехват GL на прототипах, метки страницы и звука, `dev.*`, `bba43a1f` повторы в скилле). test-all зелёный (backend 1550, telemetry 84, EditMode 1066, PlayMode 118), transport 24.
- Игра, ветка `client-telemetry` (worktree `/projects/pixel-swarm/.claude/worktrees/client_telemetry`), HEAD `89a9bfd9`: `cefad88e` поле пути роя (эталоны md5 совпали), `76cb5d6d` таймеры `game.swarm.field*`, `game.particles/sparks/lights.update`, `game.lights.pass`, `b9d8c18d` стик на iPadOS, `b15c8796` submodule `bba43a1f`, `b6d21d42` шрифт оверлея, `devProfilerMarkers`, протокол `1b8ac62a9790766c`, `89a9bfd9` переключатели шаблона `?dpr=native`, `?pixelated=0`. EditMode игры 925 (50 skipped), бэкенд игры 567.
- Сборки (не залиты): `builds/release-web`, `builds/release-telegram` (собраны до `89a9bfd9`, переключатели скопированы в их TemplateData руками), `builds/dev-website` (19 МБ wasm, падает на iPhone), `builds/local-web` (mimalloc ВКЛ, http разрешён, без сжатия), `builds/local-web-nomimalloc`, `builds/local-dev` (NullReferenceException каждый кадр и чёрный экран — баг dev-сборки, не найден; подозрение на dev-сборщики телеметрии `StandardTelemetryCollectorsExtensions.cs:81` или `GamePipeline`). Перед деплоем пересобрать релизные после всех правок.
- Патч `WebBootConfigStep` для несжатых сборок: `/tmp/claude-1000/lb/WebBootConfigStep-plain.patch` (не закоммичен).

### Найдено тестами на устройствах (после деплоя 09:11)
- Android Mali-G52: меню 8.6 → 72 FPS (фон в RT). Забег 38 → 110 мс главного потока за 5 минут; у монолита 34-39 мс. Разбор `handoff/perf_research_2.md`: поле пути (исправлено, ждёт деплоя), искры = draw call каждая, частицы пересобираются целиком, IL2CPP OptimizeSize, растяжение кадра.
- iPhone 15 Pro: стабильно.
- iPad: Telegram WebView медленный целиком (rAF 65-250 мс), Chrome 60 FPS; в Chrome забег `webgl.context_lost` на 23.8 с после затыков отрисовки (след оборванной сессии). Разбор `handoff/ios_research.md` (нет прогрева шейдеров, канвас растянут x2, пересборка мешей, `getError`).
- iPhone 11 iOS 16.6 (подробно в журнале 12:16 и ниже, память `webgl-power-preference-ios.md`):
  - Симптом: через ~30 с забега кадр раз в 5 с, стоит вся страница (таймеры тоже), фенсы GPU не срабатывают (`TIMEOUT_EXPIRED`). После этого Safari **залипает**: любая WebGL-страница, даже пустая сцена Unity, идёт 0.2 FPS до перезапуска Safari (reload и другой origin не помогают). Все прошлые выводы «пустая сцена 5 с» были сделаны в залипшем Safari.
  - `powerPreference` НЕ причина (с чистого Safari 3+3 прогона, все 60 FPS). Коммит игры `7e696f26` (Default) опирается на ошибочный A/B: откатить или переписать сообщение до деплоя.
  - Меню 5 минут — без остановок. Первая попытка автозабега: страница сама перезагрузилась на загрузке сцены забега (падение процесса).
  - Виновата отрисовка: забег с `skip=all` (ни одного draw) — 0 остановок до уровня 5 (62 с). `skip=17..24` (все программы, что появляются только в забеге) — 0 остановок до уровня 6. `skip=24` — остановки на 36 с. `skip=17,18,19,20` — остановки (на 24 с забега; в этом прогоне пользователь повернул телефон в альбомную). Значит виновата одна из программ **#21 (vs=2610 fs=690), #22 (vs=4434 fs=10471), #23 (vs=3189 fs=11036)** или сочетание с #24 (vs=2976 fs=5084, появляется на повышении уровня). Номера программ стабильны между прогонами (те же размеры).
  - Ориентация: до 13:22 телефон, видимо, лежал в портретной (под заставкой «поверните»), с 13:22 — альбомная. Повторить в альбомной контрольный забег без пропусков.
  - **Причина найдена (14:27, журнал):** `nointerpolation` в варианте `_SWARM_BODY` шейдера `Sprite.shader`. Правка в worktree игры не закоммичена; на iPhone 11 проверена сборкой `builds/local-autorun-fix`: 0 остановок, 4 мин забега. Номера программ в probe плавают, рой метится по исходнику.

### Автономный стенд iPhone 11 (без рук пользователя)
- Режим разработчика включён, Developer Disk Image 16.6 смонтирован (`/home/nndd/.claude/jobs/9180826f/tmp/ddi/16.6`), pymobiledevice3 в venv `/home/nndd/.claude/jobs/9180826f/tmp/pmd`.
- `/tmp/claude-1000/lb/safari-restart.sh [url]` — перезапуск Safari по USB (снимает залипание) и открытие url; Safari сначала восстанавливает прошлую вкладку на ~3 с.
- `/tmp/claude-1000/lb/iosctl.py list|eval|reload` — управление вкладкой (ios_webkit_debug_proxy :9222). Скриншот WebGL через инспектор чёрный.
- Автозабег: игра `?autorun=1` (коммит игры `20742f1c`: соло-забег сам, движение по кругу, неуязвимость, выбор карт, `[Autorun] alive t=N` каждые 10 с). Сборка `builds/local-autorun` → `/autorun/`; с замером GL — `/autorun-probe/` (index.html грузит `/probe.js?t=`).
- `probe.js` (в корне сервера): `p=N` пресеты, `skip=all|id,id`, `only=`, `dump=id`, `totals=1`, `nofence=1`, `pp=`, `aa=`, `pma=`, `stencil=`, `defaults=1`; пишет `NEW <формат>`, `TOTALS`, `draws by program`, `program #id vs= fs=`.
- `/home/nndd/.claude/jobs/9180826f/tmp/runa.sh "<query>" <секунд>` — перезапуск Safari, автозабег с замером, итог: старт забега, уровни, число остановок.

### Локальный стенд
- Сервер: `python3 -I /tmp/claude-1000/lan/serve.py /tmp/claude-1000/lan/release-web-plain` на 8770 (фоновая задача; отдаёт `.br` с `Content-Encoding: br`, POST `/log` → `/tmp/claude-1000/lan/log.txt`). Страницы: `/throttle/?sync=compile|readPixels|finish|getError|fbo&rate=24000&mem=N&tex=N`, `/prod/` (прод-сборка), `/local/` → симлинк на `builds/local-web`. `remote-log.js` шлёт сердцебиение раз в 2 с, ошибки, консоль. Убивать сервер по pid из `ss -ltnp | grep :8770` (не `pkill -f`: совпадает с собственной командой и рвёт shell, код 144).
- Телефон 192.168.0.147 видит машину 192.168.0.109 только с выключенным VPN на телефоне; ufw открыт пользователем для 192.168.0.0/24: 8770-8771, 34997-34999, 54997-55511 (35020 для профайлера WebGL — уточнить, открыт ли). Unity по http: нужен `insecureHttpOption` AlwaysAllowed и сборка без сжатия.
- Профайлер WebGL: плеер подключается к `ws://192.168.0.109:35020/` (websockify общего редактора → 34999); на 34999 слушает ещё batchmode-копия чужой сессии `balance`.

### Дальше
1. iPhone 11: закоммитить правку `Sprite.shader` (по разрешению пользователя); решить судьбу `7e696f26` (откат); правило «без `nointerpolation` в WebGL-шейдерах» — в доки фреймворка? Проверить на прод-сборке после деплоя (iPad Chrome `context_lost` тоже может быть отсюда).
2. Найти NRE каждого кадра в dev-сборке (для профайлера на Android).
3. По решению пользователя: следующие оптимизации (искры одним мешем, бюджет частиц, IL2CPP OptimizeSpeed, прогрев шейдеров, ограничение DPR), затем общий деплой.
