---
name: code-audit
description: Audit code for problems of architecture, code style, structure and real bugs against the rules of the framework docs, and write verified findings to a report in docs/audits. Read-only for code. Use when the user runs /code-audit or asks to find problems in the code, architecture or structure of a folder, the current changes or the whole project. For the .claude configuration use /audit.
---

# Code Audit

Finds problems in code and writes a report. It never changes code and never commits: fixes are a separate task.

## Arguments

- `/code-audit <path>...` - these folders or files
- `/code-audit changed` - the `.cs` files changed against `main`, committed or not
- `/code-audit all` - `client/Assets`, `backend`, `shared`, and `tools/framework` when the user names it

No argument: ask which area to audit.

## Rules

Read before auditing, whole:

- `tools/framework/.claude/docs/ARCHITECTURE_PRINCIPLES.md` (P1-P14)
- `tools/framework/.claude/docs/CODE_STYLE_FULL.md`

The docs are the only source of rules: no finding from a personal taste. A local exception written in a doc of the game
(`.claude/docs`) is not a finding. For the touched layers also read the sections of `ARCHITECTURE_PATTERNS.md` of the framework
and of the game, and the docs they point at.

## Step 1 - Candidates

```
.claude/skills/code-audit/audit.sh <folder>...
```

It prints candidates, not verdicts, for four kinds of checks: architecture (P1-P14, dependency direction, framework naming a game),
style, structure and garbage, bug signs. The folders of the dependency check and the namespaces of the game are in
`.claude/code-audit.json` of the project.

## Step 2 - Reading

The script does not see meaning, so read the code of the area for what it misses:

- **Architecture:** classes that hold several responsibilities, features that know each other, logic in the wrong layer, duplicated logic.
- **Structure:** a folder that mixes features, leftovers after a move, a type that lives away from its feature.
- **Bugs:** subscriptions on a lifetime longer than their owner, a subscription on an entity kept after its end, lost exceptions, races and
  work off the main thread, a value read before its setup, one-sided changes of the network protocol, a network request without an answer.

With `all`, or an area above about 200 files, give each top feature folder to its own `Agent`, one read-only agent per folder and
at most 8 in parallel; pass them the rules above and the report format below. Keep the verification of Step 3 for yourself.

## Step 3 - Verification

Every finding is confirmed by a look at the lines, then:

- A candidate that does not break a rule is dropped, not reported "just in case".
- A bug states a concrete scenario: what input or state, what goes wrong.
- A mass style violation is one finding: the rule, the count, up to 10 examples.
- A dead code candidate is checked for callers by a grep over `.cs`, and for its `.meta` guid in assets.

## Step 4 - Report

Write `docs/audits/<YYYY-MM-DD>_<area>.md` in Russian, identifiers in English:

```markdown
# Аудит кода: <area> (<date>)

Область: <paths>, файлов: <n>. Проверено правил: архитектура, стиль, структура, баги.

## Итог
Критичных: N, важных: N, мелких: N. <the three problems to fix first>

## Критичные
### <short title>
- Где: `path/File.cs:123`
- Правило: `P9` / `CODE_STYLE_FULL § Errors` / баг
- Проблема: <what and the scenario>
- Как исправить: <a concrete step>

## Важные
## Мелкие

## Не проверено
<what was skipped and why>
```

Severity: **критичная** - a real bug or data loss; **важная** - a broken principle that makes the code harder to change; **мелкая** - style
without a risk.

In the reply: the path of the report, the counts by severity and the three first problems. Do not retell the report.
