#!/usr/bin/env bash
# Restarts Safari on an iPhone / iPad over USB and opens a url: the only way, without a human, to clear the stuck WebGL
# state of Safari (a GPU hang leaves every later page at ~1 frame / 5 s until Safari restarts).
# Steps: mounts the Developer Disk Image ("ddi" of the device with ideviceimagemounter, or "ddiAutoMount" with
# `pymobiledevice3 mounter auto-mount`), starts ios_webkit_debug_proxy if it is not running (iosctl.py proxy start),
# sends the tab to about:blank (so the restored tab does not reload the page that stuck the browser), restarts Safari
# with `pymobiledevice3 developer dvt launch --kill-existing com.apple.mobilesafari`, waits for the page, opens the url.
# Needs Developer Mode on (Settings > Privacy & Security), the screen unlocked, Web Inspector on.
#
# Usage: safari-restart.sh --device <name> [url]
# Environment: DEVICE_LAB_DEVICES, DEVICE_LAB_HOME (see devicelab.py)
# Example: safari-restart.sh --device iphone11 'https://example.com/?autorun=1'
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)

usage() { sed -n '2,/^set -euo/p' "$0" | sed '$d; s/^# \{0,1\}//'; }
device=; url=
while [ $# -gt 0 ]; do
    case $1 in
        -h|--help) usage; exit 0 ;;
        --device) [ $# -ge 2 ] || { echo "error: --device needs a name" >&2; exit 2; }; device=$2; shift 2 ;;
        -*) echo "error: unknown option $1 (--help)" >&2; exit 2 ;;
        *) [ -z "$url" ] || { echo "error: one url only" >&2; exit 2; }; url=$1; shift ;;
    esac
done
[ -n "$device" ] || { echo "error: --device <name> is required (--help)" >&2; exit 2; }
vars=$(python3 "$here/devicelab.py" shell "$device") || exit 2
eval "$vars"
ctl() { "$DL_PY" "$here/iosctl.py" --device "$DL_NAME" "$@"; }

idevice_id -l 2>/dev/null | grep -qx "$DL_UDID" \
    || { echo "error: $DL_NAME ($DL_UDID) is not connected over USB (idevice_id -l)" >&2; exit 1; }

if [ -n "$DL_DDI_AUTO" ]; then
    timeout 120 "$DL_PMD" mounter auto-mount $DL_PMD_ARGS >/dev/null \
        || { echo "error: pymobiledevice3 mounter auto-mount failed (Developer Mode on?)" >&2; exit 1; }
elif ! ideviceimagemounter -u "$DL_UDID" list 2>/dev/null | grep -q "ImageSignature\[1\]"; then
    ideviceimagemounter -u "$DL_UDID" "$DL_DDI/DeveloperDiskImage.dmg" "$DL_DDI/DeveloperDiskImage.dmg.signature" >/dev/null \
        || { echo "error: mounting $DL_DDI failed (Developer Mode on, screen unlocked?)" >&2; exit 1; }
fi

ctl proxy status >/dev/null || ctl proxy start
# A stuck tab may not answer: going to about:blank is an attempt, the restart below is what matters.
ctl open about:blank >/dev/null 2>&1 || echo "note: the old tab did not answer, restarting anyway"
timeout 60 "$DL_PMD" developer dvt launch $DL_PMD_ARGS --kill-existing com.apple.mobilesafari >/dev/null \
    || { echo "error: restarting Safari failed (Developer Mode on, DDI mounted?)" >&2; exit 1; }
ctl --timeout 20 wait-page
sleep 2
[ -z "$url" ] || ctl open "$url" >/dev/null
ctl list
