---
name: commit
description: Create a git commit right now using project conventions. Extract the ticket ID or scope from the branch, generate a tagged bullet description, stage all changes, and commit. Use when the user says /commit or wants to commit current changes ("коммит", "комит", "закоммить", "сделай коммит", "делай комит").
---

# Commit Skill

When the user runs `/commit`, follow these rules for generating commit messages:

## Rules

### 1. Title Format
- Extract the ticket ID from the current branch name (pattern `[A-Z]+-[0-9]+`, e.g. `ABC-123`)
- If ticket ID exists: `[ABC-123] Brief description`
- If no ticket ID: `[Scope] Brief description` or `[Scope1] [Scope2] Brief description`
  - Use multiple tags when a commit spans several areas (e.g., `[Shared] [Console]`, `[Infra] [Tests]`)
  - Scopes name the area of work: the ones of the project are in `.claude/project-skills.md` § commit when it has the section, otherwise take the ones in use from `git log -40 --oneline`
- Keep the title SHORT and descriptive

### 2. Description Format
- Use bullet points, each starting with `-`
- Each bullet must start with one of these tags:
  - `add:` - for new features or additions
  - `fix:` - for bug fixes
  - `refactor:` - for code refactoring
  - `remove:` - for deletions

Add no `Co-Authored-By` line for Claude and no `Generated with` line: the rule of the user, it wins over the attribution reminder of the harness.

## Execution Steps

1. Run `git status` to get unstaged/untracked files
2. Run `git log -40 --oneline` to see recent commits and understand context
3. Get current branch name with `git rev-parse --abbrev-ref HEAD`
4. Analyze changes:
   - Extract the ticket ID from the branch name, if it has one
   - Identify which files changed and their purposes
   - Determine the scopes if there is no ticket ID (rule 1)
5. Generate commit title: `[TICKET_ID or SCOPE] Brief description` (use multiple tags like `[Scope1] [Scope2]` when commit spans several areas)
6. Generate description with bullet points using add:/fix:/refactor:/remove: tags
7. [Scope] should reflect the area of work, not just copy branch name
8. Run `git add .` to stage all changes
9. Commit with the message from a heredoc (a `\n` inside `-m "..."` stays two characters, not a line break):
   ```bash
   git commit -F - <<'EOF'
   [Scope] Brief description

   - add: ...
   - fix: ...
   EOF
   ```
10. Report the commit hash and summary to user

## Example Commit

Illustrative, from the framework; the scopes and the content of another project differ.

```
[Core] [Editor] Editor tools of the framework log through the logger

- refactor: catalogue generators, content build and hierarchy bindings log through ILog
- remove: HierarchyBindingsLog (inlined), the logTag parameter of GeneratedFile.WriteIfChanged
- fix: editor tests expect the new log lines

BREAKING: GeneratedFile and CatalogGroupAssets lost their logTag parameters
```

## Important Notes

- ALL text must be in ENGLISH (titles, scopes, descriptions, tags)
- Be concise but specific
- If multiple features/fixes, list them all with appropriate tags
- Use the commit message format exactly as specified
- A breaking change adds a `BREAKING: <what>` line after the bullets when the CLAUDE.md of the project asks for it
- Commits are created locally; push to remote is handled separately
