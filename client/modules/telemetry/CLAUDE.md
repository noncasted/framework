# Telemetry module

The module `telemetry` (`Internal.Telemetry`, namespace `Global.Telemetry`; `AddSocketClientTelemetry(options)` or
`AddHttpClientTelemetry(options, TelemetryHttpOptions)`, `AddStandardTelemetryCollectors`): metrics, errors and the startup trace
over the meta socket, which the game gives as `ITelemetrySocket`, so it is called in the scope of the meta backend
(`Internal.Telemetry.Socket`, compiled only with the `network` package, `FRAMEWORK_NETWORK`), or as JSON POSTs of `JsonUtility` to
`<BaseUrl>/start|batch|end|startup` (`fetch` of the page on WebGL, System.Net.Http elsewhere; no MemoryPack, network or
UnityWebRequest; a failure is a warning and a new link). `IClientTelemetry`: `StartSession(lifetime, tags)` per part of the
game, `Mark`, `SendStartupTrace` after every authorization of the socket. Nothing runs without a session; a game adds a
collector as `ITelemetryCollector` in the scope of the telemetry, or from a scope below (a run) with
`AddCollector(lifetime, collector)`: sampled in every session while the lifetime lives, a live one started per session with the
session cut by its lifetime. The standard collectors also time the frame: markers in the `PlayerLoop` for a session only
(`frame.phase.*`, `frame.fixed_steps`, `frame.spikes` and the marks `frame.spike` / `frame.spike.<phase>` from 250 ms, `gc.allocated`;
under 1 us per frame in the editor), `render.width` / `height` / `pixels` of the drawing buffer, `settings.*`, and on WebGL
`frame.gpu.query` (`EXT_disjoint_timer_query_webgl2`, one frame in 30), `render.canvas_*` / `dpr`, `web.raf.dt.*` (an own chain of
`requestAnimationFrame`), `web.gl.*` numbers of the context; `AddSocketClientTelemetry` adds `net.meta.rtt` / `bytes_*`. On WebGL `AddClientTelemetry` also listens to the context of the canvas (`Module.canvas`, from the build of the
scope, nothing prevented): the metric `web.gl.context_lost`, the marks `webgl.context_lost` / `webgl.context_restored` and the error
`WebGL context lost`; a loss outside a session waits on the page and comes once with the next one. Unity stops its player loop while the
context is lost, so all this is sent only after a restore; without one the samples just stop and the session closes as `lost`. Also on WebGL:
`web.gl.errors` (`gl.getError` once a sample, its time `web.gl.errors_ms`) with the marks `webgl.error.<code>` and an error per code; the errors emscripten raises itself in its gl* functions
(`GL.recordError`, never seen by `getError`) apart: `web.gl.emscripten_errors`, the marks `webgl.emscripten.<code>` with the function and
an error `... in <glCall> (emscripten)`; a page opened with `?glcheck` checks every call of the context (`getError` after it, slow, nothing
wrapped without the parameter): `web.gl.check_errors`, the first error of each code and call as the mark `webgl.check.<code>` with
`call(arguments)`, the frame and the time of the page and an error by the code and the call, its codes still counted once a sample in `web.gl.errors`;
the calls of `GLctx`, wrapped once per context: `web.gl.shader_compiles` / `shader_compile_ms` / `program_links` / `program_link_ms`
(the queries of the status in the time), `web.gl.buffer_bytes` / `buffer_data_calls` / `texture_bytes` / `texture_uploads`, the mark
`webgl.shader.slow` (`slowShaderMs`); the marks of the page (`page.hidden` / `visible` / `pagehide[.persisted]` / `pageshow.persisted` /
`freeze` / `resume` / `fullscreen.enter` / `fullscreen.exit`, `page.resize` with the sizes, collapsed) and `audio.context.<state>` of
the `AudioContext` of Unity. A development build adds `dev.*` (counters it alone feeds, the markers of `devProfilerMarkers`). Everywhere: `memory.texture_count`,
the warnings of the log of Unity as the level `Warning` under their own limits, and the trace of the open session (local storage, a
file outside the web, with the main numbers of its last 5 samples) that the next start of the same browser reports as
`SessionStart.Previous` when the page died inside it.
The loads of assets in a session are marks `load.start` / `end` / `failed` / `slow` / `unfinished` with the key and the duration
(`AssetLoads` of the core; `slowLoadSeconds` of the options). On the web the startup trace begins at the origin of the page: the
root `Page` (navigation, files with sizes and cache, the gap to the build, the spans of the template); the build puts
`WebTemplate~/startup-trace.js` (`window.startupTrace.begin/end/mark`) first into the `<head>` of the built `index.html`
(`TelemetryStartupTraceInjection`), the template of the game only calls it. Depends on `network`.

The backend side: `backend/ClientTelemetry/CLAUDE.md`. The end to end tests of the transport and their host:
`backend/ClientTelemetry.TestHost/CLAUDE.md`.
