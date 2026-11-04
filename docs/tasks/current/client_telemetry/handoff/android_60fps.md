# Android Mali-G52 MC2: how far from 60 FPS (2026-11-04)

Device: Chrome 156, Android 10, Mali-G52 MC2, 8 cores, 2460x1080 native (895x393 CSS @2.75).
Budget at 60 FPS: 16.7 ms, in practice ~14 ms of main thread (the browser and the compositor take the rest).

## CPU does not depend on the resolution
| session | build | render | bodies avg | cpu.main | update | postlate | game.render.total |
|-|-|-|-|-|-|-|-|
| d4ee89fa | before 39/40 | 820x360 | 36 | 24.6 | 5.7 | 14.5 | 13.1 |
| eacd26c2 | e8a88090 | 820x360 | 158 | 25.9 | 8.7 | 15.5 | 13.1 |
| a5c40bd9 | f4a1a986 | 2460x1080 | 62 | 20.2 | 6.3 | 12.5 | 10.2 |
| 24545363 | c8becfcb | 2460x1080 | 47 (max 209) | 17.2 | 5.1 | 10.7 | 8.8 |
Native resolution costs no visible main-thread time; steps 39/40 cut ~4 ms of render.

## CPU gap to 60 (main thread, c8becfcb)
- ~50 bodies: 17.2 ms -> need ~14: -3..-4 ms.
- ~185 bodies: 20 ms -> -6 ms.
- ~300 bodies: ~30 ms (f4a1a986) -> -16 ms.
Floor: postlate 10.7 ms at ~50 bodies is mostly the GL calls of Unity through ANGLE (~1000 calls/frame, step 37), update ~5 ms.
Known cuts: step 44 (~-1..-2 at a crowd), step 45 (~-1 always), swarm bodies as one mesh (-3..-4 at 300), DamageNumbers QuadMesh (-0.1..-0.3).

## GPU: unknown
No timer query on Mali (web.gl.timer_query=0) and every run is capped at 30, so the GPU time is hidden under the cap.
Measure: two runs with ?autorun=1&fps=60, one at High (native), one at Low (895x393). frame.dt.p50 against frame.cpu.main:
dt ~ cpu -> CPU-bound; dt(High) >> dt(Low) -> the resolution costs GPU.
