---
name: pr
description: Create a GitHub pull request for the current branch with a formatted English title and body. Use when the user runs /pr or asks to create or open a pull request ("пулл реквест", "создай PR", "открой PR", "запрос на слияние", "pull request", "github pr").
---

# PR Skill

When the user runs `/pr`, create a GitHub pull request for the current branch.

## Rules

### 1. PR Title Format
- The title format of the `commit` skill (`.claude/skills/commit/SKILL.md` § Title Format): the ticket ID of the branch name,
  otherwise the scopes of the work
- Title must be SHORT (under 70 characters) and descriptive
- ALL text in ENGLISH

### 2. PR Body Format
```
## Overview

[2-3 sentences describing the feature/fix and its purpose]

## Changes

### Feature/Area Name (add/fix/refactor)
- Brief bullet describing what was added or changed
- Another bullet

### Another Area (refactor)
- Brief bullet

## Test plan
- <concrete step>
```

## Execution Steps

1. Get branch info:
   - `git rev-parse --abbrev-ref HEAD` → current branch name
   - `git log main..HEAD --oneline` → all commits in this branch
   - `git diff main...HEAD --name-only` → all changed files

2. Analyze the scope of work:
   - Extract ticket ID from branch name (pattern: `[A-Z]+-[0-9]+`)
   - Group changed files by area (the scopes of the title)
   - Read 2-4 KEY source files (not prefabs/meta/scenes) to understand what was implemented
   - Focus on: new .cs files, modified core files — skip .meta, .prefab, .unity, .asset

3. Generate PR title:
   - Use ticket ID if found, else derive scope from the work done
   - Summarize the main feature in a few words

4. Generate PR body:
   - Overview: what problem is solved / what feature is added
   - Changes: group by logical area, use same add:/fix:/refactor: convention as commits
   - Test Plan: specific, actionable steps to verify the changes

5. Push branch if needed:
   - Check if remote tracking branch exists: `git status -sb`
   - If not pushed: `git push -u origin HEAD`

6. Create PR:
   ```
   gh pr create --title "..." --body "..." --base main
   ```

7. Report the PR URL to the user

## Key Principles

- Read ACTUAL source files to understand what was built, not just file names
- PR body should explain the WHY and WHAT, not just list file names
- Test plan must be concrete — not "test the feature" but specific steps
- If branch has many commits, synthesize them into coherent summary (not just list commits)
- Base branch is always `main` unless user says otherwise

## Example

Illustrative, from the framework; the scopes and the content of another project differ.

**Branch:** `client-logger`

**Title:** `[Core] Client logger ILog<T> with levels in OptionsContainer`

**Body:**
```
## Overview

Runtime code of the client logs through ILog<T> instead of Debug.Log: a template is formatted only at an enabled level,
and the levels come from the options of the game.

## Changes

### Logger (add)
- add: ILog<T>, bound by the container generator to Logs.Of<T>(), templates with 0-4 generic arguments
- add: LogOptions in OptionsContainer with development and release levels and rules by prefix

### Runtime of the framework (refactor)
- refactor: modules and platforms log through ILog<T>
- remove: DebugOptions.EnableLogs

## Test plan
- tools/scripts/test-all.sh: EditMode and PlayMode pass
- Play in the editor at the level Warning: no Info lines in the console
```
