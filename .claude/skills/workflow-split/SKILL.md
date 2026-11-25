---
name: workflow-split
description: Save the current session state into the task's info/progress/result files so a fresh session can continue without context loss. Use when the user runs /workflow-split or says split workflow, context split, save session, or continue later ("раздели воркфлоу", "раздели сессию", "продолжи позже").
---

# Workflow Split Skill

When the user runs `/workflow-split [task_name]`, save all current session context into the task's `_info.md`, `_progress.md`, and `_result.md` files so that the next Claude Code session can seamlessly continue.

Use this when context is running low mid-workflow and the work needs to carry over to a fresh session.

If `task_name` is omitted, find the most recently modified task folder in `docs/tasks/current/`.

---

## Phase 1 — Identify the Task

1. Find the task folder: `docs/tasks/current/<task_name>/`
2. Read all existing files: `<task_name>_info.md`, `<task_name>_progress.md`, `<task_name>_result.md`
3. If no task folder is found — tell the user and stop
4. Files written before the English templates may carry the Russian names: `## План реализации`, `### Что я хочу`, `### Цель`, `### Контекст`, `## Заметки`, `Текущий момент остановки`, `### Что сделано`, `### Измененные файлы`, `### Отличия от плана`, `### Нерешенные вопросы`; update those sections under their names, do not add English duplicates

---

## Phase 2 — Collect Current State

### Step 1 — Gather what was done

- Run `git diff main --name-only` to get all changed files
- Run `git diff --name-only` (unstaged) and `git diff --cached --name-only` (staged) for uncommitted work
- Run `git log main..HEAD --oneline` to see commits on the branch (if any)
- Review conversation context: what steps were completed, what was being worked on

### Step 2 — Determine what remains

- Compare completed work against the plan in `<task_name>_info.md`
- For each step in the Plan: mark as `[x]` completed, `[/]` in-progress, or `[ ]` not started
- Identify the exact point where work stopped — what was being done right now
- Update `completed_steps` and `blocked_steps` arrays in `_info.md` front-matter

### Step 3 — Collect non-obvious findings

From the current session, gather:
- Discovered edge cases or complications
- Decisions made and why
- Workarounds applied
- Files that turned out to be important but weren't in the original plan
- Any failed approaches and why they failed

---

## Phase 3 — Update `<task_name>_info.md`

Update the Plan section (under `## Implementation plan`). Do NOT modify Requirements (`### What I want`, `### Goal`, `### Context`).

What to update:
- Step statuses: `[ ]` → `[/]` → `[x]` based on what was completed
- Add newly discovered steps if necessary
- Remove steps that turned out to be unnecessary
- Add new key files discovered during work
- Update front-matter: `status`, `phase`, `updated`, `completed_steps`, `blocked_steps`

What NOT to update:
- `### What I want` — Requirements are stable, do not change without user agreement
- `### Goal` and `### Context` — same rule

If the plan is still accurate — only update step statuses and front-matter.

---

## Phase 4 — Update `<task_name>_progress.md`

This is the CRITICAL file. It must contain everything the next session needs to continue.

Preserve all existing notes. Restructure to snapshot-first format:

```markdown
---
task: <task_name>
updated: <YYYY-MM-DD>
---

## Snapshot

| Step | Status | Evidence | Blocker |
|------|--------|----------|---------|
| [N] | [x] / [ ] | [test name or command] | [or —] |

## Notes

### [YYYY-MM-DD HH:MM] Session split
**Where the work stopped:** [Exact description: which file was being edited,
what line, what exactly was being done. Enough detail for a cold start
without loss of context.]

**Important findings:**
[All non-obvious discoveries from this session: specific files, patterns,
edge cases, decisions, workarounds, failed approaches.]

**Changed files (at the split):**
| File | Status | What changed |
|------|--------|--------------|
| `path/to/File.cs` | committed / uncommitted | [description] |

[Previous notes from old _progress.md preserved below]
```

Rules for `_progress.md`:
- **Preserve all existing notes** — append, don't replace
- The snapshot table must reflect current state accurately
- The "Where the work stopped" section must be detailed enough for a cold start
- Include file paths, line numbers, and specific context where relevant
- If there are uncommitted changes — note exactly what they are

---

## Phase 5 — Update `<task_name>_result.md`

Result.md is a living document and the primary source for the next session.
Update it with current progress:

1. In `### What was done` — add completed items from this session
2. In `### Changed files` — add rows for files changed in this session:
   ```markdown
   | File | What changed | Step | Evidence |
   |------|--------------|------|----------|
   | `path/to/File.cs` | [description] | [N] | [test or command] |
   ```
3. In `### Deviations from the plan` — add any deviations discovered
4. In `### Open questions` — add open questions from this session

---

## Phase 6 — Output Continuation Prompt

After updating the files, output only the prompt below, for the user to paste into a new session. It is not an instruction
for this session: do not act on it and add nothing to it.

```
[COPY-PASTE THIS INTO THE NEXT SESSION]

Within /workflow, continue the work on docs/tasks/current/<task_name>/. Read <task_name>_result.md (primary source), <task_name>_info.md (the current plan) and <task_name>_progress.md (snapshot + findings) and continue from where the work stopped.
```

---

## Rules

- Code identifiers and file paths — English
- Prose of the task files and of the continuation prompt — English; the chat with the user stays in Russian
- NEVER guess file paths — verify with Glob/Grep
- Git diff is the source of truth for changed files
- Do NOT create commits — only update documentation files
- Do NOT modify any source code files
- Preserve all existing content in `_progress.md` notes — only append and restructure to snapshot format
- The goal is zero information loss between sessions
- Keep "Where the work stopped" as specific as possible — vague descriptions defeat the purpose
- If the task has no meaningful progress yet — say so and skip the update
- Update the front-matter of `_info.md` and `_progress.md` (`_result.md` has none)
- **`_result.md` is the primary source for continuation — it must be kept up to date**
- **When updating `_info.md` — change only the Plan section, never Requirements**
