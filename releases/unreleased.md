# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

### 1. Агенты `audit-swarm` переехали в скилл, `.omp` в корне игры не нужен

Кого касается: игры со ссылками в `.omp/agents` (gamejam-autobattler, mines-leader, storytale-workshop). После обновления указателя
эти ссылки битые: агентов больше нет в `tools/framework/.omp/agents`.

Поиск: `ls -la .omp .omp/agents 2>&1`

Сделать:
- `git rm -r .omp` (ссылки `audit-scout.md`, `audit-verifier.md` и свой `config.yml` с теми же лимитами); если `.omp` не в git, то
  `rm -r .omp`.
- Один раз на машине: `tools/framework/.claude/skills/audit-swarm/install.sh`. Он ссылается на агентов из
  `.claude/skills/audit-swarm/agents` в `~/.omp/agent/agents` и ставит лимиты `task.maxRuntimeMs` / `task.maxConcurrency` в глобальный
  config omp. Если `omp` из любой папки уже видит `audit-scout`, этот шаг пропустить.
