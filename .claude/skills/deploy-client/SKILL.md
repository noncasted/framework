---
name: deploy-client
description: Test, build and upload the Unity WebGL client of the game in one background run of tools/framework/tools/scripts/deploy-client.sh - the local tests (tools/scripts/run-tests.sh) and one batchmode Unity build per target (.claude/deploy-client.json) run in parallel, each in its own reflink copy of the working tree, with submodule stripping and checks; the uploads to the Coolify host (tools/scripts/deploy-webgl.sh) follow only when everything passed. The open Unity editor is not used. Use when the user runs /deploy-client or asks to build and deploy the web client or the Telegram build, ship the WebGL build, or update the client on Coolify.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /deploy-client, deploy-client, задеплой клиент, залей клиент, залей веб билд, залей телеграм билд, залей фронт, деплой клиента, деплой телеграма, deploy client, deploy webgl, deploy telegram, ship the web build
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Deploy Client

One script does the whole run: `tools/framework/tools/scripts/deploy-client.sh`, from the root of the game. It takes a
reflink snapshot of the working tree (uncommitted edits, `tools/framework` and `client/Library` included), runs the
tests of the game in it and, at the same time, one batchmode Unity per target, each in its own reflink copy of the
snapshot; checks every build; uploads target after target only when the tests and every check passed; deletes the copies.
The open editor of the user is not touched - no MCP, no profile switch, no handover to other sessions. The scheme of the
run, the json, the environment and the exit codes are in the comment at the top of the script.

Why copies: Unity's incremental build (Bee) deletes the output of the previous build when the next one writes to another
folder (2026-10-17: building telegram after web removed `client/Build/Build/*`, the web upload shipped an `index.html`
without its wasm). Each target now builds in its own copy and uploads from it, so no build sees the output of another.
A passed build leaves its `client/Library` (without `Bee` and the stripping backups) in
`/var/tmp/<game>-deploy-client.<key of the tree>.<target>`; the next build of that target from the same tree starts from it
when it is newer than the Library of the tree, so a tree without its own editor (a worktree) does not import again in every
build what came since its Library was last updated.

The game describes itself in `.claude/deploy-client.json`: the tests and deploy scripts, the targets (name, build
profile, output, arguments of the deploy script, what `index.html` must or must not hold, files the output must hold),
the pairs of template files that must be byte-equal. A target that is not in the json does not exist: stop and say so.
The urls, credentials, the backend baked into the build and the notes for people are in `.claude/project-skills.md`
§ deploy-client. Without the json the script exits 2: stop and say the game has to add it.

Invoking this skill is the user's go-ahead for the uploads - do not ask again. Never upload a build that was not produced
by this run, and never one from a tree whose tests fail: the script enforces both (failed tests kill the builds of the
run, nothing is uploaded).

## Arguments

DEPLOY below is the `deploy` script of the json, ARGS(t) the `deployArgs` of target t (pixel-swarm:
`tools/scripts/deploy-webgl.sh` and `--target web`).

| Argument | What runs |
|----------|-----------|
| none | `deploy-client.sh`: tests + every target of the json, in its order |
| `<target>` ... | `deploy-client.sh <target> ...`: tests + those targets |
| `--notest` / `skip-tests` (with any of the above) | `deploy-client.sh [targets] --notest`: no tests |
| `--no-deploy` (with any of the above) | `deploy-client.sh [targets] --no-deploy`: tests, builds and checks, no upload (a dry run, or a measure of the times) |
| `--list [t]` | only `DEPLOY ARGS(t) --list` (every target when none is given), then report |
| `--rollback <t> <tag>` | only `DEPLOY ARGS(t) --rollback <tag>`, then report. The target is required: tags of the targets look alike |

`--notest` only when the user typed it (or `skip-tests`) in this request - never on your own, not even to save time or
after a failing test run. `--list` and `--rollback` never run the tests or a build.

## Step 1 - Preflight

Read `.claude/deploy-client.json` and run, in parallel, `DEPLOY ARGS(t) --list` for every target of this run.

- `--list` proves the SSH credentials work before minutes of tests and builds. If it fails, stop and report (the
  credentials are in `.claude/project-skills.md`). `(no releases yet)` is fine: the target was never deployed.
- Remember the live tag of each target: the report gives it as the rollback target.

## Step 2 - Run

From the root of the game, in the background (`run_in_background`), then wait for the notification - do not poll, do not
sleep:

```bash
tools/framework/tools/scripts/deploy-client.sh [targets] [--notest]
```

A full run takes several minutes (the longest build plus the uploads). Environment, only when needed:

- `FRAMEWORK_SOURCE=<dir>` - `tools/framework` of the snapshot from that folder: deploy a change of the framework before
  the pointer of the submodule moves (only when the user or the task says so).
- `KEEP_COPY=1` - keep the copies (path printed at the end) to look into a failed build; delete them by hand afterwards,
  each is a full tree.
- `UNITY_BUILD_SLOTS`, `UNITY_BUILD_MIN_FREE_GB`, `UNITY_BUILD_MEM_WAIT`, `UNITY_BUILD_TIMEOUT` - see the script; leave
  the defaults unless the user asks.

Never pass `FORCE=1` and never work around a refusal of the deploy script: it refuses a build whose `build-guid.txt` is
already live (`FORCE=1` only when the user asked for a redeploy of the same build), a web build on the telegram target and
the other way round, an `index.html` that references a missing `Build/*` payload. Report the refusal.

## Step 3 - Outcome

The script ends with `==== deploy-client summary ====`: a table stage / result / seconds / details (snapshot, copies,
tests, `build <t>`, `checks <t>`, `deploy <t>`), the total time and memory, `Logs: <folder>` and `DEPLOY-CLIENT PASSED`
or `DEPLOY-CLIENT FAILED`. Logs (default `/tmp/<game>-deploy-client/<time>/`): `snapshot.log`, `tests.log` and `tests/`,
`build-<t>.log` (Unity) and `build-<t>.out`, `deploy-<t>.log`, `deploy-<t>-list.log`.

- Exit 0 - everything passed and every target was uploaded, `--list` names the new tag as current.
- Exit 1 - a stage failed, the summary says which. Failed tests: the script prints the failed tests and the summary of the
  test run, nothing was built to the end or uploaded. A failed build or check: nothing was uploaded. A failed upload:
  the targets before it stay deployed, the rest were not uploaded - report which.
- Exit 2 - bad arguments or `.claude/deploy-client.json` (an unknown target, a missing field): fix the call or report the
  json, do not edit the script.

Do not fix the tests, do not rerun with `--notest`, do not retry a build on your own - the user decides. The `wasm-ld`
heap-corruption abort in LTO is random (memory `webgl-wasm-ld-flaky`): say so in the report and let the user decide on a
rerun. `low memory`: the tests refuse with less than 8 GB available, a build fails after `UNITY_BUILD_MEM_WAIT` seconds
below `UNITY_BUILD_MIN_FREE_GB` - report it, the user may close something and rerun. `Waiting for a free build slot` /
`Waiting for memory` in `build-<t>.out` is normal: other builds on the machine hold the slots; the run goes on by itself.

## Step 4 - Report

Tell the user, in Russian, from the summary:

- the tests: passed with the totals per suite (the test summary the script printed), or "skipped by `--notest`", or the
  failed tests and the log folder
- per target: the tag and the size uploaded, the url (`URL: ...` from `deploy-<t>.log` when `PUBLIC_URL` is set in
  `deploy.env`; otherwise the url of `.claude/project-skills.md`; a Telegram Mini App link is the one BotFather has, with the
  trailing slash), and the rollback command `DEPLOY ARGS(t) --rollback <tag>` with the live tag of step 1 (none for a
  first deploy)
- once: the stripping of the `checks <t>` rows (backup folder, number of submodules, bytes; the names are the
  `SubmodulesToStrip` of the stripping settings of the game), whether `deploy-<t>.log` has `nginx.conf changed` (then the
  web service needs a restart in Coolify; until then a path served by it may still show the old config), the total time
  and the log folder

The working tree is not changed by the run (the builds reserialize assets in their copies only): nothing to list or revert.
