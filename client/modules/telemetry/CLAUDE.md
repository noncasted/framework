# Telemetry module

The module `telemetry` (`Internal.Telemetry`, namespace `Global.Telemetry`): metrics, errors and the startup trace of a
client. Depends on `network`. The backend side: `backend/ClientTelemetry/CLAUDE.md`. The end to end tests of the transport
and their host: `backend/ClientTelemetry.TestHost/CLAUDE.md`.

## Wiring

- The transport, one of two: `AddSocketClientTelemetry(options)` over the meta socket, which the game gives as
  `ITelemetrySocket`, so it is called in the scope of the meta backend (`Internal.Telemetry.Socket`, compiled only with the
  `network` package, `FRAMEWORK_NETWORK`); or `AddHttpClientTelemetry(options, TelemetryHttpOptions)`, JSON POSTs of
  `JsonUtility` to `<BaseUrl>/start|batch|end|startup` (`fetch` of the page on WebGL, System.Net.Http elsewhere; no
  MemoryPack, network or UnityWebRequest; a failure is a warning and a new link).
- `IClientTelemetry`: `StartSession(lifetime, tags)` per part of the game, `Mark`, `SendStartupTrace` after every
  authorization of the socket. Nothing runs without a session.
- Collectors: `AddStandardTelemetryCollectors`; a game adds its own as `ITelemetryCollector` in the scope of the telemetry, or
  from a scope below (a run) with `AddCollector(lifetime, collector)`: sampled in every session while the lifetime lives, a
  live one started per session with the session cut by its lifetime.

## What it sends

The exact names are in the code: a folder per family in `Runtime/Collectors`, the page in `Runtime/Page`. The families and
what the code does not say at a glance:

- Frame: markers in the `PlayerLoop` for a session only (`frame.phase.*`, `frame.fixed_steps`, `frame.spikes`, the marks
  `frame.spike` / `frame.spike.<phase>` from 250 ms, `gc.allocated`); `render.*` of the drawing buffer, `settings.*`,
  `memory.texture_count`. `AddSocketClientTelemetry` adds `net.meta.*`, a development build `dev.*` (`devProfilerMarkers`).
- WebGL only: `frame.gpu.query` (`EXT_disjoint_timer_query_webgl2`, one frame in 30), `render.canvas_*` / `dpr`,
  `web.raf.dt.*` (an own chain of `requestAnimationFrame`), and `web.gl.*`: the calls of `GLctx`, wrapped once per context
  (shader compiles and program links with the queries of the status in their time, buffer and texture uploads; the mark
  `webgl.shader.slow`, `slowShaderMs`).
- GL errors come from three sources, kept apart: `web.gl.errors` (`gl.getError` once a sample, the marks
  `webgl.error.<code>`); the errors emscripten raises itself in its gl* functions (`GL.recordError`, never seen by
  `getError`): `web.gl.emscripten_errors`, the marks `webgl.emscripten.<code>` with the function; a page opened with
  `?glcheck` checks every call of the context (`getError` after it, slow, nothing wrapped without the parameter):
  `web.gl.check_errors`, the first error of each code and call as the mark `webgl.check.<code>` with `call(arguments)`, its
  codes still counted once a sample in `web.gl.errors`.
- Context loss (the canvas is listened to from the build of the scope, nothing prevented): the metric
  `web.gl.context_lost`, the marks `webgl.context_lost` / `webgl.context_restored`, the error `WebGL context lost`. Unity
  stops its player loop while the context is lost, so all this is sent only after a restore; without one the samples just
  stop and the session closes as `lost`. A loss outside a session waits on the page and comes once with the next one.
- The page: the marks `page.*` (hidden / visible, pagehide, pageshow, freeze / resume, fullscreen, resize with the sizes,
  collapsed) and `audio.context.<state>` of the `AudioContext` of Unity.
- Loads of assets in a session: the marks `load.start` / `end` / `failed` / `slow` / `unfinished` with the key and the
  duration (`AssetLoads` of the core; `slowLoadSeconds` of the options).
- The warnings of the log of Unity go as the level `Warning` under their own limits.
- A session the page died in: the trace of the open session (local storage, a file outside the web, with the main numbers of
  its last 5 samples) is reported by the next start of the same browser as `SessionStart.Previous`.
- The startup trace on the web begins at the origin of the page: the root `Page` (navigation, files with sizes and cache, the
  gap to the build, the spans of the template). The build puts `WebTemplate~/startup-trace.js`
  (`window.startupTrace.begin/end/mark`) first into the `<head>` of the built `index.html`
  (`TelemetryStartupTraceInjection`); the template of the game only calls it.
