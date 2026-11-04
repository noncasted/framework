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
