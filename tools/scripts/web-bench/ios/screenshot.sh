#!/usr/bin/env bash
# Takes a screenshot of an iPhone / iPad over USB with `pymobiledevice3 developer dvt screenshot` (the screen as it is,
# WebGL included; a screenshot through the Web Inspector gives a black canvas). On iOS 17+ the developer services go
# through a tunnel: "tunnel": "userspace" in devices.json passes --userspace (no root); the DDI must be mounted
# (safari-restart.sh does it).
#
# Usage: screenshot.sh --device <name> [--resize <percent>] <out.png>
#   --resize  also writes <out>.small.png scaled to that percent (needs magick)
# Environment: DEVICE_LAB_DEVICES, DEVICE_LAB_HOME (see devicelab.py)
# Example: screenshot.sh --device ipad7 --resize 25 out/shot.png
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)

usage() { sed -n '2,/^set -euo/p' "$0" | sed '$d; s/^# \{0,1\}//'; }
device=; out=; resize=
while [ $# -gt 0 ]; do
    case $1 in
        -h|--help) usage; exit 0 ;;
        --device) [ $# -ge 2 ] || { echo "error: --device needs a name" >&2; exit 2; }; device=$2; shift 2 ;;
        --resize) [ $# -ge 2 ] || { echo "error: --resize needs a percent" >&2; exit 2; }; resize=$2; shift 2 ;;
        -*) echo "error: unknown option $1 (--help)" >&2; exit 2 ;;
        *) [ -z "$out" ] || { echo "error: one output file only" >&2; exit 2; }; out=$1; shift ;;
    esac
done
[ -n "$device" ] && [ -n "$out" ] || { echo "error: --device <name> and <out.png> are required (--help)" >&2; exit 2; }
[ -z "$resize" ] || command -v magick >/dev/null || { echo "error: --resize needs magick (imagemagick)" >&2; exit 1; }
vars=$(python3 "$here/devicelab.py" shell "$device") || exit 2
eval "$vars"

idevice_id -l 2>/dev/null | grep -qx "$DL_UDID" \
    || { echo "error: $DL_NAME ($DL_UDID) is not connected over USB (idevice_id -l)" >&2; exit 1; }
mkdir -p "$(dirname "$out")"
timeout 40 "$DL_PMD" developer dvt screenshot $DL_PMD_ARGS "$out" >/dev/null \
    || { echo "error: the screenshot failed (DDI mounted? safari-restart.sh mounts it)" >&2; exit 1; }
if [ -n "$resize" ]; then
    magick "$out" -resize "$resize%" "${out%.png}.small.png"
fi
echo "$out"
