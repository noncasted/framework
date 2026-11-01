---
name: auto
description: Autonomous mode - the user is away from the computer, the session keeps working on the task without asking anything, restarts crashed or hung processes (Unity editor, cluster, tests) itself and writes every decision it takes into a decisions file for the user to review later. Use when the user runs /auto [task or path], or says they are leaving / away and the work must go on without them.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /auto, auto, автономно, я отошёл, я ушёл, работай без меня, не жди меня, продолжай сам
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

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
   Create it if missing (template below); if it exists, append a new session section.
2. Write down the starting point in it: the task, `git status --short | head -30`, the HEAD commit, the state of the
   Unity editor (`pgrep -af unityhub-unity-editor`, Play on/off) and of the cluster.
3. Do not wait for confirmations anywhere - the "Начинаем реализацию?" of `workflow`, the Step 5.5 gate, the wave
   preview of `orchestrate`: decide and record the decision instead.

```markdown
---
task: <task>
updated: <YYYY-MM-DD HH:MM>
---

# Решения, принятые без пользователя

## Сессия <YYYY-MM-DD HH:MM>
Старт: <HEAD sha>, <кратко git status>, Unity: <pid / нет>, кластер: <да / нет>

### Требуют внимания
<!-- decisions the user is most likely to want to change, and postponed actions; keep at the top -->

### Решения
| # | Время | Вопрос | Решение | Альтернативы | Почему | Как откатить |
|---|-------|--------|---------|--------------|--------|--------------|

### Инциденты
| Время | Что упало / зависло | Признаки | Что сделано | Итог |
|-------|---------------------|----------|-------------|------|

### Отложено до пользователя
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
   "Требуют внимания".
4. Keep working. Do not stop to wait for the user's opinion on a decision already recorded.

Forbidden in auto mode even when it seems right (record it under "Отложено до пользователя" with the exact command and
continue with something else): `git push`, commits outside the task branch / without a request in the task, deploy
(`deploy-client`, Coolify, Steam), deleting user data, untracked files or branches, `git reset --hard`,
`git checkout -- <files>` over changes not made in this session, force operations, changing credentials or the
`OptionsContainer` environment on disk, spending money (paid generation beyond what the task needs).

## Phase 3 — Process watchdog

The session owns the Unity editor and the cluster while the user is away (record that in the decisions file).
Long operations run with `run_in_background` / `Monitor`, never with a blocking wait that can hang the session.

### Unity editor

Signs of trouble (check them before restarting, `.claude/docs/UNITY_EDITOR_WORKFLOW.md`):
- **crashed**: no `unityhub-unity-editor` process with `-projectpath ${CLAUDE_PROJECT_DIR}/client`, or
  `client/Logs/Editor.log` ends with `Fatal error` / a native stack / `SIGABRT`; MCP "No Unity Editor instances found".
- **hung**: the process is there, MCP calls time out 3 times in a row over at least 5 minutes, `Editor.log` has not
  grown for 5 minutes (`stat -c %Y`), and no compile / domain reload / test run / asset import is in progress in the
  log tail. A busy main thread at 100% CPU with a growing log is not a hang - wait.
- **stuck modal dialog** (nobody can click it): MCP times out while the log is silent right after a save / scene
  change / license prompt.
- **stuck compile**: see "Compile requested but never started" in `UNITY_EDITOR_WORKFLOW.md` - try the documented
  `TickCompilationPipeline` workaround before a restart.

Restart procedure:
1. Save the evidence: `tail -200 client/Logs/Editor.log > <scratchpad>/editor_crash_<time>.log`.
2. If the editor still answers `execute_code`: leave Play, save open scenes
   (`EditorSceneManager.SaveOpenScenes()`), `AssetDatabase.SaveAssets()`.
3. Kill: `kill <pid>`, wait 20 s, then `kill -9 <pid>` and its `AssetImportWorker` children
   (`pgrep -f "parentPid <pid>"`). If the process is stopped (`State: T` in `/proc/<pid>/status`, a leftover `gdb`),
   `kill -CONT <pid>` first.
4. Wait 30 s - Unity Hub often relaunches the editor by itself. If not, launch it detached:
   `setsid nohup /tools/unity/<version>/Editor/Unity -projectPath ${CLAUDE_PROJECT_DIR}/client >/dev/null 2>&1 &`
   (`<version>` from `client/ProjectSettings/ProjectVersion.txt`).
5. Wait for readiness with `Monitor` on `Editor.log` (until the MCP bridge line / `mcpforunity://editor/state`
   `data.advice.ready_for_tools`), up to 15 min. A startup hang - the "Editor Startup Hangs" section of
   `UNITY_EDITOR_WORKFLOW.md` (read-only folders, stray `.tmp.*.meta`).
6. Record the incident (signs, actions, result). Redo the step that was running, but not the operation that
   crashed in the same form: if it is in the "Operations That Crash the CoreCLR Editor" table, use the documented
   alternative; if it crashed twice in the same way, find another route and add the case to
   `UNITY_EDITOR_WORKFLOW.md` / `.claude/docs/CLAUDE_MISTAKES.md`.
7. Three restarts within an hour without progress - stop using the editor: continue with what does not need it (code
   that must compile, backend, docs), and put the rest under "Требуют внимания".

The MPPM clone (two clients) is restarted through its scenario `Stop()` / `Start()` from the main editor, not killed
(`UNITY_EDITOR_WORKFLOW.md`).

### Other processes

- Backend cluster (`start-cluster` skill): health endpoint / `backend/.telemetry/logs` silent and requests time out -
  stop the `dotnet` processes of `backend/Orchestration/Aspire`, start again with the skill. Free ports with
  `ss -tlnp` before restart.
- `dotnet test`, builds, scripts: run with a `timeout` matched to the expected time (x3), kill on expiry, look at the
  output for the hanging test, rerun once; a repeatable hang - record it and continue with other steps.
- Kill only processes this session started or the processes named above. Never kill the user's other programs.

## Phase 4 — Keep going

- Work through the plan to the end. When a step is blocked, record it and take the next step that does not depend
  on it; come back to the blocked one when the cause may have gone (after a restart, after another step).
- Verify each step as usual (tests, compile, renders) - nobody will check it for you before you continue.
- Context: check with `python3 .claude/skills/orchestrate/context_meter.py --session <own session id>`; over 250 000
  tokens - save the state as `workflow-split` does (task files) and continue as described in Phase 5 of the
  `orchestrate` skill (next generation as an agent) instead of stopping with a copy-paste prompt.
- Keep `_progress.md` / `_result.md` of the task up to date: the user reads them on return together with the
  decisions file.

## Phase 5 — Finish

When the task is done or nothing more can move:
1. Final state into the decisions file: what is done, what is blocked, the editor / cluster state (Play off, scenes
   saved, console without new errors), `git status --short`.
2. Sort "Требуют внимания" by importance.
3. Notify (`PushNotification` if available) and write the final message in Russian: result in 3-7 lines, link to the
   decisions file, number of decisions / incidents, the list "Требуют внимания" and "Отложено до пользователя".

---

## Rules for agents (put into every agent brief in auto mode)

```
Пользователь недоступен. Не задавай вопросов: выбирай обратимый вариант, ближайший к существующим паттернам, и
перечисляй принятые решения в итоговом отчёте (вопрос / решение / альтернативы / почему). Не делай push, deploy,
reset --hard и удалений чужих файлов. Unity editor трогай только если он закреплён за тобой в брифе; если он упал или
завис - не перезапускай сам, верни отчёт "EDITOR DOWN" с признаками.
```

The session (or the orchestrator) copies the agents' decisions into the decisions file and handles "EDITOR DOWN" with
the watchdog above.

## Rules

- No questions, no waiting - decide, record, continue.
- Every decision and incident goes to the decisions file before or right after the action.
- Irreversible and outward-facing actions are postponed, never taken.
- Restart only what is down by the documented signs; save the evidence first.
- Prose in Russian, identifiers and paths in English.
