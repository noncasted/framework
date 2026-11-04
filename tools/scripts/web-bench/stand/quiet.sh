#!/usr/bin/env bash
# Waits until the machine is quiet: the 1-minute load average below a threshold. Builds of other sessions spoil a
# measure (a load of 31 spoiled the runs of a whole A/B once), so every measured run of the stand waits here first.
#
# Usage: quiet.sh [--max <load>] [--timeout <seconds>]
#   --max      threshold of the 1-minute load average; default $QUIET_LOAD or 12
#   --timeout  give up with exit code 1 after this; default $QUIET_TIMEOUT or 1800
# Prints the load when it starts waiting and when it is quiet. Example: quiet.sh --max 8 && node run.mjs ...
set -euo pipefail

max="${QUIET_LOAD:-12}"
timeout="${QUIET_TIMEOUT:-1800}"
while (($#)); do
    case "$1" in
        --max) max="$2"; shift 2 ;;
        --timeout) timeout="$2"; shift 2 ;;
        -h|--help) sed -n '2,8p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "quiet.sh: unknown argument $1" >&2; exit 2 ;;
    esac
done

load() { cut -d' ' -f1 /proc/loadavg; }
below() { awk -v l="$(load)" -v m="$max" 'BEGIN { exit !(l < m) }'; }

start=$SECONDS
if ! below; then
    echo "quiet: load $(load) >= $max, waiting (up to ${timeout} s)" >&2
    until below; do
        if ((SECONDS - start >= timeout)); then
            echo "quiet: load still $(load) >= $max after ${timeout} s" >&2
            exit 1
        fi
        sleep 15
    done
fi
echo "quiet: load $(load) < $max after $((SECONDS - start)) s" >&2
