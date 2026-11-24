---
name: plan-architecture
description: Create, review or extend the plan of a development or refactoring task (`<task>_info.md`) against the project architecture principles (ARCHITECTURE_PRINCIPLES.md P1-P14) - audits the touched code (big classes, clutter, dead code, polling, views from code, wrong dependency direction), adds the missing steps, risks and questions to the user, writes no code. Use when the user runs /plan-architecture [task text | plan path | docs/tasks/current/<task>/], or says "проверь план", "спланируй по архитектуре", "архитектурный план", "architecture plan", "review the plan".
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /plan-architecture, проверь план, спланируй по архитектуре, архитектурный план, сверь план с принципами, architecture plan, review the plan, plan by architecture
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Plan Architecture Skill

`/plan-architecture [task text | path]` - brings the plan of a task in line with the project architecture. Output: the
edited `<task>_info.md` and a short report. **No code, no implementation, no Unity, no git.**

Sources, by authority:
1. `tools/framework/.claude/docs/ARCHITECTURE_PRINCIPLES.md` - P1-P14, the hard rules (P14 stands before P13 in the file: cite by
   number). Read it whole, always.
2. `tools/framework/.claude/docs/ARCHITECTURE_PATTERNS.md` (shared by the games) and `.claude/docs/ARCHITECTURE_PATTERNS.md`
   (this game), one numbering over both - "if X -> the plan has Y" with doc links. Read only the sections of the
   touched layers (table in Step 2) in both files, never a whole file.
3. `tools/framework/.claude/docs/GAMEPLAY_ARCHITECTURE.md` - the layout of a gameplay assembly, entities, states; read
   it whole when the task touches gameplay.
4. `.claude/docs/ARCHITECTURE_DECISIONS.md` - session decisions. `[пользователь]` = rule; `[агент]` = NOT a
   rule: a hint marked "(неподтверждено, ARCHITECTURE_DECISIONS §N)" or a question.
5. `docs/tasks/complete/REFACTOR_OVERVIEW.md` § «Общая идея» - the model of what the user calls a problem in code.

A source of the game (`.claude/docs/...`, `docs/tasks/complete/...`) the project lacks is skipped, not a stop.

The plan rests on the principles. A pattern enters the plan only when the task hits its "if"; one line and a link,
never copied doc text.

## Step 1 — Input and workspace

- Path to `docs/tasks/current/<task>/` or one of its files: read `_info.md` (and `_result.md` / `_progress.md` if
  present). Any other `.md`: it is the task text or the plan itself (edit that file).
- Plain text and no workspace: create it as `workflow` Phase 1 steps 1-8 (`.claude/skills/workflow/SKILL.md`); skip
  its Step 9, do not start implementation.
- Done steps (`[x]`) are history: never rewrite them; a gap in them becomes a risk or a new follow-up step.

## Step 2 — Layers and touched code

Mark the touched layers and find the code **pointwise**: `grep -rn` for the named classes / folders in `client/Assets`,
`shared`, `backend`; read only hit ranges (`sed -n a,bp`, `grep -A`). Never read whole files.

| Layer | Signs | PATTERNS | DECISIONS |
|-------|-------|----------|-----------|
| Layout | a new feature, folder or assembly of gameplay | 1.0 C-LAY | — |
| Data and assets | EnvAsset, options, catalogue, writer | 1.10 C-DATA, 1.11 C-GEN | §2 |
| Effects and services | upgrade, effect, companion, `OnSetup` | 1.1 C-DI, 1.5 C-LT, 1.6 C-RX | §1, §3 |
| Entities | `ILocalX` / `IRemoteX`, entity prefab, state machine, brain | 1.3 C-EN, 1.4 C-SM | — |
| View | prefab, bindings, pool, renderer, light, vfx | 1.2 C-SC, 1.7 C-VW | §4 |
| UI | screen, HUD, panel, text, icon | 1.8 C-UI | §5 |
| ECS | many per-frame entities, ECS world, system | 1.9 C-ECS | §6 |
| Network | `NetworkProperty`, union, id, snapshot, command | 2 N, 4 S | §7 |
| Backend | grain, `State<T>`, messaging, console | 3 B | — |
| Always | verification, editor, process | 5 T, 6 E, 7 M (by keyword) | §8, §9 |

Read a section: `grep -n '^##' <file>`, then `sed -n` that range only.

## Step 3 — Audit of the touched area (mandatory, before checking the plan)

The area = the folders of the touched files plus their feature folder, not only the named classes; for a new feature
also the folder of its closest existing analogue (a new attack -> the existing attacks). No analogue in the game (its
first player, enemy, run): the analogue in the reference (`.claude/project-skills.md` § architecture, else pixel-swarm
`client/Assets/GamePlay`); list its folders and compare the layout the plan gives the feature with it (C-LAY-1).

```
.claude/skills/code-audit/audit.sh <folder>...                  # current tree
AUDIT_ROOT=<dir> .claude/skills/code-audit/audit.sh <folder>...  # an old tree: git archive <rev> | tar -x -C <dir>
```

It prints candidates: files > 300 lines (P11), extra top-level types in a file (P11; `IX` + `X` is allowed), folder
contents / orphan `.meta` / temp names (P12), types named nowhere else (P13), `Has(` / `IsActive(` inside update
(P2), `switch` by kind and `IsLocal` / `IsHost` branches (P1, P9), `new GameObject` / `AddComponent` (P3), index /
name fields in options and writers (P4, P5), public mutable fields (P6), local / remote pairs with the same members
(P13), dependency direction (P14: `sibling` - a feature names a sibling feature's class, `upward` - a utility (folders in
`.claude/code-audit.json` of the project) names a game feature, `composer` - a feature names its own composer).

Confirm each candidate with one grep / a look at the lines (a facade or a composer may know its features; a
`Definitions` / data type is not a feature). Also look by eye at the folder lists for another feature's files and
leftovers of moves. Draw the hierarchy of the area: **root / composer -> feature classes -> shared utilities**.

## Step 4 — Check the plan against P1-P14

Per principle: applies / n/a; for each applicable one, the step that meets it, else a gap. Typical gaps:

- **P10** refactoring: step `[0]` "эталон «до»" (data dump, deterministic harness with md5, frames rendered without
  stealing focus) and tests on the **old** code before any edit; later steps check "after == before". A feature:
  acceptance tests of the new behaviour. Always a list "Изменения поведения" ("нет" is an explicit answer).
- **P5** new asset / kind in the network: explicit `_id` (next free), uniqueness throw at load + a test.
- **P3** new runtime objects: prefab + bindings or `PrefabPool<T>`, look in the prefab, a grep in the acceptance.
- **P1 / P14** several things: a class per thing, a composer on top; features talk through the composer's interfaces /
  events; repeated logic -> a utility below that knows no feature. A feature that adds one more `case` to existing
  `switch`-es by kind (count them) -> a question: extend as is or cut into classes first (a separate refactoring).
- **P2** subscription in `OnSetup` on a named lifetime, no per-frame checks. **P6** intent methods, private set.
- **P1 / P12 gameplay** a thing with its own life and view is an entity, its modes are states, its feature is one
  folder; a plan with a `Model` / `Views` split or an `enum State` driven by one `Tick` is rewritten
  (`GAMEPLAY_ARCHITECTURE.md`, C-LAY-2, C-EN-1, C-SM-1).
- **P9** separate roots per variant. **P7** many per-frame entities -> the ECS world of the game. **P8** no fallbacks.
- **P11-P13** the confirmed audit findings: a refactoring cuts / moves / deletes them all in its area; a feature fixes
  only what it touches (a class it extends past 300 lines, a folder it adds to) - the rest goes to "Аудит" as
  "вне задачи".
- **Process** (DECISIONS §9, PATTERNS E-1): steps of 2-4 items with own acceptance; one editor owner for compile,
  generators, migrations, tests, Play; a step touching another active task's files goes after it.

Conflicts: PATTERNS §8 (K-*), DECISIONS «Конфликты» (К*), two principles pulling apart, or only an `[агент]` source ->
a question to the user, not a silent choice.

## Step 5 — Write into the plan

Edit `<task>_info.md` in place, minimal diff, `workflow` format:

- **Order of steps - top-down (P14):** `[0]` reference / tests -> data -> the root and its wiring -> the feature
  classes -> the utilities extracted from their repeats -> cleanup (dead code, folders) -> verification -> docs. A
  utility whose repeat already exists in the code (ten copies of a pool) may come first - say so in the сверка. Do not renumber
  done steps; new ones get free numbers and the dependencies say the order.
- New steps: a full `workflow` block (`Статус: [ ] pending`, Цель / Как / Проверка / Файлы / Зависит от /
  Блокирует); fix `total_steps`. Fixed steps: edit only the "Как" / "Проверка" line.
- Each addition ends with its basis: `(P3)`, `(P10; ARCHITECTURE_PATTERNS T-7)`, `(HIERARCHY_BINDINGS.md § ...)`.
- `### Аудит` - table `| Файл | Строк | Проблема | Принцип | Шаг / вне задачи |`, confirmed findings only, then the
  hierarchy root -> features -> utilities in 3-6 lines (as is, and as planned if it changes).
- `### Изменения поведения`, `### Риски` (append, no duplicates), `### Вопросы пользователю` (numbered, options and
  source, `блокирует [N]`), `### Архитектурная сверка` (one line per principle: `P3 - шаг [4]`, `P5 - n/a`).

Budget: follow the size of the task - a feature usually 3-8 edits and an audit of 3-8 rows; an existing plan grows by
at most ~25%. "Вне задачи" findings: one row each for the worst, the rest of the sizes in one row. A met principle gets
only its line in the сверка. No sections beyond these five.

## Step 6 — Report

Short, in Russian: the plan path; the audit findings (top ones); what was added / changed, by principle; the
questions, blocking ones first. Do not start implementation - the user decides.

## Rules

- Edit only the plan (and the new workspace of Step 1). Never `ARCHITECTURE_*.md`, docs, code, other tasks.
- Facts about code come from grep and the audit, not memory; a path in the plan is a real path.
- A recorded "Решение пользователя" of this task overrides a principle: note it in the сверка, do not reopen it.
