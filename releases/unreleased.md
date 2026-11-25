# Not released yet

Steps for the games from the commits after the last tag, the format is in [README](README.md). A new step is added at the end in the same
commit as the change. At a release the steps move to `<version>.md`, only this header stays here.

### 1. The agents of `audit-swarm` moved into the skill, a game needs no `.omp` in its root

Who: games with links in `.omp/agents` (gamejam-autobattler, mines-leader, storytale-workshop). After the pointer moves these links
are broken: the agents are no longer in `tools/framework/.omp/agents`.

Search: `ls -la .omp .omp/agents 2>&1`

Do:
- `git rm -r .omp` (the links `audit-scout.md`, `audit-verifier.md` and an own `config.yml` with the same limits); if `.omp` is not in git,
  `rm -r .omp`.
- Once per machine: `tools/framework/.claude/skills/audit-swarm/install.sh`. It links the agents of
  `.claude/skills/audit-swarm/agents` into `~/.omp/agent/agents` and sets the limits `task.maxRuntimeMs` / `task.maxConcurrency` in the global
  config of omp. If `omp` already sees `audit-scout` from any folder, skip this step.
