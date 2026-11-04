# Profile: CPU profiles of the wasm

`$WB/profile/`: `profile.py` (one CLI over Chrome DevTools `.cpuprofile` files, Python stdlib; `cpuprofile.py`, `tree.py`,
`phases.py`, `runs.py` are its parts), `debug-wasm.sh` (function names in the profile). Tests:
`python3 -m unittest discover -s $WB/profile/tests`.

## Names instead of `wasm-function[N]`

A release wasm has no names: nearly every node is `wasm-function[N]`, `profile.py` warns on stderr, and phases and rows are not
found (everything lands in `outside loop`). Swap it for the debug wasm of the same link before the run (framework.js stays: both come from one
link):

```
$WB/profile/debug-wasm.sh <build> <unity project | game root of that build>
$WB/profile/debug-wasm.sh --restore <build>
```

The debug wasm is `Library/Bee/artifacts/WebGL/build/debug_WebGL_wasm/build.wasm` of the Unity project that made the build,
compressed with `brotli -q 4`; the original stays as `Build/*.wasm.br.orig`. The script refuses a debug wasm whose time is more than
10 minutes away from the build (it belongs to another build of that project); `--force` only when you know it is the same link.
So the project must be the one that built this exact build: the kept copy of `deploy-client --no-deploy` (`<work>/<target>/client`,
SKILL.md § A build), never `client/` of the game after a later build. The debug wasm is the code before the submodule stripping:
the profile is of the unstripped player, the same code paths.

A profile with names comes only from a build that has its own debug wasm: a `deploy-client` copy kept for it
(`KEEP_COPY=1 ... --no-deploy`, SKILL.md § A build: `debug-wasm.sh <work>/<target>/<output> <work>/<target>/client` before
`<work>` is deleted). A build made in the editor (`builds/`, `client/Build`) usually has none any more: the next build of that
project overwrites `Library/Bee`, and the script refuses the newer wasm. Without it read only the totals (`#busy`, `#(program)`, `#(garbage collector)`), not phases or rows.

## Reports

Frames: a profile of the stand needs none - its `prof` step counts the GL frames of the window (`frames`, `frameMs` in
`summary.json`) and `profile.py` reads them from the `summary.json` next to the profile. Otherwise `--frames F` (the frame
count of the profiled window: the game's frame counter, a heartbeat) or `--frame-ms M` (frames = duration / M). Times in ms/frame
where frames are known, else ms.

```
P() { python3 "$WB/profile/profile.py" "$@"; }   # a function: a command in a variable does not split in zsh
P top <cpuprofile> ['<regex>'] [N]             # self top N, then the top N inclusive of the names matching regex
P callers <cpuprofile> <prefix> [depth]        # call paths of a function
P kids <cpuprofile> '<regex>' [--frames F]     # children of a function, wrappers skipped (--depth)
P selfunder <cpuprofile> '<regex>' [--frames F]  # self time of the functions under it
P phases <cpuprofile> [--frames F]             # time per Unity player-loop phase
P cmp <a> <b> [...] [--frames Fa,Fb,...] [--rows <file>]  # side by side + the biggest growth first -> last (no idle / program)
P fit --run <objects> <frames> <cpuprofile> ... [--rows <file>] # mean per object count, slope per 100 objects
```

`--rows`: the rows of the game for `cmp` / `fit`, one per line, `key` or `label<TAB>key`; a key is a function name (prefix match) or
a synthetic row (`#busy`, `#(program)`, `#phase Update`, `#phase outside loop (GL js)`, ...). The rows file of a game is named in
its `.claude/project-skills.md` § device-lab; without one the defaults are busy, program, gc and the phases.

How to read: `#busy` is all CPU of the main thread of the page; `#phase outside loop (GL js)` is the GL calls of the browser after
the frame of the player (ANGLE cost, see the count step of the stand); a growth between runs shows up first in the growth list of
`cmp`. One profile is not a measure: compare runs of an A/B series ([stand.md](stand.md)).
