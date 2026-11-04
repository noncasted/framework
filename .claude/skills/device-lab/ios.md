# iOS: iPhone / iPad Safari over USB

`$WB/ios/`: `setup.sh` (once per machine), `iosctl.py` (the Safari tab over `ios_webkit_debug_proxy`), `safari-restart.sh`,
`screenshot.sh`, `run-res.sh` (one production autorun), `devicelab.py` (the config, used by the others), `probes/` (GL probes and
`probe-run.py`, `uni-scan.py`). Devices by name from `~/.config/device-lab/devices.json` (SKILL.md § Configuration): `--device <name>`.

## Once per machine and per device

- System tools (Arch): `yay -S usbmuxd ios-webkit-debug-proxy libimobiledevice imagemagick`.
- `$WB/ios/setup.sh [--ddi <dir>:<version>]`: the venv of pymobiledevice3 (+ websocket-client) in `~/.local/share/device-lab/pmd`,
  the Developer Disk Image of iOS <= 16 into `ddi/<version>`, a template of `devices.json`, then validates every ios entry. A second
  run changes nothing that is in place.
- On the device: Developer Mode on (Settings > Privacy & Security; the item appears after a developer tool paired with it), Safari >
  Advanced > Web Inspector on, Auto-Lock: Never, the screen unlocked, the computer trusted.
- iOS 17+: `"ddiAutoMount": true` (pymobiledevice3 fetches its personalized image) and `"tunnel": "userspace"` (no root).

## Driving the tab

Run `iosctl.py` with the venv python (it needs websocket-client):

```
PY=~/.local/share/device-lab/pmd/bin/python
$PY $WB/ios/iosctl.py --device iphone11 proxy start|stop|status   # ios_webkit_debug_proxy -c <udid>:<proxyPort> -F
$PY $WB/ios/iosctl.py --device iphone11 list                      # pages: index, title, url (--page N picks one)
$PY $WB/ios/iosctl.py --device iphone11 open '<url>'              # location.href=...; Page.navigate does nothing on iOS 16
$PY $WB/ios/iosctl.py --device iphone11 eval '<js>'               # value printed; a thrown exception -> exit 1
$PY $WB/ios/iosctl.py --device iphone11 wait '<js>' --timeout 120 # until truthy
$PY $WB/ios/iosctl.py --device iphone11 console 30                # console messages for 30 s
```

Each device has its own proxy port (`proxyPort`): both phones at once are fine. iOS 16+ multiplexes the page behind `Target.*`;
the script handles it.

- **A stuck Safari** (about 1 frame in 5 s on every page after a GPU hang, see WEBGL_TROUBLESHOOTING I1 / G1):
  `$WB/ios/safari-restart.sh --device iphone11 ['<url>']` mounts the DDI, starts the proxy, sends the tab to `about:blank`, kills and
  starts Safari through pymobiledevice3, waits for the page, opens the url. Nothing else cures it; start every series with it.
- **Screenshot**: `$WB/ios/screenshot.sh --device ipad7 [--resize 25] <out.png>` (pymobiledevice3; a screenshot through the
  inspector gives a black WebGL canvas). The DDI must be mounted (`safari-restart.sh` does it).

## A production run

```
$WB/ios/run-res.sh --device iphone11 --out /tmp/claude-1000/ios/<name> --seconds 60 [--query '&dpr=1'] [--project <game root>]
```

A fresh Safari on `https://<host>/<autorunQuery>&r=<random><query>`, `<seconds>` of play, the canvas size and a screenshot
(`run.txt`, `shot.png`, `shot.small.png`). The session goes to the production telemetry: read it with the `client-telemetry` skill
(`sessions --tag run`, the start time from `run.txt`). A cold device off the charger, a pause between runs; three runs in a row
doubled the CPU of every system on an iPhone 11.

## GL probes

```
python3 $WB/ios/probes/probe-run.py --device ipad7 --probe uni --out /tmp/claude-1000/ios/uni1 [--autorun] [--query '...'] [--url <url>]
python3 $WB/ios/probes/uni-scan.py /tmp/claude-1000/ios/uni1/probe.json [--uniform '<regex>']
```

`probe-run.py` restarts Safari on the production page, injects the probe when the game made its context (`--ready`), samples
screenshots and progress, writes `probe.json`, `progress.txt`, `console.txt`, `shot_*.png`. Probes: `probe` (counts a second,
texture events, context loss), `frame` (every GL call of one frame), `buf` (vertex / index buffers read back against shadow copies),
`tex` (RGBA8 uploads read back), `uni` (all uniforms of every program over time; `uni-scan.py` finds the rows that became zero - it
found `glstate_matrix_projection[3]` zeroed on iPadOS 18.2, WEBGL_TROUBLESHOOTING I2). The header of each `.js` says what it reads.

Not checked on hardware yet after the move into the framework (2026-11-04): if a device command fails where the old one worked,
report the error line, do not patch the script on the fly.
