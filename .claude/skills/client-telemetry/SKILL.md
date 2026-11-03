---
name: client-telemetry
description: Watch and analyse the performance telemetry of game clients - wait for a new session and observe it, only of a menu or only of a run by its tag, or analyse a given session by id, live or archived, or a startup trace of a client - and report CPU or GPU limits, freezes, memory growth, overheating, slow loads and errors. Use when the user runs /client-telemetry, asks to look at the client telemetry, the performance on a phone or a device, a live session, why the game lags on a player's device, or says they are starting the game now and asks to watch it.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /client-telemetry, client-telemetry, телеметрия клиента, посмотри телеметрию, перформанс на телефоне, тормозит на телефоне, почему лагает, живая сессия, посмотри сессию, сессия забега, сессия меню, сейчас запущу игру, client telemetry, phone performance, live session
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Run the mode that fits the request and give the report.

# Client Telemetry

A session is one part of the game: the game opens it and sets its tags, for example `menu` or `run` (the values belong to the game,
`sessions` shows them; the script knows no list of them). Outside a session there is no data. The script only reads (GET).

One sitting of a player is several sessions in a row: a menu, a run, a menu again. Each has its own id; `startup=` in a report ties a
session to the startup trace of its client, on one socket, and `previous_unclosed` / `unclosed_on_client` tie a session the client did not
close to the next session of the same browser (below). Otherwise the same device at adjacent times is a guess, say so. A report covers one session: a slow menu says nothing of the
run.

```
T=.claude/skills/client-telemetry/telemetry    # from the root of the project
```

Options go after the subcommand: `$T sessions --url <u>`, not `$T --url <u> sessions`; `--url` and `--env-file` too.

## Modes

**1. New session** ("I start the game now", "watch the next session"):

```
$T monitor --timeout 300 --seconds 60 [--tag T]... [--platform P] [--build B] [--os O]
```

It takes the ids of all sessions at the start as the baseline, waits for an id out of it, observes that session and prints the
report. New means an id the baseline does not hold, not a time: the clock of a device plays no part.

**A run or a menu: `--tag`.** Without it the first new session of a starting game is usually its menu. To watch a run: `--tag run`, and
leave `--timeout` long enough for the user to reach it. The tag goes to the backend and the baseline is taken under it: an exact match,
a repeated `--tag` means all of the given, a session of another tag is neither awaited nor counted, a session without tags (`tags=-`)
never matches. A session opened before `monitor` is in its baseline: to join a run already going, find it with
`$T sessions --tag run --status live` and use `watch`.

Every player is tracked, so a stranger may come first. Narrow it: `--platform` and `--build` go to the backend, `--os` (`Android`,
`iOS`, ...) is compared by the script. Check the `new` line against the device the user named: a wrong device is no finding, say so
and run `monitor` again. When several are new, one is observed (a live one, the newest) and the others are printed as `other_new`
lines at the end: analyse them with mode 2 if they matter.

**2. A given session** (by id, live or archived; closed ones are kept for the retention of the backend, 7 days by default):

```
$T summary <id> [--windows 6]      # the whole session so far
$T watch <id> --seconds 60         # a live one: observes, reports only what came during the watch
$T errors <id>                     # groups of errors with a stack
```

No id yet: `$T sessions --tag run` lists only the runs, `--tag menu` only the menus (the tags of the game, the same exact match). A menu
and the run after it are two ids: analyse each on its own.

Other commands: `$T sessions [--status live|ended|lost] [--tag T]... [--platform P] [--build B] [--metric M --average-below X --average-above X]
[--min-errors N] [--limit 20]` (newest first, each line with `tags=`; a live session has no summary, so `--metric` and `--min-errors` leave it out) and
`$T errors` (kinds of errors over the closed sessions).

An observation ends by `--seconds`, or when the session closes (`end reason=` its status):
- `ended`: its part of the game is over. A new session closes the previous one: the start of a run ends the menu.
- `lost`: the socket broke, which counts as a crash of the client, or the gateway itself died and the timer of the server closed the
  session. A closed id stays closed. Whether the client really died shows only when the same browser opens a session again: see point 7.

To follow a player into the next part, start a new `monitor` before they get there.

## How long a call lasts

A Bash call defaults to 2 minutes and is cut at 10. Give `timeout: 600000` to any `monitor` or a `watch` over 100 seconds, with
`--timeout` + `--seconds` under 580. A longer wait runs in the background: Bash with `run_in_background: true`, you are called back
when it exits, its output is in the file of the task. Do not poll it and do not sit in a loop of short calls.

## Where it reads from

The project gives the address and the env file in `${CLAUDE_PROJECT_DIR}/.claude/project-skills.md`:

```
## client-telemetry

- url: `https://meta.example.com`
- env-file: `~/.config/<game>/deploy.env`
```

`--url` and `--env-file` win over the section (a local host or cluster). The env file holds `CONSOLE_TOKEN=...`. The script reads only
that line, as data, and sends it only in the `Authorization` header.

**The token:** never read, print, source or pass the env file or its value yourself; the script has no argument for it. Plain http
is refused except on this machine, a redirect is refused.

**Startups** ("why is the start slow", "what loads for so long", a slow scope, addressables):

```
$T startups [--limit N]
$T startup <id> [--top N]
```

The trace of the start of a client, the one thing sent without a session, under the same flag. `startups` lists the records; `final`
is a whole trace, `snapshot` one still open when it was sent (it stays so when the client goes). `startup <id>` gives `own_time` (the
longest own times: a duration without the time of the children), `unfinished` and the `sessions` of this start. `summary` of a session
names its record as `startup=`, `None` when it has none. A device field the client did not send prints as `-`: it is unknown, not a finding.

Read the own time, never the duration of a parent. A long own time over many frames at the frame rate of the game (`ms_per_frame` about
17) is the game running while it waits, usually for the player: not a load. A load takes few frames, with a high `ms_per_frame`. The
span names come from the code of the game, requests appear as `GET <url>`. No allocations outside the editor: they are not in the report.

On the web the trace begins at the origin of the page, not at the start of the build: `page` (before `own_time`) is the root span `Page`,
and `page X until the build started at Y` is the time the player looked at the page before the first code of the build. In it: `navigation`
(DNS, connection, the wait for the answer, the answer), `Document` (from the answer to DOMContentLoaded: a blocking script of the head,
such as an SDK of a platform, lands here) and `Load event`; `files` loaded before the build, the longest first, with `network` (what came
over the network), `cached` (`yes` from the cache of the browser) and `-` for a file of another origin that tells no sizes (no
Timing-Allow-Origin); `build_start`, from the last file to the build: the compilation of the wasm and the start of the instance, long on a
weak phone; `template`, the spans the template of the game sets with `window.startupTrace` (an `(unfinished)` one ends at the start of
the build). A start that the user calls long while the trace of the build is short is in `page`: name the longest of its parts. `page none`:
not the web, or a client built before the page trace.

## Exit codes

| Code | Meaning | Do |
|------|---------|----|
| 0 | done | analyse |
| 1 | host unreachable, network timeout, 5xx, 400, redirect, not JSON | report the message; 503: the backend cannot answer now, repeat once |
| 2 | arguments, section of the project, env file, no token | report what is missing, do not guess an address |
| 3 | 401, 403 | the token was refused: tell the user, do not look for another one |
| 4 | no such session | check the id with `sessions` |
| 5 | no new session in the time of `monitor` | the checks below |

**No new sessions?** In this order: the flag of the client telemetry on the Features page of the console (off: no sessions at all);
the game opens a session, the flag alone gives nothing; the client is built against this address; the filters of the command;
with `--tag`, whether the game has reached the part that sets the tag and whether the value is its own (`sessions` shows the tags). Tell the user which of them you could not check.

## Reading the report

A report: `session` (with `tags=`), `device`, `digest` (closed only; `warnings=` counted apart from `errors=`), `previous_unclosed` and
`unclosed_on_client` (point 7), `not_reported` (metrics of the framework that have no value), `metric` table
of the whole range, `trend` (the range cut in windows by time), `marks`, `loads`, `errors`. Numbers of a live session are of "so far".
Background samples (`hidden=`) are out of the tables: never read a background pause as a freeze. `dropped_*` above 0 means the summary
does not cover everything; say it.

Go in this order, each from the numbers:

1. **CPU or GPU limit.** `frame.cpu`, `frame.cpu.main` against `frame.gpu` and the frame budget (`frame.dt.p50`). The larger one close
   to the frame time is the limit. All three may be in `not_reported` (the build has no frame timing stats, or the platform gives none):
   then the limit cannot be determined, write that. Do not infer it from the fps. `render.*` tell the load, not the limit.
   Where the timings are missing or disagree, split the frame (14 metrics, all platforms): `frame.phase.<early|fixed|pre|update|prelate|postlate>`
   (ms per frame, `.max` the worst frame of the sample) are the top systems of the player loop timed by Stopwatch, in a release WebGL too;
   their sum is close to `frame.cpu.main` (in a browser each is rounded to the step of `performance.now`, 0.1 ms without cross-origin
   isolation: read the means, not a `.max` under 0.1). A long frame is in the `.max` of its phase in one sample, its `frame.dt.max`,
   `frame.over*` and `frame.fixed_steps.max` may be in the next: they are measured one frame later. `update` and `prelate` are the scripts of the game (UniTask continuations included), `fixed`
   physics and `FixedUpdate` with `frame.fixed_steps` steps per frame (above 1 with a long frame: the steps feed the lag), `postlate` the
   rendering on the main thread (WebGL issues the GL calls there, so a GPU that blocks shows here). `frame.dt` well above the sum of the
   phases: the time is outside the player loop (the browser, the compositor, a GPU that holds the next frame).
   `frame.gpu.query` (WebGL with `EXT_disjoint_timer_query_webgl2`, `web.gl.timer_query`=1) is the GPU time of one whole frame in 30, the
   real GPU limit where `frame.gpu` is absent; none with `web.gl.timer_query`=0 (most phones, Safari), which is no finding.
   `render.width`/`render.height`/`render.pixels` are the real drawing buffer, `render.canvas_*` the CSS size, `render.dpr` the ratio
   applied: a fill-bound shader costs in proportion to `render.pixels`. `web.raf.dt.avg`/`.max` are the frames the browser gives the page,
   measured apart from Unity: long ones with a short `frame.cpu.main` - the browser or the GPU holds the frames; short ones with a long
   `frame.dt` - Unity does not keep up (with `settings.target_fps` set, Unity may skip browser frames on purpose). `settings.*` (quality
   index, `vsync`, `target_fps` -1 the default, `time_scale`, `msaa`) and `web.gl.*` (version, `max_texture_size`, `antialias`,
   `preserve_buffer`, `power_preference` 0 default 1 high-performance 2 low-power) are the configuration; compare sessions with them.
2. **Freezes.** `frame.over33`, `frame.over50` (frames over 33 and 50 ms, summed in windows) and `frame.dt.max` against `frame.dt.p50`.
   Rare high `max` with a calm p95 are single freezes; check the same window for `gc.collections`, `marks`, errors. Every frame of 250 ms
   or more is counted in `frame.spikes` and the first 64 of a session leave the marks `frame.spike` and `frame.spike.<phase>`: the longest
   part of that frame, `browser` when it was outside the player loop (the browser did not run the frame, a GPU stall, a lost context
   coming back). `gc.allocated` is the managed garbage per sample (summed in windows): steady megabytes per second feed the collections.
   `net.meta.rtt` is the answer time of a telemetry batch on the meta socket (network, two frames, the server), `net.meta.bytes_*` its traffic.
3. **Memory growth.** `growth` and the trend of `memory.*`, `web.wasm.heap`, `web.js.used`: growing to the end of a session is a suspect
   leak, a step at a mark is a load. Name the metric and the rate per minute.
4. **Overheating.** The fps falls window by window while the load (`render.draw_calls`, `render.triangles`, the counters of the game)
   stays the same, usually with `frame.gpu` or `frame.cpu` growing without a new load. No thermal metric exists: this is an
   inference, name it so. If the load grew too, it is the load, not the heat.
5. **Errors.** Groups by `fingerprint`: count, rows, `first` and `last` (seconds since the start, on the scale of `t_s`), whether they meet a drop in the trend. The stack: `errors <id>`.
   `omitted_*` above 0: not all kinds are listed. Groups of level `warning` are the warnings of the log of Unity (a glyph missing in a font
   of TextMeshPro, a texture that failed to load), at most 16 kinds and 48 records a session: a cause only with a matching change in the
   trend. `WebGL error <CODE>` groups and the marks `webgl.error.<code>` (first 16) come from `gl.getError` once a sample, `web.gl.errors`
   counts the codes per sample (summed in windows): `out_of_memory` means the GPU memory of the page is over (textures, render targets),
   a loss of textures or of text often follows. `memory.texture_count` (all loaded textures) and `memory.textures` (bytes) show the load.
6. **A black screen with a live client.** See `web.gl.context_lost` and the marks `webgl.context_lost` / `webgl.context_restored`, with the error
   `WebGL context lost`. Unity (checked on 7000.0.0a10 in Chromium) stops its whole player loop while the context is lost, so a loss without a restore reaches the
   server as nothing at all: the samples stop and the session closes as `lost`. The marks and the error arrive only after a restore, which the game does not ask for. Samples that
   go on over the black screen with the metric at 0: the engine does not see a lost context, look elsewhere (a hung GPU process is not excluded). A loss outside a session comes at the time 0 of the next one.
7. **A client that died.** The browser keeps the open session (id, tags, last sample, last 8 marks) and its next session reports it
   if it did not close. `previous_unclosed <id> server_status=...` in the report of the next one, `unclosed_on_client next=<id>` in
   the report of the old one; `unclosed_on_client none` for a closed session means no later session of that browser reported it (it
   closed on the client, or the browser never came back: no proof either way). `last_sample` against the last `t_s` of the old session:
   later means the last seconds never reached the server (a frozen page, a dead socket). `last_marks`: `page.pagehide` (a reload, a
   closed tab, a navigation) or `page.pagehide.persisted` (the back-forward cache) before the end is a page that went away; `page.hidden`
   then nothing is a page killed in the background; no page mark with a running game is a crash of the tab (WebContent on iOS, OOM).
   `since` is the time from the last record to the next start: seconds is a reload by the browser, hours is the next visit. The new
   session also has the mark `session.previous_unclosed` at its start. Another tab of the same game may report a session that is
   still alive: its `server_status` is `live` then.
   Marks of the page in any session: `page.hidden`/`page.visible` (the tab), `page.freeze`/`page.resume` (Chromium froze the tab), first 64.
   An iPad is `os=iPadOS` (its version is unknown, `-`, when it shows the user agent of a Mac); Telegram on iOS is `browser=Telegram WebView`.

8. **Loads.** `loads` counts the loads of assets of the session (the catalogue groups `Group: <name>`, the scenes `Scene: <name>`, a
   scene not yet loaded is `Scene`), from the marks `load.*`: `failed` with the type of the error and the time it took, `slow` when one
   ran over the limit of the game (`ClientTelemetryOptions.slowLoadSeconds`, 5 s by default), `unfinished` what was still loading at the end of the session (with the time so far: a load that never ended, such as a
   group whose content is missing, a hung download), `longest` of the ended ones. A load begun before the session ends in it without
   a start. Content that did not show up (a missing sound, a track that did not change) is first a failed, unfinished or absent load of
   its group; `loads none` in a part that should load says the content came from an earlier part or was never asked for.

## The final report

```
Conclusion: 1-3 sentences, what limits this session on this device (or "no problem found in this range").
Numbers: the device; the tags of the session; the range (seconds, samples, hidden); for each point of the order the values and windows that back it.
Not determined: what the data cannot answer, and why (not_reported, too short a range, a live session, dropped rows).
```

Never give a guess as a conclusion: a cause the numbers do not show goes under "Not determined", or is marked as a hypothesis with what
would check it. Coincidence in one window is no cause. Do not advise on the code unless asked.
