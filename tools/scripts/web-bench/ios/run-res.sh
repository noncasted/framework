#!/usr/bin/env bash
# One autorun of the production game on an iPhone / iPad: a fresh Safari (safari-restart.sh), the page
# https://<host>/<autorunQuery>&r=<random><query> (host and autorunQuery from .claude/device-lab.json of the game),
# <seconds> of play, then the size of the canvas and a screenshot. A cold device off the charger gives comparable numbers;
# read the session itself with the client-telemetry skill (production sessions do reach the telemetry).
#
# Usage: run-res.sh --device <name> --out <dir> --seconds <n> [--query '<query>'] [--project <game dir>]
#   --query    extra url parameters, e.g. '&fps=60&dpr=native' (a leading & or ? is optional)
#   --project  root of the game (default: the current directory)
# Output in <dir>: run.txt (url, opened / end times UTC, canvas size), shot.png, shot.small.png (40%)
# Environment: DEVICE_LAB_DEVICES, DEVICE_LAB_HOME (see devicelab.py)
# Example: run-res.sh --device ipad7 --out /tmp/res-low --seconds 60 --query '&dpr=1' --project /projects/pixel-swarm
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)

usage() { sed -n '2,/^set -euo/p' "$0" | sed '$d; s/^# \{0,1\}//'; }
device=; out=; seconds=; query=; project=$PWD
need() { [ "$1" -ge 2 ] || { echo "error: $2 needs a value" >&2; exit 2; }; }
while [ $# -gt 0 ]; do
    case $1 in
        -h|--help) usage; exit 0 ;;
        --device) need $# "$1"; device=$2; shift 2 ;;
        --out) need $# "$1"; out=$2; shift 2 ;;
        --seconds) need $# "$1"; seconds=$2; shift 2 ;;
        --query) need $# "$1"; query=$2; shift 2 ;;
        --project) need $# "$1"; project=$2; shift 2 ;;
        *) echo "error: unknown argument $1 (--help)" >&2; exit 2 ;;
    esac
done
[ -n "$device" ] && [ -n "$out" ] && [ -n "$seconds" ] \
    || { echo "error: --device, --out and --seconds are required (--help)" >&2; exit 2; }
[[ $seconds =~ ^[0-9]+$ ]] || { echo "error: --seconds must be a whole number" >&2; exit 2; }
host=$(python3 "$here/devicelab.py" game host --project "$project") || exit 2
autorun=$(python3 "$here/devicelab.py" game autorunQuery --project "$project") || exit 2
vars=$(python3 "$here/devicelab.py" shell "$device") || exit 2
eval "$vars"

query=${query#[?&]}
autorun=${autorun#\?}
url="https://$host/?${autorun:+$autorun&}r=$RANDOM${query:+&$query}"
mkdir -p "$out"
"$here/safari-restart.sh" --device "$device" "$url" >/dev/null
{ echo "url $url"; echo "opened $(date -u +%T)Z"; } | tee "$out/run.txt"
sleep "$seconds"
# The screenshot first: it is the evidence of the run even when the page does not answer the eval. A Unity 6+ loader
# has no global Module: the canvas is the element of the template (#unity-canvas), else the first one.
"$here/screenshot.sh" --device "$device" --resize 40 "$out/shot.png" >/dev/null
canvas=$("$DL_PY" "$here/iosctl.py" --device "$device" eval \
    'JSON.stringify((c => ({w: c.width, h: c.height, dpr: devicePixelRatio}))(document.querySelector("#unity-canvas") || document.querySelector("canvas")))')
{ echo "canvas $canvas"; echo "end $(date -u +%T)Z"; } | tee -a "$out/run.txt"
