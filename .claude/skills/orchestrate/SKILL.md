---
name: orchestrate
description: Turn the current session into the orchestrator of a task - split it into steps, run the steps with subagents (parallel where independent, sequential where dependent), keep every context (own and agents') under 300k tokens with handoffs. Use when the user runs /orchestrate [task text or path to a task file/folder] or asks to orchestrate a task with agents.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /orchestrate, orchestrate, оркестрируй, оркестратор, раздай агентам, выполни агентами
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Orchestrate Skill

`/orchestrate [task text | path]` - this session becomes the **orchestrator**: it plans, delegates every
implementation step to subagents, checks their results and keeps the state on disk. The orchestrator itself does not
write production code; it reads only what it needs to plan and to verify.

Context limit: **300 000 tokens** for the orchestrator and for every agent. Measure, do not guess:

```
python3 .claude/skills/orchestrate/context_meter.py --session <own session id>   # id = the uuid in the scratchpad path
python3 .claude/skills/orchestrate/context_meter.py --agent <agentId>           # id returned by the Agent tool
python3 .claude/skills/orchestrate/context_meter.py --marker <ORCH marker>      # an agent measuring itself
```

Exit code 2 = over the limit. The output is `<tokens> (<percent>% of 300000) ok|OVER <transcript>`.

---

## Phase 1 — Task workspace

1. Argument resolution:
   - a path to `docs/tasks/current/<task>/` or one of its files - use that workspace (read `_result.md` first, then
     `_info.md`, `_progress.md`);
   - any other path (`docs/...md`, a plan file) - read it, it is the task text;
   - plain text - it is the task text.
2. Without a workspace, create one exactly as the `workflow` skill does (Phase 1, steps 1-8:
   `.claude/skills/workflow/SKILL.md`), but **do not stop for "Начинаем реализацию?"** - the user already asked to
   orchestrate. Ask only if the Step 5.5 gate of `workflow` fires.
3. Add `docs/tasks/current/<task>/<task>_orchestration.md`:

```markdown
---
task: <task>
orchestrator_session: <session id>
orchestrator_generation: 1
updated: <YYYY-MM-DD HH:MM>
---

## Граф шагов

| Шаг | Зависит от | Волна | Агент (marker / agentId) | Поколение | Статус | Handoff |
|-----|-----------|-------|---------------------------|-----------|--------|---------|
| 1 | — | 1 | ORCH-<task>-1-g1 / a1b2... | 1 | in_progress | — |

## Ресурсы
<!-- shared resources and who holds them now: Unity editor, cluster, generated catalogues, a file -->

## Журнал
### [YYYY-MM-DD HH:MM] ...
```

## Phase 2 — Plan the waves

Build the step graph from the plan in `_info.md`.

A step may run **in parallel** with another only when all of these hold:
- neither depends on the result of the other (types, files, generated code, decisions);
- their file sets do not intersect (also generated files: `*.g.cs`, catalogues, `*Bindings`, asset maps, `.meta`);
- at most one of them needs the Unity editor (refresh, compile, Play, `execute_code`, catalog generation) - the
  editor is shared and single-owner (`.claude/docs/UNITY_EDITOR_WORKFLOW.md`, "Shared Editor and Handover");
- at most one of them runs the backend cluster or `dotnet test` over the same projects;
- neither changes `tools/framework` (one submodule commit at a time).

Otherwise - sequential. When in doubt - sequential. Group the steps into waves (wave N starts when its dependencies
are done) and write the graph into `_orchestration.md`. Show the user the graph in a few lines and continue without
waiting.

Steps that are only research (read code, find files, compare options) are good parallel candidates; give them the
`Explore` agent when they need no edits.

## Phase 3 — Brief each agent

Every agent gets a self-contained prompt (it sees nothing of this conversation):

```
ORCH marker: ORCH-<task>-<step>-g<generation>

Ты агент оркестратора задачи docs/tasks/current/<task>/. Твой шаг: [N] <title>.

Цель: <what must be true after the step>
Как: <concrete actions, files>
Проверка: <test / command / observable behavior>
Не трогай: <files and resources of parallel agents; Unity editor if not yours>
Прочитай: CLAUDE.md, <the docs from the step's documentation list>, <handoff file if any>

Контекст: следи за своим контекстом. После каждого крупного действия (и не реже чем каждые ~15 вызовов
инструментов) выполни
  python3 .claude/skills/orchestrate/context_meter.py --marker ORCH-<task>-<step>-g<generation>
Как только контекст больше 250000 токенов (или код выхода 2) - прекрати работу, допиши handoff
docs/tasks/current/<task>/handoff/<step>_g<generation>.md по шаблону ниже и закончи ответом
"HANDOFF: <путь>". Не начинай новую подзадачу, если до 250000 осталось меньше 30000.

Шаблон handoff:
  ## Шаг <N> - handoff поколения <g>
  ### Сделано (с evidence: команда / тест / файл)
  ### Текущий момент остановки (файл, место, что именно делалось)
  ### Осталось
  ### Находки и решения (неочевидное, тупики и почему)
  ### Изменённые файлы (committed / uncommitted)

Итоговый ответ (когда шаг выполнен): "DONE", список изменённых файлов, evidence проверки, отклонения от плана,
открытые вопросы. Коротко - не пересказывай код.
```

Rules for the brief:
- One step per agent. Name real files (verified with Grep/Glob), not guesses.
- Repeat the project rules the step touches (fail fast, Lifetime for subscriptions, `[Inject]` methods public across
  assemblies, Russian prose / English code, no commits unless the task says so).
- Rules of the project for a kind of step are in `.claude/project-skills.md` § orchestrate, when it has one: the
  brief carries them, and the step is accepted only by a report that answers each.
- Do not pass the orchestrator's hypothesis as a fact; pass what is verified and what is not.
- Launch the independent agents of a wave **in one message** (parallel tool calls). Use `isolation: "worktree"` only
  for parallel steps that must edit overlapping areas and will be merged later - otherwise the shared working tree.
- Record marker and agentId of each launch in `_orchestration.md` right away.

## Phase 4 — Watch, verify, hand off

While agents run (they report back by notification; do not poll in a loop):
- On a notification read only the final report, not the transcript. Check the evidence yourself when it is cheap
  (`git diff --stat`, run the named test, grep the change); spawn a verification agent only for a large step.
- If a report is "HANDOFF: <path>": launch the next generation of the same step (`g+1`, new marker) with the same
  brief plus "Прочитай handoff <path> - продолжай с момента остановки, не переделывай сделанное". Update the graph.
- Between waves, and when an agent runs long, check its size: `context_meter.py --agent <agentId>`. An agent over
  300 000 without a handoff: stop it (`TaskStop`), write the handoff from its transcript tail
  (`tail` of the jsonl, read only the last messages), relaunch a new generation.
- A failed step: decide - retry with a corrected brief (one retry), split it, or mark it `blocked` in `_info.md` and
  continue with what does not depend on it. Never mark a step done without evidence.
- After each finished step: update the step status in `_info.md`, the snapshot of `_progress.md`, the file table of
  `_result.md`, the graph in `_orchestration.md`. The files on disk are the state - the conversation is not.
- Start the next wave as soon as its dependencies are done (do not wait for the whole previous wave).

## Phase 5 — Own context

Check own size with `context_meter.py --session <own id>` after every wave and after every few notifications.

- Keep own context small: delegate file reading to agents, ask agents for short reports, do not read full transcripts.
- Over **250 000**: finish the current bookkeeping and hand off the orchestration:
  1. update `_orchestration.md` (graph, running agents with markers / agentIds, resources) and `_progress.md`
     ("Текущий момент остановки");
  2. write `docs/tasks/current/<task>/handoff/orchestrator_g<n>.md`: what is done, what runs right now, what is next,
     decisions the user should see;
  3. increment `orchestrator_generation`;
  4. launch the next orchestrator as an agent (`general-purpose`): "Ты оркестратор поколения <n+1> задачи
     docs/tasks/current/<task>/. Прочитай .claude/skills/orchestrate/SKILL.md и выполняй его с Phase 4, начиная с
     handoff <path> и <task>_orchestration.md. ORCH marker: ORCH-<task>-orchestrator-g<n+1>. Свой контекст меряй через
     --marker." Running agents keep running; their notifications reach this session - forward each one to the new
     orchestrator with `SendMessage` and do nothing else.
  5. Tell the user in one line that the orchestration moved to generation n+1.
- If agents cannot spawn agents in this environment, instead output the continuation prompt for a fresh session
  (same text as in step 4) and stop.

## Phase 6 — Completion

When every step is done or blocked:
1. Run the task's overall check (tests, build, the scenario from `_info.md`) - with an agent if it is heavy.
2. Finalize `_result.md` (Phase 3 of the `workflow` skill), close `_orchestration.md` with the totals: steps, agents,
   handoffs, blocked steps.
3. Report to the user in Russian: what is done (with evidence), what is blocked and why, the decisions agents took,
   the files changed. Do not commit unless asked; suggest `/workflow-complete`.

---

## Rules

- The orchestrator plans, briefs, verifies, records. Implementation goes to agents.
- Every agent prompt carries its ORCH marker and the context instructions - no exceptions.
- Parallel only with disjoint files and a single owner of each shared resource (Unity editor, cluster, submodule).
- Status changes go to disk at once (`_info.md`, `_progress.md`, `_result.md`, `_orchestration.md`).
- Agents' reports are claims: verify the evidence before marking done.
- Prose in Russian, identifiers and paths in English.
