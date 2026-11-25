---
name: workflow
description: Create a structured task workspace (info/progress/result files) from a description and begin implementation. Use when the user runs /workflow or says create workflow, task workspace, or start task.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /workflow, workflow, воркфлоу, запусти воркфлоу, create workflow, task workspace, начни задачу, workflow task
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Workflow Skill

When the user runs `/workflow [description]`, create a structured task workspace with three files and begin implementation.

This is an enhanced version of `/task` that maintains a living workspace throughout the task lifecycle.

---

## Workspace Structure

Active tasks live under `docs/tasks/current/`, completed tasks move to `docs/tasks/complete/`.

```
docs/tasks/
  current/<task_name>/
    <task_name>_info.md         — Requirements (stable) + mutable Plan
    <task_name>_progress.md     — State snapshot + chronology
    <task_name>_result.md       — Living result document
  complete/<task_name>/
    <task_name>.md              — Condensed summary (created by /workflow-complete)
```

`<task_name>` — lowercase, underscores, from the short task name (e.g. `bot_card_strategies`).

---

## Phase 1 — Create Brief (`<task_name>_info.md`)

Follow the same research process as `/task`:

### Step 1 — Extract ALL requirements

Parse the user's message after `/workflow`. **Preserve everything — nothing is noise.**

- Make a full list of ALL goals, subtasks, constraints and context from the user's message
- If the user mentioned 7 items, the list must have 7 items. Drop nothing
- Extract all mentioned class names, system names, file names, features
- Fix typos, normalize informal names to proper C# identifiers
- Keep the user's descriptions of the behaviour next to the technical mapping

### Step 2 — Search the codebase

For every class/system/feature mentioned:
- `Grep` for the class name in `client/Assets/`, `backend/`, or `shared/` to find its file
- `Glob` by pattern if a group of files is involved
- Read key files briefly — enough to understand current implementation
- If new files will be created, find the correct `.csproj`

### Step 3 — Map task to documentation

Include docs that are **relevant** to this task:

| Task touches... | Include |
|-----------------|---------|
| MonoBehaviour, `[Inject]`, `ISceneService`, `IScopeSetup` | `tools/framework/.claude/docs/COMMON_CONTAINER.md` |
| `Advise`, `View`, `Lifetime`, subscriptions, cleanup | `tools/framework/.claude/docs/COMMON_LIFETIMES.md` |
| `EventSource`, `ViewableProperty`, `ViewableList` | `tools/framework/.claude/docs/COMMON_REACTIVE_BASICS.md` |
| `UniTask`, async methods, file I/O, `IReadOnlyList` | `tools/framework/.claude/docs/API_DESIGN_FULL.md` |
| member order, naming, braces, `NoAwait` | `tools/framework/.claude/docs/CODE_STYLE_FULL.md` |
| Grain, State, `[Transaction]`, Orleans backend | `tools/framework/.claude/docs/COMMON_ORLEANS.md` |
| Blazor, razor, `@inject`, console UI | `tools/framework/.claude/docs/BLAZOR.md` |
| IOrleans, AddressableDictionary, messaging | `tools/framework/.claude/docs/COMMON_ORLEANS.md` |
| "which pattern", architectural choice | `tools/framework/.claude/docs/DECISION_TREES.md` |
| common pitfalls, known mistakes | `.claude/docs/CLAUDE_MISTAKES.md` |
| terminology, vocabulary | `.claude/docs/VOCABULARY.md` |
| keyword lookup, documentation finder | `.claude/docs/TRIGGERS.md` |
| testing, xUnit, test logs | `.claude/docs/TESTING.md` |
| telemetry, metrics, logs | `.claude/docs/TELEMETRY.md` |
| error lookup, debugging | `.claude/docs/ERRORS.md` |
| deploy, Coolify, docker-compose, Aspire | `.claude/docs/DEPLOY.md` |
| deploy failure, troubleshooting | `.claude/docs/DEPLOY_TROUBLESHOOTING.md` |
| reactive values, ViewableProperty patterns | `tools/framework/.claude/docs/COMMON_REACTIVE_VALUES.md` |
| reactive collections, ViewableList | `tools/framework/.claude/docs/COMMON_REACTIVE_COLLECTIONS.md` |
| reactive patterns, event wiring | `tools/framework/.claude/docs/COMMON_REACTIVE_PATTERNS.md` |
| lifetime patterns, scoped cleanup | `tools/framework/.claude/docs/COMMON_LIFETIMES_PATTERNS.md` |

### Step 4 — Decompose into steps

Break the task into ordered concrete steps with two-level decomposition:

1. **First level — by user requirements.** Each requirement becomes a group.
2. **Second level — by files.** Within each group, list concrete file changes.

Rules:
- Name the target file explicitly (real path found in Step 2)
- If a new file must be created, mark it: `[new file — add to X.csproj]`
- Steps must be sequenced so dependencies come first
- Each step must have an acceptance criterion — a concrete test, command, or observable behavior that proves the step is done

### Step 5 — Verify completeness (CRITICAL)

**Re-read the user's original message.** For every requirement:
- Verify it is covered by a concrete step
- If something is NOT covered — add it now

### Step 5.5 — Gate: Ask or Proceed?

Before writing `_info.md`, check:
1. Are there conflicting requirements with no clear priority?
2. Does the task require choosing between architectural approaches where both are valid but have different risk profiles?
3. Does the task reference external context not present in the codebase (past sprints, verbal agreements, other tasks)?

If ANY checked: output the specific ambiguity, propose 2-3 options with tradeoffs, and ask the user **one concise question**. Wait for answer before proceeding to Step 6.

If NONE checked: proceed to Step 6 without questions.

### Step 6 — Write `<task_name>_info.md`

Save the brief to `docs/tasks/current/<task_name>/<task_name>_info.md`.

**Mandatory YAML front-matter at the top of the file:**

```yaml
---
task: <task_name>
status: pending
phase: brief
created: <YYYY-MM-DD>
updated: <YYYY-MM-DD>
total_steps: 0
completed_steps: []
blocked_steps: []
---
```

**File structure:** Requirements first (stable — do not change without user agreement), then Plan (mutable — update when approach changes).

```markdown
## Task: [short name]

### What I want

[Several paragraphs that convey the essence of the task in detail. Keep here as much of the user's original message as possible: what must be done, why, the constraints, examples, context, details. Do not cut it down to one sentence — write it out in full, so that a week later the whole picture can be restored without rereading the history.]

### Goal
[The full description of the goal. Every item of the user must be present.]

### Context
[Motivation, constraints, preferences, links to other tasks.]

## Implementation plan

#### [N] Step title
- **Status:** [ ] pending | [/] in_progress | [x] completed | [!] blocked
- **Goal:** [one sentence — what must be true after the step]
- **How:** [concrete actions]
- **Check:** [a concrete test, command, or observable behavior]
- **Files:** [list of files]
- **Depends on:** [steps or —]
- **Blocks:** [steps or —]

### Key files

| File | Role in the task |
|------|------------------|
| `path/to/File.cs` | [what and why] |

### Docs to read
- `tools/framework/.claude/docs/COMMON_CONTAINER.md` — [the specific reason]

### Risks
[Specific gotchas. Omit section if no risks.]
```

### Step 7 — Initialize `<task_name>_progress.md`

Create the file with this template. The snapshot table goes first, then chronology:

```markdown
---
task: <task_name>
updated: <YYYY-MM-DD>
---

## Snapshot

| Step | Status | Evidence | Blocker |
|------|--------|----------|---------|
| [N] | [ ] / [x] | [test name or command] | [or —] |

## Notes
<!-- Findings, decisions and useful information are written here during the implementation -->
```

### Step 8 — Initialize `<task_name>_result.md`

Create the file with this template. It is a living document — fill it gradually as steps complete, not only at the end:

```markdown
## [Task name] — Result

### Status: In progress

### What was done
<!-- Populated as steps complete -->

### Changed files

| File | What changed | Step | Evidence |
|------|--------------|------|----------|

### Deviations from the plan
<!-- Recorded immediately when approach changes -->

### Open questions
<!-- Populated as open questions arise -->
```

### Step 9 — Confirm with user

Output the brief to the user and ask, in Russian: **"Start the implementation?"**

---

## Phase 2 — During Implementation

While working on the task, **actively maintain `<task_name>_progress.md`** and **gradually populate `<task_name>_result.md`**.

### Update priorities

1. **Update step status in `_info.md`** — `[ ]` → `[/]` → `[x]`.
2. **Update `_progress.md` snapshot** — mark completed steps with evidence.
3. **If the plan changes** — update `_info.md` (Plan section) and record the diff in `_progress.md`.
4. **Populate `_result.md`** — add rows to the files table as changes are made.

### What to write in `_progress.md`

- Important findings during code investigation (non-obvious dependencies, edge cases)
- Decisions made and why (if there was a choice between options)
- Problems discovered and how they were solved
- Changes to the plan relative to `<task_name>_info.md` (when and why)
- List of actually changed/created files

### When to update

- Before starting each major step — record what we are starting
- After discovering something non-obvious — record the finding
- After solving a problem — record the problem and solution
- When the plan changes — record what and why changed

### Format for entries

Each entry — with a timestamp:

```markdown
### [YYYY-MM-DD HH:MM] Step title or finding title
Content.
```

---

## Phase 3 — Completion

When the task is complete, finalize `<task_name>_result.md`:

1. Change `### Status:` to `Done`.
2. Ensure `### What was done` lists all completed items.
3. Ensure `### Changed files` table is complete with Evidence column.
4. Fill `### Deviations from the plan` if any.
5. Fill `### Open questions` if any.

```markdown
## [Task name] — Result

### Status: Done

### What was done
[A short description of the result — what is implemented, 3-7 items]

### Changed files

| File | What changed | Step | Evidence |
|------|--------------|------|----------|
| `path/to/File.cs` | [a short description of the change] | [N] | [test name or command] |

### Deviations from the plan
[What was done differently from <task_name>_info.md. Omit if plan was followed exactly.]

### Open questions
[What remains to be done or needs attention. Omit if everything is done.]
```

---

## Rules

- Code identifiers and file paths — English
- Prose labels and the text of the task files — English; the chat with the user stays in Russian
- Steps must name real files found via search, never guessed paths
- Do not lose user requirements during transformation
- **Plan (in `_info.md`) is the source of truth. When the approach changes, update the Plan section in `_info.md` and record the diff in `_progress.md` and `_result.md`.**
- `<task_name>_progress.md` — snapshot first, chronology second; updated continuously
- `<task_name>_result.md` — living document, populated gradually as steps complete
- On a repeated `/workflow` call for the same task — read `_result.md` as the primary source, `_info.md` as the current plan
- On a repeated `/workflow` call for the same task — continue the work in the existing folder in `docs/tasks/current/`, do not create a new one
- Task files written before the English templates may carry the Russian names: `## Задача:`, `### Что я хочу`, `### Цель`, `### Контекст`, `## План реализации` (step fields `Статус` / `Цель` / `Как` / `Проверка` / `Файлы` / `Зависит от` / `Блокирует`), `## Заметки`, `— Результат`, `### Статус:` (`В работе` / `Завершено`), `### Что сделано`, `### Измененные файлы`, `### Отличия от плана`, `### Нерешенные вопросы`; read and update those sections under their names, do not add English duplicates
- All working files (`<task_name>_info.md`, `<task_name>_progress.md`, `<task_name>_result.md`) are created in `docs/tasks/current/<task_name>/`
- Only `/workflow-complete` moves a task to `docs/tasks/complete/`
