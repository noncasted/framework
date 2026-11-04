#!/usr/bin/env bash
# Prepares this machine for the iOS tools of web-bench:
#   - checks the system tools (usbmuxd, ios_webkit_debug_proxy, libimobiledevice; Arch: yay -S usbmuxd
#     ios-webkit-debug-proxy libimobiledevice imagemagick);
#   - creates the venv of pymobiledevice3 (+ websocket-client for iosctl.py) in ~/.local/share/device-lab/pmd;
#   - copies a Developer Disk Image into ~/.local/share/device-lab/ddi/<version> (iOS <= 16 only; iOS 17+ use
#     "ddiAutoMount": true, pymobiledevice3 downloads its personalized image itself);
#   - writes a template of ~/.config/device-lab/devices.json when there is none, then validates every ios entry.
# Nothing is installed system-wide. Running it again changes nothing that is already in place.
#
# Usage: setup.sh [--ddi <src dir>:<version>]... [--reinstall]
#   --ddi        a folder with DeveloperDiskImage.dmg and DeveloperDiskImage.dmg.signature, copied to ddi/<version>
#   --reinstall  recreate the venv
# Environment:
#   PMD_SPEC           pip requirement of pymobiledevice3 (default pymobiledevice3==11.49.4, the tested one)
#   DEVICE_LAB_HOME    data folder (default ~/.local/share/device-lab)
#   DEVICE_LAB_DEVICES devices file (default ~/.config/device-lab/devices.json)
# Example:
#   setup.sh --ddi ~/Downloads/DeveloperDiskImage-16.6:16.6
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)

usage() { sed -n '2,/^set -euo/p' "$0" | sed '$d; s/^# \{0,1\}//'; }
ddis=(); reinstall=
while [ $# -gt 0 ]; do
    case $1 in
        -h|--help) usage; exit 0 ;;
        --ddi) [ $# -ge 2 ] || { echo "error: --ddi needs <src dir>:<version>" >&2; exit 2; }; ddis+=("$2"); shift 2 ;;
        --reinstall) reinstall=1; shift ;;
        *) echo "error: unknown argument $1 (--help)" >&2; exit 2 ;;
    esac
done

missing=()
for tool in python3 ios_webkit_debug_proxy idevice_id ideviceimagemounter; do
    command -v "$tool" >/dev/null || missing+=("$tool")
done
if [ ${#missing[@]} -gt 0 ]; then
    echo "error: missing tools: ${missing[*]} (Arch: yay -S usbmuxd ios-webkit-debug-proxy libimobiledevice)" >&2
    exit 1
fi
command -v magick >/dev/null || echo "note: no magick (imagemagick): screenshot.sh --resize and the small copies are off"
systemctl is-active --quiet usbmuxd.service 2>/dev/null \
    || echo "note: usbmuxd.service is not active now; udev starts it when a device is plugged in"

data=${DEVICE_LAB_HOME:-$HOME/.local/share/device-lab}
devices=${DEVICE_LAB_DEVICES:-$HOME/.config/device-lab/devices.json}
spec=${PMD_SPEC:-pymobiledevice3==11.49.4}
mkdir -p "$data/run" "$data/ddi"

if [ -n "$reinstall" ]; then rm -rf "$data/pmd"; fi
if [ ! -x "$data/pmd/bin/pymobiledevice3" ]; then
    echo "creating the venv $data/pmd ($spec)"
    python3 -m venv "$data/pmd"
    "$data/pmd/bin/pip" install --quiet --upgrade pip
    "$data/pmd/bin/pip" install --quiet "$spec" websocket-client
fi
"$data/pmd/bin/python" -c "import websocket" \
    || { echo "error: websocket-client is missing in $data/pmd (setup.sh --reinstall)" >&2; exit 1; }
echo "pymobiledevice3 $("$data/pmd/bin/pymobiledevice3" version 2>/dev/null | tail -1) at $data/pmd"

for entry in "${ddis[@]}"; do
    src=${entry%:*}; version=${entry##*:}
    [ "$src" != "$entry" ] && [ -n "$version" ] || { echo "error: --ddi $entry: expected <src dir>:<version>" >&2; exit 2; }
    for file in DeveloperDiskImage.dmg DeveloperDiskImage.dmg.signature; do
        [ -f "$src/$file" ] || { echo "error: $src/$file is missing" >&2; exit 1; }
    done
    mkdir -p "$data/ddi/$version"
    cp -n "$src/DeveloperDiskImage.dmg" "$src/DeveloperDiskImage.dmg.signature" "$data/ddi/$version/"
    cmp -s "$src/DeveloperDiskImage.dmg" "$data/ddi/$version/DeveloperDiskImage.dmg" \
        || { echo "error: $data/ddi/$version/DeveloperDiskImage.dmg differs from $src (remove it first)" >&2; exit 1; }
done
for dir in "$data"/ddi/*/; do [ -d "$dir" ] && echo "ddi $(basename "$dir"): $(ls "$dir" | tr '\n' ' ')"; done

if [ ! -f "$devices" ]; then
    mkdir -p "$(dirname "$devices")"
    cat > "$devices" <<'EOF'
{
  "devices": [
    {"name": "iphone", "kind": "ios", "udid": "<udid from idevice_id -l>", "proxyPort": 9222,
     "ddi": "~/.local/share/device-lab/ddi/<ios version>"},
    {"name": "ipad", "kind": "ios", "udid": "<udid from idevice_id -l>", "proxyPort": 9223,
     "ddiAutoMount": true, "tunnel": "userspace"},
    {"name": "android", "kind": "android", "serial": null}
  ]
}
EOF
    echo "wrote the template $devices: fill in the udids, then run setup.sh again"
    exit 1
fi
names=$("$data/pmd/bin/python" -c 'import json,sys; print(" ".join(d["name"] for d in json.load(open(sys.argv[1]))["devices"] if d.get("kind") == "ios"))' "$devices")
for name in $names; do
    python3 "$here/devicelab.py" shell "$name" >/dev/null
    echo "device $name: ok"
done
echo "connected now: $(idevice_id -l 2>/dev/null | tr '\n' ' ' || true)"
