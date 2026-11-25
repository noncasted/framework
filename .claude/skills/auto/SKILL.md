---
name: auto
description: Autonomous mode - the user is away from the computer, the session keeps working on the task without asking anything, restarts crashed or hung processes (Unity editor, cluster, tests) itself and writes every decision it takes into a decisions file for the user to review later. Use when the user runs /auto [task or path], or says they are leaving / away and the work must go on without them ("автономно", "я отошёл", "я ушёл", "работай без меня", "не жди меня", "продолжай сам").
---

# Auto Skill

`/auto [task text | path]` - the user is away. The session works on until the task is done or truly blocked. Nobody
answers questions, nobody clicks dialogs, nobody restarts the editor - the session does it.

The task: the argument; without one - the task already running in this conversation (the current
`docs/tasks/current/<task>/` workspace or the last request). `/auto` combines with the other skills: `/auto` on top of
`/orchestrate` keeps the orchestrator autonomous; its agents get the same rules (put the "Rules for agents" block
below into every brief).

---

## Phase 1 — Start

1. Find the decisions file:
   - a task workspace exists - `docs/tasks/current/<task>/<task>_decisions.md`;
   - otherwise - `docs/tasks/auto/<YYYY-MM-DD>_<short_slug>_decisions.md`.
   Create it if missing (template below); if it exists, append a new session section. A file written before the
   English template may carry the Russian names (`## Сессия`, `### Требуют внимания`, `### Решения`, `### Инциденты`,
   `### Отложено до пользователя`): the new session section takes the English names.
2. Write down the starting point in it: the task, `git status --short | head -30`, the HEAD commit, the state of the
   Unity editor (`pgrep -af unityhub-unity-editor`, Play on/off) and of the cluster.
3. Do not wait for confirmations anywhere - the "Start the implementation?" of `workflow`, the Step 5.5 gate, the wave
   preview of `orchestrate`: decide and record the decision instead.

```markdown
---
task: <task>
updated: <YYYY-MM-DD HH:MM>
---

# Decisions taken without the user

## Session <YYYY-MM-DD HH:MM>
Start: <HEAD sha>, <git status in short>, Unity: <pid / none>, cluster: <yes / no>

### Needs attention
<!-- decisions the user is most likely to want to change, and postponed actions; keep at the top -->

### Decisions
| # | Time | Question | Decision | Alternatives | Why | How to roll back |
|---|------|----------|----------|--------------|-----|------------------|

### Incidents
| Time | What crashed / hung | Signs | What was done | Result |
|------|---------------------|-------|---------------|--------|

### Postponed for the user
<!-- actions that are forbidden in auto mode, with the exact command to run -->
```

## Phase 2 — Decide instead of asking

Never call `AskUserQuestion`, never end a turn with a question. When a question comes up:

1. Look for the answer: the task files, `CLAUDE.md`, `.claude/docs`, the memory, the existing code and its patterns,
   git history (`git log -S`, how similar things were done).
2. Choose the option that is **reversible, smallest, and closest to the existing patterns**; for visual work -
   produce variants (`visualize` skill) and continue with the most fitting one, keeping the rest for the user.
3. Record it in the decisions table at once (before acting): question, decision, alternatives, reason, how to roll
   back (file, commit, flag). A decision that changes the requirements or the architecture also goes to
   "Needs attention".
4. Keep working. Do not stop to wait for the user's opinion on a decision already recorded.

Forbidden in auto mode even when it seems right (record it under "Postponed for the user" with the exact command and
continue with something else): `git push`, commits outside the task branch / without a request in the task, deploy
(`deploy-client`, Coolify, Steam), deleting user data, untracked files or branches, `git reset --hard`,
`git checkout -- <files>` over changes not made in this session, force operations, changing credentials or the
`OptionsContainer` environment on disk, spending money (paid generation beyond what the task needs).

## Phase 3 — Process watchdog

The session owns the Unity editor and the cluster while the user is away (record that in the decisions file).
Long operations run with `run_in_background` / `Monitor`, never with a blocking wait that can hang the session.

### Unity editor

Signs of trouble (check them before restarting, `.claude/docs/UNITY_EDITOR_WORKFLOW.md`):
- **crashed**: `.claude/skills/control/control editor status` says `down` or `crashed` (it looks for the editor of
  `editor.project` of `.claude/control.json`, `client` without it), or the `log` it names ends with `Fatal error` / a native
  stack / `SIGABRT`; MCP "No Unity Editor instances found".
- **hung**: the process is there, MCP calls time out 3 times in a row over at least 5 minutes, `Editor.log` has not
  grown for 5 minutes (`stat -c %Y`), and no compile / domain reload / test run / asset import is in progress in the
  log tail. A busy main thread at 100% CPU with a growing log is not a hang - wait.
- **stuck modal dialog** (nobody can click it): MCP times out while the log is silent right after a save / scene
  change / license prompt.
- **stuck compile**: see "Compile requested but never started" in `UNITY_EDITOR_WORKFLOW.md` - try the documented
  `TickCompilationPipeline` workaround before a restart.

Restart procedure:
1. `.claude/skills/control/control editor status`; on `hung`, `stopped` or `down`: `control editor restart`. It saves what a responsive
   editor can save, keeps the log (`evidence`), kills the process with its workers, starts it and waits for the bridge.
2. On a startup hang read the "Editor Startup Hangs" section of `UNITY_EDITOR_WORKFLOW.md` (read-only folders, stray `.tmp.*.meta`).
3. Record the incident (signs, actions, result). Redo the step that was running, but not the operation that
   crashed in the same form: if it is in the "Operations That Crash the CoreCLR Editor" table, use the documented
   alternative; if it crashed twice in the same way, find another route and add the case to
   `UNITY_EDITOR_WORKFLOW.md` / `.claude/docs/CLAUDE_MISTAKES.md`.
4. Three restarts within an hour without progress - stop using the editor: continue with what does not need it (code
   that must compile, backend, docs), and put the rest under "Needs attention".

The MPPM clone (two clients) is restarted through its scenario `Stop()` / `Start()` from the main editor, not killed
(`UNITY_EDITOR_WORKFLOW.md`).

### Other processes

- Backend cluster: health endpoint / `backend/.telemetry/logs` silent and requests time out - `control cluster restart`, after a
  backend change `control cluster rebuild`.
- `dotnet test`, builds, scripts: run with a `timeout` matched to the expected time (x3), kill on expiry, look at the
  output for the hanging test, rerun once; a repeatable hang - record it and continue with other steps.
- Kill only processes this session started or the processes named above. Never kill the user's other programs.

## Phase 4 — Keep going

- Work through the plan to the end. When a step is blocked, record it and take the next step that does not depend
  on it; come back to the blocked one when the cause may have gone (after a restart, after another step).
- Verify each step as usual (tests, compile, renders) - nobody will check it for you before you continue.
- Context: check with `python3 .claude/skills/orchestrate/context_meter.py --session <own session id>`; over 250 000
  tokens - save the state as `workflow-split` does (task files; an orchestrator also writes its handoff, Phase 5 of the
  `orchestrate` skill) and, as nobody is there to compact the session, launch the next generation as an agent
  (`general-purpose`) instead of stopping with a copy-paste prompt: "You are generation <n+1> of the task
  docs/tasks/current/<task>/, the user is away. Read .claude/skills/auto/SKILL.md and continue from the task files (an
  orchestrator: .claude/skills/orchestrate/SKILL.md from Phase 4, the handoff <path> and <task>_orchestration.md). ORCH
  marker: ORCH-<task>-orchestrator-g<n+1>. Measure your own context with --marker." Running agents keep running; their
  notifications reach this session - forward each one to the new generation with `SendMessage` and do nothing else.
- Keep `_progress.md` / `_result.md` of the task up to date: the user reads them on return together with the
  decisions file.

## Phase 5 — Finish

When the task is done or nothing more can move:
1. Final state into the decisions file: what is done, what is blocked, the editor / cluster state (Play off, scenes
   saved, console without new errors), `git status --short`.
2. Sort "Needs attention" by importance.
3. Notify (`PushNotification` if available) and write the final message in Russian: result in 3-7 lines, link to the
   decisions file, number of decisions / incidents, the list "Needs attention" and "Postponed for the user".

---

## Rules for agents (put into every agent brief in auto mode)

```
The user is unavailable. Do not ask questions: choose the reversible option closest to the existing patterns, and
list the decisions you took in the final report (question / decision / alternatives / why). Do not push, deploy,
reset --hard or delete files that are not yours. For work in the editor take your own copy (`control copy create <name>`,
skill `control`) and pass its `unity_instance` to every MCP call. Restart a crashed or hung editor and cluster yourself
through `control`.
```

The session (or the orchestrator) copies the agents' decisions and incidents into the decisions file.

## Rules

- No questions, no waiting - decide, record, continue.
- Every decision and incident goes to the decisions file before or right after the action.
- Irreversible and outward-facing actions are postponed, never taken.
- Restart only what is down by the documented signs; save the evidence first.
- The decisions file, task files and agent briefs in English, identifiers and paths too; the chat with the user in Russian.
