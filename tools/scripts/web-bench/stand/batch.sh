#!/usr/bin/env bash
# A/B series of the stand: every variant runs --runs times, interleaved (A B, then B A, ...) so the drift of the machine
# falls on both sides; each run waits for a quiet machine (quiet.sh) and logs the load before and after.
#
# Usage: batch.sh --out <dir> --variant <name>:<build>[:<query>] --variant ... [--runs 3] [--max-load 12] -- <run.mjs options>
#   --variant  a name, a build (a key of "builds" of the game config or a folder) and an optional extra query ("&upgrades=0");
#              two or more
#   --runs     runs per variant, default 3
#   --max-load passed to quiet.sh, default $QUIET_LOAD or 12
#   after --    the options of run.mjs shared by every run (not --out, --build, --query, --quiet)
# Each run writes <dir>/<name>_<i>/ (summary.json, profiles, shots); <dir>/batch.log lists the runs with the load.
# A failed run stops the batch (a meta leak or a broken build would spoil every other run too).
# Example (root of the game):
#   tools/framework/tools/scripts/web-bench/stand/batch.sh --out /tmp/claude-1000/ab1 \
#     --variant before:/var/tmp/g-before/builds/website --variant after:/var/tmp/g-after/builds/website \
#     -- --autorun --device mali-g52 --cpu 6 --gpu --prof 40 --shot 38 --end 46
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
out=""; runs=3; max="${QUIET_LOAD:-12}"; variants=()
while (($#)); do
    case "$1" in
        --out) out="$2"; shift 2 ;;
        --variant) variants+=("$2"); shift 2 ;;
        --runs) runs="$2"; shift 2 ;;
        --max-load) max="$2"; shift 2 ;;
        --) shift; break ;;
        -h|--help) sed -n '2,16p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "batch.sh: unknown argument $1 (run.mjs options go after --)" >&2; exit 2 ;;
    esac
done
[[ -n "$out" ]] || { echo "batch.sh: --out is required" >&2; exit 2; }
((${#variants[@]} >= 2)) || { echo "batch.sh: two or more --variant" >&2; exit 2; }
for a in "$@"; do
    case "$a" in --out|--build|--query|--quiet) echo "batch.sh: $a is set by the batch" >&2; exit 2 ;; esac
done
mkdir -p "$out"
[[ -z "$(ls -A "$out")" ]] || { echo "batch.sh: --out $out is not empty" >&2; exit 2; }

for ((i = 1; i <= runs; i++)); do
    order=("${variants[@]}")
    if ((i % 2 == 0)); then
        order=(); for ((k = ${#variants[@]} - 1; k >= 0; k--)); do order+=("${variants[k]}"); done
    fi
    for spec in "${order[@]}"; do
        IFS=: read -r name build query <<< "$spec"
        [[ -n "$name" && -n "$build" ]] || { echo "batch.sh: --variant $spec is not <name>:<build>[:<query>]" >&2; exit 2; }
        "$here/quiet.sh" --max "$max"
        echo "$name $i start load $(cut -d' ' -f1 /proc/loadavg) $(date +%T)" | tee -a "$out/batch.log"
        node "$here/run.mjs" "$@" --build "$build" ${query:+--query "$query"} --out "$out/${name}_$i"
        echo "$name $i end load $(cut -d' ' -f1 /proc/loadavg) $(date +%T)" | tee -a "$out/batch.log"
    done
done
