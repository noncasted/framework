# web_perf_tools: what pixel-swarm built for the web performance, moved into the framework

Started 2026-11-04 from the task client_telemetry (pixel-swarm, steps 18-46). Prose with the user in Russian, code and docs in English.

## Steps (in this order, the user agreed)
1. **Bench and device lab** - moved to its own task `docs/tasks/current/web_bench_tools/` (parallel session).
2. **Render resolution on phones.** `web-shell.js` renderPixelRatio (whole divisor, 360 units limit, `?dpr=`), modes Auto / Low / High
   (Auto = Low on iOS), `WebRenderPixelRatio.jslib`, `settings.resolution`. Into the `web` / `settings` modules; the button stays in a game.
3. **Adaptive frame rate 60/30.** `AutoFrameRateRule` (>12 ms for 8 s -> 30, <6 ms for 30 s -> 60), the memory on the device,
   marks `fps.auto.*`, modes Auto/30/60, `?fps=`.
4. **Shader check for WebGL.** `WebGlShaderTablesTests` of pixel-swarm (no ImmCB / arrays indexed at run time, no nointerpolation) as
   a framework EditMode test over the shaders of a game.
5. **Shader warm-up under the loading screen.** `ShaderVariantCollection.WarmUpProgressively` helper with `AddLoadingScreen` / `AssetLoads`.
6. **Frame allocation guard.** `FrameAllocationTests` of pixel-swarm as a shared test helper.
7. **Docs.** Done by the WebGL troubleshooting doc (below).
Open: static tilemap baking, `QuadMesh`, IL2CPP OptimizeSpeed - in `docs/plan/render_mesh_utilities.md` (OptimizeSpeed: likely reverted, the build size matters more).
Stays in the game: swarm, GamePipeline and its persistent targets, `game.*` metrics, stress content.

## WebGL troubleshooting doc
Agent ORCH-web_perf_tools-doc-g1: `.claude/docs/WEBGL_TROUBLESHOOTING.md` from the commits, the docs and the sessions of every project.

## Journal
### [2026-11-04 10:40] Created
- The user agreed to steps 1-7. Agent ORCH-web_perf_tools-doc-g1 (a8f305355e700dc03) writes the WebGL troubleshooting doc into jobs tmp/webgl-doc (no repo edits); the orchestrator commits it.
### [2026-11-04 10:55] Split
- Step 1 -> task web_bench_tools (scripts snapshot there). Tilemaps / QuadMesh / OptimizeSpeed -> docs/plan/render_mesh_utilities.md.
### [2026-11-04 12:25] Start of steps 2-6 (the user: "делай")
- Agents in their own copies of the game worktree (base: pixel-swarm main 1b1d5f3d, framework 35ad2a52 = 0.4.0); each gives a framework diff + a game diff, the orchestrator (client_telemetry session) commits.
- Wave 1: ORCH-web_perf_tools-23-g1 (steps 2+3, resolution and frame rate: same Settings files), ORCH-web_perf_tools-46-g1 (steps 4+6, test helpers). Then step 5 (warm-up).
- Side: ORCH-client_telemetry-47-g1: tools/stress/serve.py --lan over https 8771.
- Agents: 23-g1 / a45fdc7fb75be851d (copy /var/tmp/ps-wpt23), 46-g1 / a53a8ebf3e3f4a176 (copy /var/tmp/ps-wpt46); client_telemetry 47-g1 / a048c2794138856dd (jobs tmp/s47). Results: jobs tmp wpt23-*, wpt46-*, step47.*
- [12:59] 23-g1 → HANDOFF (handoff/23_g1.md): code done, website builds; modules/resolution + modules/framerate. 23-g2 / a9f6c4821dc8a2876: tests + before/after stand.
- [13:05] 46-g1 DONE: framework 68d439f0 (Internal.TestTools: FrameAllocations, WebGlShaderCheck), game d2431301 (+ Light.shader flat varying fix, visual check pending). Unity 1083/0 in the copy. Pushed main.
- [13:13] 23-g2 DONE: tests 1088/0 in the copy, stand before/after identical (resolution modes, ?dpr, ?fps). Framework f5affc6a pushed; game e9272395 local (not pushed to main: the user saw a backend redeploy in Coolify after my push of d2431301, asked). Deploy-client of e9272395 started 13:12.
- [13:40] main 58daa21e deployed: web 20261104-133943-58daa21e7, tg 20261104-133955-58daa21e7 (tests backend 576, Unity 1098/0). Research of slow Unity tests: ORCH-client_telemetry-unitytests-g1 / a9430c5c94a1c1676 -> handoff/unity_tests_research.md.
