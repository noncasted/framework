# WebGL on iOS Safari: GPU hangs, the stuck browser, testing on a device

**TL;DR:** no `nointerpolation` / `flat` varyings in a shader that ships to WebGL. On iOS Safari a hang of the GPU leaves the
whole browser at one frame in 5 s until Safari is restarted, so every test on an iOS device starts from a freshly restarted
Safari. A GPU problem is found by bisecting the draws on the device, with the programs tagged by their source.

## Shader rules

- **No flat varyings.** WebKit runs WebGL through ANGLE on Metal. GL takes the value of a flat varying from the last vertex
  of a triangle, Metal from the first, and ANGLE emulates the GL convention by rewriting the index buffer of every draw of
  such a program. On iOS 16 (iPhone 11, Safari 16.6) this hung the GPU of the page after ~30 s of a run of pixel-swarm (the
  swarm bodies of `Sprite.shader`, `_SWARM_BODY`, added with the batching of the swarm on 2026-10-26).
  - A value that is the same at every vertex (a state packed into the vertex colour, an id) is passed as an ordinary
    interpolated varying: interpolating equal values gives the value back.
  - Where such a value is compared (`group == FRONT_GROUP`) or used as an index, `round` it in the fragment shader first:
    the interpolation may miss it by an ulp.
- **Compressed textures.** iOS has no DXT: Unity decompresses DXT textures on the CPU at load. A WebGL build that targets
  iOS uses ASTC (or ships both).

## The stuck browser

Symptom of a hung GPU on iOS Safari (and every iOS browser, all are WebKit):
- one frame every ~5.0 s, the timers of the page stop too (a heartbeat from `setInterval` comes every 5 s);
- the fences of WebGL2 stay unsignalled: `clientWaitSync` returns `TIMEOUT_EXPIRED` (37147);
- Unity then creates new buffers on every frame, because the old ones are still in flight (a consequence, not the cause);
- no `webglcontextlost`, no error in the console.

After that **the whole browser stays in this state**: every WebGL page, an empty Unity scene and another origin included,
runs at 0.2 FPS. A reload does not clear it; only closing Safari from the app switcher does. A test run after the game has
hung the GPU measures the stuck browser, not the variant under test. A wrong A/B came out exactly like this (an empty scene
"also lags", `powerPreference` "fixes it"); from a fresh Safari both variants ran at 60 FPS.

Rules:
- Restart Safari before every run of an A/B.
- Between variants check that the empty scene still runs at 60 FPS.

## Finding the guilty draw

Wrap the prototypes of `WebGL2RenderingContext` in a script loaded first in `<head>` (a probe), driven by the query string,
and bisect on the device:
1. Skip every draw (`drawElements` / `drawArrays` return at once). No hang means the drawing is guilty. The screen is black in
   this mode, that is expected.
2. Skip the draws of groups of programs, then of one program.
3. **Tag a program by its source, not by its number.** The order of `createProgram` changes between runs (two programs
   swapped places), so a skip by number is unreliable. Mark the program at `linkProgram` by a uniform name only it has.
4. Once the program is known, cut it down by substitution in `shaderSource`:
   - a trivial fragment shader with the same outputs (is it the cost of the pixels?);
   - `gl_Position` outside the clip volume (is it rasterization, or the vertices?);
   - a cap on the count of indices per draw (is it the volume of the vertices?);
   - the suspected feature removed (`flat`, a dynamically indexed array, `GL_EXT_shader_framebuffer_fetch`).
5. Rule out the buffers: shadow the index buffers (`bufferData` / `bufferSubData` on `ELEMENT_ARRAY_BUFFER`) and compare the
   largest index of every draw to the capacity of the enabled attributes; log the attribute layouts per program.
   `vertexAttribPointer` must keep the enabled flag set by `enableVertexAttribArray`.

The pixel-swarm case: the hang stayed with the trivial fragment shader, with the vertices culled, without the array and without
the framebuffer fetch; it went away with the draws skipped, with 6 indices per draw and without `flat`. No out of bounds read in
60 000 draws; the vertex layout was the same as that of every other sprite.

## A device bench without hands

What drives an iPhone from the Linux machine over USB:
- **Developer Mode** on the phone (Settings, Privacy & Security; the item appears only after a developer tool has paired with the
  phone) and the **Developer Disk Image** of its iOS version, mounted with `ideviceimagemounter` (again after every reboot of the phone).
- **Restarting Safari**: `pymobiledevice3 developer dvt launch --kill-existing com.apple.mobilesafari` (`idevicedebug kill` cannot
  kill Safari). Navigate to `about:blank` first: Safari restores the last tab for a few seconds after the start.
- **Opening a URL and running JS in the tab**: `ios_webkit_debug_proxy` (port 9222) and `Runtime.evaluate` of
  `location.href = ...` over its websocket. A screenshot of a WebGL canvas through the inspector is black.
- **A run without a human**: the game plays itself by a query switch (pixel-swarm: `?autorun=1`: a solo run, movement in a
  circle, invulnerability, the first card), and logs a heartbeat. The page sends its console, its errors and a heartbeat every
  2 s to a POST endpoint of the local server; a gap of the heartbeat of 3 s or more is a stall.
- The screen of the phone must not lock (Auto-Lock: Never); nobody touches the phone or turns it during a series.

A build for a phone on the LAN over http: `PlayerSettings.insecureHttpOption = AlwaysAllowed` and no compression (a browser does
not decode `Content-Encoding: br` outside a secure context), with the project steps that rewrite the `.data` taught the
uncompressed file. The phone sees the machine only with its VPN off. Load the probe as
`document.write('<script src="/probe.js?t=' + Date.now() + '">')`, or Safari serves an old one from its cache.
