---
name: branch-commit
description: Create a new branch from the current branch, carry over uncommitted changes, and commit them following project conventions. Use when the user runs /branch-commit or asks for a new branch with a commit of the current changes ("ветка и коммит", "создай ветку и коммит", "новая ветка с коммитом", "перенеси изменения в ветку", "branch commit").
---

# Branch Commit Skill

Create a new branch from the current branch, move uncommitted changes there, and commit them — all in one step.

This is useful when you've been working on `main` (or any branch) and realize the changes should live on their own branch before committing. The skill handles branch creation, change transfer, and commit message generation.

## Usage

```
/branch-commit                     — auto-generate branch name from changes
/branch-commit my-feature-name     — use the provided branch name
```

## Execution Steps

### 1. Gather context

Run these in parallel:
- `git status` — see what's changed
- `git branch --show-current` — know the current branch
- `git diff` and `git diff --cached` — understand staged and unstaged changes
- `git log --oneline -5` — recent commits for style reference

If there are no changes (no staged, unstaged, or untracked files), stop and tell the user there's nothing to commit.

### 2. Determine branch name

**If the user provided a name as an argument**, use it as-is.

**If not**, generate one from the changes:
- Look at the diff to understand what was changed
- Format: `<scope>/<short-description>`
  - Scope prefix: `fix/`, `feat/`, `refactor/`, `update/`
- The description part: 2-4 words, lowercase, separated by hyphens
- Examples: `feat/boss-lasso`, `fix/null-ref-in-room-bake`, `refactor/extract-player-service`, `update/session-search-timeout`

Present the chosen branch name to the user and proceed (don't ask for confirmation unless something is ambiguous — speed matters here).

### 3. Create the branch and commit

```bash
# Create and switch to new branch (carries uncommitted changes automatically)
git checkout -b <branch-name>
```

`git checkout -b` preserves all working tree and index state, so no stashing is needed.

### 4. Stage and commit

Message format: the `commit` skill (`.claude/skills/commit/SKILL.md` § Rules) - the title with the scopes, the tagged
bullets, no `Co-Authored-By` line for Claude.

Stage files selectively (avoid secrets like `.env`), then commit:

```bash
git add <specific-files>
git commit -m "$(cat <<'EOF'
[Scope] Brief description

- add: new feature X
- fix: issue Y
EOF
)"
```

### 5. Report result

Tell the user:
- The branch name that was created
- A summary of what was committed
- Remind that the branch is local only (not pushed)

## Important

- **Language:** All commit text in ENGLISH
- **Don't push** — the branch stays local, user decides when to push
- **Don't amend** — always create a new commit
- **Sensitive files** — never stage `.env`, credentials, or secrets
- **No --no-verify** — respect git hooks
