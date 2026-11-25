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

### 2. The skill `start-cluster` is a pointer to `control`, its ports table is gone

Who: games whose notes or docs refer to the ports table of `start-cluster` (mines-leader: `.claude/project-skills.md`
§ start-cluster, control, "the framework defaults of the table of `start-cluster` + 200").

Search: `git grep -n 'start-cluster' -- .claude docs CLAUDE.md`

Do:
- The table held the ports of one game, so the skill no longer has it. A note that counts from it gets the ports themselves: the
  table of the game stays in its `.claude/project-skills.md`, the sentence about the defaults of the skill goes.
- Nothing to do for the link: `/start-cluster` starts the cluster with `control cluster start` as before, and `benchmark-analyse`
  now starts it the same way (`cluster` of `.claude/control.json`).
