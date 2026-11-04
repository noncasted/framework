---
name: device-lab
description: Measure and debug the WebGL client of a game outside the player's hands - the headless Chromium stand over CDP (a phone emulated, CPU throttled, the production host mapped to a local build, the meta cut), A/B series on a quiet machine, CPU profiles of the wasm with function names, GL call counts and the GL probe, and real devices over USB - iPhone / iPad Safari (ios_webkit_debug_proxy, Safari restart, screenshots, GL probes of uniforms, textures and buffers), Android Chrome over adb, local builds served to phones on the LAN with a remote log. Use when the user asks to run the stand, a headless run, a wasm profile, an A/B of two builds, to open or restart Safari on the iPhone or iPad, to test on a phone, Android, adb, a LAN build, a GL probe, or why a build is slow or broken on a device.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /device-lab, device-lab, стенд, headless, прогон на стенде, профиль wasm, профиль с именами, A/B сборок, сравни сборки, iPhone, iPad, Safari, перезапусти Safari, залип WebGL, Android, adb, сборка в LAN, на телефоне по сети, GL-пробник, пробник uniform, скриншот с телефона, headless stand, wasm profile, GL probe, LAN build
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Pick the section, read its file and run it.

# Device Lab

The scripts are in `tools/scripts/web-bench/` of the framework; from the root of a game:

```
WB=tools/framework/tools/scripts/web-bench    # in the framework itself: WB=tools/scripts/web-bench
```

`ls $WB` empty or missing: the submodule of the game is older than web-bench - update it (`update-framework`) or point `WB` at a
checkout of the framework that has it (`WB=/projects/framework/tools/scripts/web-bench`).

Every script has a header and `--help`: read it before the first run of a script, the files below only choose and connect.

| Need | Section |
|------|---------|
| a measure or a bug reproduced in headless Chromium: CPU profile, GL calls per frame, a recorded frame, screenshots, a scenario over the JS API of the game, an A/B of builds | [stand.md](stand.md) |
| reading a `.cpuprofile`: self / inclusive top, callers, children, player-loop phases, profiles side by side, the cost per object count; the debug wasm with function names | [profile.md](profile.md) |
| iPhone / iPad Safari over USB: open a page, eval JS, restart a stuck Safari, screenshots, a production run, GL probes | [ios.md](ios.md) |
| a local build on a phone of the LAN with its console in a log file | [lan.md](lan.md) |
| Chrome on an Android phone over adb: open, eval, screenshot | [android.md](android.md) |

**The telemetry of a session is not here.** A production run on a device (`ios/run-res.sh`, a run the user starts) is read with the
`client-telemetry` skill: do not repeat its analysis. The stand and the LAN builds send no telemetry at all (below).

## Configuration

Values of the game: `.claude/device-lab.json` at the root of the game (no values of a game in the scripts or here):

```json
{
  "host": "game.example",              // production host of the page; the stand maps it to the local build
  "metaHost": "meta.game.example",     // the host the build really opens its meta / telemetry socket to; mapped to 127.0.0.1:1 (cut)
  "builds": {"web": "client/Build"},   // named builds for --build / --variant: folders from the root, each with index.html and Build/
  "autorunQuery": "?autorun=1",        // query that makes the game play itself (--autorun, run-res.sh, probe-run.py --autorun)
  "scenarioApi": "stress"              // window.<scenarioApi>.<method> called by the "call" steps of a stand scenario
}
```

`host` and `metaHost` are required, the rest only for the options that use them. `metaHost` must be where the build really goes:
check `summary.meta.sockets` of the first run of a build - a socket to `host` itself (a warning `WEBSOCKET TO THE PAGE HOST`)
means the build takes an empty or relative meta url and `metaHost` is not what it uses (the local server got it, nothing left
the machine, but the meta check saw nothing). The notes of the game (which build is fresh, its
rows file for `profile.py`) are in `.claude/project-skills.md` § device-lab.

Values of the machine: `~/.config/device-lab/devices.json` (`DEVICE_LAB_DEVICES` overrides), data in `~/.local/share/device-lab`
(`DEVICE_LAB_HOME`: `pmd/` venv of pymobiledevice3, `ddi/<version>/`, `certs/<host>/` of the stand, `run/` pid files and logs):

```json
{"devices": [
  {"name": "iphone11", "kind": "ios", "udid": "<udid>", "proxyPort": 9222, "ddi": "~/.local/share/device-lab/ddi/16.6"},
  {"name": "ipad7", "kind": "ios", "udid": "<udid>", "proxyPort": 9223, "ddiAutoMount": true, "tunnel": "userspace"},
  {"name": "android-mali", "kind": "android", "serial": null, "cdpPort": 9224}
]}
```

- ios: exactly one of `ddi` (a folder with `DeveloperDiskImage.dmg(.signature)`, iOS <= 16) and `ddiAutoMount` (iOS 17+);
  `tunnel: "userspace"` for the developer services of iOS 17+ without root. `ios/setup.sh` writes a template and validates it.
- android: `serial` null = the only connected device; `cdpPort` the local port of the adb forward (default 9222, which the proxy
  of an iPhone takes too: give each device its own port).
- Ports are per device and per stand: two proxies, a stand and an adb forward can run at once only on different ports.

## A build for the stand and the devices

The stand and `lan/` need a full build: `index.html` and its `Build/*.{loader.js,framework.js.br,wasm.br,data.br}`. An `index.html`
without `Build/` (an output folder Bee emptied, see `deploy-client`) fails the stand with a 404 and exit 1: that is right, take
another build.

- A fresh build without the editor and without an upload: the script of `deploy-client` from the root of the game,
  `KEEP_COPY=1 tools/framework/tools/scripts/deploy-client.sh <target> --no-deploy [--notest]` (in the background, minutes). It takes
  the batchmode slots `/tmp/claude-1000/unity-build.slot<i>.lock` of the machine (never the old `unity-batchmode.lock`, never a
  Unity of your own beside them), prints `Copies kept: <work>`; the build is `<work>/<target>/<output of the target>` and its
  Unity project `<work>/<target>/client` (the debug wasm of `profile/debug-wasm.sh`). Copy the build out (`cp -a --reflink=auto`),
  then delete `<work>`: each copy is a full tree. `--notest` is fine here, nothing is uploaded.
- An existing build of the game: `builds` of `.claude/device-lab.json` (the notes of `project-skills.md` say how fresh it is).
- Never build in the open editor of the user or in the working tree of another session.

## Rules learned the hard way

- **No stand session reaches the production telemetry.** The stand maps the meta host to `127.0.0.1:1`, every host but the page
  and the meta to `~NOTFOUND`, and stops with exit 3 the moment a request to the meta or another host gets an answer; `--prod` / `PROD=1` lifts that only when the user asked for production explicitly. Once
  the stand leaked sessions (Linux Chrome155, fake Chrome140 Android10) into the telemetry of a game. After a change of the stand
  run `--leak-test`: it must end with exit 3.
- **A quiet machine for every measure.** The 1-minute load average below 12 (`stand/quiet.sh`, built into `--quiet` and
  `batch.sh`): a load of 31 from builds of other sessions spoiled a whole A/B once. Below 12 is still noisy: identical builds gave
  ±30% (busy 2.5-4.8 ms/frame) on this machine. A difference under 30% needs `--max-load 6`-`8` and 3 runs per point, reported as
  the median and min-max. An A/B runs on one machine, interleaved before / after (`stand/batch.sh`).
- **One Chromium stand at a time** unless the user asked for parallel ones (then another `--port`): before a series
  `pgrep -fa remote-debugging-port` - wait for a stand of someone else to end, or name it in the report.
- **At 60 fps the frame time is the rAF interval**, not the work: compare busy ms/frame (`#busy` of `profile.py`), not frame time
  or fps.
- **Devices are noisy.** An iPhone after three runs in a row gave x2 CPU in every system, and one on the charger throttles: a cold
  phone off the charger, pauses between runs; compare builds on the stand, confirm on the device.
- **Stop servers by pid** (`ss -ltnp 'sport = :<port>'`), never `pkill -f <pattern>`: the pattern matches the own shell (exit 144).
  A port held by a process this session did not start belongs to someone else: take another port.
- **iPhone / iPad:** the screen unlocked (Auto-Lock: Never), Web Inspector on, nobody touches it during a series. A stuck WebGL
  (about 1 frame in 5 s on every page) is cured only by `ios/safari-restart.sh`; every series starts from a fresh Safari.
- **LAN:** only tcp 8770-8771 reach the machine from a phone (ufw); a local build sends no telemetry (the meta answers 403 to a LAN
  origin), its measure is the remote log.
- Outputs go to `/tmp/claude-1000/<topic>/...` or the scratchpad, never into the tree of the game.

## Report

In Russian: what ran (build, device or preset, CPU rate, steps), the numbers with their units and frames (ms/frame, GL calls per
frame, fps), the A/B as before / after / delta with the spread of the runs and the load of the machine, the files (absolute
paths: summary, profiles, screenshots), and anything that makes a number doubtful (load, heat, a failed step). Open screenshots
with `claude-show-image`.

The WebGL problems already met and how they were found: `.claude/docs/WEBGL_TROUBLESHOOTING.md` of the framework (`WEBGL_IOS.md`
in a tree that does not have it yet).
