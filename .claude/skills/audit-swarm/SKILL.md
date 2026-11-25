---
name: audit-swarm
description: Code audit by a swarm of cheap subagents in oh-my-pi (omp) - the session splits the area into packages, audits each with a parallel audit-scout, verifies every finding with its own adversarial audit-verifier, and writes only verified findings to docs/audits. Use in omp when the user runs /audit-swarm or asks for a code audit with subagents, a parallel audit, or an audit to hand over to a stronger model. In Claude Code use code-audit.
---

# Audit swarm

The session is the orchestrator: it plans, spawns, collects and writes the report. It does not read the code of the area itself
(its context stays small, the agents read); it reads only the answers of the agents and, when two of them disagree, the lines in
dispute. Read-only for code; no commits.

Agents (`agents/` of this skill, linked into `~/.omp/agent/agents` by `install.sh`): `audit-scout` audits one package,
`audit-verifier` tries to refute one finding. When the `task` tool does not list them, run `install.sh` of this skill once and restart
omp. Tool: `task` (several tasks in one call run in parallel; `wait` for the results). Limits: the global config of omp, set by
`install.sh` (`task.maxRuntimeMs`, `task.maxConcurrency`).

## Arguments

- `/audit-swarm <path>...` - these folders
- `/audit-swarm changed` - the folders of the `.cs` files changed against `main`
- no argument: ask which area.

Paths of the docs: `.claude/docs` in the framework, `tools/framework/.claude/docs` in a game (plus `.claude/docs` of the game).
The candidates script: `.claude/skills/code-audit/audit.sh`.

Resume: when `docs/audits/<date>_<area>.work/` already has `plan.md` and scout files, go on from the first step whose files are
missing (scouts of the packages without `scout-<PREFIX>.md`, then verifiers of the findings without `verify-<ID>.md`).

## Step 1 - Packages

1. Count the `.cs` files per folder (`find <area> -name '*.cs' -not -path '*/Generated/*' -not -name '*.g.cs'`, grouped by the
   folder two or three levels down).
2. Cut the area into packages of 15-40 files along features (a module, an assembly, `core/Runtime/Scopes`, the container
   generator). Tests go with the package they test. A folder above 40 files is cut by its subfolders. A cheap model reads about one file a minute: 40 files fit
   the runtime limit of an agent (`task.maxRuntimeMs`, 40 min), a bigger package times out.
3. Give each package an ID prefix (`CORE`, `GEN`, `NET`, `TEL`, `UI`, ...) and a focus: the 2-4 things it is most likely to break and
   the docs for them, from the keyword table of `INDEX.md` (lifetimes -> `COMMON_LIFETIMES.md`, container -> `COMMON_CONTAINER.md`,
   WebGL -> `WEBGL_TROUBLESHOOTING.md`, ...).
4. Run `audit.sh <folders of the package>` per package and keep its output for the brief.
5. Write the plan into `docs/audits/<YYYY-MM-DD>_<area>.work/plan.md`: packages, files, prefix, focus. Show it in a few lines and go
   on without waiting.

## Step 2 - Scouts

Spawn one `audit-scout` per package, all in one `task` call (the concurrency limit queues the rest). Brief of each:

```
Package: <folders>  ID prefix: <PREFIX>
Rules: read whole <docs>/ARCHITECTURE_PRINCIPLES.md, <docs>/CODE_STYLE_FULL.md, and for the focus <docs>/<...>.md
Focus: <2-4 lines>
Candidates of audit.sh:
<its output for the package>
```

Save each answer as `docs/audits/<date>_<area>.work/scout-<PREFIX>.md`. A scout that timed out or returned nothing in the format:
spawn it once more with its package cut in two; a second failure goes to "Не проверено".

## Step 3 - Verifiers

1. Collect the findings of all scouts. Merge duplicates (same place, same claim) under the first ID. Drop nothing else yourself.
2. Spawn one `audit-verifier` per finding, in one `task` call. Brief: the finding block as the scout wrote it, but WITHOUT its
   `Could be wrong if` line (the verifier finds its own doubts), plus `Run: <id>` for the scratch folder
   `.build/audit-swarm/<date>_<area>/<ID>/`. Mass style findings and pure structure findings (a file in the wrong folder, several
   types in a file) are checked by the verifier too, but cheaply: it only confirms the count and the examples.
3. Save the answers as `docs/audits/<date>_<area>.work/verify-<ID>.md`.
4. A FALSE verdict on a finding the scout marked `Evidence: test`, or a verdict without lines: spawn a second verifier for it. Two
   verifiers that disagree: read the lines in dispute yourself and decide; write the reason.

## Step 4 - Report

`docs/audits/<YYYY-MM-DD>_<area>.md`, Russian, identifiers in English, the format of the `code-audit` skill (Итог, Критичные,
Важные, Мелкие, Не проверено) with these changes:

- only CONFIRMED findings go into the severity sections, with the severity of the verifier;
- each finding gets the line `- Проверка: <run|reading>, <one sentence of the verifier>`;
- a section `## Не доказано` - the UNPROVEN ones with what would decide them;
- a section `## Отброшено` - the FALSE ones, one line each: the claim and the refuting fact (it calibrates the scouts);
- in `Итог`: packages, scouts, verifiers, how many findings were confirmed / false / unproven.

Keep the `.work/` folder: the stronger model that reviews the report reads it for the evidence. In the reply: the path of the report,
the counts and the three first problems.
