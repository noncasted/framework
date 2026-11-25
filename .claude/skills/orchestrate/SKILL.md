---
name: orchestrate
description: Turn the current session into the orchestrator of a task - split it into steps, run the steps with subagents (parallel where independent, sequential where dependent), keep every context (own and agents') under 300k tokens with handoffs; with --architecture the plan goes through plan-architecture first, every code step gets an independent architecture review and the task ends with a code audit. Use when the user runs /orchestrate [--architecture] [task text or path to a task file/folder] or asks to orchestrate a task with agents ("оркестрируй", "оркестратор", "раздай агентам", "выполни агентами").
---

# Orchestrate Skill

`/orchestrate [--architecture] [task text | path]` - this session becomes the **orchestrator**: it plans, delegates every
implementation step to subagents, checks their results and keeps the state on disk. The orchestrator itself does not
write production code; it reads only what it needs to plan and to verify.

`--architecture` turns on the architecture guard: Phase 1b, the additions marked **[arch]** in Phases 3, 4 and 6. Record
`architecture: true` in the front-matter of `_orchestration.md`: a next generation of the orchestrator keeps it.

Context limit: **300 000 tokens** for the orchestrator and for every agent. Measure, do not guess:

```
python3 .claude/skills/orchestrate/context_meter.py --session <own session id>   # id = the uuid in the scratchpad path
python3 .claude/skills/orchestrate/context_meter.py --agent <agentId>           # id returned by the Agent tool
python3 .claude/skills/orchestrate/context_meter.py --marker <ORCH marker>      # an agent measuring itself
```

Exit code 2 = over the limit. The output is `<tokens> (<percent>% of 300000) ok|OVER <transcript>`.

---

## Phase 1 — Task workspace

1. Argument resolution (`--architecture` first is the flag, the rest is the argument):
   - a path to `docs/tasks/current/<task>/` or one of its files - use that workspace (read `_result.md` first, then
     `_info.md`, `_progress.md`); files written before the English templates may carry the Russian names (`## План реализации`,
     `### Аудит`, `### Архитектурная сверка`, `### Вопросы пользователю`, `Текущий момент остановки`, `## Граф шагов`,
     `## Ресурсы`, `## Журнал` of `_orchestration.md`, the rest as in the Rules of `workflow`): keep those sections under
     their names;
   - any other path (`docs/...md`, a plan file) - read it, it is the task text;
   - plain text - it is the task text.
2. Without a workspace, create one exactly as the `workflow` skill does (Phase 1, steps 1-8:
   `.claude/skills/workflow/SKILL.md`), but **do not stop for "Start the implementation?"** - the user already asked to
   orchestrate. Ask only if the Step 5.5 gate of `workflow` fires.
3. Add `docs/tasks/current/<task>/<task>_orchestration.md`:

```markdown
---
task: <task>
orchestrator_session: <session id>
orchestrator_generation: 1
architecture: <true with --architecture, else false>
updated: <YYYY-MM-DD HH:MM>
---

## Step graph

| Step | Depends on | Wave | Agent (marker / agentId) | Generation | Status | Handoff |
|------|------------|------|---------------------------|------------|--------|---------|
| 1 | — | 1 | ORCH-<task>-1-g1 / a1b2... | 1 | in_progress | — |

## Resources
<!-- shared resources and who holds them now: Unity editor, cluster, generated catalogues, a file -->

## Log
### [YYYY-MM-DD HH:MM] ...
```

## Phase 1b — Architecture of the plan [arch]

Before the waves, one agent runs the `plan-architecture` skill on the workspace: "Run
.claude/skills/plan-architecture/SKILL.md for docs/tasks/current/<task>/; the answer is its report (Step 6)". It edits only
`_info.md`: steps, "Audit", "Architecture check", questions. Its new steps and changed dependencies go into the graph
of Phase 2. Its "Questions to the user": ask the blocking ones before the steps they block (one `AskUserQuestion`), go on
with what they do not block; record the answers in `_info.md`.

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

You are an agent of the orchestrator of the task docs/tasks/current/<task>/. Your step: [N] <title>.

Goal: <what must be true after the step>
How: <concrete actions, files>
Check: <test / command / observable behavior>
Do not touch: <files and resources of parallel agents; Unity editor if not yours>
Read: CLAUDE.md, <the docs from the step's documentation list>, <handoff file if any>

Context: watch your context. After every big action (and at least every ~15 tool calls) run
  python3 .claude/skills/orchestrate/context_meter.py --marker ORCH-<task>-<step>-g<generation>
As soon as the context is above 250000 tokens (or the exit code is 2) - stop the work, write the handoff
docs/tasks/current/<task>/handoff/<step>_g<generation>.md by the template below and end with the answer
"HANDOFF: <path>". Do not start a new subtask when less than 30000 are left to 250000.

Handoff template:
  ## Step <N> - handoff of generation <g>
  ### Done (with evidence: command / test / file)
  ### Where the work stopped (file, place, what exactly was being done)
  ### Left
  ### Findings and decisions (the non-obvious, dead ends and why)
  ### Changed files (committed / uncommitted)

Final answer (when the step is done): "DONE", the list of changed files, the evidence of the check, deviations from
the plan, open questions. Short - do not retell the code.
```

**[arch]** the brief also carries:

```
Architecture: read tools/framework/.claude/docs/ARCHITECTURE_PATTERNS.md (the sections of the layers of the step)<,
GAMEPLAY_ARCHITECTURE.md for gameplay>. Model: <the analogue folder from the "Architecture check" of the plan> - repeat
its layout. In the final report - "Architecture check of the step": the new folders and classes and why they are there,
which principles P1-P14 the step touched and how they are met.
```

Rules for the brief:
- One step per agent. Name real files (verified with Grep/Glob), not guesses.
- Repeat the project rules the step touches (fail fast, Lifetime for subscriptions, `[Inject]` methods public across
  assemblies, English in code, comments and task files, no commits unless the task says so).
- Rules of the project for a kind of step are in `.claude/project-skills.md` § orchestrate, when it has one: the
  brief carries them, and the step is accepted only by a report that answers each.
- Do not pass the orchestrator's hypothesis as a fact; pass what is verified and what is not.
- Launch the independent agents of a wave **in one message** (parallel tool calls). Use `isolation: "worktree"` only
  for parallel steps that must edit overlapping areas and will be merged later - otherwise the shared working tree.
- Record marker and agentId of each launch in `_orchestration.md` right away.
- Model per step: `model: "sonnet"` for search and reading, running tests, mechanical edits and verification of evidence;
  the inherited model for design, new code and the [arch] reviews.

## Phase 4 — Watch, verify, hand off

While agents run (they report back by notification; do not poll in a loop):
- On a notification read only the final report, not the transcript. Check the evidence yourself when it is cheap
  (`git diff --stat`, run the named test, grep the change); spawn a verification agent only for a large step.
- **[arch]** A step that added or changed code is done only after an architecture review: a fresh agent (not the
  implementer) gets the step, its file list and `git diff` range, and the analogue folder - not the orchestrator's
  opinion - and runs `code-audit` Steps 1-3 on the changed files and their feature folders against P1-P14,
  `GAMEPLAY_ARCHITECTURE.md` and the analogue. It answers with confirmed findings only (file:line, principle). A finding
  in the step: one more round of the same step agent with the findings as notes (`SendMessage`, or a new generation with
  them in the brief), then the review again; a second round with findings -> `blocked`, the user decides. A finding
  outside the step: a follow-up step in `_info.md`. Steps of docs or research skip the review.
- If a report is "HANDOFF: <path>": launch the next generation of the same step (`g+1`, new marker) with the same
  brief plus "Read the handoff <path> - continue from where the work stopped, do not redo what is done". Update the graph.
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
- Over **250 000**: finish the current bookkeeping and prepare the session for a compact:
  1. update `_orchestration.md` (graph, running agents with markers / agentIds, resources) and `_progress.md`
     ("Where the work stopped");
  2. write `docs/tasks/current/<task>/handoff/orchestrator_g<n>.md`: what is done, what runs right now, what is next,
     decisions the user should see;
  3. increment `orchestrator_generation`;
  4. tell the user in one line that the state is on disk and the session is ready for `/compact`, with the line to
     continue with after it: "Continue the orchestration of docs/tasks/current/<task>/: read
     .claude/skills/orchestrate/SKILL.md, the handoff <path> and <task>_orchestration.md, carry on from Phase 4."
- Do not launch a next orchestrator as an agent: every notification would have to be relayed to it, and the work would
  leave the session the user talks to (a session whose user is away follows the `auto` skill). Running agents keep
  running and report to this session, before and after the compact; until the compact only record their reports on disk.

## Phase 6 — Completion

When every step is done or blocked:
1. Run the task's overall check (tests, build, the scenario from `_info.md`) - with an agent if it is heavy.
   **[arch]** Then a `/code-audit` agent over the folders the task touched (`code-audit` skill, the report goes to
   `docs/audits`): drift between steps shows only on the whole area. Its critical and important findings of this task
   become one final step; the rest go to the report to the user.
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
- Task files, handoffs and agent prompts in English, identifiers and paths too; the chat with the user in Russian.
