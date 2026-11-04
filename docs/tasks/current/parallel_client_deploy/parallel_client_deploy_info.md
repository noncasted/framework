---
task: parallel_client_deploy
status: done
phase: complete
created: 2026-11-04
updated: 2026-11-04
total_steps: 4
completed_steps: [1, 2, 3]
blocked_steps: []
---

# parallel_client_deploy

## Задача

Ускорить `/deploy-client` примерно в 2 раза: тесты и сборки всех целей проекта идут одновременно, каждая в своей копии
проекта со своим процессом Unity (batchmode). Выкладка - только если тесты прошли; упали - сборки выбрасываются, ничего
не выкладывается (правило скилла «never upload a build from a tree whose tests fail» сохраняется).

## Исходные данные (замер 2026-11-04, pixel-swarm, коммит f4a1a986)

Последовательно сейчас: `run-tests.sh` ~4.5 мин, сборка website ~6 мин, сборка telegram ~5 мин - итого ~16 мин.
Параллельно ожидается ~7-8 мин (упор в самую долгую сборку + борьба за CPU). Машина: 24 ядра, 61 ГБ (свободно ~33).

## Что уже есть

- Скилл: `.claude/skills/deploy-client/SKILL.md` (фреймворк) - сейчас собирает в ОТКРЫТОМ редакторе через MCP
  (`manage_build`), цель за целью, и выкладывает каждую сразу после её сборки (Bee удаляет выход предыдущей сборки
  при сборке в другую папку - инцидент 2026-10-17). Цели, профили, папки - `.claude/project-skills.md` игры.
- Batchmode-сборка платформы web: `Internal.WebGlBuild.Build` (client/platforms/web, Editor) - активный профиль из
  `-activeBuildProfile`, `PlayerBuildSteps.Run`, код выхода 1 при любой ошибке. Проверено в client_telemetry:
  `Unity -batchmode -quit -projectPath <copy>/client -buildTarget WebGL -activeBuildProfile "Assets/Settings/Build Profiles/<profile>.asset" -executeMethod Internal.WebGlBuild.Build -buildOutput ../builds/<name> -logFile <log>`.
- Копия проекта за секунды: `git archive HEAD` игры + `git archive HEAD` сабмодуля `tools/framework` +
  `cp -a --reflink=auto client/Library` (так делались /var/tmp/ps-deployN в client_telemetry). Фиксирует ровно
  коммит, без незакоммиченных правок - решить, нужно ли брать и рабочее дерево (run-tests.sh снимает reflink-снимок
  рабочего дерева, см. его Snapshot).
- `tools/scripts/run-tests.sh` игры: свои слоты Unity (2), отказ при < 8 ГБ свободной памяти.
- Выкладка: `tools/scripts/deploy-webgl.sh --target <t>` с `BUILD_DIR=<abs>` (держит 5 релизов).

## Требования

1. Один коммит (или один снимок дерева) -> N+1 копий: тесты и каждая цель в своей копии, запуск одновременно.
2. Ограничение параллелизма по памяти/CPU (слоты, как у тестов), чтобы два деплоя или деплой + агенты не положили машину;
   общий лок `/tmp/claude-1000/unity-batchmode.lock` из сессий client_telemetry - не стандарт скилла, заменить слотами.
3. Тесты упали -> сборки останавливаются (или дожидаются и выбрасываются), выкладки нет, отчёт как сейчас.
4. Проверки скилла на каждую цель сохраняются: stripping сабмодулей (`SubmoduleStrippingSettings`, `StripAutomaticallyAfterBuild`
   - убедиться, что batchmode-путь их применяет так же, как MCP-путь), `build-guid.txt` новый, `index.html` содержит то, что
   требует таблица проекта, одинаковые общие файлы шаблонов (`cmp web-shell.js` у pixel-swarm).
5. Addressables: контент строится под активную цель (WebGL) в каждой копии - проверить, что в копии нет устаревших `aa/*`.
6. Открытый редактор пользователя не трогается вовсе (сейчас скилл занимает его на минуты).
7. Копии удаляются после выкладки (каждая ~12 ГБ на диске до reflink-разделения).
8. Общий скрипт в `tools/scripts/` фреймворка (или игры - решить по правилу «нет кода игры во фреймворке»), скилл вызывает его;
   работает для всех игр с целями из `.claude/project-skills.md`.

## Проверка

- Деплой pixel-swarm обеих целей новым путём: время от старта до выкладки (до/после), прод открывается, `build-guid.txt` сменился.
- Искусственно упавший тест -> ни одной выкладки.
- Две цели собираются одновременно без порчи выходов друг друга.

## Не трогать

Сессия client_telemetry (worktree /projects/pixel-swarm/.claude/worktrees/client_telemetry) деплоит вручную тем же способом -
не ломать `deploy-webgl.sh` и `Internal.WebGlBuild.Build` по интерфейсу без нужды.

## Документация
- `/projects/framework/CLAUDE.md` (правила 1, 7, 8: нет кода игры, fail fast, BREAKING + releases/unreleased.md)
- `.claude/skills/deploy-client/SKILL.md`, `.claude/skills/control/copies.py` (приём reflink по частям)
- Исследование: `handoff/R1_research.md` (run-tests.sh), `handoff/R2_research.md` (batchmode-сборка), R3 - ниже в решениях

## Решения (оркестратор, по исследованию)
- **Снимок = рабочее дерево** (как сейчас: run-tests.sh снимает рабочее дерево, редактор собирает рабочее дерево):
  один reflink-снимок игры + `tools/framework` (с правками) + `client/Library`; из него reflink-копии на тесты и на каждую цель.
  Тесты запускаются скриптом игры ИЗ копии тестов (ROOT = копия), так все видят ровно одно дерево.
- **Скрипт во фреймворке** `tools/scripts/deploy-client.sh`: общий код, без значений игры. Значения - в файле игры
  `.claude/deploy-client.json` (как `code-audit.json`, `control.json`): цели (имя, профиль, папка вывода, цель deploy-webgl.sh,
  что должно / не должно быть в `index.html`), пары файлов шаблонов для `cmp`. `project-skills.md` ссылается на него.
  Игровые `run-tests.sh` и `deploy-webgl.sh` вызываются как есть (интерфейс не меняется: client_telemetry).
- **Слоты сборок**: свой пул `/tmp/claude-1000/unity-build.slot<i>.lock` (flock -n), `UNITY_BUILD_SLOTS` (2), порог памяти
  `UNITY_BUILD_MIN_FREE_GB`; тесты держат свои слоты run-tests.sh. Общий лок `unity-batchmode.lock` деплой не берёт.
  Сборки стартуют сразу вместе с тестами.
- **Тесты упали** -> свои Unity-процессы сборок убиваются по маркеру в environ (приём run-tests.sh), ни одной выкладки, код != 0.
- **Выкладка** только после PASS тестов и проверки всех целей; цели выкладываются по очереди, каждая из своей копии
  (Bee не удаляет чужой выход). Падение выкладки цели - остальные не выкладываются.
- **Stripping в batchmode**: `Internal.WebGlBuild` явно грузит настройки Web Stripping Tool из Settings.json проекта, ставит
  `StripAutomaticallyAfterBuild`, бросает (exit 1) при пустых настройках - без пути игры. Проверка скрипта сильная:
  `stripping_info.json` и `*.wasm.br.bak` в папке бэкапа копии (одна папка - слабая проверка, создаётся всегда).
- **Копии** удаляются в trap EXIT (`KEEP_COPY=1` оставляет); логи - в папке логов.
- **`FRAMEWORK_SOURCE=<dir>`**: взять `tools/framework` снимка из другой папки (проверка изменений фреймворка до сдвига
  указателя сабмодуля, нужно для шага 4).
- Шаг «вернуть редактор на Linux» и handover редактора из скилла уходят: редактор не используется.

## План

| Шаг | Что | Файлы | Приёмка |
|-----|-----|-------|---------|
| 1 | Явные настройки stripping в batchmode | `client/platforms/web/Editor/WebGlBuild.cs` | компиляция `client/project` (batchmode), throw при пустых настройках |
| 2 | Скрипт параллельного деплоя | [новый] `tools/scripts/deploy-client.sh` (+ при необходимости `tools/scripts/lib/*.sh`), [новый, игра] `/projects/pixel-swarm/.claude/deploy-client.json` | pixel-swarm `--no-deploy`: тесты + 2 цели одновременно, проверки целей PASS, время; копия с упавшим тестом и заглушкой deploy-webgl.sh -> ни одного вызова выкладки, сборки убиты; копии удалены |
| 3 | Скилл и документация | `.claude/skills/deploy-client/SKILL.md`, `/projects/pixel-swarm/.claude/project-skills.md`, `releases/unreleased.md`, `CLAUDE.md` (tools/scripts, Platforms web) | скилл описывает только новый путь, аргументы `--list/--rollback/--notest/<target>` сохранены |
| 4 | Боевой деплой pixel-swarm обеих целей новым путём | — | время до/после (~16 мин -> ?), `build-guid.txt` сменился, `--list` current = новый тег, прод открывается (curl 200 + wasm) |

Зависимости: 1 -> 2 -> 3 -> 4 (последовательно: шаг 2 проверяет сборки с изменением шага 1; общий ресурс - память/Unity batchmode).
