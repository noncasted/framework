# game_g1: Pixel Swarm on framework a88498ba (done)

Worktree `/projects/pixel-swarm/.claude/worktrees/client_telemetry`, branch `client-telemetry`, HEAD `e19b6b7e`, tree clean. Nothing pushed or
deployed, no cluster started.

## Commits

- `b66088cd` [Framework]: submodule `ed3868a9` -> `d50671b8`.
- `78492e9a` [Shared] [Tests]: protocol goldens (`UPDATE_PROTOCOL_GOLDEN=1`), `ProtocolHash.Value` `8de0da71dd89cd3f` -> `65cd3fcd58328aa7`.
  The schema diff has only `SharedClientTelemetry` changes: Mark.Detail/DurationMs, StartupSpan.File/TransferBytes/Cached, SessionStart.Previous +
  PreviousSession, ErrorLevel.Warning=3. That change was expected.
- `c339e4f0` [Framework]: submodule -> `a88498ba` (the jslib key is a string again).
- `e19b6b7e` merge of `main` (7e6aac96 Tablet UI). The conflict was only `_structureHash` of `LobbyScreen.prefab` and `// Structure:` of
  `LobbyScreenBindings.g.cs`. I resolved that hunk only (not `checkout --ours` of the whole file) and regenerated the bindings with
  `HierarchyBindingsBatch.RegeneratePrefabs` in batchmode, using a temporary driver that is removed (not committed). New hash `ca091d47...`,
  `HierarchyBindingsValidator.ValidatePrefab` 0 errors. In the prefab: `PixelBack` (da7c71d9...) on Back from our side, two `UIWidthLayout`, `_uiWidthLayout`
  and `_reference: {x: 512}` from main.

## Steps of unreleased.md

The templates already carry all the spans (`77bd03cd`). The game has no `ITelemetryStartupSource` of its own, no `TelemetryStartupReport`/`Build`, no direct
`Addressables.Load/Instantiate` calls, no `ITelemetryPage` fake and no `switch` over `ErrorLevel`. The `AddCollector` fakes are already there. No other game code to change.

## Tests (after the merge)

- Backend: build 0 errors, 567/567 passed.
- EditMode (`run-tests.sh --unity-only`, UNITY_SLOTS=1 = `unity-batchmode.lock`): 914 total, 866 passed, 48 skipped, 0 failed.
  Before the merge one run had a failure of `ProfilerNestingTests.Finish_ParentClosedEarlier_CoversTheLateChild`: a floating-point comparison of
  `StartMs + DurationMs`, 2.5342000000000002 vs 2.5341999999999998 with equal `EndNs`. The test is flaky in the framework and worth fixing.

## Builds (batchmode `-buildTarget WebGL -activeBuildProfile <profile> -executeMethod Internal.WebGlBuild.Build`, under flock)

| | guid | bytes | telegram-web-app.js | first in `<head>` |
|-|-|-|-|-|
| `builds/telegram` | 83cd7048... | 13 545 469 | 1 (telegram-shell.js 2) | `<script data-startup-trace>` |
| `builds/website` | 0ff744ab... | 13 535 354 | 0 | `<script data-startup-trace>` |

Both: stripping ran (`Library/com.unity.web.stripping-tool/<name>-<guid>`), the catalog has no StandaloneLinux64, no duplicate bundles, both `cmp` checks OK,
`telemetryTraceKey=framework` absent from framework.js. The active profile is website.
Bee deletes the `Build/` of the previous output folder when another folder is built. So telegram was backed up before the website build
and restored after it.

## Smoke (website, headless Chromium + node static server)

- No ReferenceError and no exceptions. `startupTrace.read()`: `Loading wheel` 14-2602 ms, `Unity loader` 15-24, `Unity instance` 24-642, marks
  quarter/half/three quarters (292), `Progress done` (642).
- WebSocket `wss://meta.pixel-swarm.xyz/?protocol=65cd3fcd58328aa7`: 403, as expected. Then the offline mode, the menu loaded (screenshot
  `smoke.png`). Offline the local server answers `StartupTrace` / `SessionStart` with "No command": there is no telemetry offline, and that is expected.
- Noise: `AudioContext was not allowed to start` (autoplay without a gesture), `[UnityCache] ... without Content-Length` (my server).

Scripts and logs: `/tmp/claude-1000/-projects-framework/335627c0-e338-455f-b883-14b9b709f1a2/scratchpad/` (`smoke.mjs`,
`build-webgl.sh [website|telegram]`, `webgl/*.log`, `smoke-website.err`, `smoke.png`).
