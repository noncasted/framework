---
name: deploy-client
description: Run all local tests (tools/scripts/run-tests.sh: backend, container generator, Unity EditMode), then build the Unity WebGL client - one build per target of the project (.claude/project-skills.md), with submodule stripping - and upload each to the Coolify host via tools/scripts/deploy-webgl.sh. Use when the user runs /deploy-client or asks to build and deploy the web client or the Telegram build, ship the WebGL build, or update the client on Coolify.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /deploy-client, deploy-client, задеплой клиент, залей клиент, залей веб билд, залей телеграм билд, залей фронт, деплой клиента, деплой телеграма, deploy client, deploy webgl, deploy telegram, ship the web build
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Deploy Client

Run all tests locally (step 1b), then build the WebGL client in the open Unity editor - one build
per target - let the Web Stripping Tool strip it, then upload the result to the host.

The project gives the targets in `.claude/project-skills.md` § deploy-client: per target the build profile (PROFILE), the output
folder (DIR, the Unity path OUTPUT), the url and what `index.html` must hold; also the credentials and the backend
url baked into the build. A target that is not in the table does not exist: stop and say so. `tools/scripts/deploy-webgl.sh`
and `tools/scripts/run-tests.sh` are scripts of the project. The checks below marked telegram apply to a Telegram
Mini App target only.

Invoking this skill is the user's go-ahead for the uploads - do not ask again before step 5.
Any failure in steps 1-4 (step 1b - the tests - included) stops the skill: never upload a build that
was not produced by this run, and never build one from a tree whose tests fail.
Each target is deployed (step 5) RIGHT AFTER its own step 4, before the next target is built: Unity's
incremental build (Bee) deletes the outputs of the previous build when the next one writes to another
folder. On 2026-10-17 building telegram after web removed `client/Build/Build/*`, the web upload that
followed shipped an `index.html` without its wasm and the site was down until a rollback. A failure of
a target stops the run; targets already deployed stay deployed - report which ones.

## Arguments

- none - every target of the project, in its order
- a target name (`web`, `telegram`, ...) - only that target
- `--list [web|telegram]` - only run `tools/scripts/deploy-webgl.sh --target <t> --list`
  (both targets when none is given) and report
- `--rollback <web|telegram> <tag>` - only run `tools/scripts/deploy-webgl.sh --target <t> --rollback <tag>`
  and report. The target is required: tags of the two targets look alike.
- `skip-tests` - combines with the build arguments above (`skip-tests`, `web skip-tests`, ...): skip
  step 1b. Only when the user typed it in this request - never on your own, not even to save time
  or after a failing test run. `--list` and `--rollback` never run the tests anyway.

| Argument | Targets | Tests (step 1b) | Build + deploy |
|----------|---------|-----------------|----------------|
| none | web, telegram | yes | yes |
| `web` / `telegram` | that one | yes | yes |
| `skip-tests` / `web skip-tests` / ... | as above | no | yes |
| `--list [t]` | t or both | no | no, list only |
| `--rollback <t> <tag>` | t | no | no, rollback only |

Below, TARGETS is the list of targets of this run; "for each target" means in that order.

## Step 1 - Preflight

Run in parallel:

```bash
git status --short
cat <DIR>/build-guid.txt                      # per target
tools/scripts/deploy-webgl.sh --target <t> --list   # per target
```

- Remember the current `build-guid.txt` value of each target in TARGETS (a missing file is fine:
  first build of that target) - step 4 compares against it.
- `--list` proves the SSH credentials work before spending minutes on a build. If it fails,
  stop and report (the credentials in `.claude/project-skills.md`).
  `(no releases yet)` for telegram is fine - the target was never deployed (one-time server
  setup: `client/deploy/README.md`, "Adding the Telegram target").
- Remember the live tag of each target - the report gives it as the rollback target.
- Remember the `git status` output - the build reserializes assets, step 6 reports the difference.
- The editor is shared between Claude sessions: call `ListAgents`, and if another session is
  running, hand over with `SendMessage` before starting the build - it blocks the editor for minutes
  (two targets: roughly twice as long).

## Step 1b - Tests

Skipped only with the `skip-tests` argument (see Arguments). Run from the repo root, in the
background (`run_in_background`; a full run takes several minutes), and wait for it to finish:

```bash
tools/scripts/run-tests.sh
```

The script runs the backend tests, the container generator Verify and the Unity EditMode tests in a
reflink snapshot of the working tree (Unity in batchmode there - the open editor is not touched; see
`.claude/docs/TESTING.md`, "Running everything locally"). It ends with a summary table (step, PASS/FAIL,
seconds, totals) and `ALL TESTS PASSED` or `TESTS FAILED`; the exit code is non-zero on any failure.

- Exit code 0 and `ALL TESTS PASSED` - go on to step 2.
- Anything else - STOP, do not build or deploy. Report in Russian: the summary table, the failed test
  names the script printed under each failing step, and the log folder (`Logs: ...`). Do not fix the
  tests, do not rerun with `skip-tests`, do not retry on your own - the user decides.
- The Unity step refuses to start with less than 8 GB of available memory (`low memory`) - report it
  as a failure too; the user may close something and rerun.

The shared editor is not needed for this step - the step 1 handover (`ListAgents` / `SendMessage`)
can wait until the tests have passed.

## Steps 2-4 - for each target

Run steps 2, 3, 4 and 5 completely for one target, then for the next. PROFILE and OUTPUT below are
that target's build profile and Unity output path from the table of the project.

### Step 2 - Prepare the editor

The stripping pass runs only when the Web Stripping Tool has active settings and
`StripAutomaticallyAfterBuild` enabled. Both live in editor state that gets reset (a profile
switch reloads the domain), so set them explicitly before EVERY build instead of trusting
`ProjectSettings/Packages/com.unity.web.stripping-tool/Settings.json`.

```
mcp__unityMCP__execute_code:
  action: execute
  code: |
    var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<Unity.Web.Stripping.Editor.SubmoduleStrippingSettings>("Assets/Settings/DefaultSubmoduleStrippingSettings.asset");
    if (settings == null) throw new System.Exception("Stripping settings asset not found");
    if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) throw new System.Exception("Editor is in Play mode");
    if (UnityEditor.EditorApplication.isCompiling) throw new System.Exception("Editor is compiling");
    if (UnityEditor.BuildPipeline.isBuildingPlayer) throw new System.Exception("A build is already running");
    Unity.Web.Stripping.Editor.StrippingProjectSettings.ActiveSettings = settings;
    Unity.Web.Stripping.Editor.StrippingProjectSettings.StripAutomaticallyAfterBuild = true;
    Internal.SharedAddressablesSync.Sync();
    return "READY strip=" + string.Join(",", settings.SubmodulesToStrip);
```

The result must start with `READY` and list at least one submodule - with an empty list the
tool silently skips stripping.

Then activate PROFILE, so the editor is on WebGL before the build starts.
Addressables builds its content inside the player build for `EditorUserBuildSettings.activeBuildTarget`
(the `[BuildTarget]` profile variable). If the editor is still on Linux, the catalog points at
`StandaloneLinux64/*.bundle`, the build ships the stale `aa/WebGL` bundles next to it, and the
deployed client fails with `Failed to decompress data for the AssetBundle` (nginx answers the
missing bundle with `index.html` and 200). This happened on 2026-10-15.

```
mcp__unityMCP__execute_code:
  action: execute
  code: |
    var path = "<PROFILE>";
    var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>(path);
    if (profile == null) throw new System.Exception("Build profile not found: " + path);
    UnityEditor.Build.Profile.BuildProfile.SetActiveBuildProfile(profile);
    return UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString();
```

The switch reimports and reloads the domain - wait until `mcpforunity://editor/state` reports
`data.advice.ready_for_tools`, then re-check with a one-line `execute_code` that
`activeBuildTarget` is `WebGL` and the active profile is PROFILE
(`UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile())`).
Anything else: stop and report. On an exception: stop and report it (leave Play mode only if the
user asked for that). When the second target follows the first, the editor is already on
WebGL - switch the profile anyway, the template and defines differ.

### Step 3 - Build

Delete the output folder first (`rm -rf <DIR>`, from the repo
root): Unity does not clean it, and the bundles and payloads of earlier builds would be uploaded next
to the new ones. Remember the `build-guid.txt` of step 1 before that.

```
mcp__unityMCP__manage_build:
  action: build
  target: webgl
  profile: <PROFILE>
  output_path: <OUTPUT>
```

The call returns a `job_id` immediately; the build itself blocks the editor main thread, so
status calls only answer once it is over. Poll until the state is no longer pending/building
(a full build takes several minutes):

```
mcp__unityMCP__manage_build:
  action: status
  job_id: <job_id>
```

If the job failed, or MCP stops answering:

```bash
pgrep -f "Editor/Unity|unityhub-unity-editor" >/dev/null && echo EDITOR_ALIVE || echo EDITOR_DEAD
grep -n -E "emcc: error|Tundra build failed|BuildFailedException|Submodule stripping failed|Caught fatal signal" client/Logs/Editor.log | tail -20
```

Report the failing lines and stop (also for the remaining target). Do not retry the build on
your own and do not relaunch the editor - a linker crash (`wasm-ld ... SIGSEGV`) has taken the
whole editor down before. The `wasm-ld` heap-corruption abort in LTO is random (memory
`webgl-wasm-ld-flaky`): say so in the report and let the user decide on a rerun.

### Step 4 - Verify the build

`<DIR>` is the output folder of the target (from the repo root).

```bash
cat <DIR>/build-guid.txt
ls -la --time-style=long-iso <DIR>/Build
ls -dt client/Library/com.unity.web.stripping-tool/*/ | head -3
grep -c telegram-web-app.js <DIR>/index.html
cmp client/Assets/WebGLTemplates/Default/TemplateData/web-shell.js client/Assets/WebGLTemplates/Telegram/TemplateData/web-shell.js
cmp client/Assets/WebGLTemplates/Default/TemplateData/style.css client/Assets/WebGLTemplates/Telegram/TemplateData/style.css
```

All of these must hold, otherwise stop:

- `build-guid.txt` differs from the value remembered in step 1
- `<DIR>/Build` holds exactly one fresh `*.wasm.br`, `*.data.br`, `*.framework.js.br` and `*.loader.js`
- the job status reported success with 0 errors
- Addressables were built for WebGL: `strings <DIR>/StreamingAssets/aa/catalog.bin | grep -c StandaloneLinux64`
  prints `0` (a non-zero count means the catalog points at bundles that are not in the build)
- the stripping pass ran: `client/Logs/Editor.log` has no `Submodule stripping failed` after this
  build started, and the stripping tool wrote a NEW backup folder for this build under
  `client/Library/com.unity.web.stripping-tool/`, named `<output folder>-<build guid>`
  (`website-<guid>` for web, `telegram-<guid>` for telegram; the guid is the new `build-guid.txt`)
- no leftovers of an earlier build: `find <DIR>/StreamingAssets/aa -name '*.bundle' -printf '%f\n' | sed -E 's/_[0-9a-f]{32}\.bundle$//' | sort | uniq -d` prints nothing
- the template matches the target: the `telegram-web-app.js` count is `0` for web and `1` or more
  for telegram (the deploy script refuses the wrong one too - catching it here saves the upload)
- both `cmp` print nothing: the Telegram template keeps byte-for-byte copies of the shared shell files
  of Default (Unity copies only the folder of the template in use). A difference means someone
  changed one template only - stop and report, copy the Default file over after the user agrees
- telegram only: `grep -c telegram-shell.js builds/telegram/index.html` is `1` or more and
  `builds/telegram/TemplateData/telegram-shell.js` exists

Then deploy this target (step 5) and only after that go to step 2 for the next target, if any.

### After the last target - back to Linux

Switch the editor back to Linux Standalone ONCE, after the last build of the run (also when a
build failed, as long as the editor is alive). The editor works on Linux; the build leaves it on
WebGL, and with WebGL active the Multiplayer Play Mode clone cannot load the URP shaders (black
render, ~0.5 GB/min log spam):

```
mcp__unityMCP__execute_code:
    UnityEditor.Build.Profile.BuildProfile.SetActiveBuildProfile(null);
    UnityEditor.EditorUserBuildSettings.SwitchActiveBuildTarget(UnityEditor.BuildTargetGroup.Standalone, UnityEditor.BuildTarget.StandaloneLinux64);
    return UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString();
```

The result must be `StandaloneLinux64`.

## Step 5 - Deploy

For the target that has just passed step 4 (one target per pass, see "Steps 2-4"):

```bash
BUILD_DIR=<DIR> tools/scripts/deploy-webgl.sh --target <t>
```

Run from the repo root. The script refuses to upload a build whose
`build-guid.txt` is already live on that target - that means step 3 did not produce a new build;
do not work around it with `FORCE=1` unless the user asked for a redeploy of the same build. It
also refuses a web build on the telegram target and the other way round (by `telegram-web-app.js`
in `index.html`) - never bypass that, rebuild with the right profile.

The script also refuses a build whose `index.html` references a `Build/*` payload that is not on disk
(the Bee cleanup above) - rebuild that target, never upload around it.

If a deploy fails, do not build or deploy the remaining target: report the state of both.

Then confirm, for each target:

```bash
tools/scripts/deploy-webgl.sh --target <t> --list
```

`current` must point at the tag the deploy printed in `Deployed <t> <tag>`.

## Step 6 - Report

Tell the user, in Russian, the result of step 1b (passed with totals per suite, or "skipped by
`skip-tests`"), then per target:

- the deployed tag and the size that was uploaded
- the link: the url of the target (the script prints
  `URL: ...` when `PUBLIC_URL` is set in `deploy.env`; otherwise give the path and say so).
  The telegram link is the one BotFather has as the Mini App URL - with the trailing slash.
- rollback command: `tools/scripts/deploy-webgl.sh --target <t> --rollback <previous tag>`
  (the live tag remembered in step 1; none for a first deploy)

And once:

- which submodules were stripped
- whether the script printed `nginx.conf changed` - then the web service needs a restart in
  Coolify (both targets are served by it; until the restart `/tg/` may still show the old config)
- files the build changed in the working tree compared with step 1 (Unity reserializes room
  prefabs, the build profiles and the Addressables `link.xml` during a build) - list them, do not
  revert or commit them
