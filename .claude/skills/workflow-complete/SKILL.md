---
name: workflow-complete
description: Finalize a /workflow task, produce a condensed summary in docs/tasks/complete/, and clean up the working folder from docs/tasks/current/. Use when the user runs /workflow-complete or says finalize, complete, or close task, complete workflow, task complete ("заверши воркфлоу", "закрой задачу").
---

# Workflow Complete Skill

When the user runs `/workflow-complete [task_name]`, finalize the task, create a condensed summary in `docs/tasks/complete/`, and clean up the working folder from `docs/tasks/current/`.

If `task_name` is omitted, look for the most recently modified task folder in `docs/tasks/current/`.

---

## Phase 1 — Identify the Task

1. Find the task folder: `docs/tasks/current/<task_name>/`
2. Read all three files: `<task_name>_info.md`, `<task_name>_progress.md`, `<task_name>_result.md`
3. Files written before the English templates may carry the Russian names: `### Статус:` (`В работе` / `Завершено`), `### Что сделано`, `### Измененные файлы`, `### Отличия от плана`, `### Нерешенные вопросы`, `## Заметки`; finalize those sections under their names, do not add English duplicates
4. If the front-matter of `<task_name>_info.md` has `status: pending` or `phase` is `brief` or `implementation` — ask the user if the task is actually done before proceeding

---

## Phase 2 — Analyze What Was Actually Done

### Step 1 — Collect real changes

Run `git diff main --name-only` (or `git diff main...HEAD --name-only` if on a feature branch) to get the actual list of changed files. Cross-reference with `<task_name>_info.md` plan and `<task_name>_progress.md` notes.

### Step 2 — Compare plan vs reality

For each step in `<task_name>_info.md`:
- Was it done as planned?
- Was it done differently? How?
- Was it skipped? Why?

Identify work that was done but NOT mentioned in the plan (emergent changes, bug fixes discovered along the way).

### Step 3 — Identify problems encountered

Scan `<task_name>_progress.md` and git commit messages for:
- Bugs found and fixed
- Unexpected complications
- Workarounds applied
- Patterns that didn't work as expected
- Compilation errors that required changes

Record these as entries under `### Deviations from the plan` or `### Open questions` in result.md.

---

## Phase 3 — Update `<task_name>_result.md` in current/

Result.md is a living document that was populated gradually during implementation. Finalize it:

1. **Update the front-matter of `<task_name>_info.md`** (`_result.md` has none):
   ```yaml
   status: completed
   phase: completed
   updated: <YYYY-MM-DD>
   ```
2. **Ensure `### Status:` is `Done`**
3. **Complete `### What was done`** — add any missing completed items
4. **Complete `### Changed files`** — table with all changed files, verified against git diff:
   ```markdown
   | File | What changed | Step | Evidence |
   |------|--------------|------|----------|
   | `path/to/File.cs` | [description] | [N] | [test name or command] |
   ```
5. **Complete `### Deviations from the plan`** — what was done differently, problems encountered and solved
6. **Complete `### Open questions`** — what remains to be done

The file list MUST match the actual git diff, not just what was planned.

---

## Phase 4 — Create Condensed Summary in complete/

Create `docs/tasks/complete/<task_name>.md` — a short, self-contained summary extracted from `<task_name>_result.md`.

Format:

```markdown
## [Task name]

### What was done
[3-5 bullet points — the essence of what was implemented, no filler]

### Key files
[Only the most important files — 3-7 max, not the full change list]

### Notes
[Non-obvious decisions, gotchas, or things to know for future work. Omit if nothing noteworthy.]
```

Rules for the condensed summary:
- **Maximum ~40 lines** — this is a quick reference, not a full report
- No "Changed files" table — only key files that matter for understanding
- No "Deviations from the plan" — irrelevant after completion
- No "Open questions" unless they are blockers for future work
- Write for someone who needs to quickly understand what was done and where to look

---

## Phase 5 — Clean Up current/

Delete the working folder `docs/tasks/current/<task_name>/` (all three files: `<task_name>_info.md`, `<task_name>_progress.md`, `<task_name>_result.md`).

The full `<task_name>_result.md` is preserved in git history if anyone needs the detailed version later.

### Research media of the task

Run the `media-cleanup` skill for the folders of the task in `docs/media`.

### What may stay in git under `docs/tasks/` (applies to any task folder that gets committed, complete/ or current/)

Commit only:
- `*.md` summaries, reports, handoffs;
- key evidence screenshots that a `.md` actually references (PNG/JPG, roughly <= 1 MB each; one GIF only if it is the evidence itself);
- a tool script only when `.claude/docs`, `docs/art`, `docs/info`, memory or a code comment points at it (better: move it to `docs/art/**/tools` or `tools/`).

Never commit (delete before the commit, or keep outside the repo in the scratchpad):
- harnesses: `*.csproj`, `*.sln(x)`, `Directory.Build.props`, `Program.cs` runners, `bin/`, `obj/`, builds;
- copies of project sources (`staging/`, `draft/`, `merged/`, `orig/` trees with `client/...` paths), patches, one-off `execute_code` snippets;
- logs, dumps, `.txt` probe outputs, frame sequences (`frames/`, `raw/`), audio re-encodes, large GIF/PNG captures, any binary > 1 MB.

Check before committing: `git status --short docs/tasks` and `git diff --cached --stat -- docs/tasks` - only `.md` and a few referenced screenshots.

---

## Phase 6 — Update Project Documentation

Based on the completed work, check and update relevant documentation in `.claude/`.

### 6.1 — Check each documentation file for relevance

Pick the docs by what the task changed:

- the docs of the framework by the keyword table of its `INDEX.md` (`tools/framework/.claude/docs` in a game, `.claude/docs` in the
  framework; the index is always loaded), `ARCHITECTURE_PATTERNS.md` and `GAMEPLAY_ARCHITECTURE.md` among them;
- the docs of the game in its `.claude/docs` (`CLAUDE_MISTAKES.md`, `VOCABULARY.md`, `TRIGGERS.md`, `TESTING.md`, `TELEMETRY.md`,
  `ERRORS.md`, `DEPLOY.md`, `DEPLOY_TROUBLESHOOTING.md` and its own, when present), picked by the keyword table of its
  `CLAUDE.md`.

### 6.2 — What to update

For each relevant doc:
1. **Read** the current file
2. **Identify** what needs adding/changing based on the task results
3. **Update** in the existing format (match style of surrounding content)
4. **Cross-reference** — add links between related docs if needed

### 6.3 — CLAUDE_MISTAKES.md

If the task revealed a mistake (visible in `<task_name>_progress.md`, git history, or known from context):
- Add a new numbered Lesson entry to `.claude/docs/CLAUDE_MISTAKES.md`
- Format: wrong code, correct code, one-line rule, link to relevant docs page

### 6.4 — VOCABULARY.md

If new concepts/terms were introduced:
- Add entries in the existing table format
- Include "do not mix" notes if similar terms exist

---

## Phase 7 — Update Memory

Check if any of the following should be saved:

1. **New critical patterns** — save to `.claude/docs/CLAUDE_MISTAKES.md` or relevant `.claude/docs/*.md`
2. **Architecture changes** — update relevant `.claude/docs/*.md`
3. **User feedback** — save to the auto-memory of the session (one file per fact, indexed in `MEMORY.md`), if applicable

Do NOT save:
- File lists (derivable from git)
- Implementation details (in the code)
- Anything already in `.claude/docs/` or task files

---

## Phase 8 — Report

Output a summary to the user, in Russian, in this structure:

```markdown
## Workflow Complete: [task name]

### Task complete
- `docs/tasks/complete/<task_name>.md` — summary created
- `docs/tasks/current/<task_name>/` — working files deleted
- Front-matter updated: `status: completed`, `phase: completed`

### Docs updated
- `.claude/docs/PATHFINDING.md` — [what changed]
- `.claude/docs/VOCABULARY.md` — [what added]
- (or "No updates needed")

### Mistakes recorded
- `.claude/docs/CLAUDE_MISTAKES.md` — Lesson N: [description]
- (or "No new mistakes found")

### Memory updated
- [what was saved]
- (or "No updates needed")
```

---

## Rules

- Code identifiers and file paths — English
- Prose of the task files, the summary and the docs — English; the report to the user in chat — Russian
- Do not guess file paths: verify them with Glob/Grep
- Add documentation only for what actually changed
- Match existing format in every doc you update
- Update only existing doc files: a missing one is not created
- Git diff is the source of truth for changed files, not the plan
- Be conservative with CLAUDE_MISTAKES.md — only add genuinely new lessons, not variations of existing ones
- The condensed summary in `complete/` is a quick reference — keep it short and useful
- The full detailed `<task_name>_result.md` is preserved in git history, no need to duplicate everything in the summary
- When finalizing, set `status: completed`, `phase: completed` and `updated` in the front-matter of `_info.md`
