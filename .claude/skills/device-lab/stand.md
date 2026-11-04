# Stand: headless Chromium over CDP

`$WB/stand/`: `run.mjs` (one run), `batch.sh` (A/B series), `quiet.sh` (wait for a quiet machine), `serve_tls.py` (the build over
TLS like the production nginx), `probe.js` (the GL probe, injected by `run.mjs`), `cdp.mjs` / `config.mjs` / `steps.mjs` /
`guard.mjs` / `network.mjs` (parts of `run.mjs`). Needs node (built-in `WebSocket` and `fetch`), `/usr/bin/chromium` (`CHROMIUM`), openssl.

What it is: the page opens with its production url `https://<host>/...` while Chromium resolves the host to `serve_tls.py` on
`127.0.0.1:<port>` (a self-signed certificate made on the first start in `~/.local/share/device-lab/certs/<host>/`, trusted by its
key only) and the meta host to `127.0.0.1:1`. A phone is emulated (size, DPR, touch, Android UA), the CPU throttled. It does not
reproduce WebKit, Mali, ANGLE of a phone or its heat: it compares builds, it does not stand for a device.

## One run

From the root of the game, a build from `builds` of `.claude/device-lab.json` or a folder:

```
node $WB/stand/run.mjs --build web --autorun --device mali-g52 --cpu 6 --gpu \
  --prof 40 --count 30 --shot 25 --end 46 --quiet 8 --out /tmp/claude-1000/bench/<name>
```

- `--build` starts `serve_tls.py` for the run and stops it by its pid; without it a server must already listen on `--port`
  (default 8792; a second stand in parallel takes another port, and its own `serve_tls.py --port`).
- `--device mali-g52 | phone-800 | desktop` (or `--size WxH --dpr N`), `--cpu 6` ~ a Mali-G52 phone, `--gpu` the GPU of the
  machine (ANGLE gl) instead of SwiftShader (`--angle`).
- Steps in seconds after the navigation, comma lists: `--prof` (CPU profile of `--prof-len` s -> `cpu_<s>.cpuprofile` and its GL frames
  `frames` / `frameMs` in the summary, which `profile.py` takes by itself: read it with [profile.md](profile.md)), `--count` (GL calls by name -> `count_<s>.json`, calls per frame in the summary), `--rec` (every
  GL call of a frame -> `rec_<s>.json`), `--shot` (`shot_<s>.png`), `--resize s:WxH`, `--err-ms` (getError after every call until
  then, slow -> `errs.json`), `--hook-record-error` (emscripten's own `GL.recordError` -> `errs.json` `emErrs`), `--end`.
- `--scenario f.json`: a JSON array of `{at, eval: "<js>"}`, `{at, call: "<method>", args: [...]}` (calls
  `window.<scenarioApi>.<method>`) and `{at, waitFor: "<js>", timeout: <s>}`; returned values go to the summary. This is how a game
  drives its stress API (pixel-swarm: `window.stress.*`) through stages.
- `--out` must be new or empty: `summary.json` (steps, GL calls per frame, the network check `meta` and its `warnings`, errors, load before / after), `console.txt`,
  `errs.json` and the files of the steps.

Network: the page host goes to the local server, the meta host to `127.0.0.1:1`, every other host to `~NOTFOUND` (blocked, never
resolved). `summary.meta`: `sockets` (every websocket: url, outcome), `attempts` / `refused` (the meta host), `requests` (fetch / XHR
to the page host that are not files of the build), `blocked` (other hosts). `summary.warnings` and `run: WARNING` lines: a
websocket to the page host (the build does not use `metaHost`, SKILL.md § Configuration), those requests, a blocked host (a
third-party dependency of the build: it fails on the stand). A warning is not a failure; read it before trusting the run.

Exit codes: 0 done; 1 a step failed, the page host answered >= 400 (an incomplete build: `index.html` without `Build/`) or
Chromium died; 2 wrong options or environment (the line says which); 3 a request to the meta or another host got an answer - the run stops at
once, report it as a leak and do not rerun until the cause is found.

`--prod` / `PROD=1`: the real host and meta, the session goes to the production telemetry. Only when the user asked for it in this
request. `--leak-test`: the self-test of the network check (the page fetches the meta host, mapped to the local server for it),
must end with exit 3.

## A/B series

```
$WB/stand/batch.sh --out /tmp/claude-1000/ab/<name> --variant before:<build> --variant after:<build>[:&<query>] --runs 3 \
  --max-load 8 -- --autorun --device mali-g52 --cpu 6 --gpu --prof 40 --shot 38 --end 46
P cmp /tmp/claude-1000/ab/<name>/{before,after}_{1,2,3}/cpu_40.cpuprofile   # frames from each summary.json
```

Variants run interleaved (A B, B A, ...), each run waits for `quiet.sh` (`--max-load`, default 12; 6-8 for a difference under
30%, SKILL.md § Rules) and logs the load; `<dir>/batch.log` lists the runs, `<dir>/<name>_<i>/` holds each. A failed run stops the
series. Compare the profiles with `profile.py cmp` (`P` of [profile.md](profile.md): all runs of both variants, the frames of
each from its `summary.json`) and report `#busy` ms/frame per variant as the median and min-max of its runs, not one number.

Two builds of one commit with a change: two `deploy-client --no-deploy` copies (SKILL.md § A build), or one build and a query switch
of the game (`after:<same build>:&<switch>`).

## Notes

- `serve_tls.py` alone: `python3 $WB/stand/serve_tls.py <build> --port 8792` from the root of the game (or `--host`); stop it by
  its pid from `ss -ltnp 'sport = :8792'`.
- `probe.js` also works pasted into the console of any page: `__gp.count(5000)`, `__gp.startRec(1)`, `__gp.info()`.
- One Chromium stand at a time on the machine unless the user asked for parallel runs: two stands share the CPU and spoil both.
