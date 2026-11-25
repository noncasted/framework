# WebGL troubleshooting: browsers, devices, the Unity WebGL player

**TL;DR:** what broke or slowed a Unity WebGL build of a game on a real browser and device, why, and how it was found and
fixed. Read the row of the symptom, then its entry. Every entry has a status: **fixed** (in the framework or in a game),
**workaround**, **open**. "Proven" means bisected or measured on the device; "hypothesis" is marked as such.

Versions in the entries: Unity 7000.0.0a10 (a12 where said), WebGL 2, `webWasm2023` on. iPhone 11 is iOS 16.6 Safari;
iPad 7 (A10, 3 GB) is iPadOS 18.2.1; the Android phone is a Mali-G52 MC2 with Chrome 156 (also as the Telegram WebView).

## Symptom -> entry

| Symptom | Entry |
|---|---|
| iOS: one frame every ~5 s, the whole Safari stays slow until restarted | [G1](#g1-flat-varyings-hang-the-gpu-on-ios), [I1](#i1-the-stuck-browser) |
| iPad / iPhone: fragment-bound frame, GPU time far above the scene | [G2](#g2-arrays-indexed-at-run-time), [G3](#g3-constant-weights) |
| Stalls of 100-300 ms when new content first appears (first enemy, light, vfx) | [G5](#g5-a-program-is-linked-at-its-first-draw) |
| A sprite drawn by a `MeshRenderer` is invisible, only its light / glow is seen | [G6](#g6-unity_spritecolor-is-zero-outside-a-spriterenderer) |
| A sprite outline turns into blocks or vanishes with a shared material | [G7](#g7-texel-size-and-getdimensions) |
| Textures load slowly on iOS, large CPU spike at load | [G4](#g4-compressed-textures-on-ios) |
| `memory access out of bounds` from a mesh update | [R1](#r1-meshsetvertexbufferdata-crashes-the-player) |
| `targetFrameRate` does something unexpected / 30 FPS cap | [R2](#r2-targetframerate-on-webgl) |
| High render CPU with few draws (Android especially) | [A1](#a1-the-cost-of-a-gl-call-through-angle), [R3](#r3-tilemaps-cost-cpu-every-frame), [R4](#r4-render-targets-recreated-every-frame) |
| A constant 0.5-1 ms of a frame in UI layout | [R5](#r5-recttransformrect-every-frame) |
| GC spikes, heap grows in steps | [R6](#r6-gc-and-the-wasm-heap) |
| Black screen, samples stop, the session closes `lost` | [R7](#r7-a-lost-context-stops-the-player-loop), [I1](#i1-the-stuck-browser) |
| Development build: black screen and a `NullReferenceException` every frame | [R8](#r8-development-build-black-with-nre-every-frame) |
| iPadOS: the UI (cards, text) vanishes after ~20-30 s, background stays | [I2](#i2-screen-space---overlay-loses-its-projection) |
| iOS: every touch or Ctrl+C crashes the game (`SuspendError`, `.catch is not a function`) | [I3](#i3-jspi-is-absent-on-ios) |
| iOS: no sound at all, AudioContext is `running` | [AU1](#au1-the-silent-mode-of-ios-mutes-webaudio) |
| iPhone: FPS falls in waves after 1.5-3 min, worse on the cable | [I4](#i4-thermal-throttling-in-waves) |
| iPad treated as a desktop, mobile controls hidden | [I5](#i5-ipados-reports-itself-as-a-mac) |
| iOS: fullscreen does nothing | [I6](#i6-no-fullscreen-api-on-iphone) |
| `WebGL error INVALID_OPERATION` once at load or after a resize | [A4](#a4-invalid_operation-at-load-and-after-a-resize) |
| Phone: GPU-bound at the native resolution, or blurry at a low one | [P1](#p1-device-pixel-ratio-of-phones), [P2](#p2-the-divisor-chosen-before-fullscreen) |
| Android: endless loading spinner after a deploy | [A5](#a5-endless-spinner-after-a-deploy) |
| Slow load on one device only | [I7](#i7-slow-network-of-a-device) |
| LAN build over http: `ERR_CONTENT_DECODING_FAILED`, "Insecure connection not allowed" | [P3](#p3-a-build-over-plain-http) |
| LAN build: no telemetry, offline | [P4](#p4-the-production-meta-refuses-a-lan-origin) |
| Release player halts on any C# exception, even a caught one | [B1](#b1-wasm-opt-drops-every-trycatch) |
| wasm grows by MB after a Unity upgrade | [B2](#b2-submodule-stripping-silently-skipped) |
| `undefined symbol: $jspiPromising` at link | [B3](#b3-webwasm2023-cannot-be-turned-off) |
| Random link failure / heap corruption in wasm-ld | [B4](#b4-wasm-ld-crashes-at-random) |
| The output of another target vanished after a build | [B5](#b5-bee-cleans-the-previous-output) |
| "Failed to decompress data for the AssetBundle" (before 0.8) | [B6](#b6-addressables-built-for-the-editor-target) |
| Page hangs at start right after a build | [B7](#b7-the-wasm-is-written-after-build-finished) |
| A `memorysetup-*` boot setting has no effect | [B8](#b8-bootconfig-of-a-web-build) |
| "CRC Mismatch" of a bundle after a Unity upgrade (before 0.8) | [B9](#b9-bundle-caches-keep-old-bytes) |
| Content files requested on every opening, a load waits on the network, an old build in the cache | [B15](#b15-the-content-directory-on-the-web) |
| Every player sees "version outdated" after a client deploy | [B10](#b10-client-before-backend) |
| WebGL build larger than expected | [B11](#b11-what-keeps-code-in-the-build) |
| `is not supported on this GPU`, `Active draw buffers with missing fragment shader outputs`, an effect not drawn | [G8](#g8-no-blending-per-render-target) |
| Sprites invisible after the Unity 7000 upgrade, sprites never SRP-batched | [G9](#g9-sprites-and-the-srp-batcher-sprite-flags-of-unity-7000) |
| Windows: a freeze of seconds at the first load, `webgl.shader.slow[compile status,8s]`, gone after a reload | [G10](#g10-loops-unrolled-by-the-d3d11-compiler) |
| Safari (iOS / iPadOS): the context is lost when a level first draws, `GLSL link error: (unknown error)`; a small program `LINK FAILED` with an empty log | [G11](#g11-an-int-scalar-plus-or-minus-an-int-vector-does-not-link-on-metal) |
| Works in the editor, throws or has empty fields only in the WebGL player | [R10](#r10-works-in-the-editor-fails-only-in-the-player) |
| iPadOS fullscreen: the exit cross covers the UI | [I9](#i9-fullscreen-and-bars-of-ios-safari) |
| Loading screen never goes away while the window is off screen | [P5](#p5-page-state-that-waits-on-raf) |
| Saves lost on a closed tab, IndexedDB wiped at start, keys differ between `/` and `/tg/` | [P7](#p7-storage-clipboard-sockets-of-the-page) |
| Telegram Desktop asks for the camera | [P6](#p6-the-mobile-page-shell) |
| Audio memory large, importer settings change nothing, a clip "not loaded" | [AU2](#au2-clips-on-webgl) |
| `Unable to parse ...framework.js.br`, `function signature mismatch` after a release, 503 | [B12](#b12-the-server-of-the-build) |
| `ld.lld: undefined symbol` / `duplicate symbol` of a native plugin | [B13](#b13-native-code-in-a-webgl-build) |
| 7000.0.0a16+: player stops at start, `Unable to load required ICU Globalization data` / an ICU call failed | [B14](#b14-submodule-stripping-on-the-net-10-bcl) |
| Mini App collapses on a swipe, wrong insets in Telegram | [T2](#t2-the-mini-app-shell) |
| Telegram build: stuck on the loading screen outside Telegram | [T1](#t1-a-telegram-build-outside-telegram) |

## Shaders and GPU

### G1. Flat varyings hang the GPU on iOS
- **Symptom:** ~30 s into a run the page drops to one frame every ~5.0 s; timers of the page stop too; no context loss, no
  console error. Then [I1](#i1-the-stuck-browser).
- **Where:** iOS 16.6, iPhone 11, Safari and Chrome (every iOS browser is WebKit).
- **Cause (proven by bisection):** WebKit runs WebGL through ANGLE on Metal. GL takes a flat varying from the last vertex of
  a triangle, Metal from the first; ANGLE emulates the GL convention by rewriting the index buffer of every draw of such a
  program. Gone with `flat` removed or with 6 indices per draw; stayed with a trivial fragment shader, culled vertices, no
  arrays, no framebuffer fetch.
- **Fix:** no `nointerpolation` / `flat` in a shader that ships to WebGL. A value equal at every vertex goes as an ordinary
  varying; `round` it in the fragment shader where it is compared or indexes something.
- **Recognize:** `web.raf.dt.max` ~5000, `frame.dt` ~5 s while `frame.cpu.main` stays normal; on the probe,
  `clientWaitSync` returns `TIMEOUT_EXPIRED` (37147) and Unity creates new buffers every frame (a consequence).
- **Status:** fixed in pixel-swarm (`Sprite.shader`, `_SWARM_BODY`, e3acef1b); `WebGlShaderCheck` of [G2](#g2-arrays-indexed-at-run-time)
  also rejects a flat varying (`flat in` / `flat out` in the compiled code).

### G2. Arrays indexed at run time
- **Symptom:** a fragment-bound frame; on iPad 7 rAF dt 29-31 ms with the CPU at 10 ms.
- **Cause (proven):** HLSLcc (Unity's GLES3 / WebGL compiler) writes a table of constants (`const` / `static const` array, a
  `switch` that fxc turns into a lookup) as a non-const `vec4 ImmCB_0[N];` filled by N assignments at the top of `main()` of
  every pixel, whether its branch runs or not; a local array of values (`half4 colors[5] = { _Ramp0, ... }`) becomes
  `TempArrayN[...]` the same way. The 4x4 Bayer table in 8 fragment shaders of pixel-swarm: 29.3 -> 20.5 ms.
- **Fix:** compute the value (Bayer: `4 * m2(low bits) + m2(high bits)`, `m2(x, y) = ((x ^ y) << 1) | y`), pick among a few
  with `?:`, or pack the table into `uint` literals picked by the bits of the index. A literal index after `[unroll]` is
  folded by fxc. Debug-only branches (ramps, heat views) go into a keyword variant.
- **Recognize:** check the compiled code, not the HLSL: `ShaderUtil.GetShaderData(shader).GetSubshader(s).GetPass(p)
  .CompileVariant(ShaderType.Vertex, keywords, ShaderCompilerPlatform.GLES3x, BuildTarget.WebGL)` returns the GLSL (vertex and
  fragment) to search for `ImmCB_` / `TempArray`. `WebGlShaderCheck` of `Internal.TestTools` does it: a game keeps an EditMode test
  `WebGlShaderCheck.AssertSafe(WebGlShaderCheck.Find(new[] { "Assets" }, thirdPartyPrefixes))` over its shaders.
- **Traps of `CompileVariant`:** for GLES3x the whole program comes with `ShaderType.Vertex` (`#ifdef VERTEX` / `#ifdef FRAGMENT`
  in one text); `ShaderType.Fragment` returns 0 bytes with `Success = true`, so a check that asks for the fragment passes over
  nothing (`forExternalTool: true` gives the stages apart). The editor answers from the imported shader: a `.shader` or an
  include changed on disk without an import (a merge, a write from outside, no refresh) gives the old GLSL silently -
  `AssetDatabase.ImportAsset` of the shader first, and compare the size with the previous one.
- **Status:** fixed in pixel-swarm (e8a88090, test `WebGlShaderTablesTests`); iPad run ~33 -> ~52 FPS.

### G3. Constant weights
- A blur loop without `[unroll]` stays a loop in GLSL and computes `exp()` of its weight per tap; with `[unroll]` the weights
  and their sum are constants. A blur of a linear value reads two taps with one linear read at their weighted middle (half
  the reads); a mask computed per tap (a threshold) cannot. pixel-swarm Occlusion 17 -> 9 reads, Outline 26 -> 18 (e8a88090).

### G4. Compressed textures on iOS
- iOS has no DXT: Unity decompresses DXT textures on the CPU at load. A WebGL build that targets iOS uses ASTC (or ships
  both).

### G5. A program is linked at its first draw
- **Symptom:** spikes of 100-300 ms (`frame.spike.postlate`) when content appears for the first time mid-run; on iPad the
  first frames after the load up to 290 ms of linking.
- **Where:** every browser; costlier on Mali / ANGLE and on WebKit (GLSL -> MSL translation, hypothesis for the size of the cost).
- **Cause:** WebGL links a GL program at the first draw of a variant; nothing is preloaded by default.
- **Fix:** warm the variants up under the loading screen: `ShaderVariantCollection.WarmUpProgressively` a few variants a frame.
  A variant warms up **only the shader instance it references**: a shader built into the player and the same shader in a
  bundle are two instances, so a game keeps one collection per owner (the pipeline asset, the bundles of a run), or every
  program is linked twice. pixel-swarm d0794b4d: 8 -> 0 links after the start of a run (iPhone 11 and Chromium), 14 programs
  in 1.6 s under the loading screen.
- **Recognize:** `web.gl.program_links` / `program_link_ms` after `run.started`, the mark `webgl.shader.slow`.
- **Status:** fixed in pixel-swarm; open: the Metal pipeline state objects WebKit builds at the first draw are not covered by a
  GL link, watch `frame.spike` after a warm-up. A shared warm-up helper is planned for the framework (web_perf_tools step 5).

### G6. `unity_SpriteColor` is zero outside a SpriteRenderer
- **Symptom:** sprites batched into one mesh (`MeshRenderer`) are transparent; only their additive light shows (blurry circles).
  Everywhere, the editor too.
- **Cause (proven):** a sprite shader that multiplies by `unity_SpriteColor` (and flips by `unity_SpriteProps`) gets zero
  from a `MeshRenderer`: only a `SpriteRenderer` fills them.
- **Fix:** a keyword (`_SPRITE_MESH` in pixel-swarm) where the tint is the vertex colour alone; the mesh material carries it,
  and the variant is in the warm-up collection. pixel-swarm f65cd918.

### G7. Texel size and GetDimensions
- `Texture2D.GetDimensions` in HLSL returns 0 under OpenGLCore and GLES3: HLSLcc binds a separate uniform. Use
  `_MainTex_TexelSize`.
- Renderers sharing one material in a SortingGroup got the `_MainTex_TexelSize` of whichever texture was drawn first; set it
  per renderer with a `MaterialPropertyBlock` when the sprite changes. peace-data-reborn (`SpriteSilhouette.SetSprite`).

### G8. No blending per render target
- **Symptom:** `<shader> is not supported on this GPU`, then `GL_INVALID_OPERATION: Active draw buffers with missing fragment
  shader outputs`; the effect is not drawn at all (no pink), on every GPU.
- **Cause (proven):** the WebGL backend of Unity 7000.0.0a10 has no separate blend per target
  (`supportsSeparatedRenderTargetsBlend` false; `OES_draw_buffers_indexed` exists in browsers, not in `framework.js`). A shader
  with `Blend N ...` / `ColorMask N ...` falls back to `Hidden/InternalErrorShader`, which has one output, and its draws into
  MRT are rejected.
- **Fix:** one `Blend` for all targets (`Blend One One, Zero One`). pixel-swarm 099fa855.

### G9. Sprites and the SRP Batcher, sprite flags of Unity 7000
- A `SpriteRenderer` never enters the SRP Batcher on 7000.0.0a10, even with a compatible shader and one material
  (`RenderLoop.DrawSRPBatcher` fires for `MeshRenderer` only): batch sprites dynamically from one atlas, per-object data in the
  vertex colour. pixel-swarm `docs/infrastructure/rendering.md`.
- Unity 7000 packs the sprite flip into bits of `unity_SpriteProps.x`; a URP 17.7 sprite shader that reads floats collapses every
  `SpriteRenderer` (invisible sprites). mines-leader moved to its own SRP.
- URP 2D: the variants of 2D lights were stripped from the WebGL build; turn off the Light2D prefiltering and the 2D variant
  stripping (peace-data-reborn 6ceba806a).

### G10. Loops unrolled by the D3D11 compiler
- **Symptom:** Chrome on Windows (ANGLE D3D11): one compile or link of 5-9 s (`webgl.shader.slow[compile status,8.38s]`,
  `[link status,7.41s]`) when a scene first draws its sprites, a frozen loading screen; after a reload the same programs cost
  milliseconds (the GPU shader cache of the browser), so only a new player or the first start after a shader change pays it.
- **Cause (proven by timing, the ANGLE step inferred):** ANGLE translates the GLSL to HLSL and compiles it with FXC. GLSL keeps
  no `[loop]`, so a loop of constant bounds may be unrolled by FXC. pixel-swarm: the stalk loops of `WheatHide.hlsl` (8 x 6
  iterations with hashes, `sin`, 3 samples) in every sprite program; FXC with `[unroll]` took 9.1 / 5.5 / 7.5 s for the three
  programs and made 200 KB of DXBC (16 KB as a loop): the same sum as the telemetry of the player (RX 7800 XT).
- **Fix:** the bound of a heavy loop is a uniform, not a constant (`for (int k = -3; k <= (int)_Loops.x; k++)`, set from C#):
  FXC cannot unroll it even when forced (`unable to unroll loop`). Check: `pass.CompileVariant(ShaderType.Fragment, keywords,
  ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64)` of a copy with `[loop]` replaced by `[unroll]`
  (`ShaderUtil.CreateShaderAsset`, the include inlined): seconds and a failure-free compile mean the loop can be unrolled.
- **Status:** fixed in pixel-swarm (`_WheatStalkLoops`); not yet confirmed on the Windows player.
- The same in the water of pixel-swarm (`Water.shader`, `_WaterLoops`): the HighFields variant `_RIVER _FALL _FLOW` was 850 KB
  of GLSL and 7.3 s of FXC; with loops of uniform bounds 224 KB and 0.8 s, the picture identical (the editor render compared
  pixel by pixel at a fixed `_WaterClock`).
- That water program also lost the context of Safari on iPad at its link, at 850 and at 224 KB alike: not the size and not
  the loops, see [G11](#g11-an-int-scalar-plus-or-minus-an-int-vector-does-not-link-on-metal) (224 KB links there once two
  lines of its GLSL are changed).

### G11. An int scalar plus or minus an int vector does not link on Metal
- **Symptom:** Safari only (ANGLE on Metal). A small program: `LINK FAILED` with an empty info log. A big one (70-850 KB of
  GLSL): `GLSL link error: (unknown error)`, then `createShader` returns null and `isContextLost()` is true - the level that
  first draws the program never starts.
- **Where:** iPad 7 (A10), iPadOS 18.2.1 Safari. Not on iPhone 11 (A13), iOS 16.6.1: the same test page links every
  failing variant there, the 351 byte pair included (whether the WebKit / ANGLE version or the GPU decides was not separated).
- **Cause (proven by minimal pairs on the device; the Metal error text was never obtained):** a GLSL statement
  `ivecN = <int scalar> - ivecN` (or `+`), as in `u_xlati8.xw = 0 - u_xlati9.yz;`. HLSLcc writes the negation of an int vector
  this way, and FXC makes that vector itself: it merges two scalar int negations (`-r` and `-g`, `-(int)_Loops.y` and
  `-(int)_Loops.z`, a function inlined with a constant direction `(0, -1)`) into one `ineg` of a vector, so no vector negation
  has to stand in the HLSL. ANGLE wraps int `+` / `-` into helpers that return the type of the left operand
  (`ANGLE_isub(0, _ua.yz)` -> `as_type<int>(uint - uint2)`): MSL that does not compile (read from the translated source,
  inferred). Pairs: `b.xw = 0 - a.yz;` (a 351 byte program) fails 3 of 3 and `1 + a.yz` fails; `b.x = 0 - a.y; b.w = 0 - a.z;`,
  `ivec2(0) - a.yz`, `-a.yz`, `3 * a.yz`, `a.yz * 3` link. Programs of 73, 122 and 224 KB that lost the context link after
  `0 -` is replaced by `ivec2(0) -` in the one or two such lines of the built GLSL. The sign split all 153 variants with a
  recorded verdict (45 failing, 108 linking); an earlier "limit of 120-131 KB" and "a constant argument" were coincidences of
  the same line appearing and vanishing. Hypothesis: the context is lost because ANGLE crashes while reporting the error of a
  big program (a crash report points into its link task).
- **Fix:** a shader for WebGL has no int negation FXC could merge into a vector. A negative int is made of a float before the
  cast (`(int)(-_Loops.y)`, a `float2(1, -1)` factor before `(int2)`, `(int)(-(float)value)`: exact under 2^24) or as
  `intN(0) - v` of the whole vector. Size, loops and the count of statements do not matter for this.
- **Recognize:** in the built GLSL ([G2](#g2-arrays-indexed-at-run-time), `CompileVariant`), an assignment to two or more int
  components that starts with a bare int literal: `^\s*u_xlat[iu][0-9]+\.[xyzw]{2,4} = -?[0-9]+ [-+] `, and the same without a
  swizzle (`^\s*u_xlat[iu][0-9]+ = -?[0-9]+ [-+] `) where the temp is declared `ivecN` / `uvecN`. A scalar that is not a literal
  on the left of such an operation (not tried on the device) needs the types and is not searched for. The MSL of a shader, without a link
  (so the context survives): compile it alone on a page and read `gl.getExtension('WEBGL_debug_shaders')
  .getTranslatedShaderSource(shader)`; on Safari it returns the Metal source. The verdicts were taken with one fresh Safari per
  program. `MTLCompilerService` in the syslog shows
  `Compilation BEGIN` without `SUCCESS` and no text.
- **Status:** fixed in the water of pixel-swarm (pixel-swarm d005dbd23): on the test page of the iPad the full
  variant (111 KB) `LINKED`, the editor render identical pixel by pixel. In the game (a LAN build, autoplay to the second
  stage of the level, no shader error in the remote log): iPad 7, iPhone 11, Redmi 12 (Chrome); headless Chromium to the
  first stage. Guarded by the EditMode test `WebGlIntVectorNegationTests` of pixel-swarm over the real keyword sets of its
  shaders.

### G12. HLSLcc and Unity traps of a shader rewrite
- **An int and a float chosen by one `?:`.** FXC packs the choices of one condition into a vector `movc`, HLSLcc gives the
  vector one type: the int travels as the bits of a float - a denormal (`2.8e-45`), or `intBitsToFloat(0xFFFFFFFD)`, a NaN, for
  a negative - which a GPU is free to flush. `(int)(cond ? 98.0 : 97.0)` does not help, FXC
  folds it back to an int. Choose ints by arithmetic on the index (`97 + kind`) or in vectors of ints only. Recognize in the
  built GLSL: a new `intBitsToFloat` / `floatBitsToInt`, constants `e-45`.
- **A material remembers the default of a shader property.** After the default of a property changes in `Properties`, a
  material asset loaded before keeps drawing with the old value (its file on disk does not change, so nothing reimports it):
  `AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate)` of every material of the shader before comparing renders.

## Rendering and the Unity WebGL player

### R1. `Mesh.SetVertexBufferData` crashes the player
- **Symptom:** `memory access out of bounds` in the WebGL player (Safari and Chromium) after moving a dynamic mesh to
  `SetVertexBufferParams` / `SetVertexBufferData`.
- **Cause:** unknown (Unity 7000.0.0a10).
- **Workaround:** `SetVertices` / `SetColors` / `SetUVs` / `SetIndices` with lists kept between frames. pixel-swarm 97c482cc.
- **Status:** open (the cause); workaround in the game.

### R2. `targetFrameRate` on WebGL
- Unity 7000 maps the cap to emscripten: 30 -> `(RAF, 2)`, 20 -> `(RAF, 3)`, 45 -> `(setTimeout, 22 ms)` (only a rate the screen
  does not divide). Own rAF pacing on top of it gave 15 FPS.
- So `Application.targetFrameRate = 30` on a 60 Hz screen is `emscripten_set_main_loop_timing(RAF, 2)`:
  every second rAF, no `setTimeout`. It is a valid way to cap phones. pixel-swarm 302fa3e1 (`?fps=`), then an adaptive
  60 / 30 rule (36344436: frame work > 12 ms for 8 s -> 30, < 6 ms for 30 s -> 60, marks `fps.auto.*`).
- A 90 / 120 Hz screen halves to 45 / 60, not 30 (open: a divisor from the measured rAF rate).
- At a 30 FPS cap the per-frame CPU numbers of the same scene were ~1.5x higher on the stand (hypothesis: lower CPU clock
  between frames); compare A/B runs at the same cap.
- **Recognize:** `settings.target_fps`, `web.raf.dt.*` against `frame.dt.*`.

### R3. Tilemaps cost CPU every frame
- `TilemapRenderer` transforms its vertices on the CPU every frame (`TilemapRendererGeometryJob`) and culls per chunk: ~25-30 %
  of render CPU of a location with 15 tilemaps (~3-4 ms on a phone). The SRP Batcher mode of the tilemap made culling 3x worse.
- **Fix:** bake static tilemaps into meshes at load (one per tilemap and texture, bounds = the chunk sort key, the
  `TilemapRenderer` off). pixel-swarm 03b14144: render -30 %, cull -42 %, frames md5-equal. A framework helper is open.

### R4. Render targets recreated every frame
- Temporary render targets taken and released per frame cost ~14 `texParameteri` each on WebGL (154 a frame) plus
  `invalidateFramebuffer`. Keep persistent targets, recreated only on a size change. pixel-swarm 7c7ea5c8: 154 -> ~0.4.

### R5. `RectTransform.rect` every frame
- Reading `rect` of a root `RectTransform` lays its anchors out again (`UpdateAnchorPositionIfTransformChanged`): 0.55 ms a frame
  on the stand (CPU x6), ~1 ms on a phone. Compare the cheap inputs (`Screen.width/height`, `sizeDelta`) and recompute on a
  change. Fixed in the framework `ui` (`SafeAreaPadding`, 0280e2dd).

### R6. GC and the wasm heap
- The GC of WebGL is not incremental: every per-frame allocation ends in a stop of the frame. pixel-swarm keeps an allocation
  guard over a fight frame (`FrameAllocationTests`; a framework helper is planned); the network of the framework allocated
  ~4 KB a frame (fixed in ea2debce, `gc.allocated` 13-15 -> 1.5-3 MB per window).
- The wasm heap grows in 32 MiB arenas of mimalloc, opened by the CPU copies of textures at load; the growth changes address
  space, not touched memory (pixel-swarm research, `web.wasm.heap`). Judge memory by touched pages, not by RSS.

### R7. A lost context stops the player loop
- On `webglcontextlost` Unity stops its player loop: no samples, no logs, a black canvas; without a restore the session closes
  as `lost`. Nothing restores it by default.
- **Recognize:** `web.gl.context_lost`, marks `webgl.context_lost` / `webgl.context_restored`, the error `WebGL context lost`
  (framework telemetry 1cae4708; a loss outside a session comes with the next one). A black screen with the metric at 0 is not
  a loss: see [I1](#i1-the-stuck-browser).

### R8. Development build black with NRE every frame
- A development WebGL build of pixel-swarm drew a black screen (or an endless loading screen) with ~120
  `NullReferenceException` a second from `UnityEngine.Bindings.ThrowHelper` (one frame stack), over http and brotli alike;
  materials grew to 91k (95 MiB). The release build is fine. **Open**, cause not searched (candidates: profiler markers of the
  pipeline, `#if DEVELOPMENT_BUILD` collectors). A development build is not usable for frame measurements until then.

### R10. Works in the editor, fails only in the player
- `MD5` gives no hash in the WebGL player of 7000 (`ArgumentNullException ... b` at `System.Guid..ctor` inside a type
  initializer): pixel-swarm patched StaticEcs to FNV-1a.
- `GC.GetAllocatedBytesForCurrentThread` is not implemented on WebGL (an error on every call): editor only (pixel-swarm 474db85c).
- `MakeGenericType` / `Invoke` over value types is not AOT-safe under IL2CPP: register generic types explicitly.
- An `internal` `[Inject]` method of another assembly is never called by the container generator, the fields stay null; on IL2CPP
  it shows as `MethodAccessException ... on type ''` (mines-leader; the framework rule CINGR010: `[Inject]` methods are public).
- A registration under `#if UNITY_WEBGL && !UNITY_EDITOR` hides a missing asmdef reference in the editor (`CINGR003 Cannot resolve`
  in the player only, pixel-swarm 7a853138): build the WebGL player to check it.
- A prefab whose script failed to compile for a while keeps an import artifact without fields; it is not reimported when the script
  compiles again, and the built player gets empty fields ("AudioPlayer needs 2 music sources, has 0"). Reimport with `ForceUpdate`,
  then clean the player content and the build cache (peace-data-reborn).
- Stripping kept away from types created by reflection breaks them (before 0.8, Addressables: `MissingMethodException: Default
  constructor not found ... GroupOperation`, `catalog.bin` does not load): a type-level `link.xml` (mines-leader 067bc4ee, f315b978); never
  `preserve="all"` on own assemblies, it turns stripping off. A stripped engine submodule with `MissingSubmoduleErrorHandling:
  Ignore` fails without a clear error: use `LogError` while testing a stripping list.

### R9. IL2CPP code generation
- `OptimizeSize` (the WebGL default of the projects) against `OptimizeSpeed`: +391 KB br (+11 %) of wasm and a longer build
  for faster ECS-style code (pixel-swarm b9a7ebda). A build check for it is open.
- Burst compiles to wasm on WebGL (`lib_burst_generated.wasm`, `--opt-for-size`, no SIMD, the main thread only): expect a
  small gain against IL2CPP.

## iOS / iPadOS Safari

### I1. The stuck browser
After a GPU hang (for example [G1](#g1-flat-varyings-hang-the-gpu-on-ios)) **the whole Safari stays in it**: every WebGL page,
an empty Unity scene and another origin included, runs at 0.2 FPS. A reload does not clear it; only closing Safari from the app
switcher does. A test after the game has hung the GPU measures the stuck browser, not the variant under test.
- A wrong A/B came out exactly like this: an empty scene "also lagged" and `powerPreference: "high-performance"` looked guilty;
  from a fresh Safari both values ran at 60 FPS. `powerPreference` is not a cause.
- Signs: one frame every ~5.0 s, timers of the page stop too (a heartbeat from `setInterval` every 5 s), fences of WebGL2 stay
  unsignalled, no `webglcontextlost`, no console error.
- Rules: restart Safari before every run of an A/B; between variants check that the empty scene still runs at 60 FPS.

### I2. Screen Space - Overlay loses its projection
- **Symptom:** after ~20-30 s in the menu the UI vanishes (cards, text; buttons still take taps), the background, its glow and
  the draw calls stay; the "Update available" notice lost its text the same way. Every release of pixel-swarm since the start.
- **Where:** iPadOS 18.2.1 Safari, iPad 7. Not on iPhone 11 (iOS 16.6) nor Android.
- **Cause (proven up to the engine):** GL probes showed no texture deleted, all 48 textures byte-equal, the same command stream;
  only `glstate_matrix_projection[3]` and `unity_MatrixVP[3]` of the UI programs change from `[0,0,0,1]` to `[0,0,0,0]` (w = 0).
  Tied to the frame count, not the time: ~1100-1200 frames (18-22 s at 60, 34-38 s at 30). Hypothesis: a tier-up bug of the wasm
  JIT of Safari 18.2 in the engine code that builds the overlay matrix (cannot be tested: [B3](#b3-webwasm2023-cannot-be-turned-off)).
  Drawing the overlay through the game's renderer list (`rendersUIOverlay`) did not help: the engine sets its matrix inside.
- **Fix:** canvases in Screen Space - Camera with a camera whose matrices are computed in C#. Framework `IScreenCanvasCamera`
  (6c384eda: `AddScreenCanvasCamera`, explicit `AddScreenCanvasOverlay`), the framework canvases built from code through it
  (f38e9819); pixel-swarm c95939ae (`UICamera`). UI intact for minutes on the iPad.
- **Status:** fixed in the framework and pixel-swarm. A game that keeps Overlay canvases still has it on iPadOS 18.2.

### I3. JSPI is absent on iOS
- **Symptom:** every touch, Ctrl/Cmd + C or X halted the game with `SuspendError` (Chromium); after a fix for that, the first touch
  on iOS crashed with `callback(...).catch is not a function`.
- **Cause (proven):** the engine runs a Unity frame inside the emscripten input callbacks (`TouchHandler`, `DispatchKeyEvent`:
  `PreemptScheduledUnityFrame` -> an async import). Called without `WebAssembly.promising` it throws `SuspendError`. Without JSPI
  (Safari, every browser on iOS) the engine sets `WebAssembly.promising = f => f`, so the result is not a promise.
- **Fix:** register the touch and key callbacks through `jspiPromising`, and treat the result as a promise only when it is one.
  pixel-swarm 53e42cff, 838cc3d5 (`JspiEventHandlers.jslib`).
- **Test rule:** a change around JSPI runs twice in Chromium: as is, and with `delete WebAssembly.promising; delete
  WebAssembly.Suspending` in an init script (what iOS has). Chromium alone passed a plugin that crashed on every iPhone.
- **Status:** fixed in pixel-swarm; a candidate for the framework.

### I4. Thermal throttling in waves
- **Symptom:** iPhone 11 runs fine, then after 1.5-3 min of a run falls in waves (~110 s period, 40-55 s at 8-24 FPS); in a wave
  the CPU `update` grows 5-10x under the same load. Worse on the charging cable; off the cable 60 FPS. After two crashes of
  builds the phone stayed in slow phases (115-120 ms a frame) for every build, the control included.
- **Cause:** iOS thermal / power throttling (measured, not a code change). Android Mali-G52 showed none.
- **Rules:** never compare builds across a slow phase; take the control build in the same series; measure off the cable; cap
  phones at 30 or adapt ([R2](#r2-targetframerate-on-webgl)).
- **Recognize:** `frame.cpu.main` and `update` jump at the same body count; the waves repeat in time, not with the content.

### I5. iPadOS reports itself as a Mac
- iPadOS sends the user agent of a Mac by default: `Application.isMobilePlatform` is false there. A Mac has no touch points, so
  the platform module checks `navigator.maxTouchPoints` (`PlatformOptions.SetMobileCheck`, `WebShell.UseMobileCheck`; telemetry
  names the OS `iPadOS`, ffb6627b). Code that reads `Application.isMobilePlatform` directly hides mobile controls on iPad.

### I6. No Fullscreen API on iPhone
- iPhone Safari and the iOS Telegram WebView have no Fullscreen API for a page (only for `<video>`): `requestFullscreen` is
  undefined. iPad has it. Safari before 16.4 has only the `webkit` prefixed names (`webkitfullscreenchange`). Framework
  `WebPage.IsFullscreenSupported`.

### I10. `Module` is not a global in Safari
- `Runtime.evaluate` of `Module.canvas` from the inspector on iPad Safari: `ReferenceError: Can't find variable: Module`. Code
  inside `framework.js` (a jslib) sees `Module`; from the page use `unityInstance.Module` (put `window.unityInstance` in the
  template) or the canvas element. Why Safari differs is not explained.

### I7. Slow network of a device
- iPad 7 loaded the game in 38 s (wasm 3.7 MB in 34.7 s) while Android took 2.8 s in the same minute: the network of that device,
  not the build. **Recognize:** the startup trace (`Page` root: files with sizes, cache, timings) of the slow device against
  another one on the same release.

### I8. What Safari lacks
- No `EXT_disjoint_timer_query_webgl2` (`web.gl.timer_query` = 0: no GPU time), no `performance.memory`, no Page Lifecycle
  (`freeze` / `resume`), `visibilitychange` sometimes late (use `pagehide`), no WebAssembly exceptions before Safari 15.2.

### I9. Fullscreen and bars of iOS Safari
- The exit cross of element fullscreen on iPadOS Safari (top left, ~80x68 CSS px) is not reported in `env(safe-area-inset-*)`:
  raise the top inset while an iOS page is fullscreen (pixel-swarm 3510221e, 72 px).
- Safari on iPhone keeps its bars in landscape (~2556x1019 of 2556x1179): a UI scale computed for the full screen comes out one
  step lower.

## Android Chrome / ANGLE

### A1. The cost of a GL call through ANGLE
- On Mali-G52 a frame of ~1000-1060 GL calls costs ~10 ms of the main thread even with an empty scene: `postlate` 10.7 ms at
  ~50 bodies. Fewer calls is the only lever: no per-frame `texParameteri` ([R4](#r4-render-targets-recreated-every-frame)), fewer
  `bufferSubData`, baked static geometry ([R3](#r3-tilemaps-cost-cpu-every-frame)), batched instances.
- The render resolution costs no main-thread time there (native 2460x1080 against 820x360). No timer query on Mali, so the GPU is
  hidden under a 30 cap: compare `?fps=60` runs at High and Low.
- **Recognize:** the GL call counter of the stand (headless Chromium, CPU x6), `web.gl.buffer_data_calls`, `render.draw_calls`.

### A2. Shader link on Mali
- The link of a program on Mali / Chrome costs tens to hundreds of ms (estimate); see [G5](#g5-a-program-is-linked-at-its-first-draw).

### A3. Divisor of the resolution jumps at a threshold
- With a whole divisor of the device pixel ratio (`renderPixelRatio = dpr / floor(shortSide / limit)`) a phone 0.75 px above the
  threshold renders 1081 rows, one below it 540: two similar phones get 4x apart in pixels. See [P1](#p1-device-pixel-ratio-of-phones).

### A4. INVALID_OPERATION at load and after a resize
- **Symptom:** one `WebGL error INVALID_OPERATION` in the first seconds (menu, the load of a run; 1-5 per session on iPad, iPhone,
  Android), and on Android 0.1-0.3 s after each `page.resize` of a fullscreen enter / exit.
- **Cause:** **open**. Not reproduced in headless Chromium (8 runs, `getError` after every call). Candidates (hypothesis): MRT with a
  depth target on a colour-only RT, ASTC uploads of mips not a multiple of the block, a blit of the engine on a canvas resize.
- **Find it:** open the page with `?glcheck=1` on the device: every call is followed by `getError`, the mark
  `webgl.check.<code>` names `call(arguments)` with the frame (framework b201c829, 7fdf2e8a). `web.gl.emscripten_errors` /
  `webgl.emscripten.<code>` separate the errors emscripten records itself (`GL.recordError`) from the context's. Or Safari Web
  Inspector: WebKit prints `WebGL: INVALID_OPERATION: <function>: <reason>`.

### A5. Endless spinner after a deploy
- **Symptom:** Android, right after an upload: an endless spinner after the Unity progress bar, no telemetry. Rolled back, could not
  reproduce (headless mobile, a profile with the old build, fullscreen during the load); a second upload of the same build loaded.
- **Cause:** **open** (hypothesis: a mix of old and new files from a cache during the release switch). Protocol hash unchanged;
  telemetry sees nothing that stalls before the auth of the socket, hence the page-level startup trace.
- Similar: Telegram on iPhone 15 Pro loaded forever while the same build opened in Chrome; killing Telegram and clearing its
  cache is the advice (hypothesis: a WebView state).

## Page, canvas, DPR, fullscreen

### P1. Device pixel ratio of phones
- A phone at DPR 2.75-3 renders ~3 Mpx at the native ratio. iPad 7 at native (2160x1480) ran a run at 40 FPS against 60 at
  1080x740, GPU-bound; Android Mali-G52 lost no CPU at native, and the low ratio looked blurry to the player.
- pixel-swarm: a whole divisor with a 360-unit limit, modes Auto / Low / High (Auto = Low on iOS / iPadOS, native elsewhere),
  `?dpr=native` / `?dpr=<n>`, `settings.resolution` (de6bf4e8); peace-data-reborn caps mobile at `dpr / ceil(dpr / 1.5)` (a whole
  number of physical pixels per canvas pixel, 42e8e762b); moving into the framework `web` / `settings` (web_perf_tools step 2).
- **Recognize:** `render.width` / `height` / `pixels`, `render.dpr`, `render.canvas_*`.

### P2. The divisor chosen before fullscreen
- Chrome on Android starts with the address bar (337 of 393 CSS rows): a divisor computed once before the start of Unity kept the
  low value after the page went fullscreen (1230x540 instead of 820x360). Recompute it on every resize and write
  `Module.devicePixelRatio` of the running instance. pixel-swarm 5263aeba.

### P3. A build over plain http
- A browser does not decode `Content-Encoding: br` outside a secure context (`ERR_CONTENT_DECODING_FAILED`): a LAN build for a
  phone is uncompressed (`WebGLCompressionFormat.Disabled`, no decompression fallback), and project steps that rewrite the `.data`
  must accept the uncompressed file.
- "Insecure connection not allowed" / "Non-secure network connections disabled", then `InvalidKeyException` (the catalog did not
  load) and an endless loading screen: `PlayerSettings.insecureHttpOption = AlwaysAllowed`. A check on `localhost` passes because
  `localhost` is a secure context; test on the LAN address (with a build profile active the setter
  writes into the profile's override).
- Over http `navigator.clipboard` does not work, and the Cache API is absent (`[UnityCache] Cache API is not supported`, only a log;
  the content bundles are then fetched on every opening, B15).
- The phone reaches the machine only with its VPN off. Load a probe with a cache buster
  (`document.write('<script src="/probe.js?t=' + Date.now() + '">')`), or Safari serves an old one.

### P4. The production meta refuses a LAN origin
- The production meta answers 403 to the Origin of a LAN build (`http://192.168.x.x:port`) and to a client with an unknown protocol
  hash: the build goes offline and sends no telemetry. Measure LAN builds by their own logs (a remote log endpoint, the probe), or
  deploy client and backend together. A stand that runs a build headless must cut the meta host
  (`--host-resolver-rules=MAP <meta host> 127.0.0.1:1`), or its sessions land in the production telemetry.

### P5. Page state that waits on rAF
- A loading-screen fade advanced only on animation frames: with the window off screen (another workspace, covered) rAF stopped
  while the page still counted as visible (no `page.hidden`), and the loading wheel never went away (`TimeoutException: Exceed
  Timeout` in `WebLoadingScreen.Show`, `frame.fps` 1). Drive page animations by `performance.now()`, never wait on rAF for state
  (pixel-swarm 7eb7e90f).

### P6. The mobile page shell
- Unity WebGL does not know `Screen.safeArea`: the insets come from `env(safe-area-inset-*)` of a probe element, which needs
  `viewport-fit=cover` (framework `web`: `AddWebSafeArea`).
- Page gestures: `touch-action: none`, `overscroll-behavior: none`, `position: fixed`, `preventDefault` on `touchmove` and the iOS
  `gesture*` events (pinch), double tap. Fullscreen and `screen.orientation.lock` only from a user gesture; the lock result is
  ignored (peace-data-reborn 42e8e762b).
- The Unity runtime calls `navigator.mediaDevices.enumerateDevices`: Telegram Desktop on Linux then asks for the camera. Stub it to an
  empty list in the shell (peace-data-reborn c53569ea9).

### P7. Storage, clipboard, sockets of the page
- `persistentDataPath` is IDBFS: MEMFS in the memory of the tab, written to IndexedDB only on a sync (without auto sync only on
  `PlayerPrefs.Save`). The bare `autoSyncPersistentDataPath` wiped IndexedDB at start (a sync in the middle of the load) and bulk
  writes blocked the main thread up to 76 ms a sync. storytale-workshop 05a0fd124: `PersistentDataSync.jspre` (no sync during the
  load, at most once a second, a sync on `visibilitychange` / `pagehide`); re-check it after every Unity upgrade.
- Call `PlayerPrefs.Save()` after every write or a closing tab loses it. `Application.dataPath` is the page URL: keys built from it
  differ between `/` and `/tg/` (peace-data-reborn f6f6fd317).
- The clipboard of a WebGL build needs a jslib over `navigator.clipboard` (with an `execCommand` fallback; none over plain http).
- A jslib WebSocket: `Connect` hung when the connection was refused or closed around `onopen`, and `onclose` after `onerror` was
  lost (peace-data-reborn d4ebdac91, 56d715397, 57fd10100; framework `network` `JsWebSocket`).
- A Web Worker's `.js` and `.wasm` without a hash in the name can pair files of two deploys: add `?build=<buildGUID>` to both and keep
  them in `StreamingAssets` (storytale-workshop 05a0fd124).

## Audio

### AU1. The silent mode of iOS mutes WebAudio
- **Symptom:** no sound on iPhone / iPad while the AudioContext is unlocked by a tap, clips decode (AAC, no errors) and the signal
  reaches the destination.
- **Cause (proven):** `navigator.audioSession.type` is `auto`: WebAudio follows the silent switch of the device.
- **Fix:** `navigator.audioSession.type = 'playback'` before `createUnityInstance`. Framework 10ca0bf3: `audio-session.js` first in
  the `<head>` of every WebGL build (`WebAudioSessionInjection`).
- **Recognize:** marks `audio.context.<state>` (`interrupted`: Safari took the audio away, a call or another tab).

### AU2. Clips on WebGL
- Every clip is decoded by WebAudio into float32 at the rate of the AudioContext (Unity's `Audio.js` decodes everything on
  Chromium): `Streaming` import saves nothing, and the decoded size is the cost. pixel-swarm runs the context at 24 kHz
  (`AudioSampleRate.jspre`, 07398c06): menu 37 -> 18.5 MB.
- The WebGL importer re-encodes every clip to AAC; mono and sample rate change nothing, quality has three steps. Small audio ships
  as `*.mp3.bytes` decoded through a blob URL (`EncodedAudio` of the core).
- A clip from `UnityWebRequestMultimedia.GetAudioClip` reports `loadState == Unloaded` (not `Loading`) while the browser decodes it:
  wait on both, with a timeout. `Play()` does not load the data: preload audio data on import (mines-leader f5cff525).
- Autoplay: `Audio.js` resumes the context on the first mousedown / touchstart and starts the pending sounds shifted by the elapsed
  time (menu music starts mid-track).
- A gapless mp3 loops without a seam in Chromium (the LAME header); FMOD of the editor and the desktop players ignores it (~0.39 s hole).
- WebKitGTK without a GStreamer mp3 decoder (Telegram Desktop on Linux) fails with `EncodingError`: open, by decision.

## Build, emscripten, wasm, deploy

### B1. wasm-opt drops every try/catch
- The post-build `wasm-opt -O3` of the Web Stripping Tool without `--enable-exception-handling` assumes calls never throw and
  removes every try/catch of a `-fwasm-exceptions` build: any C# exception, even a caught one, halts the release player.
  Fixed in the fork (b599cf0f). After an update of the tool, check that `wasm-dis` of the shipped wasm still has `(try`.

### B2. Submodule stripping silently skipped
- On Unity 7000 the tool's `[InitializeOnLoad]` cannot load the settings asset on the first domain reload: the empty path was saved
  and post-build stripping stopped (raw wasm 15.6 -> 23.5 MB in mines-leader). Fixed in the fork (d6bb4d5e, retry with
  `delayCall`), and `Internal.WebGlBuild` now loads the settings itself and fails without them (954068cf, c061e543).

### B3. `webWasm2023` cannot be turned off
- Unity 7000.0.0a10 does not link without `webWasm2023` (`undefined symbol: $jspiPromising`): SIMD and native exceptions cannot be
  switched off to test a JIT bug. The engine is precompiled; emscripten flags do not touch its code.

### B4. wasm-ld crashes at random
- **Symptom:** a WebGL build with LTO (`DiskSizeMaxLTO`, `-Oz`) aborts with heap corruption or `SIGSEGV` inside `wasm-ld`
  (`Caught fatal signal`, `wasm-ld ... SIGSEGV` in `Editor.log`); it can take the whole editor down. Seen as `wasm-ld ... failed
  (received SIGSEGV (-11))` in the LTO pass `WebAssembly MC Lower Pre Pass`, SIGABRT `corrupted size vs. prev_size`, or `LLVM ERROR:
  SmallVector unable to grow`, 58-87 s into the link, on Unity 7000.0.0a9 and a10, with 18-39 GB of RAM free. A failed build also
  deletes its output folder.
- **Cause:** **open**, random (Unity 7000 alpha toolchain). The same tree builds on the next try.
- **Workaround:** rerun the build; an automated build reports it instead of looping on it (mines-leader and peace-data-reborn
  `deploy-client` skills). A batchmode build in its own copy of the project keeps the editor out of it.

### B5. Bee cleans the previous output
- Building a second target (`website` after `web_telegram`) into another folder deleted the `Build` folder of the first: Bee cleans
  the previous output. Deploy each target right after its build, or build each in its own copy of the project (framework
  `deploy-client`, 6f49c1df).

### B6. Addressables built for the editor target
- **Before 0.8** (Addressables are gone since; the content bundles are built with every player for its target, B15).
- A build profile started while the editor was on another target switched the player to WebGL but built the Addressables for the
  old one: the catalog pointed at `aa/StandaloneLinux64/` and the page failed with "Failed to decompress data for the AssetBundle".
  Switch the editor to WebGL first; check the catalog of the output.

### B7. The wasm is written after "Build Finished"
- `Build Finished, Result: Success` comes before the stripping pass: for a moment `*.wasm.br` is 0 bytes, and a page started then
  fails silently, like a hang. Wait for the build job and a wasm of real size.
- Output folders keep the bundles of earlier builds: delete the folder before a build.

### B8. boot.config of a web build
- A web build ignores `ProjectSettings/boot.config` and the player arguments for `memorysetup-*` keys: the line has to go into the
  `boot.config` inside `.data` (a project build step rewrites it). `BrotliStream` compiles in the editor but fails at run time with
  `TypeLoadException`. An exception in such a post-build step is only logged and the build still reports Success.
- pixel-swarm turns mimalloc off this way (`memorysetup-main-allocator-mimalloc-enabled=0`, 07398c06): its 32 MiB arenas were never
  given back; run heap 174-209 -> 138 MiB (not checked on phones).

### B9. Bundle caches keep old bytes
- **Before 0.8** (`BundleUrlVersioning` is gone; the files of the content bundles are named by their hash, B15).
- A bundle name with Append Hash is computed from its input assets, not from its bytes: a Unity upgrade changes the bytes under the
  same name (`unitybuiltinassets`), and two caches served the old file -> "CRC Mismatch": the Addressables cache
  (`UseAssetBundleCache: 0`, `UniqueBundleIds: 1`) and the UnityCache of the WebGL loader in IndexedDB keyed by URL, with bundles
  served `immutable`. Fix: `BundleUrlVersioning` adds `?crc=<crc>` to every http bundle URL (mines-leader, now in the framework).
  `immutable` is safe only while the URL carries the CRC.

### B10. Client before backend
- A client with a new protocol hash went out while the backend kept the old one: every player got the "version outdated" notice
  (close code 4001). Compare the protocol hash with the deployed backend before an upload; a probe socket with
  `?protocol=<hash>` closed with 4001 means that hash is refused.

### B11. What keeps code in the build
- One managed reference keeps a whole engine module in the player: `versionDefines` of UGUI / URP / RP Core / UniTask
  (`PACKAGE_PHYSICS`, `ENABLE_UIELEMENTS_MODULE`, ...) are removed in the forks; IMGUI `OnGUI` kept the IMGUI and TextCore modules
  (95fcb47d); NuGet MemoryPack kept ~200 KB br of formatters (trimmed fork, 73237c8a); Addressables wrote `preserve="all"` for
  bundle types (fork, before 0.7). Find the reason a module stays in `Library/Bee/artifacts/WebGL/ManagedStripped/wasm/UnityLinkerToEditorData.json`.
- `webGLDebugSymbols: 2` embeds the names into the wasm (+11 MB raw, peace-data-reborn dev profile).
- An editor left on the WebGL target breaks the Multiplayer Play Mode clone (black, only core shaders): switch back to the desktop
  target after a WebGL build (peace-data-reborn).
- LZ4 hides empty pixel art in bundles: judge texture cost by unpacked size (up to 70x the bundle size).

### B12. The server of the build
- Brotli without the decompression fallback: the server sends `Content-Encoding: br` and the right MIME type itself (nginx locations
  for `\.wasm\.br`, `\.js\.br`, `\.json\.br`, `\.br`, the typed ones first; `gzip off`), or the loader fails with "Unable to parse
  Build/<name>.framework.js.br". A second build under a prefix (`/tg/`) needs a `^~` location that repeats the rules (mines-leader
  5711f739, pixel-swarm `client/deploy/README.md`).
- With "Name Files As Hashes" off the payload names stay between releases: a cached `framework.js` met a new wasm (`RuntimeError:
  function signature mismatch`). `Cache-Control: no-cache` on every payload, `no-store` on `index.html` (mines-leader 5367b07e,
  5535d20a).
- nginx behind Traefik: `listen [::]:80` (`localhost` resolves to `::1` first, the healthcheck failed -> 503), `absolute_redirect off`,
  relative symlinks (mines-leader 287355fe).
- The meta checks `Origin` on the WebSocket handshake too: a new client domain goes into `CORS_ALLOWED_ORIGINS` first, and a value set
  in Coolify overrides the default of the compose (the client silently went offline). Check with `curl -X OPTIONS -H 'Origin: ...'`.

### B13. Native code in a WebGL build
- A `DllImport("__Internal")` of a jslib function outside `#if UNITY_WEBGL && !UNITY_EDITOR` breaks a standalone IL2CPP link
  (`ld.lld: undefined symbol`, peace-data-reborn).
- A plugin source (`.cpp` / `.c`) put into `Assets/Plugins` before its `.meta` is compiled into every IL2CPP platform: `duplicate
  symbol` against the same library of another package in the WebGL link. Write the `.meta` (platforms) first (storytale-workshop
  b1d603421).

### B14. Submodule stripping on the .NET 10 BCL
- From 7000.0.0a16 the BCL calls ICU at the start of the player (`Il2CppData/Icu/icudt.dat`): keep the submodule `ICU`. The group
  `Advanced Text Generator Support` of the stripping tool contains ICU (with HarfBuzz and TextCoreTextEngine), so stripping the
  group strips ICU too; strip `TextCoreTextEngine` alone. The `System.*` definitions of the tool target the old mscorlib and
  match nothing on .NET 10. Sizes per Unity version and the profiling of unused submodules: `client/measurements` (ANALYSIS.md).

### B15. The content bundles on the web
- Since 0.8 the content loaded after the start is AssetBundles of the engine (`CONTENT.md`): `StreamingAssets/Content/<name>.<hash>.bundle`
  (`.gz` with `IsGzip`), named by their hash, so a new build is a new URL and `immutable` is safe. A bundle is fetched whole into MEMFS
  (`temporaryCachePath/Content`), loaded by `AssetBundle.LoadFromFile`, deleted when unloaded; the Cache API keeps it by URL and deletes
  at start the entries of other builds. A second opening makes no request to `Content/`.
- The CDN of Devvit takes the type from the magic bytes and never compresses `UnityFS`: ship a fetched bundle of sprites and prefabs with
  `IsGzip` (the page unpacks it with `DecompressionStream`, Safari 16.4+); a body the browser decoded already is taken as it is.
- Without the Cache API (plain http, P3) every opening fetches again, with one log line. `IsPreloaded` puts the bundle into the `.data`.
- Before 0.8 it was one content directory fetched by Range requests over an archive of zeros (MEMFS times pinned, `cache: 'no-store'`):
  all that is gone with it.


## Telegram WebView

### T1. A Telegram build outside Telegram
- Outside Telegram the build needs `?tgDebug=1&tgInitData=query_id%3Dtest`; with empty init data it stalls on the loading screen,
  which covers the canvas, so a touch test passes falsely.
- The iOS Telegram WebView is WebKit: everything of the iOS section applies, and it has no Fullscreen API ([I6](#i6-no-fullscreen-api-on-iphone)).
  Telegram on iOS does not name itself in the user agent; its bridge does (telemetry, ffb6627b). The Android WebView is Chrome
  (`wv` in the user agent): the Android section applies.
- Emulation does not reproduce everything: cards took taps in headless Chromium while they did not react on a real Android phone in
  Telegram.

### T2. The Mini App shell
- Before Unity starts the shell calls `ready()`, `expand()` and `disableVerticalSwipes()` (Bot API 7.7): without the last one a swipe
  on a stick collapses the Mini App. `requestFullscreen()` only on android / ios.
- The safe area is `safeAreaInset + contentSafeAreaInset`, updated on their change events and on `fullscreenChanged` /
  `viewportChanged`.
- The Mini App under `/tg/` of the web domain has the Origin of that domain ([B12](#b12-the-server-of-the-build)).
- Debugging on a phone: Eruda cannot be turned on from the URL there; use `chrome://inspect` of a desktop with "Enable WebView
  Inspection" on Android (peace-data-reborn 42e8e762b).

## Diagnosis toolkit

- **Telemetry of the client** (`modules/telemetry`, read with the `client-telemetry` skill): `frame.*` (phases, `frame.spike.<phase>`
  from 250 ms), `web.raf.dt.*` (the browser's own frame), `frame.gpu.query` where the timer query exists, `render.*` (buffer size,
  DPR), `web.gl.*` (errors, emscripten errors, compiles / links, buffer and texture uploads, context loss), the marks `webgl.*`,
  `page.*` (hidden, pagehide, fullscreen, resize), `audio.context.*`, `memory.*`, `web.wasm.heap`, `settings.*`. A session that
  died is reported by the next start (`SessionStart.Previous`). `client/modules/telemetry/CLAUDE.md` lists them.
- **`?glcheck=1`:** `getError` after every call of the context, the failing call named in `webgl.check.<code>`. Slow: a diagnostic
  session, not a measurement.
- **Startup trace:** the `Page` root (navigation, files with sizes and cache) to the first frame; `AssetLoads` marks `load.*`.
- **Finding the guilty draw on a device** (a probe script wrapping `WebGL2RenderingContext`, first in `<head>`, driven by the query
  string):
  1. Skip every draw (`drawElements` / `drawArrays` return at once). No hang means the drawing is guilty (the screen is black, that is
     expected).
  2. Skip the draws of groups of programs, then of one.
  3. **Tag a program by its source, not by its number**: the order of `createProgram` changes between runs. Mark it at `linkProgram` by
     a uniform name only it has.
  4. Cut the program down by substitution in `shaderSource`: a trivial fragment shader with the same outputs; `gl_Position` outside the
     clip volume; a cap on the indices per draw; the suspected feature removed (`flat`, an indexed array, `GL_EXT_shader_framebuffer_fetch`).
  5. Rule out the buffers: shadow the index buffers and compare the largest index of every draw to the capacity of the enabled
     attributes. `vertexAttribPointer` must keep the enabled flag set by `enableVertexAttribArray`.
  The same probe read the uniforms of every program per frame for [I2](#i2-screen-space---overlay-loses-its-projection), and counted
  the GL calls of a frame for [A1](#a1-the-cost-of-a-gl-call-through-angle).
- **A device bench without hands (iPhone / iPad over USB from Linux):** Developer Mode (appears after a developer tool paired with
  the phone) and the Developer Disk Image of its iOS, mounted with `ideviceimagemounter` / `pymobiledevice3 mounter auto-mount` after
  every reboot; restart Safari with `pymobiledevice3 developer dvt launch --kill-existing com.apple.mobilesafari` (navigate to
  `about:blank` first: Safari restores the last tab); open a URL and run JS through `ios_webkit_debug_proxy` and
  `Runtime.evaluate`; screenshots with `pymobiledevice3 developer dvt screenshot` (a screenshot of a WebGL canvas through the inspector
  is black). The game plays itself by a query switch (pixel-swarm `?autorun=1`) and posts its console, errors and a heartbeat every
  2 s to a local endpoint; a heartbeat gap of 3 s or more is a stall. Auto-Lock off; nobody touches the phone during a series. Moving
  into the framework as `tools/scripts/web-bench/` and the `device-lab` skill (web_perf_tools step 1).
- **Headless stand:** Chromium `--headless=new --use-angle=swiftshader` over CDP: CPU throttling (x6 for a phone), host mapping to a
  local build, the meta cut, touch emulation, a CPU profile of the wasm with names (the names are in
  `Library/Bee/artifacts/WebGL/build/debug_WebGL_wasm/build.wasm`; stripping renumbers functions, so align the live wasm with the
  `.wasm.br.bak` of the stripping tool). It does not reproduce WebKit, Mali or the thermal state of a phone.
- **Safari Web Inspector** (a Mac, or `ios_webkit_debug_proxy`): the WebKit console names the GL function and the reason of an error.
