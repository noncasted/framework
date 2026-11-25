---
name: split-commit
description: Analyze current changes and split them into logical, focused commits with clean messages. Use when the user runs /split-commit or asks to commit changes in parts ("раздели коммит", "разбей на коммиты", "организуй коммиты", "split commit", "commit everything in parts", "too many changes for one commit").
---

# Split Commit Skill

When the user runs `/split-commit`, analyze all current changes and split them into logical groups, committing each group separately. The goal is to produce a clean, readable git history instead of one massive commit with unrelated changes mixed together.

Use this skill whenever the user has a large set of uncommitted changes and wants to organize them into multiple focused commits. Also use when the user says things like "commit everything in parts", "split this into commits", "too many changes for one commit", or "organize my changes".

## Commit Message Rules

Message format: the `commit` skill (`.claude/skills/commit/SKILL.md` § Rules) - the title with the ticket ID or the scopes,
the tagged bullets, the `BREAKING:` line. Every commit of the split follows it.

Add no `Co-Authored-By` line for Claude and no `Generated with` line: the rule of the user, it wins over the attribution reminder of the harness.

### Language
- ALL text must be in ENGLISH (titles, scopes, descriptions, tags)

## Execution Steps

### Phase 1: Gather Context

1. Run `git status` to see all changed/untracked files
2. Run `git diff` and `git diff --cached` to understand what changed
3. Run `git log -40 --oneline` to see recent commits and understand naming style
4. Get current branch name with `git rev-parse --abbrev-ref HEAD` (for ticket ID extraction)

### Phase 2: Plan the Split

Analyze all changes and group them by logical purpose. The key principle: each commit should represent ONE coherent idea that makes sense on its own.

Good grouping strategies:
- **By feature/scope**: all files related to one feature go together (e.g., "add shop consumables" = shared payload + backend handler + client component + config + artwork)
- **By layer when independent**: if changes in different layers are unrelated, split them (e.g., backend infra refactor separate from client UI fix)
- **Tests with their code**: test files go in the same commit as the code they test
- **Config/docs separate**: pure config changes or documentation updates can be their own commit
- **Renames/moves separate**: bulk renames or file moves are cleaner as their own commit

Bad grouping (avoid):
- One commit per file (too granular, loses context)
- Grouping by file type regardless of purpose (all .cs together, all .razor together)
- Mixing unrelated features just because they touch the same directory

Print the plan as a numbered list and go straight to Phase 3 — do not wait for a confirmation:
```
Split:
1. [Client] Add shop consumables (5 files)
   - client/Assets/GamePlay/Level/Shop/ShopConsumables.cs
   - client/Assets/GamePlay/Level/Shop/ShopAssortment.cs
   - ...
2. [Infra] Refactor task scheduler (4 files)
   - ...
3. [Tests] Add task balancer tests (3 files)
   - ...
```

### Phase 3: Execute Commits

For each group, in order:

1. Stage ONLY the files in that group: `git add <file1> <file2> ...`
   - NEVER use `git add .` or `git add -A` — stage specific files only
   - For renamed/moved files, stage both old and new paths
2. Verify staging is correct: `git diff --cached --stat`
3. Commit with the formatted message
4. Report: commit hash + summary

After all commits, show a final summary:
```
Done! Created N commits:
  abc1234 [Client] Add shop consumables
  def5678 [Infra] Refactor task scheduler
  ghi9012 [Tests] Add task balancer tests
```

## Edge Cases

- **Partial file changes**: if one file contains changes for two different groups, put it into the group it fits best and mention it in the final summary (don't try to split hunks with `git add -p` — keep it simple)
- **Dependencies between groups**: commit the dependency first (e.g., shared model before backend code that uses it)
- **Very few changes**: if changes naturally form just 1 group, make that one commit
- **Binary files** (images, assets): group with the feature they belong to

## Important Notes

- NEVER ask the user to confirm the split plan: plan, then commit right away
- Use `git add` with explicit file paths, never `-A` or `.`
- Commits are created locally; push to remote is handled separately
- If the user disagrees with the grouping afterwards, they can ask to redo it (the commits are local)
